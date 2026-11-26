#requires -Version 7.0
<#
.SYNOPSIS
    校验固定 R02 混合负载收据，并按调用方明确批准的指标预算判定。
.DESCRIPTION
    PASS 仅表示该份收据的指标达标，不代表连接池、数据库宿主或完整生产容量验收通过。
#>
[CmdletBinding()]
param(
    [string]$ReceiptPath,
    [string]$ExpectedBuildDirectory,
    [string]$ExpectedWorkloadPath,
    [string]$ExpectedProfile,
    [string]$ExpectedConcurrency,
    [string]$BudgetApprovalId,
    [string]$MinRequestsPerSecond,
    [string]$MaxP99Milliseconds,
    [string]$MaxFailureRate
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
$utf8 = [System.Text.UTF8Encoding]::new($false, $true)

function Read-JsonFile([string]$Path) {
    $bytes = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Path).ProviderPath)
    $value = ConvertFrom-Json -AsHashtable -NoEnumerate -InputObject $utf8.GetString($bytes).TrimStart([char]0xFEFF)
    if ($value -isnot [System.Collections.IDictionary]) { throw 'Expected a JSON object.' }
    return $value
}

function Read-Budget([string]$Value, [string]$Name, [double]$Maximum = [double]::MaxValue) {
    $number = 0.0
    if (![double]::TryParse($Value, [System.Globalization.NumberStyles]::Float,
            [System.Globalization.CultureInfo]::InvariantCulture, [ref]$number) -or
        ![double]::IsFinite($number) -or $number -lt 0 -or $number -gt $Maximum) {
        throw "Invalid explicit budget: $Name."
    }
    return $number
}

function Read-Number($Value, [string]$Name) {
    if (($Value -isnot [int] -and $Value -isnot [long] -and $Value -isnot [double]) -or
        ![double]::IsFinite([double]$Value) -or $Value -lt 0) {
        throw "Invalid nonnegative numeric field: $Name."
    }
    return [double]$Value
}

function Read-Count($Value, [string]$Name) {
    if (($Value -isnot [int] -and $Value -isnot [long]) -or $Value -lt 0 -or $Value -gt [int]::MaxValue) {
        throw "Invalid nonnegative Int32 field: $Name."
    }
    return [long]$Value
}

function Assert-Equal($Actual, $Expected, [string]$Name) {
    # 不接受 JSON 字符串/布尔值被 PowerShell 隐式转换成数字。
    if ($Expected -is [bool]) { $same = $Actual -is [bool] -and $Actual -eq $Expected }
    elseif ($Expected -is [string]) { $same = $Actual -is [string] -and $Actual -ceq $Expected }
    else { $same = (Read-Number $Actual $Name) -eq $Expected }
    if (!$same) { throw "Mismatched field: $Name. Expected $Expected." }
}

function Assert-Rate($Value, [double]$Expected, [string]$Name) {
    $actual = Read-Number $Value $Name
    if ([Math]::Abs($actual - $Expected) -gt [Math]::Max(0.000001, $Expected * 0.000000001)) {
        throw "Inconsistent derived rate: $Name."
    }
}

function Test-Window($Window, [int]$Milliseconds, [int]$Concurrency, [string]$Name) {
    if ($Window -isnot [System.Collections.IDictionary]) { throw "Missing $Name." }
    $counts = @{}
    foreach ($field in @('attempted', 'readAttempts', 'writeAttempts', 'successful', 'successfulWithinWindow',
            'failed', 'failedReads', 'failedWrites', 'peakInFlight', 'completedAfterWindow', 'latencyHistogramOverflowCount')) {
        $counts[$field] = Read-Count $Window[$field] "$Name.$field"
    }
    if ($counts.successful + $counts.failed -ne $counts.attempted -or
        $counts.readAttempts + $counts.writeAttempts -ne $counts.attempted -or
        $counts.writeAttempts -ne [Math]::Floor($counts.attempted / 10) -or
        $counts.failedReads + $counts.failedWrites -ne $counts.failed -or
        $counts.failedReads -gt $counts.readAttempts -or $counts.failedWrites -gt $counts.writeAttempts -or
        $counts.successfulWithinWindow -gt $counts.successful -or
        $counts.completedAfterWindow -gt $counts.attempted -or
        $counts.attempted - $counts.successfulWithinWindow - $counts.failed -gt $counts.completedAfterWindow -or
        $counts.peakInFlight -gt $Concurrency -or $counts.peakInFlight -gt $counts.attempted -or
        ($counts.attempted -gt 0 -and $counts.peakInFlight -eq 0) -or
        $counts.latencyHistogramOverflowCount -gt $counts.successful) {
        throw "Inconsistent request counts: $Name."
    }
    Assert-Equal $Window.windowMilliseconds $Milliseconds "$Name.windowMilliseconds"
    Assert-Equal $Window.commandTimeoutSeconds 30 "$Name.commandTimeoutSeconds"
    Assert-Equal $Window.latencyBucketWidthMilliseconds 1 "$Name.latencyBucketWidthMilliseconds"
    Assert-Equal $Window.latencyHistogramMaxMilliseconds 120000 "$Name.latencyHistogramMaxMilliseconds"
    $elapsed = Read-Number $Window.elapsedIncludingDrainMilliseconds "$Name.elapsedIncludingDrainMilliseconds"
    if ($elapsed -lt $Milliseconds) { throw "Incomplete measurement window: $Name." }
    $writePercent = if ($counts.attempted -eq 0) { 0 } else { $counts.writeAttempts * 100.0 / $counts.attempted }
    Assert-Rate $Window.actualWritePercent $writePercent "$Name.actualWritePercent"
    Assert-Rate $Window.admissionRequestsPerSecond ($counts.attempted * 1000.0 / $Milliseconds) "$Name.admissionRequestsPerSecond"
    Assert-Rate $Window.successfulCompletionsWithinWindowPerSecond ($counts.successfulWithinWindow * 1000.0 / $Milliseconds) "$Name.successfulCompletionsWithinWindowPerSecond"
    Assert-Rate $Window.successfulCompletionsIncludingDrainPerSecond ($counts.successful * 1000.0 / $elapsed) "$Name.successfulCompletionsIncludingDrainPerSecond"

    $last = 0.0
    $overflowTail = $false
    foreach ($quantile in @(@('p50Milliseconds', 0.50), @('p95Milliseconds', 0.95), @('p99Milliseconds', 0.99))) {
        $field = $quantile[0]
        if (!$Window.Contains($field)) { throw "Missing percentile: $Name.$field." }
        $rank = [Math]::Ceiling($counts.successful * $quantile[1])
        $expectedNull = $counts.successful -eq 0 -or $rank -gt $counts.successful - $counts.latencyHistogramOverflowCount
        if ($expectedNull) {
            if ($null -ne $Window[$field]) { throw "Unmeasurable percentile must be null: $Name.$field." }
            $overflowTail = $true
        }
        else {
            $number = Read-Number $Window[$field] "$Name.$field"
            if ($overflowTail -or $number -lt $last -or $number -gt 120000 -or $number -ne [Math]::Floor($number)) {
                throw "Inconsistent fixed-histogram percentile: $Name.$field."
            }
            $last = $number
        }
    }
    return $counts
}

$result = [ordered]@{
    gate = 'SqlMixedCapacityGate-v1'
    status = 'INVALID'
    scope = 'mixed-workload-metrics'
    productionCapacityVerified = $false
    violations = @()
}
try {
    foreach ($name in @('ReceiptPath', 'ExpectedBuildDirectory', 'ExpectedWorkloadPath', 'ExpectedProfile',
            'ExpectedConcurrency', 'BudgetApprovalId', 'MinRequestsPerSecond', 'MaxP99Milliseconds', 'MaxFailureRate')) {
        if ([string]::IsNullOrWhiteSpace((Get-Variable -Name $name -ValueOnly))) { throw "Missing explicit input: $name." }
    }
    if ($BudgetApprovalId.Length -gt 160 -or $BudgetApprovalId -match '[\r\n]') { throw 'Invalid budget approval reference.' }
    $result.approvalReference = $BudgetApprovalId
    $limits = [ordered]@{
        minRequestsPerSecond = Read-Budget $MinRequestsPerSecond 'MinRequestsPerSecond'
        maxP99Milliseconds = Read-Budget $MaxP99Milliseconds 'MaxP99Milliseconds'
        maxFailureRate = Read-Budget $MaxFailureRate 'MaxFailureRate' 1
    }
    $result.thresholds = $limits
    $profiles = @{ '1C2G' = @(1, 2147483648L); '2C4G' = @(2, 4294967296L); '4C8G' = @(4, 8589934592L); '8C16G' = @(8, 17179869184L) }
    if (!$profiles.ContainsKey($ExpectedProfile) -or $ExpectedConcurrency -cnotin @('1', '8', '16', '32', '64')) {
        throw 'Expected profile or concurrency is outside the R02 matrix.'
    }
    $concurrency = [int]$ExpectedConcurrency
    $expected = Read-JsonFile $ExpectedWorkloadPath
    $receipt = Read-JsonFile $ReceiptPath
    $result.receiptSha256 = (Get-FileHash -LiteralPath $ReceiptPath -Algorithm SHA256).Hash
    $result.workloadSha256 = (Get-FileHash -LiteralPath $ExpectedWorkloadPath -Algorithm SHA256).Hash
    Assert-Equal $receipt.schema 'SqlMixedCapacityReport-v1' 'schema'
    Assert-Equal $receipt.probe 'SqlMixedCapacityProbe-v1' 'probe'
    Assert-Equal $expected.schema 'SqlMixedCapacityWorkload-v1' 'expected.schema'
    Assert-Equal $expected.workloadStatus 'APPROVED_EXPLORATORY' 'expected.workloadStatus'
    Assert-Equal $expected.provider 'PostgreSQL' 'expected.provider'
    Assert-Equal $expected.database 'bing_sql_aot_test' 'expected.database'
    Assert-Equal $expected.writeOperation 'update by primary key' 'expected.writeOperation'
    Assert-Equal $expected.pooling $true 'expected.pooling'
    # 探针逐项枚举这两个数组，标量配置即使值相同也不是可执行的负载合同。
    if ($expected.profiles -isnot [array] -or $expected.concurrency -isnot [array]) {
        throw 'Expected workload profiles and concurrency must be arrays.'
    }
    if ($expected.profiles -cnotcontains $ExpectedProfile -or $expected.concurrency -notcontains $concurrency) {
        throw 'Expected cell is absent from the selected workload.'
    }
    foreach ($field in @{ rows = 100000; readPercent = 90; writePercent = 10; warmupSeconds = 30; measureSeconds = 120; maxPoolSize = 32 }.GetEnumerator()) {
        Assert-Equal $expected[$field.Key] $field.Value "expected.$($field.Key)"
        Assert-Equal $receipt.workload[$field.Key] $field.Value "workload.$($field.Key)"
    }
    foreach ($item in @($expected, $receipt.workload)) {
        Assert-Equal $item.thresholdStatus 'PENDING_APPROVAL' 'workload.thresholdStatus'
        foreach ($field in @('capacityThresholds', 'buildThresholds')) {
            if (!$item.Contains($field) -or $null -ne $item[$field]) { throw "Exploratory workload must retain null $field; pass approved budgets explicitly." }
        }
    }
    Assert-Equal $receipt.profile $ExpectedProfile 'profile'
    Assert-Equal $receipt.concurrency $concurrency 'concurrency'
    Assert-Equal $receipt.connection.provider 'PostgreSQL' 'connection.provider'
    Assert-Equal $receipt.connection.database 'bing_sql_aot_test' 'connection.database'
    Assert-Equal $receipt.connection.pooling $true 'connection.pooling'
    Assert-Equal $receipt.connection.maxPoolSize 32 'connection.maxPoolSize'
    Assert-Equal $receipt.commandTimeoutSeconds 30 'commandTimeoutSeconds'
    Assert-Equal $receipt.connectionTimeoutSeconds 15 'connectionTimeoutSeconds'
    $profile = $profiles[$ExpectedProfile]
    Assert-Equal $receipt.containerLimits.cpuCount $profile[0] 'containerLimits.cpuCount'
    Assert-Equal $receipt.containerLimits.memoryBytes $profile[1] 'containerLimits.memoryBytes'
    $period = Read-Number $receipt.containerLimits.cpuPeriod 'containerLimits.cpuPeriod'
    $quota = Read-Number $receipt.containerLimits.cpuQuota 'containerLimits.cpuQuota'
    if ($period -le 0 -or $quota / $period -ne $profile[0]) { throw 'Inconsistent recorded CPU quota.' }

    $result.assemblySha256 = [ordered]@{}
    foreach ($name in @('Bing.Data.Sql.Benchmarks.dll', 'Bing.Data.dll', 'Bing.Data.Sql.dll', 'Bing.Dapper.Core.dll',
            'Bing.Dapper.PostgreSql.dll', 'Dapper.dll', 'Npgsql.dll')) {
        $hash = (Get-FileHash -LiteralPath (Join-Path $ExpectedBuildDirectory $name) -Algorithm SHA256).Hash
        $recorded = $receipt.assemblySha256[$name]
        if ($recorded -isnot [string] -or $recorded -ine $hash) { throw "Missing or mismatched candidate SHA256: $name." }
        $result.assemblySha256[$name] = $hash
    }
    $warmup = Test-Window $receipt.measurement.warmup 30000 $concurrency 'warmup'
    $measure = Test-Window $receipt.measurement.measure 120000 $concurrency 'measure'
    $rate = $measure.successfulWithinWindow / 120.0
    $failureRate = if ($measure.attempted -eq 0) { 0.0 } else { $measure.failed / [double]$measure.attempted }
    $p99 = $receipt.measurement.measure.p99Milliseconds
    $violations = [System.Collections.Generic.List[string]]::new()
    if ($warmup.attempted -eq 0 -or $warmup.failed -gt 0) { $violations.Add('warmup') }
    if ($measure.successful -eq 0) { $violations.Add('noSuccessfulRequests') }
    if ($rate -lt $limits.minRequestsPerSecond) { $violations.Add('minRequestsPerSecond') }
    if ($null -eq $p99 -or $p99 -gt $limits.maxP99Milliseconds) { $violations.Add('maxP99Milliseconds') }
    if ($failureRate -gt $limits.maxFailureRate) { $violations.Add('maxFailureRate') }
    $result.profile = $ExpectedProfile
    $result.concurrency = $concurrency
    $result.observed = [ordered]@{
        successfulCompletionsWithinWindowPerSecond = $rate
        p99Milliseconds = $p99
        failureRate = $failureRate
        peakInFlight = $measure.peakInFlight
    }
    $result.violations = @($violations.ToArray())
    $result.status = if ($violations.Count -eq 0) { 'PASS' } else { 'FAIL' }
}
catch { $result.error = $_.Exception.Message }
$result | ConvertTo-Json -Depth 12
if ($result.status -eq 'PASS') { exit 0 }
if ($result.status -eq 'FAIL') { exit 1 }
exit 2
