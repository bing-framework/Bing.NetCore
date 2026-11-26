#requires -Version 7.0
[CmdletBinding()]
param([string]$ResultsDirectory = 'artifacts/validation/r02-stage-diagnostic/tests')

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'

$repository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../..')).Path
$outputRoot = [System.IO.Path]::GetFullPath($ResultsDirectory, $repository)
$runRoot = Join-Path $outputRoot ('stage-diagnostic-' + [Guid]::NewGuid().ToString('N'))
$scriptUnderTest = Join-Path $PSScriptRoot '../Invoke-SqlMixedStageDiagnostic.ps1'
$utf8 = [System.Text.UTF8Encoding]::new($false)
$cases = [System.Collections.Generic.List[object]]::new()

function Test-Case([string]$Name, [scriptblock]$Body) {
    try {
        & $Body
        $script:cases.Add([pscustomobject]@{ name = $Name; status = 'PASS' })
    }
    catch {
        $script:cases.Add([pscustomobject]@{ name = $Name; status = 'FAIL'; error = $_.Exception.Message })
    }
}

function Assert([bool]$Condition, [string]$Message = 'Assertion failed') {
    if (-not $Condition) { throw $Message }
}

function New-DatabaseCommandStats([int]$Calls) {
    $totalExecutionMilliseconds = $Calls * 0.25
    return [pscustomobject]@{
        calls = $Calls
        rows = $Calls
        totalExecutionMilliseconds = $totalExecutionMilliseconds
        meanExecutionMilliseconds = if ($Calls -eq 0) { $null } else { $totalExecutionMilliseconds / $Calls }
        sharedBlocksHit = $Calls * 2
        sharedBlocksRead = 0
        sharedBlocksDirtied = $Calls
        sharedBlocksWritten = 0
        blockReadMilliseconds = 0
        blockWriteMilliseconds = 0
        walRecords = $Calls
        walBytes = $Calls * 24
    }
}

function New-StageReport([int]$ReadAttempts = 90, [int]$WriteAttempts = 10) {
    $attempted = $ReadAttempts + $WriteAttempts
    return [pscustomobject]@{
        schema = 'SqlMixedStageDiagnostic-v2'
        profile = '1C2G'
        concurrency = 1
        measurement = [pscustomobject]@{
            measure = [pscustomobject]@{
                attempted = $attempted
                readAttempts = $ReadAttempts
                writeAttempts = $WriteAttempts
            }
            diagnosticConnectionOpen = [pscustomobject]@{
                count = $attempted
                failed = 0
                totalTicks = 100
                maxTicks = 10
                meanMilliseconds = 0.1
                maxMilliseconds = 1
            }
            diagnosticDatabaseWaits = [pscustomobject]@{
                intervalMilliseconds=100; sampleCount=5; activeSessionObservations=4
                lockObservations=0; walSyncObservations=2; walWriteObservations=0
                otherIoObservations=0; noWaitEventObservations=1; otherWaitObservations=1
            }
            diagnosticDatabaseCommands = [pscustomobject]@{
                read = New-DatabaseCommandStats $ReadAttempts
                write = New-DatabaseCommandStats $WriteAttempts
                wal = [pscustomobject]@{
                    records = $WriteAttempts
                    bytes = $WriteAttempts * 24
                    writes = 0
                    syncs = 0
                    writeMilliseconds = 0
                    syncMilliseconds = 0
                    statsReset = '2026-11-25T00:00:00Z'
                }
                statementsReset = '2026-11-25T00:00:00Z'
            }
            diagnosticStages = @(
                New-StageSummary 'scope-and-compose' $attempted
                New-StageSummary 'read-freeze-plan' $ReadAttempts
                New-StageSummary 'read-execute-and-materialize' $ReadAttempts
                New-StageSummary 'write-execute' $WriteAttempts
            )
        }
    }
}

function New-StageSummary([string]$Name, [int]$Count) {
    return [pscustomobject]@{
        stage=$Name; count=$Count; totalTicks=1000
        latencyBucketWidthMilliseconds=0.1; latencyHistogramMaxMilliseconds=100
        latencyHistogramOverflowCount=0
        p50Milliseconds=$(if ($Count) { 0.1 } else { $null })
        p95Milliseconds=$(if ($Count) { 0.2 } else { $null })
        p99Milliseconds=$(if ($Count) { 0.3 } else { $null })
    }
}

function New-CpuCapture {
    return [pscustomobject]@{
        status = 'PASS'
        exitCode = 0
        processId = 4321
        binarySha256 = 'binary-hash'
        collectorSha256 = 'collector-hash'
        traceSha256 = 'trace-hash'
    }
}

function New-CpuSummary {
    return [pscustomobject]@{
        status = 'PASS'
        processId = 4321
        traceSha256 = 'trace-hash'
        measurementWindowValid = $true
        observationWindow = 'measure-through-drain'
        StartMilliseconds = 100
        StopMilliseconds = 200
    }
}

if (Test-Path -LiteralPath $scriptUnderTest -PathType Leaf) {
    . $scriptUnderTest
}

Test-Case 'StageDiagnosticScriptDefinesRequiredFunctions' {
    Assert (Test-Path -LiteralPath $scriptUnderTest -PathType Leaf) 'Diagnostic script is missing.'
    Assert ($null -ne (Get-Command Get-SqlMixedStageArguments -ErrorAction SilentlyContinue)) 'Argument helper is missing.'
    Assert ($null -ne (Get-Command Assert-SqlMixedStageReport -ErrorAction SilentlyContinue)) 'Report validator is missing.'
}

Test-Case 'StageDiagnosticArgumentsBindOneCoreAndOneWorker' {
    $workload = Join-Path $runRoot 'fixed workload.json'
    $actual = @(Get-SqlMixedStageArguments $workload)
    $expected = @('--mixed-stage-probe', $workload, '1C2G', '1', '/results/stages.json')
    Assert (($actual -join "`n") -ceq ($expected -join "`n")) 'Diagnostic arguments did not preserve the fixed profile, concurrency, workload, and output path.'
}

Test-Case 'StageDiagnosticArgumentsAllowCustomOutputPath' {
    $workload = Join-Path $runRoot 'fixed workload.json'
    $output = '/results/stage report.json'
    $actual = @(Get-SqlMixedStageArguments $workload $output)
    $expected = @('--mixed-stage-probe', $workload, '1C2G', '1', $output)
    Assert (($actual -join "`n") -ceq ($expected -join "`n")) 'A custom output path changed the fixed workload arguments.'
}

Test-Case 'StageDiagnosticCommandKeepsDefaultCapacityInvocation' {
    $workload = '/app/fixed workload.json'
    $actual = @(Get-SqlMixedStageCommand -WorkloadPath $workload)
    $expected = @('timeout', '240s', 'dotnet', '/app/Bing.Data.Sql.Benchmarks.dll',
        '--mixed-stage-probe', $workload, '1C2G', '1', '/results/stages.json')
    Assert (($actual -join "`n") -ceq ($expected -join "`n")) 'The default stage invocation changed.'
}

Test-Case 'StageDiagnosticCpuCommandWrapsWorkloadAndPreservesPathsWithSpaces' {
    $workload = '/app/validation workload.json'
    $actual = @(Get-SqlMixedStageCommand -WorkloadPath $workload -WithCpu)
    $expected = @('timeout', '300s', 'dotnet', '/results/cpu-tool/SqlCpuTrace.dll', 'capture-workload',
        '/app/Bing.Data.Sql.Benchmarks.dll', '/results/cpu', '240', '--',
        '--mixed-stage-probe', $workload, '1C2G', '1', '{output}')
    Assert (($actual -join "`n") -ceq ($expected -join "`n")) 'The CPU capture command did not preserve the workload path and output placeholder.'
}

Test-Case 'StageDiagnosticCpuReportAcceptsMatchingCandidateAndWindow' {
    Assert-SqlMixedStageCpuReport (New-CpuCapture) (New-CpuSummary) 'binary-hash' 'collector-hash' 'trace-hash'
}

Test-Case 'StageDiagnosticCpuReportRejectsCandidateMismatch' {
    $rejected = $false
    try { Assert-SqlMixedStageCpuReport (New-CpuCapture) (New-CpuSummary) 'other-binary' 'collector-hash' 'trace-hash' }
    catch { $rejected = $true }
    Assert $rejected 'A CPU report for another benchmark candidate was accepted.'
}

Test-Case 'StageDiagnosticCpuReportRejectsCollectorMismatch' {
    $rejected = $false
    try { Assert-SqlMixedStageCpuReport (New-CpuCapture) (New-CpuSummary) 'binary-hash' 'other-collector' 'trace-hash' }
    catch { $rejected = $true }
    Assert $rejected 'A CPU report from another collector was accepted.'
}

Test-Case 'StageDiagnosticCpuReportRejectsTraceMismatch' {
    $rejected = $false
    try { Assert-SqlMixedStageCpuReport (New-CpuCapture) (New-CpuSummary) 'binary-hash' 'collector-hash' 'other-trace' }
    catch { $rejected = $true }
    Assert $rejected 'A CPU report for another trace was accepted.'
}

Test-Case 'StageDiagnosticCpuReportRejectsFailedCapture' {
    $capture = New-CpuCapture
    $capture.status = 'FAIL'
    $rejected = $false
    try { Assert-SqlMixedStageCpuReport $capture (New-CpuSummary) 'binary-hash' 'collector-hash' 'trace-hash' }
    catch { $rejected = $true }
    Assert $rejected 'A failed CPU capture was accepted.'
}

Test-Case 'StageDiagnosticCpuReportRejectsWrongProcessId' {
    $summary = New-CpuSummary
    $summary.processId++
    $rejected = $false
    try { Assert-SqlMixedStageCpuReport (New-CpuCapture) $summary 'binary-hash' 'collector-hash' 'trace-hash' }
    catch { $rejected = $true }
    Assert $rejected 'A CPU summary for another process was accepted.'
}

Test-Case 'StageDiagnosticCpuReportRejectsMissingMeasurementWindow' {
    $summary = New-CpuSummary
    $summary.measurementWindowValid = $false
    $rejected = $false
    try { Assert-SqlMixedStageCpuReport (New-CpuCapture) $summary 'binary-hash' 'collector-hash' 'trace-hash' }
    catch { $rejected = $true }
    Assert $rejected 'A CPU summary without a valid mixed-load window was accepted.'
}

Test-Case 'StageDiagnosticCpuReportRejectsInvalidMeasurementWindow' {
    $summary = New-CpuSummary
    $summary.StopMilliseconds = $summary.StartMilliseconds
    $rejected = $false
    try { Assert-SqlMixedStageCpuReport (New-CpuCapture) $summary 'binary-hash' 'collector-hash' 'trace-hash' }
    catch { $rejected = $true }
    Assert $rejected 'A CPU summary with an invalid measurement interval was accepted.'
}

Test-Case 'StageDiagnosticAcceptsExpectedStagesAndCounts' {
    $report = New-StageReport
    Assert-SqlMixedStageReport $report
}

Test-Case 'StageDiagnosticRejectsMissingDiagnosticDatabaseCommands' {
    $report = New-StageReport
    $report.measurement.PSObject.Properties.Remove('diagnosticDatabaseCommands')
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'A report missing server command timings was accepted.'
}

Test-Case 'StageDiagnosticRejectsReadCommandCountMismatch' {
    $report = New-StageReport
    $report.measurement.diagnosticDatabaseCommands.read.calls++
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'Read command calls inconsistent with measured reads were accepted.'
}

Test-Case 'StageDiagnosticRejectsWriteCommandCountMismatch' {
    $report = New-StageReport
    $report.measurement.diagnosticDatabaseCommands.write.calls++
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'Write command calls inconsistent with measured writes were accepted.'
}

Test-Case 'StageDiagnosticRejectsReadRowsMismatch' {
    $report = New-StageReport
    $report.measurement.diagnosticDatabaseCommands.read.rows++
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'Read rows inconsistent with measured reads were accepted.'
}

Test-Case 'StageDiagnosticRejectsWriteRowsMismatch' {
    $report = New-StageReport
    $report.measurement.diagnosticDatabaseCommands.write.rows++
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'Write rows inconsistent with measured writes were accepted.'
}

Test-Case 'StageDiagnosticRejectsNegativeDatabaseCommandMetric' {
    $report = New-StageReport
    $report.measurement.diagnosticDatabaseCommands.read.sharedBlocksHit = -1
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'A negative database command metric was accepted.'
}

Test-Case 'StageDiagnosticRejectsNonFiniteDatabaseCommandMetric' {
    foreach ($value in @([double]::NaN, [double]::PositiveInfinity)) {
        $report = New-StageReport
        $report.measurement.diagnosticDatabaseCommands.read.blockReadMilliseconds = $value
        $rejected = $false
        try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
        Assert $rejected 'A non-finite database command metric was accepted.'
    }
}

Test-Case 'StageDiagnosticRejectsMissingDatabaseCommandMetric' {
    $report = New-StageReport
    $report.measurement.diagnosticDatabaseCommands.read.PSObject.Properties.Remove('walBytes')
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'A database command statistic missing a required metric was accepted.'
}

Test-Case 'StageDiagnosticRejectsMissingDatabaseWalMetric' {
    $report = New-StageReport
    $report.measurement.diagnosticDatabaseCommands.wal.PSObject.Properties.Remove('syncMilliseconds')
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'Database WAL statistics missing a required metric were accepted.'
}

Test-Case 'StageDiagnosticRejectsMissingDatabaseStatementsReset' {
    $report = New-StageReport
    $report.measurement.diagnosticDatabaseCommands.statementsReset = ' '
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'Database statement statistics without a reset timestamp were accepted.'
}

Test-Case 'StageDiagnosticRejectsMissingDatabaseWalStatsReset' {
    $report = New-StageReport
    $report.measurement.diagnosticDatabaseCommands.wal.statsReset = ''
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'Database WAL statistics without a reset timestamp were accepted.'
}

Test-Case 'StageDiagnosticRejectsDatabaseCommandMeanMismatch' {
    $report = New-StageReport
    $report.measurement.diagnosticDatabaseCommands.read.meanExecutionMilliseconds = 0.5
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'A database command mean inconsistent with total time and calls was accepted.'
}

Test-Case 'StageDiagnosticAcceptsZeroDatabaseCommandCounts' {
    $report = New-StageReport 0 0
    Assert-SqlMixedStageReport $report
    Assert ($null -eq $report.measurement.diagnosticDatabaseCommands.read.meanExecutionMilliseconds) 'A zero-call read group must have no mean.'
    Assert ($null -eq $report.measurement.diagnosticDatabaseCommands.write.meanExecutionMilliseconds) 'A zero-call write group must have no mean.'
}

Test-Case 'StageDiagnosticRejectsNonNullMeanForZeroDatabaseCommandCalls' {
    $report = New-StageReport 0 0
    $report.measurement.diagnosticDatabaseCommands.read.meanExecutionMilliseconds = 0
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'A zero-call database command group with a non-null mean was accepted.'
}

Test-Case 'StageDiagnosticRejectsMissingDiagnosticConnectionOpen' {
    $report = New-StageReport
    $report.measurement.PSObject.Properties.Remove('diagnosticConnectionOpen')
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'A report missing diagnostic connection-open measurements was accepted.'
}

Test-Case 'StageDiagnosticRejectsDiagnosticConnectionOpenCountMismatch' {
    $report = New-StageReport
    $report.measurement.diagnosticConnectionOpen.count++
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'A connection-open count inconsistent with attempted operations was accepted.'
}

Test-Case 'StageDiagnosticRejectsFailedDiagnosticConnectionOpen' {
    $report = New-StageReport
    $report.measurement.diagnosticConnectionOpen.failed = 1
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'A report with failed diagnostic connection opens was accepted.'
}

Test-Case 'StageDiagnosticRejectsNegativeDiagnosticConnectionOpenTicks' {
    $report = New-StageReport
    $report.measurement.diagnosticConnectionOpen.totalTicks = -1
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'A report with negative connection-open total ticks was accepted.'
}

Test-Case 'StageDiagnosticRejectsDiagnosticConnectionOpenTicksAboveExecutionStages' {
    $report = New-StageReport
    $report.measurement.diagnosticConnectionOpen.totalTicks = 2001
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'Connection-open ticks above the combined execution-stage ticks were accepted.'
}

Test-Case 'StageDiagnosticRejectsWrongSchema' {
    $report = New-StageReport
    $report.schema = 'SqlMixedCapacityReport-v1'
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'Wrong report schema was accepted.'
}

Test-Case 'StageDiagnosticRejectsMissingStage' {
    $report = New-StageReport
    $report.measurement.diagnosticStages = @($report.measurement.diagnosticStages | Where-Object stage -ne 'write-execute')
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'A report missing a required stage was accepted.'
}

Test-Case 'StageDiagnosticRejectsStageCountMismatch' {
    $report = New-StageReport
    $report.measurement.diagnosticStages[1].count++
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'A stage count inconsistent with the request totals was accepted.'
}

Test-Case 'StageDiagnosticRejectsConcurrencyMismatch' {
    $report = New-StageReport
    $report.concurrency = 8
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'A report from a different concurrency level was accepted.'
}

Test-Case 'StageDiagnosticRejectsProfileMismatch' {
    $report = New-StageReport
    $report.profile = '2C4G'
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'A report from a different resource profile was accepted.'
}

Test-Case 'StageDiagnosticRejectsDuplicateStageName' {
    $report = New-StageReport
    $report.measurement.diagnosticStages[3].stage = 'read-execute-and-materialize'
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'A duplicate stage name was accepted.'
}

Test-Case 'StageDiagnosticRejectsAttemptTotalDifferentFromReadAndWriteSum' {
    $report = New-StageReport
    $report.measurement.measure.attempted++
    $report.measurement.diagnosticStages[0].count++
    $rejected = $false
    try { Assert-SqlMixedStageReport $report } catch { $rejected = $true }
    Assert $rejected 'Attempted count inconsistent with read and write totals was accepted.'
}

Test-Case 'StageDiagnosticAcceptsReadOnlyAndWriteOnlyCounts' {
    foreach ($counts in @(@(1, 0), @(0, 1))) {
        $report = New-StageReport $counts[0] $counts[1]
        Assert-SqlMixedStageReport $report
    }
}

Test-Case 'StageDiagnosticAcceptsConsistentZeroReadAndWriteCounts' {
    $report = New-StageReport 0 0
    Assert-SqlMixedStageReport $report
}

Test-Case 'StageDiagnosticDispatchesDockerCommandWithoutHelperNameCollision' {
    $fixtureRoot = Join-Path $runRoot 'docker-dispatch'
    $publishDirectory = Join-Path $fixtureRoot 'publish'
    $resultsDirectory = Join-Path $fixtureRoot 'result'
    [System.IO.Directory]::CreateDirectory($publishDirectory) | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $publishDirectory 'Bing.Data.Sql.Benchmarks.dll'), '', $utf8)

    function docker { throw 'FIXTURE_DOCKER_REACHED' }
    $PublishDirectory = $publishDirectory
    $ResultsDirectory = $resultsDirectory
    $WorkloadPath = Join-Path $PSScriptRoot '../r02-workload.json'
    $caught = $false
    try { Invoke-SqlMixedStageDiagnostic | Out-Null } catch { $caught = $true }

    Assert $caught 'The injected Docker command failure should be surfaced.'
    $environmentPath = Join-Path $resultsDirectory 'environment.json'
    Assert (Test-Path -LiteralPath $environmentPath -PathType Leaf) 'The failed run did not persist its environment receipt.'
    $environment = Get-Content -LiteralPath $environmentPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert ([string]$environment.error -like '*FIXTURE_DOCKER_REACHED*') 'The Docker command fixture was not reached; helper dispatch may have recursed.'
    Assert (@($environment.cleanup).Count -eq 0) 'No Docker resources should be registered before the initial info command succeeds.'
}

Test-Case 'StageDiagnosticRetainsLegacyV1ReplayWithoutNewObservations' {
    $legacy = New-StageReport
    $legacy.schema = 'SqlMixedStageDiagnostic-v1'
    $legacy.measurement.PSObject.Properties.Remove('diagnosticDatabaseWaits')
    Assert-SqlMixedStageReport $legacy
}

foreach ($mutation in @('MissingQuantile','NaNQuantile','UnorderedQuantile','MissingWaits','InconsistentWaits','NegativeWaits','OverflowTail')) {
    Test-Case "StageDiagnosticRejects$mutation" {
        $candidate = New-StageReport
        switch ($mutation) {
            'MissingQuantile' { $candidate.measurement.diagnosticStages[0].PSObject.Properties.Remove('p99Milliseconds') }
            'NaNQuantile' { $candidate.measurement.diagnosticStages[0].p99Milliseconds = [double]::NaN }
            'UnorderedQuantile' { $candidate.measurement.diagnosticStages[0].p50Milliseconds = 1 }
            'MissingWaits' { $candidate.measurement.PSObject.Properties.Remove('diagnosticDatabaseWaits') }
            'InconsistentWaits' { $candidate.measurement.diagnosticDatabaseWaits.walSyncObservations++ }
            'NegativeWaits' { $candidate.measurement.diagnosticDatabaseWaits.otherIoObservations = -1 }
            'OverflowTail' { $candidate.measurement.diagnosticStages[0].latencyHistogramOverflowCount = 99 }
        }
        $rejected = $false
        try { Assert-SqlMixedStageReport $candidate } catch { $rejected = $_.Exception.Message -match 'latency|wait observations' }
        Assert $rejected "Invalid v2 $mutation was not rejected for its diagnostic contract."
    }
}

Test-Case 'StageDiagnosticAcceptsOverflowWithNullTail' {
    $candidate = New-StageReport
    $stage = $candidate.measurement.diagnosticStages[0]
    $stage.latencyHistogramOverflowCount = 2
    $stage.p99Milliseconds = $null
    Assert-SqlMixedStageReport $candidate
}

$report = [ordered]@{
    outputDirectory = $runRoot
    passed = @($cases | Where-Object status -eq 'PASS').Count
    failed = @($cases | Where-Object status -eq 'FAIL').Count
    cases = $cases
}
[System.IO.Directory]::CreateDirectory($runRoot) | Out-Null
[System.IO.File]::WriteAllText((Join-Path $runRoot 'stage-diagnostic-tests.json'), ($report | ConvertTo-Json -Depth 8) + [Environment]::NewLine, $utf8)
Write-Output ($report | ConvertTo-Json -Depth 8)
if ($report.failed -gt 0) { exit 1 }
exit 0
