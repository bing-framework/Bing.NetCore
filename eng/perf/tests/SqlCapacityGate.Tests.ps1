#requires -Version 7.0
[CmdletBinding()]
param([string]$ResultsDirectory = 'output/sql-capacity-gate-tests')

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$repository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../..')).Path
$gate = Join-Path $PSScriptRoot '../Invoke-SqlCapacityGate.ps1'
$pwsh = (Get-Process -Id $PID).Path
$outputRoot = [System.IO.Path]::GetFullPath($ResultsDirectory, $repository)
$runRoot = Join-Path $outputRoot ('gate-' + [Guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($runRoot) | Out-Null
$utf8 = [System.Text.UTF8Encoding]::new($false)
$cases = [System.Collections.Generic.List[object]]::new()
$candidate = 'A' * 64
$identityFileNames = @(
    'Bing.Data.Sql.Benchmarks.dll',
    'Bing.Data.dll',
    'Bing.Data.Sql.dll',
    'Bing.Dapper.Core.dll',
    'Bing.Dapper.PostgreSql.dll',
    'Dapper.dll',
    'Npgsql.dll'
)

function Test-Case([string]$Name, [scriptblock]$Body) {
    try {
        & $Body
        $cases.Add(@{ name = $Name; status = 'PASS' })
    }
    catch {
        $cases.Add(@{ name = $Name; status = 'FAIL'; error = $_.Exception.Message })
    }
}

function New-CapacityReceipt {
    return @{
        probe = 'SqlQueryCapacityProbe-v1'
        path = 'runtime'
        provider = 'PostgreSQL'
        benchmarkSha256 = $candidate
        rows = 10000
        requests = 1000
        concurrency = 16
        pooling = $true
        maxPoolSize = 32
        warmupRequests = 64
        warmupFailed = 0
        measure = @{
            successful = 990
            failed = 10
            successfulRequestsPerSecond = 1250.0
            p99Milliseconds = 12.5
        }
    }
}

function New-AssemblyFixture([string]$Name, [string[]]$ChangedFiles = @()) {
    $directory = Join-Path $runRoot $Name
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    foreach ($fileName in $identityFileNames) {
        $variant = if ($ChangedFiles -contains $fileName) { 'candidate' } else { 'baseline' }
        [System.IO.File]::WriteAllText((Join-Path $directory $fileName), "$variant`:$fileName", $utf8)
    }
    return $directory
}

function New-CapacityReceiptV2([string]$BuildDirectory) {
    $receipt = New-CapacityReceipt
    $receipt.probe = 'SqlQueryCapacityProbe-v2'
    $receipt.Remove('benchmarkSha256')
    $receipt.assemblySha256 = @{}
    foreach ($fileName in $identityFileNames) {
        $receipt.assemblySha256[$fileName] = (Get-FileHash -LiteralPath (Join-Path $BuildDirectory $fileName) -Algorithm SHA256).Hash
    }
    return $receipt
}

function New-ExpectedWorkload {
    return @{
        path = 'runtime'
        rows = 10000
        requests = 1000
        concurrency = 16
        pooling = $true
        maxPoolSize = 32
        observedServer = $false
    }
}

function Invoke-Gate([string]$Name, [string]$ReceiptJson, [int]$ExpectedExit, [string]$ExpectedStatus,
    [string]$ExpectedHash = $candidate, [string]$Throughput = '1250', [string]$P99 = '12.5', [string]$FailureRate = '0.01',
    [string]$WorkloadJson, [string]$ExpectedBuildDirectory) {
    $receiptPath = Join-Path $runRoot "$Name receipt.json"
    [System.IO.File]::WriteAllText($receiptPath, $ReceiptJson, $utf8)
    $arguments = @('-NoProfile', '-NonInteractive', '-File', $gate, '-ReceiptPath', $receiptPath)
    if ($PSBoundParameters.ContainsKey('ExpectedBuildDirectory')) {
        $arguments += @('-ExpectedBuildDirectory', $ExpectedBuildDirectory)
    }
    else {
        $arguments += @('-ExpectedBenchmarkSha256', $ExpectedHash)
    }
    $arguments += @('-MinRequestsPerSecond', $Throughput, '-MaxP99Milliseconds', $P99, '-MaxFailureRate', $FailureRate)
    if ($PSBoundParameters.ContainsKey('WorkloadJson')) {
        $workloadPath = Join-Path $runRoot "$Name expected workload.json"
        [System.IO.File]::WriteAllText($workloadPath, $WorkloadJson, $utf8)
        $arguments += @('-ExpectedWorkloadPath', $workloadPath)
    }
    $resultLines = & $pwsh @arguments 2>&1
    $code = $LASTEXITCODE
    $resultText = $resultLines -join [Environment]::NewLine
    [System.IO.File]::WriteAllText((Join-Path $runRoot "$Name gate.json"), $resultText, $utf8)
    if ($code -ne $ExpectedExit) { throw "Expected exit $ExpectedExit, got $code. Output: $resultText" }
    $result = ConvertFrom-Json -InputObject $resultText
    if ($result.status -ne $ExpectedStatus) { throw "Expected $ExpectedStatus, got $($result.status)." }
    return $result
}

function Assert-Violations($Result, [string[]]$Expected) {
    $actual = @($Result.violations)
    if (($actual -join ',') -ne ($Expected -join ',')) {
        throw ('Expected violations ' + ($Expected -join ',') + ', got ' + ($actual -join ','))
    }
}

Test-Case 'CapacityGate_PassesAtAllThreeLimits_AndBindsTheReceipt' {
    $result = Invoke-Gate 'boundary' (New-CapacityReceipt | ConvertTo-Json -Depth 5) 0 'PASS'
    Assert-Violations $result @()
    $receiptDigest = (Get-FileHash -LiteralPath (Join-Path $runRoot 'boundary receipt.json') -Algorithm SHA256).Hash
    if ($result.benchmarkSha256 -cne $candidate -or $result.receiptSha256 -cne $receiptDigest -or
        $result.observed.successfulRequestsPerSecond -ne 1250 -or $result.observed.p99Milliseconds -ne 12.5 -or
        $result.observed.failureRate -ne 0.01 -or $result.observed.failed -ne 10 -or $result.observed.requests -ne 1000 -or
        $result.workload.concurrency -ne 16 -or $result.workload.rows -ne 10000 -or $result.workload.maxPoolSize -ne 32) {
        throw 'The result did not bind the input and its observed values.'
    }
}

Test-Case 'CapacityGate_PassesWhenTheApprovedWorkloadMatches' {
    $profile = New-ExpectedWorkload
    $result = Invoke-Gate 'matching-workload' (New-CapacityReceipt | ConvertTo-Json -Depth 5) 0 'PASS' `
        -WorkloadJson ($profile | ConvertTo-Json)
    Assert-Violations $result @()
    if (@($result.workloadMismatches).Count -ne 0) { throw 'Matching workload reported a mismatch.' }
    foreach ($field in $profile.Keys) {
        if ($result.workload.$field -cne $profile[$field] -or $result.expectedWorkload.$field -cne $profile[$field]) {
            throw "The result did not record the observed and expected $field."
        }
    }
}

Test-Case 'CapacityGate_RejectsDifferentWorkloadEvenWhenMetricsPass' {
    $profile = @{
        path = 'template'; rows = 20000; requests = 2000; concurrency = 32
        pooling = $false; maxPoolSize = 64; observedServer = $true
    }
    $result = Invoke-Gate 'different-workload' (New-CapacityReceipt | ConvertTo-Json -Depth 5) 2 'INVALID' `
        -WorkloadJson ($profile | ConvertTo-Json)
    $actual = @($result.workloadMismatches | Sort-Object) -join ','
    $expected = @($profile.Keys | Sort-Object) -join ','
    if ($actual -cne $expected) { throw "Expected workload mismatches $expected, got $actual." }
}

Test-Case 'CapacityGate_RejectsIncompleteOrUntypedWorkloadInputs' {
    $receipt = New-CapacityReceipt
    $valid = $receipt | ConvertTo-Json -Depth 5
    $profile = New-ExpectedWorkload
    $profile.Remove('observedServer')
    $null = Invoke-Gate 'missing-workload-field' $valid 2 'INVALID' -WorkloadJson ($profile | ConvertTo-Json)
    $profile = New-ExpectedWorkload
    $profile.observedServer = 'false'
    $null = Invoke-Gate 'string-workload-boolean' $valid 2 'INVALID' -WorkloadJson ($profile | ConvertTo-Json)
    $profile = New-ExpectedWorkload
    $profile.concurrency = '16'
    $null = Invoke-Gate 'string-workload-count' $valid 2 'INVALID' -WorkloadJson ($profile | ConvertTo-Json)
    $profile = New-ExpectedWorkload
    $profile.writePercentage = 20
    $null = Invoke-Gate 'unsupported-business-workload' $valid 2 'INVALID' -WorkloadJson ($profile | ConvertTo-Json)
    $profile = New-ExpectedWorkload
    $receipt.maxPoolSize = '32'
    $null = Invoke-Gate 'string-receipt-pool-size' ($receipt | ConvertTo-Json -Depth 5) 2 'INVALID' `
        -WorkloadJson ($profile | ConvertTo-Json)
}

Test-Case 'CapacityGate_ReportsAllExceededLimits' {
    $result = Invoke-Gate 'limits' (New-CapacityReceipt | ConvertTo-Json -Depth 5) 1 'FAIL' -Throughput '1251' -P99 '12.49' -FailureRate '0.009'
    Assert-Violations $result @('minRequestsPerSecond', 'maxP99Milliseconds', 'maxFailureRate')
}

Test-Case 'CapacityGate_RejectsFailedWarmupEvenWhenMetricsPass' {
    $receipt = New-CapacityReceipt
    $receipt.warmupFailed = 1
    $result = Invoke-Gate 'warmup' ($receipt | ConvertTo-Json -Depth 5) 1 'FAIL'
    Assert-Violations $result @('warmupFailed')
}

Test-Case 'CapacityGate_ReportsUnavailableP99WhenEveryRequestFails' {
    $receipt = New-CapacityReceipt
    $receipt.measure.successful = 0
    $receipt.measure.failed = 1000
    $receipt.measure.successfulRequestsPerSecond = 0
    $receipt.measure.p99Milliseconds = $null
    $result = Invoke-Gate 'all-failed' ($receipt | ConvertTo-Json -Depth 5) 1 'FAIL' -Throughput '0' -FailureRate '1'
    Assert-Violations $result @('maxP99Milliseconds')
    if ($null -ne $result.observed.p99Milliseconds -or $result.observed.failureRate -ne 1) {
        throw 'Unavailable latency and measured failure rate must remain distinguishable.'
    }
}

Test-Case 'CapacityGate_RejectsAnUnrelatedCandidate' {
    $result = Invoke-Gate 'wrong-candidate' (New-CapacityReceipt | ConvertTo-Json -Depth 5) 2 'INVALID' -ExpectedHash ('B' * 64)
    if ($result.error -notmatch 'benchmark SHA256') { throw 'The result did not identify the candidate mismatch.' }
}

Test-Case 'CapacityGate_RejectsMissingOrInconsistentMetrics' {
    $receipt = New-CapacityReceipt
    $receipt.measure.Remove('p99Milliseconds')
    $null = Invoke-Gate 'missing-p99' ($receipt | ConvertTo-Json -Depth 5) 2 'INVALID'
    $receipt = New-CapacityReceipt
    $receipt.measure.failed = 9
    $null = Invoke-Gate 'wrong-count' ($receipt | ConvertTo-Json -Depth 5) 2 'INVALID'
    $receipt = New-CapacityReceipt
    $receipt.measure.successfulRequestsPerSecond = '1250'
    $null = Invoke-Gate 'string-rate' ($receipt | ConvertTo-Json -Depth 5) 2 'INVALID'
    $receipt = New-CapacityReceipt
    $receipt.concurrency = 1001
    $null = Invoke-Gate 'wrong-concurrency' ($receipt | ConvertTo-Json -Depth 5) 2 'INVALID'
}

Test-Case 'CapacityGate_RejectsInvalidThresholds' {
    $valid = New-CapacityReceipt | ConvertTo-Json -Depth 5
    $null = Invoke-Gate 'negative-throughput' $valid 2 'INVALID' -Throughput '-1'
    $null = Invoke-Gate 'invalid-failure-rate' $valid 2 'INVALID' -FailureRate '1.1'
    $null = Invoke-Gate 'text-throughput' $valid 2 'INVALID' -Throughput 'invalid'
}

Test-Case 'CapacityGate_RejectsMalformedOrUnknownReceipts' {
    $null = Invoke-Gate 'broken-json' '{"probe":}' 2 'INVALID'
    $receipt = New-CapacityReceipt
    $receipt.probe = 'other-probe'
    $null = Invoke-Gate 'unknown-schema' ($receipt | ConvertTo-Json -Depth 5) 2 'INVALID'
}

Test-Case 'CapacityGateV2_PassesWhenAllExpectedAssembliesMatch' {
    $buildDirectory = New-AssemblyFixture 'v2-matching-build'
    $receipt = New-CapacityReceiptV2 $buildDirectory
    $result = Invoke-Gate 'v2-matching' ($receipt | ConvertTo-Json -Depth 6) 0 'PASS' `
        -ExpectedBuildDirectory $buildDirectory
    if ($result.identityScope -cne 'sql-assemblies' -or @($result.assemblySha256.PSObject.Properties).Count -ne $identityFileNames.Count) {
        throw 'The result did not report all seven SQL assembly identities.'
    }
    foreach ($fileName in $identityFileNames) {
        $expectedHash = $receipt.assemblySha256[$fileName].ToUpperInvariant()
        $actualHash = $result.assemblySha256.PSObject.Properties[$fileName].Value
        if ($actualHash -cne $expectedHash) { throw "The result has an incorrect SHA256 for $fileName." }
    }
}

Test-Case 'CapacityGateV2_RejectsSameEntryWithChangedSqlAssemblies' {
    $buildDirectory = New-AssemblyFixture 'v2-mutable-build'
    $receipt = New-CapacityReceiptV2 $buildDirectory
    $receiptJson = $receipt | ConvertTo-Json -Depth 6
    $errors = [System.Collections.Generic.List[string]]::new()
    foreach ($fileName in @('Bing.Data.Sql.dll', 'Bing.Dapper.PostgreSql.dll')) {
        $path = Join-Path $buildDirectory $fileName
        $originalBytes = [System.IO.File]::ReadAllText($path, $utf8)
        try {
            [System.IO.File]::WriteAllText($path, "replacement:$fileName", $utf8)
            $result = Invoke-Gate "v2-changed-$fileName" $receiptJson 2 'INVALID' `
                -ExpectedBuildDirectory $buildDirectory
            $expectedError = "Missing or mismatched receipt SHA256 for $fileName."
            if ($result.error -cne $expectedError) { $errors.Add("$fileName returned '$($result.error)'.") }
        }
        finally {
            [System.IO.File]::WriteAllText($path, $originalBytes, $utf8)
        }
    }
    if ($errors.Count -gt 0) { throw ($errors -join ' ') }
}

Test-Case 'CapacityGateV2_RejectsMissingRequiredAssemblyFingerprint' {
    $buildDirectory = New-AssemblyFixture 'v2-missing-fingerprint-build'
    $receipt = New-CapacityReceiptV2 $buildDirectory
    $receipt.assemblySha256.Remove('Npgsql.dll')
    $result = Invoke-Gate 'v2-missing-fingerprint' ($receipt | ConvertTo-Json -Depth 6) 2 'INVALID' `
        -ExpectedBuildDirectory $buildDirectory
    if ($result.error -notmatch 'Npgsql.dll') { throw 'The result did not name the missing assembly fingerprint.' }
}

Test-Case 'CapacityGate_RejectsMixingV1AndV2IdentityInputs' {
    $buildDirectory = New-AssemblyFixture 'mixed-identity-build'
    $v2Receipt = New-CapacityReceiptV2 $buildDirectory
    $null = Invoke-Gate 'v2-with-legacy-input' ($v2Receipt | ConvertTo-Json -Depth 6) 2 'INVALID'
    $v1Receipt = New-CapacityReceipt
    $null = Invoke-Gate 'v1-with-build-input' ($v1Receipt | ConvertTo-Json -Depth 6) 2 'INVALID' `
        -ExpectedBuildDirectory $buildDirectory
}

$report = [ordered]@{
    passed = @($cases | Where-Object status -eq 'PASS').Count
    failed = @($cases | Where-Object status -eq 'FAIL').Count
    cases = $cases
}
$json = $report | ConvertTo-Json -Depth 6
[System.IO.File]::WriteAllText((Join-Path $runRoot 'capacity-gate-tests.json'), $json + [Environment]::NewLine, $utf8)
Write-Output $json
if ($report.failed -gt 0) { exit 1 }
exit 0
