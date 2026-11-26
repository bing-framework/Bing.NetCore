[CmdletBinding()]
param([string]$ResultsDirectory = 'artifacts/validation/sql-ci-transport/diagnostics-test')

[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
. (Join-Path $root 'eng/ci/SqlSmokeDiagnostics.ps1')
$smoke = Get-Content -LiteralPath (Join-Path $root 'eng/ci/tests/Invoke-SqlBenchmarkSmoke.Tests.ps1') -Encoding utf8 -Raw
if ($smoke -notmatch 'Start-SqlSmokeCpuMonitor' -or $smoke -notmatch 'Stop-SqlSmokeCpuMonitor' -or
    $smoke -notmatch 'cpu-attribution-progress\.jsonl' -or $smoke -notmatch 'cpu-attribution-command\.json') {
    throw 'CPU self-test must emit monitored progress and command timing.'
}
$directory = if ([IO.Path]::IsPathRooted($ResultsDirectory)) { $ResultsDirectory } else { Join-Path $root $ResultsDirectory }
$directory = [IO.Path]::GetFullPath($directory)
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$path = Join-Path $directory 'cpu-progress.jsonl'
$monitor = Start-SqlSmokeCpuMonitor -Path $path -OwnerProcessId $PID -Arguments @('run', '--self-test')
$child = $null
try {
    $child = Start-Process -FilePath (Get-Command pwsh).Source -ArgumentList '-NoProfile', '-Command', 'Start-Sleep -Seconds 20' -PassThru -WindowStyle Hidden
    $deadline = [Diagnostics.Stopwatch]::StartNew()
    do {
        Start-Sleep -Milliseconds 100
        $lines = @(Get-Content -LiteralPath $path -Encoding utf8 -ErrorAction SilentlyContinue)
        $samples = @($lines | ForEach-Object { try { $_ | ConvertFrom-Json } catch { } } | Where-Object { $_.event -eq 'sample' -and $_.processes.id -contains $child.Id })
    } while ($samples.Count -eq 0 -and $deadline.Elapsed.TotalSeconds -lt 15)
    if ($samples.Count -eq 0) { throw 'Monitor did not record the child process.' }
} finally {
    if ($child -and -not $child.HasExited) { Stop-Process -Id $child.Id -Force }
    Stop-SqlSmokeCpuMonitor -Monitor $monitor -ExitCode 0
}
$events = @(Get-Content -LiteralPath $path -Encoding utf8 | ForEach-Object { $_ | ConvertFrom-Json })
if ($events[0].event -ne 'start' -or $events[-1].event -ne 'finish' -or
    $events[-1].exitCode -ne 0 -or $events[-1].elapsedMilliseconds -lt 0) { throw 'Missing timing boundaries.' }
if (-not @($events | Where-Object { $_.event -eq 'sample' -and $_.processes.id -contains $PID }).Count) {
    throw 'Monitor did not record the owner process.'
}
if (Get-Job -Id $monitor.Job.Id -ErrorAction SilentlyContinue) { throw 'Monitor job was not removed.' }
$failurePath = Join-Path $directory 'failure-progress.jsonl'
$failureMonitor = Start-SqlSmokeCpuMonitor -Path $failurePath -OwnerProcessId $PID -Arguments @('--self-test')
try {
    throw 'controlled failure'
} catch {
    if ($_.Exception.Message -ne 'controlled failure') { throw }
} finally {
    Stop-SqlSmokeCpuMonitor -Monitor $failureMonitor -ExitCode 9
}
$failureEvents = @(Get-Content -LiteralPath $failurePath -Encoding utf8 | ForEach-Object { $_ | ConvertFrom-Json })
if ($failureEvents[-1].event -ne 'finish' -or $failureEvents[-1].exitCode -ne 9 -or
    (Get-Job -Id $failureMonitor.Job.Id -ErrorAction SilentlyContinue)) { throw 'Failure cleanup or exit status was lost.' }
Write-Output 'SQL_SMOKE_DIAGNOSTICS_PASS'
