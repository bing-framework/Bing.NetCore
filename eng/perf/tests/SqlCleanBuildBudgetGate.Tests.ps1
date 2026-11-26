#requires -Version 7.0
[CmdletBinding()]
param([string]$ResultsDirectory = 'artifacts/validation/r02/build-gate/tests')

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$resultRoot = [System.IO.Path]::GetFullPath($ResultsDirectory, $repositoryRoot)
[System.IO.Directory]::CreateDirectory($resultRoot) | Out-Null
$runRoot = Join-Path $resultRoot ('run-' + [Guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($runRoot) | Out-Null
$utf8 = [System.Text.UTF8Encoding]::new($false)
$gate = Join-Path $PSScriptRoot '../Invoke-SqlBuildBudgetGate.ps1'
$runtimeRoot = Join-Path $runRoot 'runtime'
[System.IO.Directory]::CreateDirectory($runtimeRoot) | Out-Null
$sourceManifest = Join-Path $runRoot 'source-manifest.json'
[System.IO.File]::WriteAllText($sourceManifest, '{"inputs":{"src.cs":"abc"},"count":1}', $utf8)
$sourceHash = (Get-FileHash -LiteralPath $sourceManifest -Algorithm SHA256).Hash
$binaryFile = Join-Path $runtimeRoot 'Bing.Data.Sql.Benchmarks.dll'
$runtimeRelativeFiles = @(
    'Bing.Data.Sql.Benchmarks.dll',
    'Bing.Data.Sql.Benchmarks.deps.json',
    'Bing.Data.Sql.Benchmarks.runtimeconfig.json',
    'Bing.Data.Sql.dll',
    'runtimes/win-x64/native/sqlnative.dll'
)
[System.IO.File]::WriteAllText($binaryFile, 'candidate assembly', $utf8)
[System.IO.File]::WriteAllText((Join-Path $runtimeRoot 'Bing.Data.Sql.Benchmarks.deps.json'), '{"runtimeTarget":{}}', $utf8)
[System.IO.File]::WriteAllText((Join-Path $runtimeRoot 'Bing.Data.Sql.Benchmarks.runtimeconfig.json'), '{"runtimeOptions":{}}', $utf8)
[System.IO.File]::WriteAllText((Join-Path $runtimeRoot 'Bing.Data.Sql.dll'), 'candidate dependency', $utf8)
$nativeDependency = Join-Path $runtimeRoot 'runtimes/win-x64/native/sqlnative.dll'
[System.IO.Directory]::CreateDirectory((Split-Path -Parent $nativeDependency)) | Out-Null
[System.IO.File]::WriteAllText($nativeDependency, 'candidate native dependency', $utf8)
$binaryHash = (Get-FileHash -LiteralPath $binaryFile -Algorithm SHA256).Hash

function New-BuildReceipt([string]$Name, [string]$Status = 'PASS', [bool]$TimedOut = $false,
    [int]$ExitCode = 0, [double]$PeakBytes = 536870912) {
    $file = Join-Path $runRoot "$Name.json"
    $runtimeFiles = [ordered]@{}
    foreach ($relative in $runtimeRelativeFiles) {
        $runtimeFile = Join-Path $runtimeRoot $relative
        $runtimeFiles[$relative] = (Get-FileHash -LiteralPath $runtimeFile -Algorithm SHA256).Hash
    }
    $runtimeManifestValue = [ordered]@{
        root = (Resolve-Path -LiteralPath $runtimeRoot).Path
        files = $runtimeFiles
        count = $runtimeFiles.Count
    }
    $runtimeManifest = Join-Path $runRoot "$Name.runtime-manifest.json"
    [System.IO.File]::WriteAllText($runtimeManifest, ($runtimeManifestValue | ConvertTo-Json -Depth 5), $utf8)
    $receipt = [ordered]@{
        status = $Status
        evidenceKind = 'CleanBuildProcessTree'
        peakMetric = 'WindowsJobCommittedBytes'
        source = [ordered]@{ clean = $true; manifest = $sourceManifest; inputCount = 1 }
        binary = [ordered]@{ file = $binaryFile; sha256 = $binaryHash }
        runtime = [ordered]@{ root = $runtimeManifestValue.root; manifest = $runtimeManifest; count = $runtimeManifestValue.count }
        measurement = [ordered]@{ ExitCode = $ExitCode; TimedOut = $TimedOut; PeakJobCommittedBytes = $PeakBytes }
    }
    [System.IO.File]::WriteAllText($file, ($receipt | ConvertTo-Json -Depth 5), $utf8)
    return $file
}

function Invoke-Gate([string]$Receipt, [string]$ExpectedManifestHash = $sourceHash,
    [string]$ExpectedAssemblyHash = $binaryHash, [string]$BudgetId = 'APPROVED-CI-MEM-01',
    [string]$BudgetMiB = '1024', [string]$ExpectedRuntimeManifestHash = '') {
    $resultPath = Join-Path $runRoot ('gate-' + [Guid]::NewGuid().ToString('N') + '.json')
    $gateArgs = @('-NoProfile', '-File', $gate,
        '-ReceiptPath', $Receipt, '-ExpectedSourceManifestSha256', $ExpectedManifestHash,
        '-ExpectedBinarySha256', $ExpectedAssemblyHash, '-BudgetApprovalId', $BudgetId,
        '-MaxCommittedMiB', $BudgetMiB, '-ResultPath', $resultPath)
    if (-not [string]::IsNullOrWhiteSpace($ExpectedRuntimeManifestHash)) {
        $gateArgs += @('-ExpectedRuntimeManifestSha256', $ExpectedRuntimeManifestHash)
    }
    $output = & (Get-Command pwsh).Source @gateArgs 2>&1
    $exitCode = $LASTEXITCODE
    $text = ($output | Out-String).Trim()
    $json = $null
    try { $json = ConvertFrom-Json -AsHashtable -NoEnumerate -InputObject $text } catch { }
    return [pscustomobject]@{ exitCode = $exitCode; report = $json; output = $text }
}

$cases = [System.Collections.Generic.List[object]]::new()
function Test-Case([string]$Name, [scriptblock]$Body) {
    try { & $Body; $cases.Add([pscustomobject]@{ name = $Name; status = 'PASS' }) }
    catch { $cases.Add([pscustomobject]@{ name = $Name; status = 'FAIL'; error = $_.Exception.Message }) }
}

Test-Case 'BudgetGate_PassesWhenCleanBuildMatchesIdentityAndStaysWithinApprovedBudget' {
    $result = Invoke-Gate (New-BuildReceipt 'within')
    if ($result.exitCode -ne 0 -or $result.report.status -ne 'PASS' -or
        $result.report.budget.approvalReference -ne 'APPROVED-CI-MEM-01' -or
        $result.report.observed.peakCommittedMiB -ne 512) { throw "Unexpected gate result: $($result.output)" }
}

Test-Case 'BudgetGate_FailsWhenPeakExceedsApprovedBudget' {
    $result = Invoke-Gate (New-BuildReceipt 'over' -PeakBytes 2147483648)
    if ($result.exitCode -ne 1 -or $result.report.status -ne 'FAIL' -or
        $result.report.violations -notcontains 'maxCommittedMiB') { throw "Expected budget failure: $($result.output)" }
}

Test-Case 'BudgetGate_RejectsTimedOutBuildAsInvalidEvidence' {
    $result = Invoke-Gate (New-BuildReceipt 'timeout' -Status 'TIMEOUT' -TimedOut $true -ExitCode 124)
    if ($result.exitCode -ne 2 -or $result.report.status -ne 'INVALID') { throw "Timeout must not be budget pass/fail evidence: $($result.output)" }
}

Test-Case 'BudgetGate_RejectsCandidateIdentityMismatch' {
    $result = Invoke-Gate (New-BuildReceipt 'identity') -ExpectedAssemblyHash ('B2' * 32)
    if ($result.exitCode -ne 2 -or $result.report.status -ne 'INVALID') { throw "Expected candidate identity rejection: $($result.output)" }
}

Test-Case 'BuildBudgetGate_PassesForMatchingRuntimeDependencyManifest' {
    $receipt = New-BuildReceipt 'runtime-match'
    $manifestFile = Join-Path $runRoot 'runtime-match.runtime-manifest.json'
    $manifestHash = (Get-FileHash -LiteralPath $manifestFile -Algorithm SHA256).Hash
    $result = Invoke-Gate $receipt -ExpectedRuntimeManifestHash $manifestHash
    if ($result.exitCode -ne 0 -or $result.report.status -ne 'PASS' -or
        $result.report.candidate.runtimeManifestSha256 -ine $manifestHash) {
        throw "Build budget gate did not accept the matching runtime dependency manifest: $($result.output)"
    }
}

Test-Case 'BuildBudgetGate_RejectsTamperedOrMissingRuntimeDependencyWithEntryUnchanged' {
    $originalEntryHash = (Get-FileHash -LiteralPath $binaryFile -Algorithm SHA256).Hash
    $originalDependency = [System.IO.File]::ReadAllText((Join-Path $runtimeRoot 'Bing.Data.Sql.dll'), $utf8)
    try {
        $tamperedReceipt = New-BuildReceipt 'runtime-tampered'
        $tamperedManifestHash = (Get-FileHash -LiteralPath (Join-Path $runRoot 'runtime-tampered.runtime-manifest.json') -Algorithm SHA256).Hash
        [System.IO.File]::WriteAllText((Join-Path $runtimeRoot 'Bing.Data.Sql.dll'), 'tampered dependency', $utf8)
        $tampered = Invoke-Gate $tamperedReceipt -ExpectedRuntimeManifestHash $tamperedManifestHash
        if ($tampered.exitCode -ne 2 -or $tampered.report.status -ne 'INVALID') {
            throw "Build budget gate accepted a tampered runtime dependency: $($tampered.output)"
        }
        if ((Get-FileHash -LiteralPath $binaryFile -Algorithm SHA256).Hash -ine $originalEntryHash) {
            throw 'Entry assembly changed during runtime dependency tamper case'
        }

        [System.IO.File]::WriteAllText((Join-Path $runtimeRoot 'Bing.Data.Sql.dll'), $originalDependency, $utf8)
        $missingReceipt = New-BuildReceipt 'runtime-missing'
        $missingManifestHash = (Get-FileHash -LiteralPath (Join-Path $runRoot 'runtime-missing.runtime-manifest.json') -Algorithm SHA256).Hash
        Remove-Item -LiteralPath (Join-Path $runtimeRoot 'Bing.Data.Sql.dll')
        $missing = Invoke-Gate $missingReceipt -ExpectedRuntimeManifestHash $missingManifestHash
        if ($missing.exitCode -ne 2 -or $missing.report.status -ne 'INVALID') {
            throw "Build budget gate accepted a missing runtime dependency: $($missing.output)"
        }
        if ((Get-FileHash -LiteralPath $binaryFile -Algorithm SHA256).Hash -ine $originalEntryHash) {
            throw 'Entry assembly changed during runtime dependency missing case'
        }
    }
    finally { [System.IO.File]::WriteAllText((Join-Path $runtimeRoot 'Bing.Data.Sql.dll'), $originalDependency, $utf8) }
}

Test-Case 'BuildBudgetGate_RejectsRuntimeManifestHashMismatch' {
    $receipt = New-BuildReceipt 'runtime-identity-mismatch'
    $result = Invoke-Gate $receipt -ExpectedRuntimeManifestHash ('C3' * 32)
    if ($result.exitCode -ne 2 -or $result.report.status -ne 'INVALID') {
        throw "Build budget gate accepted a mismatched runtime manifest: $($result.output)"
    }
}

Test-Case 'BuildBudgetGate_RejectsUnlistedRuntimeOutputFile' {
    $receipt = New-BuildReceipt 'runtime-unlisted'
    $manifestHash = (Get-FileHash -LiteralPath (Join-Path $runRoot 'runtime-unlisted.runtime-manifest.json') -Algorithm SHA256).Hash
    $unlistedFile = Join-Path $runtimeRoot 'Unexpected.RuntimeDependency.dll'
    $entryHash = (Get-FileHash -LiteralPath $binaryFile -Algorithm SHA256).Hash
    try {
        [System.IO.File]::WriteAllText($unlistedFile, 'unlisted runtime dependency', $utf8)
        $result = Invoke-Gate $receipt -ExpectedRuntimeManifestHash $manifestHash
        if ($result.exitCode -ne 2 -or $result.report.status -ne 'INVALID') {
            throw "Build budget gate accepted an unlisted runtime output file: $($result.output)"
        }
        if ((Get-FileHash -LiteralPath $binaryFile -Algorithm SHA256).Hash -ine $entryHash) {
            throw 'Entry assembly changed during unlisted runtime output case'
        }
    }
    finally {
        if (Test-Path -LiteralPath $unlistedFile) { Remove-Item -LiteralPath $unlistedFile -Force }
    }
}

Test-Case 'BudgetGate_RehashesBuiltBinaryArtifact' {
    $receipt = New-BuildReceipt 'tampered-binary'
    [System.IO.File]::WriteAllText($binaryFile, 'replaced assembly', $utf8)
    $result = Invoke-Gate $receipt
    if ($result.exitCode -ne 2 -or $result.report.status -ne 'INVALID') { throw "Expected binary artifact identity rejection: $($result.output)" }
    [System.IO.File]::WriteAllText($binaryFile, 'candidate assembly', $utf8)
}

Test-Case 'BudgetGate_RejectsNonFiniteOrMissingPeak' {
    $receiptPath = New-BuildReceipt 'missing-peak'
    $receipt = ConvertFrom-Json -AsHashtable -NoEnumerate -InputObject ([System.IO.File]::ReadAllText($receiptPath, $utf8))
    $receipt.measurement.Remove('PeakJobCommittedBytes')
    [System.IO.File]::WriteAllText($receiptPath, ($receipt | ConvertTo-Json -Depth 5), $utf8)
    $result = Invoke-Gate $receiptPath
    if ($result.exitCode -ne 2 -or $result.report.status -ne 'INVALID') { throw "Expected invalid measurement rejection: $($result.output)" }

    $zeroResult = Invoke-Gate (New-BuildReceipt 'zero-peak' -PeakBytes 0)
    if ($zeroResult.exitCode -ne 2 -or $zeroResult.report.status -ne 'INVALID') {
        throw "Zero committed-memory peak must be invalid evidence: $($zeroResult.output)"
    }
}

Test-Case 'BudgetGate_RejectsNonFinitePeak' {
    $receiptPath = New-BuildReceipt 'infinite-peak'
    $text = [System.IO.File]::ReadAllText($receiptPath, $utf8)
    $text = [System.Text.RegularExpressions.Regex]::Replace($text, '"PeakJobCommittedBytes"\s*:\s*[^,}\r\n]+', '"PeakJobCommittedBytes":1e999')
    [System.IO.File]::WriteAllText($receiptPath, $text, $utf8)
    $result = Invoke-Gate $receiptPath
    if ($result.exitCode -ne 2 -or $result.report.status -ne 'INVALID') { throw "Expected non-finite peak rejection: $($result.output)" }
}

Test-Case 'BudgetGate_RequiresAnApprovalReference' {
    $result = Invoke-Gate (New-BuildReceipt 'approval') -BudgetId ''
    if ($result.exitCode -ne 2 -or $result.report.status -ne 'INVALID' -or
        $result.report.error -notlike 'Required input is missing: BudgetApprovalId.*') {
        throw "Expected missing approval rejection: $($result.output)"
    }
}

Test-Case 'BudgetGate_RequiresAnExplicitBudgetValue' {
    $result = Invoke-Gate (New-BuildReceipt 'budget') -BudgetMiB ''
    if ($result.exitCode -ne 2 -or $result.report.status -ne 'INVALID' -or
        $result.report.error -notlike 'Required input is missing: MaxCommittedMiB.*') {
        throw "Expected missing budget rejection: $($result.output)"
    }
}

$cases | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runRoot 'tests.json') -Encoding utf8
$cases | Format-Table name, status -AutoSize
if (@($cases | Where-Object status -EQ 'FAIL').Count -gt 0) { exit 1 }
exit 0
