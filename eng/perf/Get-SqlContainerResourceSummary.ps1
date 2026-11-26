#requires -Version 7.0
<#
.SYNOPSIS
    用测量窗口两侧的 cgroup 样本汇总容器资源；证据不完整时保持未验证。
#>
[CmdletBinding()]
param(
    [string]$SamplesPath,
    [long]$WindowStartUnixMilliseconds,
    [long]$WindowStopUnixMilliseconds,
    [string]$ResultPath,
    [ValidateRange(1, 1024)][int]$CpuCount = 1
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'

function ConvertFrom-SqlCgroupIoStat([string]$Text) {
    $devices = [System.Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($line in ($Text -split "`r?`n" | Where-Object { $_.Trim().Length -gt 0 })) {
        if ($line -notmatch '^\s*(\d+:\d+)\s+(.+?)\s*$') { throw 'io.stat contains an invalid device row.' }
        $device = $Matches[1]
        if ($devices.ContainsKey($device)) { throw 'io.stat contains a duplicate device.' }
        $counters = [System.Collections.Generic.Dictionary[string, long]]::new([StringComparer]::Ordinal)
        foreach ($part in ($Matches[2] -split '\s+')) {
            if ($part -notmatch '^([a-z]+)=([0-9]+)$') { throw 'io.stat contains an invalid counter.' }
            $counter = $Matches[1]
            if ($counters.ContainsKey($counter)) { throw 'io.stat contains a duplicate counter.' }
            $counters.Add($counter, [long]::Parse($Matches[2], [Globalization.CultureInfo]::InvariantCulture))
        }
        $devices.Add($device, $counters)
    }
    return ,$devices
}

function New-SqlContainerResourceUnverified([string]$Reason) {
    return [ordered]@{
        schema = 'SqlContainerResourceSummary-v1'
        status = 'NOT_VERIFIED'
        scope = 'container-cgroup-not-physical-host'
        reason = $Reason
    }
}

# CPU 使用实际包围区间，内存区分采样峰值与生命周期峰值，IO 保持逐设备口径。
function Get-SqlContainerResourceSummary {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$SamplesPath,
        [Parameter(Mandatory)][long]$WindowStartUnixMilliseconds,
        [Parameter(Mandatory)][long]$WindowStopUnixMilliseconds,
        [ValidateRange(1, 1024)][int]$CpuCount = 1
    )

    if ($WindowStopUnixMilliseconds -le $WindowStartUnixMilliseconds) {
        throw 'Window stop must be later than window start.'
    }
    if ($CpuCount -lt 1) { throw 'CPU count must be positive.' }

    $lines = [System.IO.File]::ReadAllLines($SamplesPath, [System.Text.Encoding]::UTF8) |
        Where-Object { $_.Trim().Length -gt 0 }
    if ($lines.Count -lt 2) { return New-SqlContainerResourceUnverified 'At least two cgroup samples are required.' }

    $samples = [System.Collections.Generic.List[object]]::new()
    try {
        foreach ($line in $lines) {
            $sample = ConvertFrom-Json -InputObject $line -AsHashtable -ErrorAction Stop
            foreach ($field in @('timestampUnixMilliseconds', 'cpuUsageMicroseconds', 'memoryCurrentBytes', 'memoryPeakBytes', 'oomKill', 'ioStat')) {
                if (-not $sample.ContainsKey($field)) { throw "Sample is missing $field." }
            }
            foreach ($field in @('timestampUnixMilliseconds', 'cpuUsageMicroseconds', 'memoryCurrentBytes', 'memoryPeakBytes', 'oomKill')) {
                $value = $sample[$field]
                if ($null -eq $value -or $value -is [bool] -or
                    $value -isnot [byte] -and $value -isnot [sbyte] -and
                    $value -isnot [int16] -and $value -isnot [uint16] -and
                    $value -isnot [int32] -and $value -isnot [uint32] -and
                    $value -isnot [int64] -and $value -isnot [uint64] -and
                    $value -isnot [single] -and $value -isnot [double] -and
                    $value -isnot [decimal]) {
                    throw "Sample field $field must be a non-null integer."
                }
                try { $integerValue = [decimal]$value }
                catch { throw "Sample field $field is outside the supported integer range." }
                if ($integerValue -ne [decimal]::Truncate($integerValue) -or
                    $integerValue -gt [long]::MaxValue -or $integerValue -lt [long]::MinValue) {
                    throw "Sample field $field must be a supported integer."
                }
                if ($field -ne 'timestampUnixMilliseconds' -and $integerValue -lt 0) {
                    throw "Sample field $field must be non-negative."
                }
            }
            $row = [ordered]@{
                timestamp = [long]$sample.timestampUnixMilliseconds
                cpu = [long]$sample.cpuUsageMicroseconds
                current = [long]$sample.memoryCurrentBytes
                peak = [long]$sample.memoryPeakBytes
                oom = [long]$sample.oomKill
                io = ConvertFrom-SqlCgroupIoStat ([string]$sample.ioStat)
            }
            foreach ($value in @($row.cpu, $row.current, $row.peak, $row.oom)) {
                if ($value -lt 0) { throw 'Cgroup counters and memory values must be non-negative.' }
            }
            if ($sample.ioStat -isnot [string]) { throw 'io.stat must be a string, including an explicitly empty value.' }
            $samples.Add($row)
        }
    }
    catch {
        return New-SqlContainerResourceUnverified $_.Exception.Message
    }

    for ($index = 1; $index -lt $samples.Count; $index++) {
        if ($samples[$index].timestamp -le $samples[$index - 1].timestamp) {
            return New-SqlContainerResourceUnverified 'Sample timestamps are not strictly increasing.'
        }
        foreach ($name in @('cpu', 'peak', 'oom')) {
            if ($samples[$index].$name -lt $samples[$index - 1].$name) {
                return New-SqlContainerResourceUnverified "Cumulative $name counter rolled back."
            }
        }
    }

    $beforeIndex = -1
    $afterIndex = -1
    for ($index = 0; $index -lt $samples.Count; $index++) {
        if ($samples[$index].timestamp -le $WindowStartUnixMilliseconds) { $beforeIndex = $index }
        if ($afterIndex -lt 0 -and $samples[$index].timestamp -ge $WindowStopUnixMilliseconds) { $afterIndex = $index }
    }
    if ($beforeIndex -lt 0 -or $afterIndex -lt 0) {
        return New-SqlContainerResourceUnverified 'Samples do not bracket both measurement window boundaries.'
    }

    $before = $samples[$beforeIndex]
    $after = $samples[$afterIndex]
    $leftDistance = $WindowStartUnixMilliseconds - $before.timestamp
    $rightDistance = $after.timestamp - $WindowStopUnixMilliseconds
    if ($leftDistance -gt 2500 -or $rightDistance -gt 2500) {
        return New-SqlContainerResourceUnverified 'A bracket sample is more than 2500 ms from its window boundary.'
    }
    if ($after.cpu -lt $before.cpu -or $after.peak -lt $before.peak -or $after.oom -lt $before.oom) {
        return New-SqlContainerResourceUnverified 'A cumulative counter rolled back across the bracketed interval.'
    }

    # IO只验证实际包围区间；区间内部的设备或计数器变化仍使证据无效。
    for ($index = $beforeIndex + 1; $index -le $afterIndex; $index++) {
        $previousIo = $samples[$index - 1].io
        $currentIo = $samples[$index].io
        if ($previousIo.Count -ne $currentIo.Count -or ($previousIo.Count -gt 0 -and
            (Compare-Object -ReferenceObject @($previousIo.Keys | Sort-Object) -DifferenceObject @($currentIo.Keys | Sort-Object)))) {
            return New-SqlContainerResourceUnverified 'io.stat device set changed between samples.'
        }
        foreach ($device in $previousIo.Keys) {
            $previousCounters = $previousIo[$device]
            $currentCounters = $currentIo[$device]
            if (Compare-Object -ReferenceObject @($previousCounters.Keys | Sort-Object) -DifferenceObject @($currentCounters.Keys | Sort-Object)) {
                return New-SqlContainerResourceUnverified "io.stat counter set changed for device $device."
            }
            foreach ($counter in $previousCounters.Keys) {
                if ($currentCounters[$counter] -lt $previousCounters[$counter]) {
                    return New-SqlContainerResourceUnverified "io.stat counter $counter rolled back for device $device."
                }
            }
        }
    }

    $startDevices = @($before.io.Keys | Sort-Object)
    $stopDevices = @($after.io.Keys | Sort-Object)
    if ($startDevices.Count -ne $stopDevices.Count -or ($startDevices.Count -gt 0 -and
        (Compare-Object -ReferenceObject $startDevices -DifferenceObject $stopDevices))) {
        return New-SqlContainerResourceUnverified 'io.stat device set changed across the bracketed interval.'
    }

    $ioDevices = [System.Collections.Generic.List[object]]::new()
    foreach ($device in $startDevices) {
        $startCounters = $before.io[$device]
        $stopCounters = $after.io[$device]
        $counterNames = @($startCounters.Keys | Sort-Object)
        if (Compare-Object -ReferenceObject $counterNames -DifferenceObject @($stopCounters.Keys | Sort-Object)) {
            return New-SqlContainerResourceUnverified "io.stat counter set changed for device $device."
        }
        $delta = [ordered]@{}
        foreach ($counter in $counterNames) {
            if ($stopCounters[$counter] -lt $startCounters[$counter]) {
                return New-SqlContainerResourceUnverified "io.stat counter $counter rolled back for device $device."
            }
            $delta[$counter] = $stopCounters[$counter] - $startCounters[$counter]
        }
        $ioDevices.Add([ordered]@{ deviceId = $device; countersDelta = $delta })
    }

    $sampledPeak = [long]0
    for ($index = $beforeIndex; $index -le $afterIndex; $index++) {
        if ($samples[$index].current -gt $sampledPeak) { $sampledPeak = $samples[$index].current }
    }
    $intervalMilliseconds = $after.timestamp - $before.timestamp
    $cpuDelta = $after.cpu - $before.cpu
    $cpuPercent = $cpuDelta / ($intervalMilliseconds * 1000.0 * $CpuCount) * 100.0

    return [ordered]@{
        schema = 'SqlContainerResourceSummary-v1'
        status = 'PASS'
        scope = 'container-cgroup-not-physical-host'
        requestedWindow = [ordered]@{ startUnixMilliseconds = $WindowStartUnixMilliseconds; stopUnixMilliseconds = $WindowStopUnixMilliseconds }
        cpu = [ordered]@{
            sampling = 'bracketed-interval'
            startSampleUnixMilliseconds = $before.timestamp
            stopSampleUnixMilliseconds = $after.timestamp
            observedIntervalMilliseconds = $intervalMilliseconds
            usageMicrosecondsDelta = $cpuDelta
            cpuCount = $CpuCount
            percentOfContainerCpuLimit = [Math]::Round($cpuPercent, 3, [MidpointRounding]::AwayFromZero)
        }
        memory = [ordered]@{
            sampling = 'memory.current including bracket samples'
            sampledPeakBytes = $sampledPeak
            cgroupLifetimePeakBytesAtStart = $before.peak
            cgroupLifetimePeakBytesAtStop = $after.peak
        }
        oomKillDelta = $after.oom - $before.oom
        # 空设备列表单独保留为未观察到计数，不伪造 IO 总量，也不丢弃有效 CPU/内存。
        io = [ordered]@{
            status = if ($ioDevices.Count -eq 0) { 'NO_DEVICE_COUNTERS' } else { 'PASS' }
            scope = 'per-device counters; devices are not added together'
            devices = @($ioDevices)
        }
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    if ([string]::IsNullOrWhiteSpace($SamplesPath) -or [string]::IsNullOrWhiteSpace($ResultPath)) {
        throw 'SamplesPath and ResultPath are required.'
    }
    $summary = Get-SqlContainerResourceSummary -SamplesPath $SamplesPath `
        -WindowStartUnixMilliseconds $WindowStartUnixMilliseconds `
        -WindowStopUnixMilliseconds $WindowStopUnixMilliseconds -CpuCount $CpuCount
    $resultFile = [System.IO.Path]::GetFullPath($ResultPath)
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($resultFile)) | Out-Null
    [System.IO.File]::WriteAllText($resultFile, ($summary | ConvertTo-Json -Depth 8) + "`n",
        [System.Text.UTF8Encoding]::new($false))
    Write-Output "$($summary.status): $resultFile"
    if ($summary.status -ne 'PASS') { exit 1 }
}
