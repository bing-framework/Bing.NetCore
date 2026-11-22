#requires -Version 7.0
<#
.EXAMPLE
    ./Invoke-SqlCapacityGate.ps1 -ReceiptPath $receipt -ExpectedBuildDirectory $candidateBuild -MinRequestsPerSecond 500 -MaxP99Milliseconds 80 -MaxFailureRate 0.01
    校验 v2 收据时，指定事先选定的候选构建目录。阈值按业务负载确定。
.PARAMETER ExpectedBenchmarkSha256
    仅供复核 v1 历史收据，摘要对应基准入口程序集。
#>
[CmdletBinding(DefaultParameterSetName = 'Legacy')]
param(
    [Parameter(Mandatory)][string]$ReceiptPath,
    [Parameter(Mandatory, ParameterSetName = 'Legacy')][string]$ExpectedBenchmarkSha256,
    [Parameter(Mandatory)][string]$MinRequestsPerSecond,
    [Parameter(Mandatory)][string]$MaxP99Milliseconds,
    [Parameter(Mandatory)][string]$MaxFailureRate,
    [string]$ExpectedWorkloadPath,
    [Parameter(Mandatory, ParameterSetName = 'Build')][string]$ExpectedBuildDirectory
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'

function Read-Threshold([string]$Value, [string]$Name, [double]$Maximum = [double]::PositiveInfinity) {
    $number = 0.0
    $parsed = [double]::TryParse($Value, [System.Globalization.NumberStyles]::Float,
        [System.Globalization.CultureInfo]::InvariantCulture, [ref]$number)
    if (!$parsed -or ![double]::IsFinite($number) -or $number -lt 0 -or $number -gt $Maximum) {
        throw "Invalid threshold $Name. Supply a finite nonnegative number. Failure rate must be between 0 and 1."
    }
    return $number
}

function Read-Count($Value, [string]$Name) {
    if (($Value -isnot [int] -and $Value -isnot [long]) -or $Value -lt 0 -or $Value -gt [int]::MaxValue) {
        throw "Field $Name must be a nonnegative Int32."
    }
    return [long]$Value
}

function Read-ReceiptMetric($Value, [string]$Name) {
    if (($Value -isnot [int] -and $Value -isnot [long] -and $Value -isnot [double]) -or
        ![double]::IsFinite($Value) -or $Value -lt 0) {
        throw "Receipt field $Name must be a finite nonnegative number."
    }
    return [double]$Value
}

function Read-Workload($Fields, [string]$Name) {
    if ($Fields -isnot [System.Collections.IDictionary] -or $Fields.path -isnot [string] -or
        $Fields.path -cnotin @('runtime', 'template')) {
        throw "$Name must contain a runtime or template path."
    }
    if ($Fields.pooling -isnot [bool] -or $Fields.observedServer -isnot [bool]) {
        throw "$Name pooling and observedServer must be booleans."
    }
    $workload = [ordered]@{
        path = $Fields.path
        rows = Read-Count $Fields.rows "$Name.rows"
        requests = Read-Count $Fields.requests "$Name.requests"
        concurrency = Read-Count $Fields.concurrency "$Name.concurrency"
        pooling = $Fields.pooling
        maxPoolSize = Read-Count $Fields.maxPoolSize "$Name.maxPoolSize"
        observedServer = $Fields.observedServer
    }
    if ($workload.rows -eq 0 -or $workload.concurrency -eq 0 -or $workload.concurrency -gt $workload.requests) {
        throw "$Name rows must be positive and concurrency must be between 1 and requests."
    }
    return $workload
}

$result = [ordered]@{ gate = 'SqlQueryCapacityGate-v1'; status = 'INVALID' }
try {
    $limits = [ordered]@{
        minRequestsPerSecond = Read-Threshold $MinRequestsPerSecond 'MinRequestsPerSecond'
        maxP99Milliseconds = Read-Threshold $MaxP99Milliseconds 'MaxP99Milliseconds'
        maxFailureRate = Read-Threshold $MaxFailureRate 'MaxFailureRate' 1
    }
    $result.thresholds = $limits

    $file = (Resolve-Path -LiteralPath $ReceiptPath).ProviderPath
    $bytes = [System.IO.File]::ReadAllBytes($file)
    # 摘要与解析使用同一份收据字节。
    $stream = [System.IO.MemoryStream]::new($bytes, $false)
    try { $result.receiptSha256 = (Get-FileHash -InputStream $stream -Algorithm SHA256).Hash }
    finally { $stream.Dispose() }
    $utf8 = [System.Text.UTF8Encoding]::new($false, $true)
    try { $receipt = ConvertFrom-Json -AsHashtable -NoEnumerate -InputObject $utf8.GetString($bytes).TrimStart([char]0xFEFF) }
    catch { throw 'Receipt must be valid UTF-8 JSON.' }
    if ($receipt -isnot [System.Collections.IDictionary] -or $receipt.provider -cne 'PostgreSQL' -or
        $receipt.path -cnotin @('runtime', 'template')) {
        throw 'Receipt is not a supported PostgreSQL capacity probe report.'
    }
    if ($PSCmdlet.ParameterSetName -eq 'Build') {
        if ($receipt.probe -cne 'SqlQueryCapacityProbe-v2' -or $receipt.assemblySha256 -isnot [System.Collections.IDictionary]) {
            throw 'ExpectedBuildDirectory requires a v2 PostgreSQL capacity receipt with assembly hashes.'
        }
        $result.identityScope = 'sql-assemblies'
        $result.assemblySha256 = [ordered]@{}
        $directory = (Resolve-Path -LiteralPath $ExpectedBuildDirectory).ProviderPath
        foreach ($name in @('Bing.Data.Sql.Benchmarks.dll', 'Bing.Data.dll', 'Bing.Data.Sql.dll', 'Bing.Dapper.Core.dll',
                'Bing.Dapper.PostgreSql.dll', 'Dapper.dll', 'Npgsql.dll')) {
            $recorded = $receipt.assemblySha256[$name]
            $expected = (Get-FileHash -LiteralPath (Join-Path $directory $name) -Algorithm SHA256).Hash
            if ($recorded -isnot [string] -or $recorded -ine $expected) {
                throw "Missing or mismatched receipt SHA256 for $name."
            }
            $result.assemblySha256[$name] = $expected
        }
    }
    else {
        if ($receipt.probe -cne 'SqlQueryCapacityProbe-v1') {
            throw 'Only v1 PostgreSQL capacity receipts use ExpectedBenchmarkSha256. Use ExpectedBuildDirectory for v2.'
        }
        if ($ExpectedBenchmarkSha256 -notmatch '\A[0-9a-fA-F]{64}\z') { throw 'Expected benchmark SHA256 must contain 64 hexadecimal digits.' }
        if ($receipt.benchmarkSha256 -isnot [string] -or $receipt.benchmarkSha256 -ine $ExpectedBenchmarkSha256) {
            throw 'Receipt benchmark SHA256 does not match the expected benchmark assembly.'
        }
        $result.identityScope = 'benchmark-only'
        $result.benchmarkSha256 = $receipt.benchmarkSha256.ToUpperInvariant()
    }

    $rows = Read-Count $receipt.rows 'rows'
    $requests = Read-Count $receipt.requests 'requests'
    $concurrency = Read-Count $receipt.concurrency 'concurrency'
    if ($rows -eq 0 -or $concurrency -eq 0 -or $concurrency -gt $requests) {
        throw 'Receipt workload is inconsistent.'
    }
    $warmupRequests = Read-Count $receipt.warmupRequests 'warmupRequests'
    $warmupFailed = Read-Count $receipt.warmupFailed 'warmupFailed'
    $measure = $receipt.measure
    if ($measure -isnot [System.Collections.IDictionary] -or !$measure.Contains('p99Milliseconds')) {
        throw 'Receipt is missing measure or measure.p99Milliseconds.'
    }
    $successful = Read-Count $measure.successful 'measure.successful'
    $failed = Read-Count $measure.failed 'measure.failed'
    if ($successful + $failed -ne $requests -or $warmupRequests -eq 0 -or $warmupFailed -gt $warmupRequests) {
        throw 'Receipt request counts are inconsistent.'
    }
    $throughput = Read-ReceiptMetric $measure.successfulRequestsPerSecond 'measure.successfulRequestsPerSecond'
    $p99 = $null
    if ($successful -gt 0) {
        $p99 = Read-ReceiptMetric $measure.p99Milliseconds 'measure.p99Milliseconds'
    }
    elseif ($null -ne $measure.p99Milliseconds -or $throughput -ne 0) {
        throw 'Receipt with no successful requests must have null P99 and zero successful throughput.'
    }
    $failureRate = $failed / $requests

    $result.workload = [ordered]@{
        path = $receipt.path
        rows = $rows
        requests = $requests
        concurrency = $concurrency
        pooling = $receipt.pooling
        maxPoolSize = $receipt.maxPoolSize
        observedServer = $null -ne $receipt.serverActivity
    }
    if ($PSBoundParameters.ContainsKey('ExpectedWorkloadPath')) {
        $workloadFile = (Resolve-Path -LiteralPath $ExpectedWorkloadPath).ProviderPath
        try {
            $profileText = $utf8.GetString([System.IO.File]::ReadAllBytes($workloadFile)).TrimStart([char]0xFEFF)
            $profile = ConvertFrom-Json -AsHashtable -NoEnumerate -InputObject $profileText
        }
        catch { throw 'Expected workload must be valid UTF-8 JSON.' }
        if ($result.workload.observedServer -and $receipt.serverActivity -isnot [System.Collections.IDictionary]) {
            throw 'Receipt serverActivity must be a JSON object or null.'
        }
        $result.expectedWorkload = Read-Workload $profile 'Expected workload'
        if ($profile.PSBase.Count -ne $result.expectedWorkload.Count) {
            throw 'Expected workload contains fields not supported by the fixed read probe.'
        }
        $result.workload = Read-Workload $result.workload 'Receipt workload'
        $result.workloadMismatches = @(
            foreach ($field in $result.expectedWorkload.Keys) {
                if ($result.expectedWorkload[$field] -cne $result.workload[$field]) { $field }
            }
        )
        if ($result.workloadMismatches.Count) {
            throw 'Receipt workload does not match the expected workload.'
        }
    }
    $result.observed = [ordered]@{
        warmupFailed = $warmupFailed
        requests = $requests
        successful = $successful
        failed = $failed
        successfulRequestsPerSecond = $throughput
        p99Milliseconds = $p99
        failureRate = $failureRate
    }
    $result.violations = @(
        if ($warmupFailed -gt 0) { 'warmupFailed' }
        if ($throughput -lt $limits.minRequestsPerSecond) { 'minRequestsPerSecond' }
        if ($null -eq $p99 -or $p99 -gt $limits.maxP99Milliseconds) { 'maxP99Milliseconds' }
        if ($failureRate -gt $limits.maxFailureRate) { 'maxFailureRate' }
    )
    $result.status = if ($result.violations.Count) { 'FAIL' } else { 'PASS' }
}
catch {
    $result.error = $_.Exception.Message
}

$result | ConvertTo-Json -Depth 5
switch ($result.status) {
    'PASS' { exit 0 }
    'FAIL' { exit 1 }
    default { exit 2 }
}
