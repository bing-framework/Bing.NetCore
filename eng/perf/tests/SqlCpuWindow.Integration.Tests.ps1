[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Collector,
    [Parameter(Mandatory)][string]$Fixture,
    [string]$ResultsDirectory = 'artifacts/validation/sql-cpu-window/tests'
)
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($ResultsDirectory)
New-Item -ItemType Directory -Path $root -Force | Out-Null
function Assert-ComponentReport([string]$Folder, $Summary) {
    $components = Get-Content -LiteralPath (Join-Path $Folder 'components.json') -Raw -Encoding utf8 | ConvertFrom-Json
    if ($components.schema -ne 'SqlCpuComponents-v1' -or $components.classificationVersion -ne 1 -or
        $components.status -ne $Summary.status -or $components.processId -ne $Summary.processId -or
        $components.traceSha256 -ne $Summary.traceSha256 -or $components.observationWindow -ne $Summary.observationWindow -or
        $components.measurementWindowValid -ne $Summary.measurementWindowValid -or
        $components.StartMilliseconds -ne $Summary.StartMilliseconds -or $components.StopMilliseconds -ne $Summary.StopMilliseconds -or
        $components.ManagedSamples -ne $Summary.ManagedSamples -or $components.Components.Count -ne 11 -or
        -not $components.twoViewsNotAdditive -or $components.allocationAttributionVerified -or
        $components.allocationAttributionStatus -ne 'NOT_COLLECTED' -or $components.readWriteSplitVerified -or
        $components.nativeKernelAttributionVerified -or $components.productionCapacityVerified -or
        $Summary.componentAttributionVerified -ne $Summary.methodCpuVerified -or $Summary.componentsReport -ne 'components.json') {
        throw 'Component scope or evidence gate differs from method report'
    }
    $leafTotal = ($components.Components | Measure-Object -Property LeafSamples -Sum).Sum
    $ownerTotal = ($components.Components | Measure-Object -Property NearestOwnerSamples -Sum).Sum
    $methodTotal = ($components.NearestOwners | Measure-Object -Property Samples -Sum).Sum
    if ($leafTotal -ne $Summary.ManagedSamples -or $ownerTotal -ne $Summary.ManagedSamples -or
        [long]$methodTotal -ne $Summary.ManagedSamples) { throw 'Component sample counts are not conserved' }
    if ($Summary.ManagedSamples -eq 0 -and @($components.Components | Where-Object {
        $null -ne $_.LeafPercent -or $null -ne $_.NearestOwnerPercent
    }).Count) { throw 'Empty sample percentages must remain unknown' }
}
& dotnet $Collector capture-workload $Fixture (Join-Path $root 'capture') 30 -- --mixed-stage-probe --window
# 无 SQL 业务栈的夹具必须保持 NOT_VERIFIED，不能因窗口正确就通过业务 CPU 验收。
if ($LASTEXITCODE -ne 1) { throw 'Fixture must not pass SQL attribution' }
$run = @(Get-ChildItem -LiteralPath (Join-Path $root 'capture') -Directory)
if ($run.Count -ne 1) { throw 'Expected one new capture' }
$receipt = Get-Content -LiteralPath (Join-Path $run[0].FullName 'capture.json') -Raw -Encoding utf8 | ConvertFrom-Json
$report = Join-Path $run[0].FullName 'report'
$summary = Get-Content -LiteralPath (Join-Path $report 'summary.json') -Raw -Encoding utf8 | ConvertFrom-Json
if ($receipt.exitCode -ne 0 -or $summary.status -ne 'NOT_VERIFIED' -or
    -not $summary.measurementWindowValid -or $summary.observationWindow -ne 'measure-through-drain' -or
    $summary.StartMilliseconds -le 0 -or $summary.StopMilliseconds -le $summary.StartMilliseconds -or
    $summary.traceDurationMilliseconds -le $summary.StopMilliseconds -or $summary.ManagedSamples -le 0 -or
    $summary.SqlStackSamples -ne 0 -or $summary.productionCapacityVerified) { throw 'Incorrect real window attribution' }
Assert-ComponentReport $report $summary
$samples = @(Get-Content -LiteralPath (Join-Path $report 'samples.jsonl') -Encoding utf8 | ForEach-Object { $_ | ConvertFrom-Json })
if ($samples.Count -eq 0 -or @($samples | Where-Object {
    $_.timeMilliseconds -lt $summary.StartMilliseconds -or $_.timeMilliseconds -ge $summary.StopMilliseconds
}).Count) { throw 'Samples leaked outside measurement window' }
$replay = Join-Path $root 'replay'
& dotnet $Collector analyze-window (Join-Path $run[0].FullName 'cpu.nettrace') $receipt.processId $replay
if ($LASTEXITCODE -ne 1 -or
    (Get-Content -LiteralPath (Join-Path $replay 'summary.json') -Raw -Encoding utf8) -ne
    (Get-Content -LiteralPath (Join-Path $report 'summary.json') -Raw -Encoding utf8)) { throw 'Window replay differs' }
if ((Get-Content -LiteralPath (Join-Path $replay 'components.json') -Raw -Encoding utf8) -ne
    (Get-Content -LiteralPath (Join-Path $report 'components.json') -Raw -Encoding utf8)) { throw 'Component replay differs' }
$wrongPid = Join-Path $root 'wrong-pid'
& dotnet $Collector analyze-window (Join-Path $run[0].FullName 'cpu.nettrace') 2147483647 $wrongPid
$wrongSummary = Get-Content -LiteralPath (Join-Path $wrongPid 'summary.json') -Raw -Encoding utf8 | ConvertFrom-Json
if ($LASTEXITCODE -ne 1 -or $wrongSummary.status -ne 'NOT_VERIFIED' -or $wrongSummary.ManagedSamples -ne 0 -or
    $wrongSummary.measurementWindowValid) { throw 'Wrong PID must not pass attribution' }
Assert-ComponentReport $wrongPid $wrongSummary
$missingRoot = Join-Path $root 'missing-window'
& dotnet $Collector capture-workload $Fixture $missingRoot 30 -- --mixed-stage-probe --no-window
if ($LASTEXITCODE -ne 1) { throw 'A mixed stage workload without markers must remain unverified' }
$missingRun = @(Get-ChildItem -LiteralPath $missingRoot -Directory)[0]
$missing = Get-Content -LiteralPath (Join-Path $missingRun.FullName 'report/summary.json') -Raw -Encoding utf8 | ConvertFrom-Json
if ($missing.status -ne 'NOT_VERIFIED' -or $missing.measurementWindowValid -or
    $missing.observationWindow -ne 'measure-through-drain' -or $missing.ManagedSamples -ne 0) {
    throw 'A mixed stage workload without markers fell back to entire-process samples'
}
$before = (Get-FileHash -LiteralPath (Join-Path $report 'components.json') -Algorithm SHA256).Hash
& dotnet $Collector analyze-window (Join-Path $run[0].FullName 'cpu.nettrace') $receipt.processId $report 2>&1 | Out-File -LiteralPath (Join-Path $root 'overwrite.log') -Encoding utf8
if ($LASTEXITCODE -ne 1 -or $before -ne (Get-FileHash -LiteralPath (Join-Path $report 'components.json') -Algorithm SHA256).Hash) {
    throw 'Existing component evidence was overwritten'
}
[IO.File]::WriteAllText((Join-Path $root 'tests.json'), (@{
    status = 'PASS'; cases = @('RealMarkersFilterSamplesAndKeepNonSqlUnverified', 'ComponentCountsAndScopeMatchManagedSamples',
        'WindowReplayMatchesCapture', 'WrongPidComponentsRemainUnverified', 'ExistingComponentReportCannotBeOverwritten',
        'MixedStageMissingMarkersCannotFallBackToEntireProcess');
    run = $run[0].FullName; realDatabaseMeasured = $false
} | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
Write-Output 'SQL_CPU_WINDOW_INTEGRATION_PASS (6)'
# 最后一次 analyze-window 的 1 是夹具无 SQL 栈的预期结果；测试成功要向同会话调用方返回 0。
exit 0
