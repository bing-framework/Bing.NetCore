#requires -Version 7.0
<#
.SYNOPSIS
    解析 PostgreSQL 16 pg_test_fsync 的一/二块同步测试；不推导业务容量。
#>
function ConvertFrom-SqlWalSyncOutput {
    [CmdletBinding()]
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Text,
        [Parameter(Mandatory)][ValidateRange(1,10)][int]$ExpectedSecondsPerTest)

    $records = [Collections.Generic.List[object]]::new()
    $headers = [Collections.Generic.HashSet[int]]::new()
    $keys = [Collections.Generic.HashSet[string]]::new()
    $seconds = $null; $writes = 0; $nonSync = $null; $inNonSync = $false
    $invariant = [Globalization.CultureInfo]::InvariantCulture
    foreach ($line in ($Text -split '\r?\n')) {
        if ($line -match '^\s*(\d+) seconds? per test\s*$') {
            if ($null -ne $seconds -or [int]$Matches[1] -ne $ExpectedSecondsPerTest) {
                throw 'Missing, duplicate or unexpected seconds-per-test header.'
            }
            $seconds = [int]$Matches[1]
        }
        elseif ($line -match '^Compare file sync methods using (one|two) 8kB writes?:\s*$') {
            $writes = if ($Matches[1] -ceq 'one') { 1 } else { 2 }
            if (!$headers.Add($writes)) { throw 'Duplicate sync method group.' }
            $inNonSync = $false
        }
        elseif ($line -cmatch "^Non-sync'ed 8kB writes:\s*$") {
            $writes = 0; $inNonSync = $true
        }
        elseif ($line -cmatch '^Compare ' -or $line -cmatch '^Test ') {
            # 后续块大小和跨描述符测试保留在原始日志，不与上述同步方法混在一起。
            $writes = 0; $inNonSync = $false
        }
        elseif (($writes -gt 0 -and $line -match '^\s*(open_datasync|fdatasync|fsync|fsync_writethrough|open_sync)\s+(.+?)\s*$') -or
            ($inNonSync -and $line -match '^\s*(write)\s+(.+?)\s*$')) {
            $method = $Matches[1]; $value = $Matches[2]
            if (!$keys.Add("${writes}:$method")) { throw 'Duplicate sync method result.' }
            $status = 'MEASURED'; $ops = $null; $micros = $null; $directIoUnsupported = $false
            if ($writes -gt 0 -and $value -cmatch '^n/a(\*)?$') {
                $status = 'UNSUPPORTED'; $directIoUnsupported = $value.EndsWith('*')
            }
            elseif ($value -cmatch '^([0-9]+(?:\.[0-9]+)?) ops/sec\s+([0-9]+(?:\.[0-9]+)?) usecs/op$') {
                $ops = [double]::Parse($Matches[1], $invariant)
                $micros = [double]::Parse($Matches[2], $invariant)
                if (![double]::IsFinite($ops) -or $ops -le 0 -or ![double]::IsFinite($micros) -or $micros -lt 0) {
                    throw 'Invalid finite positive operations/sec or nonnegative microseconds/op.'
                }
            }
            else { throw "Invalid sync method measurement: $method" }
            if ($inNonSync) {
                $nonSync = [pscustomobject]@{operationsPerSecond=$ops; meanMicrosecondsPerOperation=$micros}
            }
            else {
                $records.Add([pscustomobject]@{writesPerOperation=$writes; method=$method; status=$status;
                    operationsPerSecond=$ops; meanMicrosecondsPerOperation=$micros; directIoUnsupported=$directIoUnsupported})
            }
        }
    }
    if ($null -eq $seconds -or $headers.Count -ne 2 -or $records.Count -ne 10 -or $null -eq $nonSync) {
        throw 'Incomplete pg_test_fsync output: header, two sync groups and final non-sync write are required.'
    }
    return [pscustomobject]@{schema='SqlWalSyncDiagnostic-v1'; capacityBudgetEvaluated=$false;
        scope='file-sync-on-disposable-docker-volume'; secondsPerTest=$seconds; blockSizeBytes=8192;
        syncMethods=@($records); nonSyncWrite=$nonSync;
        interpretation='Rounded utility averages; not request latency, CPU attribution or proof of durable physical storage.'}
}
