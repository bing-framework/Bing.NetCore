#requires -Version 7.0
[CmdletBinding()]
param([string]$ResultsDirectory = 'artifacts/validation/sql-container-resources/tests')

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'

$repository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../..')).Path
$resultsRoot = [System.IO.Path]::GetFullPath($ResultsDirectory, $repository)
[System.IO.Directory]::CreateDirectory($resultsRoot) | Out-Null
$utf8 = [System.Text.UTF8Encoding]::new($false)
$summaryScript = Join-Path $PSScriptRoot '../Get-SqlContainerResourceSummary.ps1'
if (Test-Path -LiteralPath $summaryScript) { . $summaryScript }

$script:cases = [System.Collections.Generic.List[object]]::new()

function New-Sample([long]$Time, [long]$Cpu, [long]$Current, [long]$Peak, [long]$Oom,
    [string]$Io = "8:0 rbytes=0 wbytes=0 rios=0 wios=0") {
    return [ordered]@{
        timestampUnixMilliseconds = $Time
        cpuUsageMicroseconds = $Cpu
        memoryCurrentBytes = $Current
        memoryPeakBytes = $Peak
        oomKill = $Oom
        ioStat = $Io
    }
}

function Invoke-SummaryCase([string]$Name, [object[]]$Samples, [long]$Start = 8000, [long]$Stop = 10000) {
    $inputPath = Join-Path $resultsRoot "$Name.jsonl"
    $jsonl = ($Samples | ForEach-Object { $_ | ConvertTo-Json -Compress }) -join "`n"
    [System.IO.File]::WriteAllText($inputPath, $jsonl + "`n", $utf8)
    return Get-SqlContainerResourceSummary -SamplesPath $inputPath `
        -WindowStartUnixMilliseconds $Start -WindowStopUnixMilliseconds $Stop -CpuCount 1
}

function Test-Case([string]$Name, [scriptblock]$Body) {
    try {
        & $Body
        $script:cases.Add([pscustomobject]@{ name = $Name; status = 'PASS' })
    }
    catch {
        $script:cases.Add([pscustomobject]@{ name = $Name; status = 'FAIL'; error = $_.Exception.Message })
    }
}

Test-Case 'ValidWindowReportsBracketedCpuMemoryIoAndOomSeparately' {
    $ioStart = "8:0 rbytes=100 wbytes=20 rios=2 wios=1`n253:0 rbytes=1000 wbytes=500 rios=10 wios=5"
    $ioEnd = "8:0 rbytes=300 wbytes=70 rios=5 wios=3`n253:0 rbytes=1500 wbytes=800 rios=14 wios=8"
    $summary = Invoke-SummaryCase 'valid' @(
        (New-Sample 7000 1000000 100 500 2 $ioStart),
        (New-Sample 8500 1200000 220 550 2 $ioStart),
        (New-Sample 9500 1400000 260 580 2 $ioStart),
        (New-Sample 11000 2000000 240 900 4 $ioEnd))
    if ($summary.status -ne 'PASS') { throw "Expected PASS, got $($summary.status)" }
    if ($summary.io.status -ne 'PASS') { throw 'Valid per-device counters must report I/O as PASS.' }
    if ($summary.cpu.sampling -ne 'bracketed-interval' -or $summary.cpu.observedIntervalMilliseconds -ne 4000 -or
        $summary.cpu.usageMicrosecondsDelta -ne 1000000 -or $summary.cpu.percentOfContainerCpuLimit -ne 25) {
        throw 'CPU must use actual bracketing samples and the container CPU limit.'
    }
    if ($summary.memory.sampledPeakBytes -ne 260 -or $summary.memory.cgroupLifetimePeakBytesAtStop -ne 900) {
        throw 'Sampled memory.current peak must remain distinct from cumulative memory.peak.'
    }
    if ($summary.oomKillDelta -ne 2) { throw 'OOM kill delta must be reported separately.' }
    $deviceDeltas = @{}; foreach ($device in $summary.io.devices) { $deviceDeltas[$device.deviceId] = $device.countersDelta.rbytes }
    if ($summary.io.devices.Count -ne 2 -or $deviceDeltas['8:0'] -ne 200 -or
        $deviceDeltas['253:0'] -ne 500 -or $null -ne $summary.io.total) {
        throw 'I/O deltas must remain per-device without a cross-device total.'
    }
    if ($summary.scope -ne 'container-cgroup-not-physical-host') { throw 'Container resource scope must be explicit.' }
}

Test-Case 'MissingBoundaryReturnsNotVerifiedWithoutZeroMetrics' {
    $summary = Invoke-SummaryCase 'missing-before' @(
        (New-Sample 8500 100 100 100 0), (New-Sample 11000 200 120 120 0))
    if ($summary.status -ne 'NOT_VERIFIED' -or $summary.cpu -or $summary.memory.sampledPeakBytes -ne $null) {
        throw 'Missing brackets must not produce synthetic zero measurements.'
    }
}

Test-Case 'BoundaryFurtherThan2500MillisecondsReturnsNotVerified' {
    $summary = Invoke-SummaryCase 'far-boundary' @(
        (New-Sample 5000 100 100 100 0), (New-Sample 11000 200 120 120 0))
    if ($summary.status -ne 'NOT_VERIFIED') { throw 'A bracket over 2500 ms must be rejected.' }
}

Test-Case 'NonMonotonicTimestampsReturnNotVerified' {
    $summary = Invoke-SummaryCase 'non-monotonic' @(
        (New-Sample 7000 100 100 100 0), (New-Sample 9500 150 110 110 0), (New-Sample 9000 200 120 120 0),
        (New-Sample 11000 250 130 130 0))
    if ($summary.status -ne 'NOT_VERIFIED') { throw 'Samples with non-monotonic timestamps must be rejected.' }
}

Test-Case 'CounterRollbackReturnsNotVerified' {
    $summary = Invoke-SummaryCase 'cpu-rollback' @(
        (New-Sample 7000 200 100 100 0), (New-Sample 11000 100 120 120 0))
    if ($summary.status -ne 'NOT_VERIFIED') { throw 'Cumulative counter rollback must be rejected.' }

    $summary = Invoke-SummaryCase 'io-rollback' @(
        (New-Sample 7000 100 100 100 0 '8:0 rbytes=10 wbytes=5 rios=1 wios=1'),
        (New-Sample 9000 150 110 110 0 '8:0 rbytes=5 wbytes=6 rios=2 wios=2'),
        (New-Sample 11000 200 120 120 0 '8:0 rbytes=20 wbytes=10 rios=3 wios=3'))
    if ($summary.status -ne 'NOT_VERIFIED') { throw 'An intermediate I/O counter rollback must be rejected.' }
}

Test-Case 'NullOrFractionalMetricsReturnNotVerified' {
    $invalidLines = @(
        '{"timestampUnixMilliseconds":7000,"cpuUsageMicroseconds":100,"memoryCurrentBytes":null,"memoryPeakBytes":100,"oomKill":0,"ioStat":"8:0 rbytes=0 wbytes=0 rios=0 wios=0"}',
        '{"timestampUnixMilliseconds":11000,"cpuUsageMicroseconds":200,"memoryCurrentBytes":120,"memoryPeakBytes":120,"oomKill":0,"ioStat":"8:0 rbytes=10 wbytes=10 rios=1 wios=1"}'
    )
    $inputPath = Join-Path $resultsRoot 'null-metric.jsonl'
    [System.IO.File]::WriteAllText($inputPath, ($invalidLines -join "`n") + "`n", $utf8)
    $summary = Get-SqlContainerResourceSummary -SamplesPath $inputPath -WindowStartUnixMilliseconds 8000 `
        -WindowStopUnixMilliseconds 10000
    if ($summary.status -ne 'NOT_VERIFIED') { throw 'A null metric must not be converted to a plausible zero.' }

    $fractionalLines = @(
        '{"timestampUnixMilliseconds":7000,"cpuUsageMicroseconds":100.5,"memoryCurrentBytes":100,"memoryPeakBytes":100,"oomKill":0,"ioStat":"8:0 rbytes=0 wbytes=0 rios=0 wios=0"}',
        '{"timestampUnixMilliseconds":11000,"cpuUsageMicroseconds":200,"memoryCurrentBytes":120,"memoryPeakBytes":120,"oomKill":0,"ioStat":"8:0 rbytes=10 wbytes=10 rios=1 wios=1"}'
    )
    [System.IO.File]::WriteAllText($inputPath, ($fractionalLines -join "`n") + "`n", $utf8)
    $summary = Get-SqlContainerResourceSummary -SamplesPath $inputPath -WindowStartUnixMilliseconds 8000 `
        -WindowStopUnixMilliseconds 10000
    if ($summary.status -ne 'NOT_VERIFIED') { throw 'A fractional counter must not be rounded into an integer.' }
}

Test-Case 'IoDeviceSetChangeReturnsNotVerified' {
    $summary = Invoke-SummaryCase 'io-device-change' @(
        (New-Sample 7000 100 100 100 0 '8:0 rbytes=10 wbytes=5 rios=1 wios=1'),
        (New-Sample 11000 200 120 120 0 '8:1 rbytes=20 wbytes=10 rios=2 wios=2'))
    if ($summary.status -ne 'NOT_VERIFIED') { throw 'Changing the device set cannot be interpreted as zero I/O.' }
}

Test-Case 'EmptyIoDeviceCountersKeepCpuAndMemoryWithoutInventingIoTotals' {
    $summary = Invoke-SummaryCase 'empty-io' @(
        (New-Sample 7000 1000000 100 500 0 ''),
        (New-Sample 8500 1200000 220 550 0 ''),
        (New-Sample 9500 1400000 260 580 0 ''),
        (New-Sample 11000 2000000 240 900 0 ''))
    if ($summary.status -ne 'PASS' -or !$summary.cpu -or !$summary.memory) {
        throw 'Empty I/O counters must preserve valid CPU and memory measurements.'
    }
    if ($summary.io.status -ne 'NO_DEVICE_COUNTERS' -or
        $summary.io.devices.Count -ne 0 -or $summary.io.Contains('total')) {
        throw 'Empty I/O counters must be explicit and must not invent I/O totals.'
    }
}

Test-Case 'EmptyIoDeviceSetChangingToNonEmptyReturnsNotVerified' {
    $summary = Invoke-SummaryCase 'empty-io-device-change' @(
        (New-Sample 7000 100 100 100 0 ''),
        (New-Sample 8500 120 110 110 0 ''),
        (New-Sample 9500 140 120 120 0 '8:0 rbytes=10 wbytes=5 rios=1 wios=1'),
        (New-Sample 11000 160 130 130 0 '8:0 rbytes=20 wbytes=10 rios=2 wios=2'))
    if ($summary.status -ne 'NOT_VERIFIED') {
        throw 'Changing from no I/O device counters to a device must remain unverified.'
    }
}

Test-Case 'NullOrNonStringIoStatReturnsNotVerified' {
    foreach ($value in @('null', '7')) {
        $inputPath = Join-Path $resultsRoot "io-stat-type-$value.jsonl"
        $jsonl = @(
            '{"timestampUnixMilliseconds":7000,"cpuUsageMicroseconds":100,"memoryCurrentBytes":100,"memoryPeakBytes":100,"oomKill":0,"ioStat":' + $value + '}',
            '{"timestampUnixMilliseconds":11000,"cpuUsageMicroseconds":200,"memoryCurrentBytes":120,"memoryPeakBytes":120,"oomKill":0,"ioStat":"8:0 rbytes=10 wbytes=10 rios=1 wios=1"}')
        [System.IO.File]::WriteAllText($inputPath, ($jsonl -join "`n") + "`n", $utf8)
        $summary = Get-SqlContainerResourceSummary -SamplesPath $inputPath `
            -WindowStartUnixMilliseconds 8000 -WindowStopUnixMilliseconds 10000
        if ($summary.status -ne 'NOT_VERIFIED') {
            throw "ioStat=$value must not be coerced into a valid I/O sample."
        }
    }
}

Test-Case 'IoDeviceChangesOutsideBracketDoNotInvalidateMeasuredWindow' {
    $summary = Invoke-SummaryCase 'io-outside-bracket' @(
        (New-Sample 6000 50 50 50 0 '8:1 rbytes=5 wbytes=5 rios=1 wios=1'),
        (New-Sample 7000 100 100 100 0 ''),
        (New-Sample 9000 200 120 120 0 ''),
        (New-Sample 11000 300 130 130 0 ''),
        (New-Sample 12000 350 140 140 0 '8:0 rbytes=10 wbytes=5 rios=1 wios=1'))
    if ($summary.status -ne 'PASS' -or $summary.io.status -ne 'NO_DEVICE_COUNTERS' -or
        $summary.cpu.usageMicrosecondsDelta -ne 200 -or $summary.memory.sampledPeakBytes -ne 130) {
        throw 'Device-set changes outside both selected bracket endpoints must not invalidate measured CPU/memory or invent I/O.'
    }
}

Test-Case 'IoDeviceRoundTripInsideBracketRemainsUnverified' {
    $summary = Invoke-SummaryCase 'io-inside-roundtrip' @(
        (New-Sample 7000 100 100 100 0 ''),
        (New-Sample 9000 200 120 120 0 '8:0 rbytes=10 wbytes=5 rios=1 wios=1'),
        (New-Sample 11000 300 130 130 0 ''))
    if ($summary.status -ne 'NOT_VERIFIED' -or $summary.reason -notlike '*device set*') {
        throw 'Identical endpoints must not hide a device-set change inside the bracket.'
    }
}

$status = if (@($script:cases | Where-Object status -eq 'FAIL').Count -eq 0) { 'PASS' } else { 'FAIL' }
$receipt = [ordered]@{
    schema = 'SqlContainerResourcesTests-v1'
    status = $status
    total = $script:cases.Count
    passed = @($script:cases | Where-Object status -eq 'PASS').Count
    failed = @($script:cases | Where-Object status -eq 'FAIL').Count
    cases = $script:cases
}
[System.IO.File]::WriteAllText((Join-Path $resultsRoot 'tests.json'),
    ($receipt | ConvertTo-Json -Depth 8) + "`n", $utf8)
$script:cases | Format-Table -AutoSize | Out-String | Write-Output
Write-Output "SQL_CONTAINER_RESOURCES_TESTS_$($status): $($receipt.passed)/$($receipt.total)"
if ($status -ne 'PASS') { exit 1 }
