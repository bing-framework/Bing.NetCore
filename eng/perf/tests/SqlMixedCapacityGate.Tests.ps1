#requires -Version 7.0
[CmdletBinding()]
param([string]$ResultsDirectory = 'artifacts/validation/r02-mixed-gate/tests')

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$repository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../..')).Path
$gate = Join-Path $PSScriptRoot '../Invoke-SqlMixedCapacityGate.ps1'
$pwsh = (Get-Process -Id $PID).Path
$outputRoot = [System.IO.Path]::GetFullPath($ResultsDirectory, $repository)
$runRoot = Join-Path $outputRoot ('gate-' + [Guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($runRoot) | Out-Null
$utf8 = [System.Text.UTF8Encoding]::new($false)
$identityFileNames = @(
    'Bing.Data.Sql.Benchmarks.dll',
    'Bing.Data.dll',
    'Bing.Data.Sql.dll',
    'Bing.Dapper.Core.dll',
    'Bing.Dapper.PostgreSql.dll',
    'Dapper.dll',
    'Npgsql.dll'
)
$workloadPath = Join-Path $repository 'eng/perf/r02-workload.json'
$expectedProfile = '2C4G'
$expectedConcurrency = '8'
$approvalReference = 'R02-EXPLORATORY-TEST-REFERENCE'

function Test-Case([string]$Name, [scriptblock]$Body) {
    try {
        & $Body
        $script:cases.Add([pscustomobject]@{ name = $Name; status = 'PASS' })
    }
    catch {
        $script:cases.Add([pscustomobject]@{ name = $Name; status = 'FAIL'; error = $_.Exception.Message })
    }
}

function New-AssemblyFixture([string]$Name, [string[]]$Omit = @()) {
    $directory = Join-Path $runRoot $Name
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    foreach ($fileName in $identityFileNames) {
        if ($Omit -contains $fileName) { continue }
        [System.IO.File]::WriteAllText((Join-Path $directory $fileName), "candidate:$fileName", $utf8)
    }
    return $directory
}

function New-ContainerLimits([string]$Profile = '2C4G', [long]$MemoryOverride = 0) {
    $cpu = switch ($Profile) { '2C4G' { 2 }; '4C8G' { 4 }; '8C16G' { 8 }; default { 2 } }
    $memory = switch ($Profile) {
        '2C4G' { 4L * 1GB }
        '4C8G' { 8L * 1GB }
        '8C16G' { 16L * 1GB }
        default { 4L * 1GB }
    }
    if ($MemoryOverride -gt 0) { $memory = $MemoryOverride }
    return @{
        cpuCount = [double]$cpu
        memoryBytes = [long]$memory
        cpuQuota = [long]($cpu * 100000)
        cpuPeriod = [long]100000
    }
}

function New-LoadResult([string]$Name, [int]$WindowMilliseconds, [int]$Attempted,
    [int]$Successful, [int]$Failed, [int]$SuccessfulWithinWindow = -1, [int]$Overflow = 0) {
    $readAttempts = [int]($Attempted - [Math]::Floor($Attempted / 10))
    $writeAttempts = [int][Math]::Floor($Attempted / 10)
    $within = if ($SuccessfulWithinWindow -ge 0) { $SuccessfulWithinWindow } else { $Successful }
    $elapsed = [double]$WindowMilliseconds + 250
    $p99 = if ($Successful -eq 0 -or ($Overflow -ge 2 -and $Successful -eq 100)) { $null } else { [double]3.0 }
    $p50 = if ($Successful -eq 0 -or $Overflow -ge $Successful) { $null } else { [double]1.0 }
    $p95 = if ($Successful -eq 0 -or ($Overflow -ge 6 -and $Successful -eq 100)) { $null } else { [double]2.0 }
    return @{
        attempted = $Attempted
        readAttempts = $readAttempts
        writeAttempts = $writeAttempts
        actualWritePercent = if ($Attempted -eq 0) { 0.0 } else { [double]($writeAttempts * 100.0 / $Attempted) }
        successful = $Successful
        successfulWithinWindow = $within
        failed = $Failed
        failedReads = [int]($Failed - $(if ($Failed -ge 10) { [Math]::Floor($Failed / 10) } else { 0 }))
        failedWrites = [int]$(if ($Failed -ge 10) { [Math]::Floor($Failed / 10) } else { 0 })
        peakInFlight = [int]4
        windowMilliseconds = [double]$WindowMilliseconds
        elapsedIncludingDrainMilliseconds = $elapsed
        completedAfterWindow = [int]($Attempted - $within - $Failed)
        admissionRequestsPerSecond = [double]($Attempted / ($WindowMilliseconds / 1000.0))
        successfulCompletionsWithinWindowPerSecond = [double]($within / ($WindowMilliseconds / 1000.0))
        successfulCompletionsIncludingDrainPerSecond = [double]($Successful / ($elapsed / 1000.0))
        commandTimeoutSeconds = [int]30
        processCpuMilliseconds = [double]25
        allocatedBytes = [long]1024
        gen0Collections = [int]0
        gen1Collections = [int]0
        gen2Collections = [int]0
        p50Milliseconds = $p50
        p95Milliseconds = $p95
        p99Milliseconds = $p99
        latencyBucketWidthMilliseconds = [int]1
        latencyHistogramMaxMilliseconds = [int]120000
        latencyHistogramOverflowCount = [int]$Overflow
        firstFailureType = if ($Failed -eq 0) { $null } else { 'System.TimeoutException' }
    }
}

function New-Receipt([string]$BuildDirectory) {
    $hashes = @{}
    foreach ($fileName in $identityFileNames) {
        $hashes[$fileName] = (Get-FileHash -LiteralPath (Join-Path $BuildDirectory $fileName) -Algorithm SHA256).Hash
    }
    return @{
        schema = 'SqlMixedCapacityReport-v1'
        probe = 'SqlMixedCapacityProbe-v1'
        workload = @{
            rows = 100000
            readPercent = 90
            writePercent = 10
            warmupSeconds = 30
            measureSeconds = 120
            maxPoolSize = 32
            thresholdStatus = 'PENDING_APPROVAL'
            capacityThresholds = $null
            buildThresholds = $null
        }
        profile = '2C4G'
        containerLimits = New-ContainerLimits
        concurrency = 8
        commandTimeoutSeconds = 30
        connectionTimeoutSeconds = 15
        connection = @{
            provider = 'PostgreSQL'
            database = 'bing_sql_aot_test'
            pooling = $true
            maxPoolSize = 32
            applicationName = 'sql_mixed_test'
        }
        assemblySha256 = $hashes
        connectionAcquisition = @{ status = 'NOT_INSTRUMENTED'; detail = 'synthetic test receipt' }
        databaseHostResources = @{ status = 'NOT_AVAILABLE'; detail = 'synthetic test receipt' }
        measurement = @{
            postgreSqlVersion = '16.1'
            warmup = New-LoadResult 'warmup' 30000 100 100 0
            measure = New-LoadResult 'measure' 120000 1000 990 10 985
            activity = @{ intervalMilliseconds = 100; sampleCount = 1200; peakActiveSessions = 8; peakLockWaitingSessions = 0 }
            warmupDatabaseStats = @{ blocksRead = 1; blocksHit = 99; tempBytes = 0; rowsInserted = 0; rowsUpdated = 0; rowsDeleted = 0; deadlocks = 0 }
            measureDatabaseStats = @{ blocksRead = 10; blocksHit = 990; tempBytes = 0; rowsInserted = 0; rowsUpdated = 100; rowsDeleted = 0; deadlocks = 0 }
        }
        runtime = '.NET 8.0.30'
        os = 'Linux test host'
        architecture = 'X64'
        processorsVisibleToProcess = 2
        connectionAcquisitionStatus = 'NOT_INSTRUMENTED'
        databaseHostResourcesStatus = 'NOT_AVAILABLE'
    }
}

function Invoke-Gate([string]$Name, $Receipt, [int]$ExpectedExit, [string]$ExpectedStatus,
    [string]$Throughput = '8.2083333333', [string]$P99 = '3', [string]$FailureRate = '0.01',
    [string]$Approval = $approvalReference, [string]$Profile = $expectedProfile,
    [string]$Concurrency = $expectedConcurrency, [string]$ExpectedWorkloadOverride) {
    $receiptPath = Join-Path $runRoot "$Name receipt.json"
    $expectedWorkload = Join-Path $runRoot "$Name expected-workload.json"
    $profileJson = if ($PSBoundParameters.ContainsKey('ExpectedWorkloadOverride')) {
        $ExpectedWorkloadOverride
    }
    else { [System.IO.File]::ReadAllText($workloadPath, $utf8) }
    [System.IO.File]::WriteAllText($expectedWorkload, $profileJson, $utf8)
    $json = if ($Receipt -is [string]) { $Receipt } else { $Receipt | ConvertTo-Json -Depth 12 }
    [System.IO.File]::WriteAllText($receiptPath, $json, $utf8)
    $arguments = @(
        '-NoProfile', '-NonInteractive', '-File', $gate,
        '-ReceiptPath', $receiptPath,
        '-ExpectedBuildDirectory', $script:activeBuildDirectory,
        '-ExpectedWorkloadPath', $expectedWorkload,
        '-ExpectedProfile', $Profile,
        '-ExpectedConcurrency', $Concurrency,
        '-BudgetApprovalId', $Approval,
        '-MinRequestsPerSecond', $Throughput,
        '-MaxP99Milliseconds', $P99,
        '-MaxFailureRate', $FailureRate
    )
    $lines = & $pwsh @arguments 2>&1
    $exitCode = $LASTEXITCODE
    $output = $lines -join [Environment]::NewLine
    [System.IO.File]::WriteAllText((Join-Path $runRoot "$Name gate-output.json"), $output, $utf8)
    if ($exitCode -ne $ExpectedExit) { throw "Expected exit $ExpectedExit, got $exitCode. Output: $output" }
    try { $result = ConvertFrom-Json -InputObject $output }
    catch { throw "Gate stdout was not one JSON object: $output" }
    if ($result.status -ne $ExpectedStatus) { throw "Expected $ExpectedStatus, got $($result.status)." }
    return $result
}

function Assert-Violations($Result, [string[]]$Expected) {
    if ((@($Result.violations) -join ',') -cne ($Expected -join ',')) {
        throw "Expected violations '$($Expected -join ',')', got '$(@($Result.violations) -join ',')'."
    }
}

$script:cases = [System.Collections.Generic.List[object]]::new()
$script:activeBuildDirectory = New-AssemblyFixture 'candidate-build'

Test-Case 'MixedGate_PassesAtBudgetBoundariesAndBindsCandidateWorkloadAndResources' {
    $receipt = New-Receipt $script:activeBuildDirectory
    $result = Invoke-Gate 'boundary' $receipt 0 'PASS' -Throughput '8.2083333333' -P99 '3' -FailureRate '0.01'
    if ($result.gate -cne 'SqlMixedCapacityGate-v1' -or $result.scope -cne 'mixed-workload-metrics' -or
        $result.productionCapacityVerified -ne $false -or $result.approvalReference -cne $approvalReference -or
        @($result.assemblySha256.PSObject.Properties).Count -ne 7 -or
        $result.profile -ne '2C4G' -or $result.concurrency -ne 8 -or
        $result.observed.successfulCompletionsWithinWindowPerSecond -ne (985 / 120.0) -or
        $result.workloadSha256 -ne (Get-FileHash -LiteralPath (Join-Path $runRoot 'boundary expected-workload.json') -Algorithm SHA256).Hash) {
        throw 'PASS must bind candidate identity, workload, resource profile, observed metrics and exploratory scope.'
    }
    Assert-Violations $result @()
}

Test-Case 'MixedGate_FailsEachApprovedMetricBudget' {
    $receipt = New-Receipt $script:activeBuildDirectory
    $throughput = Invoke-Gate 'below-throughput' $receipt 1 'FAIL' -Throughput '8.3'
    Assert-Violations $throughput @('minRequestsPerSecond')
    $p99 = Invoke-Gate 'above-p99' $receipt 1 'FAIL' -P99 '2.9'
    Assert-Violations $p99 @('maxP99Milliseconds')
    $errors = Invoke-Gate 'above-error-rate' $receipt 1 'FAIL' -FailureRate '0.009'
    Assert-Violations $errors @('maxFailureRate')
}

Test-Case 'MixedGate_AcceptsOneCoreProfileAndRejectsMismatchedLimits' {
    $receipt = New-Receipt $script:activeBuildDirectory
    $receipt.profile = '1C2G'
    $receipt.containerLimits = @{ cpuCount = 1.0; memoryBytes = 2147483648L; cpuQuota = 100000L; cpuPeriod = 100000L }
    $result = Invoke-Gate 'one-core' $receipt 0 'PASS' -Profile '1C2G'
    if ($result.profile -cne '1C2G' -or $result.productionCapacityVerified -ne $false) {
        throw 'The reduced profile must retain its identity and exploratory scope.'
    }
    $receipt.containerLimits.memoryBytes = 1073741824L
    $null = Invoke-Gate 'one-core-wrong-memory' $receipt 2 'INVALID' -Profile '1C2G'
    $receipt.containerLimits.memoryBytes = 2147483648L
    $receipt.containerLimits.cpuQuota = 200000L
    $null = Invoke-Gate 'one-core-wrong-cpu' $receipt 2 'INVALID' -Profile '1C2G'
}

Test-Case 'MixedGate_RejectsMissingApprovalOrBudget' {
    $receipt = New-Receipt $script:activeBuildDirectory
    $null = Invoke-Gate 'missing-approval' $receipt 2 'INVALID' -Approval ''
    $null = Invoke-Gate 'missing-p99-budget' $receipt 2 'INVALID' -P99 ''
    $null = Invoke-Gate 'nonfinite-budget' $receipt 2 'INVALID' -P99 'Infinity'
}

Test-Case 'MixedGate_RejectsWrongSchemaAndNonExploratoryWorkload' {
    $receipt = New-Receipt $script:activeBuildDirectory
    $receipt.schema = 'SqlQueryCapacityReport-v2'
    $null = Invoke-Gate 'wrong-schema' $receipt 2 'INVALID'
    $receipt = New-Receipt $script:activeBuildDirectory
    $receipt.workload.thresholdStatus = 'APPROVED_FOR_PRODUCTION'
    $null = Invoke-Gate 'wrong-threshold-state' $receipt 2 'INVALID'
}

Test-Case 'MixedGate_RejectsStageDiagnosticsAsCapacityEvidence' {
    $receipt = New-Receipt $script:activeBuildDirectory
    $receipt.schema = 'SqlMixedStageDiagnostic-v1'
    $result = Invoke-Gate 'stage-diagnostic' $receipt 2 'INVALID'
    if ($result.error -notmatch 'schema') { throw 'Diagnostic output must be rejected by schema.' }
}

Test-Case 'MixedGate_RejectsV2StageDiagnosticsAsCapacityEvidence' {
    $receipt = New-Receipt $script:activeBuildDirectory
    $receipt.schema = 'SqlMixedStageDiagnostic-v2'
    $result = Invoke-Gate 'stage-diagnostic-v2' $receipt 2 'INVALID'
    if ($result.error -notmatch 'schema') { throw 'V2 diagnostic output must be rejected by schema.' }
}

Test-Case 'MixedGate_RejectsWalSyncDiagnosticAsCapacityEvidence' {
    $receipt = New-Receipt $script:activeBuildDirectory
    $receipt.schema = 'SqlWalSyncDiagnostic-v1'
    $result = Invoke-Gate 'wal-sync-diagnostic' $receipt 2 'INVALID'
    if ($result.error -notmatch 'schema') { throw 'File-sync rates must not be accepted as business capacity.' }
}

Test-Case 'MixedGate_RejectsCandidateAssemblyMismatchAndMissingFingerprint' {
    $changedBuild = New-AssemblyFixture 'changed-candidate-build'
    $receipt = New-Receipt $changedBuild
    [System.IO.File]::WriteAllText((Join-Path $changedBuild 'Bing.Data.Sql.dll'), 'changed candidate', $utf8)
    $previousBuild = $script:activeBuildDirectory
    $script:activeBuildDirectory = $changedBuild
    $result = Invoke-Gate 'changed-candidate' $receipt 2 'INVALID'
    $script:activeBuildDirectory = $previousBuild
    if ($result.error -notmatch 'Bing.Data.Sql.dll') { throw 'Mismatched candidate should name its assembly.' }
    $missing = New-Receipt $script:activeBuildDirectory
    $missing.assemblySha256.Remove('Npgsql.dll')
    $result = Invoke-Gate 'missing-fingerprint' $missing 2 'INVALID'
    if ($result.error -notmatch 'Npgsql.dll') { throw 'Missing fingerprint should name its assembly.' }
}

Test-Case 'MixedGate_RejectsProfileConcurrencyAndEffectiveLimitMismatch' {
    $receipt = New-Receipt $script:activeBuildDirectory
    $null = Invoke-Gate 'profile-argument-mismatch' $receipt 2 'INVALID' -Profile '4C8G'
    $null = Invoke-Gate 'concurrency-argument-mismatch' $receipt 2 'INVALID' -Concurrency '16'
    $receipt = New-Receipt $script:activeBuildDirectory
    $receipt.containerLimits = New-ContainerLimits '2C4G' (3L * 1GB)
    $null = Invoke-Gate 'memory-limit-mismatch' $receipt 2 'INVALID'
    $receipt = New-Receipt $script:activeBuildDirectory
    $receipt.containerLimits.cpuCount = 1
    $null = Invoke-Gate 'cpu-limit-mismatch' $receipt 2 'INVALID'
}

Test-Case 'MixedGate_RejectsFixedWorkloadAndWindowDrift' {
    $receipt = New-Receipt $script:activeBuildDirectory
    $receipt.workload.rows = 99999
    $null = Invoke-Gate 'rows-drift' $receipt 2 'INVALID'
    $receipt = New-Receipt $script:activeBuildDirectory
    $receipt.workload.readPercent = 80
    $null = Invoke-Gate 'ratio-drift' $receipt 2 'INVALID'
    $receipt = New-Receipt $script:activeBuildDirectory
    $receipt.measurement.measure.windowMilliseconds = 119000
    $null = Invoke-Gate 'window-drift' $receipt 2 'INVALID'
    $expected = Get-Content -LiteralPath $workloadPath -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
    $expected.profiles = '2C4G'
    $null = Invoke-Gate 'profile-array-drift' (New-Receipt $script:activeBuildDirectory) 2 'INVALID' `
        -ExpectedWorkloadOverride ($expected | ConvertTo-Json -Depth 8)
    $expected = Get-Content -LiteralPath $workloadPath -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
    $expected.concurrency = 8
    $null = Invoke-Gate 'concurrency-array-drift' (New-Receipt $script:activeBuildDirectory) 2 'INVALID' `
        -ExpectedWorkloadOverride ($expected | ConvertTo-Json -Depth 8)
}

Test-Case 'MixedGate_RejectsInconsistentCountsAndForgedRates' {
    $receipt = New-Receipt $script:activeBuildDirectory
    $receipt.measurement.measure.writeAttempts = 99
    $null = Invoke-Gate 'write-count-drift' $receipt 2 'INVALID'
    $receipt = New-Receipt $script:activeBuildDirectory
    $receipt.measurement.measure.successful = 991
    $null = Invoke-Gate 'result-count-drift' $receipt 2 'INVALID'
    $receipt = New-Receipt $script:activeBuildDirectory
    $receipt.measurement.measure.successfulCompletionsWithinWindowPerSecond = 9999
    $null = Invoke-Gate 'forged-rate' $receipt 2 'INVALID'
}

Test-Case 'MixedGate_FailsWarmupErrorsAndWindowsWithNoSuccesses' {
    $receipt = New-Receipt $script:activeBuildDirectory
    $receipt.measurement.warmup = New-LoadResult 'warmup' 30000 100 99 1 99
    $result = Invoke-Gate 'warmup-failure' $receipt 1 'FAIL'
    Assert-Violations $result @('warmup')
    $receipt = New-Receipt $script:activeBuildDirectory
    $receipt.measurement.measure.successful = 0
    $receipt.measurement.measure.successfulWithinWindow = 0
    $receipt.measurement.measure.failed = 1000
    $receipt.measurement.measure.failedReads = 900
    $receipt.measurement.measure.failedWrites = 100
    $receipt.measurement.measure.actualWritePercent = 10
    $receipt.measurement.measure.successfulCompletionsWithinWindowPerSecond = 0
    $receipt.measurement.measure.successfulCompletionsIncludingDrainPerSecond = 0
    $receipt.measurement.measure.completedAfterWindow = 0
    $receipt.measurement.measure.p50Milliseconds = $null
    $receipt.measurement.measure.p95Milliseconds = $null
    $receipt.measurement.measure.p99Milliseconds = $null
    $result = Invoke-Gate 'empty-success-window' $receipt 1 'FAIL' -Throughput '0' -FailureRate '1'
    Assert-Violations $result @('noSuccessfulRequests', 'maxP99Milliseconds')
}

Test-Case 'MixedGate_FailsWhenP99FallsIntoHistogramOverflow' {
    $receipt = New-Receipt $script:activeBuildDirectory
    $receipt.measurement.measure.attempted = 100
    $receipt.measurement.measure.successful = 100
    $receipt.measurement.measure.failed = 0
    $receipt.measurement.measure.readAttempts = 90
    $receipt.measurement.measure.writeAttempts = 10
    $receipt.measurement.measure.failedReads = 0
    $receipt.measurement.measure.failedWrites = 0
    $receipt.measurement.measure.successfulWithinWindow = 100
    $receipt.measurement.measure.admissionRequestsPerSecond = 100 / 120
    $receipt.measurement.measure.successfulCompletionsWithinWindowPerSecond = 100 / 120
    $receipt.measurement.measure.successfulCompletionsIncludingDrainPerSecond = 100 / 120.25
    $receipt.measurement.measure.p99Milliseconds = $null
    $receipt.measurement.measure.latencyHistogramOverflowCount = 2
    $result = Invoke-Gate 'overflow-p99' $receipt 1 'FAIL' -Throughput '0' -FailureRate '1'
    if ($null -ne $result.observed.p99Milliseconds) { throw 'Overflow P99 must remain unavailable.' }
}

$report = [ordered]@{
    outputDirectory = $runRoot
    passed = @($script:cases | Where-Object status -eq 'PASS').Count
    failed = @($script:cases | Where-Object status -eq 'FAIL').Count
    cases = $script:cases
}
$reportPath = Join-Path $runRoot 'mixed-capacity-gate-tests.json'
[System.IO.File]::WriteAllText($reportPath, ($report | ConvertTo-Json -Depth 8) + [Environment]::NewLine, $utf8)
Write-Output ($report | ConvertTo-Json -Depth 8)
if ($report.failed -gt 0) { exit 1 }
exit 0
