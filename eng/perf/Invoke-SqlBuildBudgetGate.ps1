#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$ReceiptPath,
    [string]$ExpectedSourceManifestSha256,
    [string]$ExpectedBinarySha256,
    [string]$ExpectedRuntimeManifestSha256,
    [string]$BudgetApprovalId,
    [string]$MaxCommittedMiB,
    [string]$ResultPath
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
$utf8 = [System.Text.UTF8Encoding]::new($false)

$report = [ordered]@{ gate = 'SqlCleanBuildBudgetGate-v1'; status = 'INVALID' }
try {
    foreach ($field in @('ReceiptPath', 'ExpectedSourceManifestSha256', 'ExpectedBinarySha256', 'BudgetApprovalId', 'MaxCommittedMiB')) {
        if ([string]::IsNullOrWhiteSpace((Get-Variable -Name $field -ValueOnly))) {
            throw "Required input is missing: $field."
        }
    }
    if ($BudgetApprovalId.Length -gt 160 -or $BudgetApprovalId -match '[\r\n]') {
        throw 'BudgetApprovalId must be a single-line approval reference of at most 160 characters.'
    }
    foreach ($hash in @($ExpectedSourceManifestSha256, $ExpectedBinarySha256)) {
        if ($hash -notmatch '\A[0-9a-fA-F]{64}\z') { throw 'Expected source and binary identities must be SHA256 hex digests.' }
    }
    $budgetMiB = [decimal]0
    if (-not [decimal]::TryParse($MaxCommittedMiB, [Globalization.NumberStyles]::Float,
            [Globalization.CultureInfo]::InvariantCulture, [ref]$budgetMiB) -or $budgetMiB -le 0) {
        throw 'MaxCommittedMiB must be a finite positive number using invariant decimal notation.'
    }

    $receiptFile = (Resolve-Path -LiteralPath $ReceiptPath).ProviderPath
    $strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
    try {
        $receiptText = $strictUtf8.GetString([System.IO.File]::ReadAllBytes($receiptFile)).TrimStart([char]0xFEFF)
        $receipt = ConvertFrom-Json -AsHashtable -NoEnumerate -InputObject $receiptText
    }
    catch { throw 'Receipt must be valid UTF-8 JSON.' }
    if ($receipt -isnot [System.Collections.IDictionary] -or $receipt.evidenceKind -cne 'CleanBuildProcessTree' -or
        $receipt.peakMetric -cne 'WindowsJobCommittedBytes') {
        throw 'Receipt is not a supported clean-build process-tree measurement.'
    }
    if ($receipt.status -cne 'PASS') { throw "Build receipt status must be PASS; received '$($receipt.status)'." }

    $source = $receipt.source
    if ($source -isnot [System.Collections.IDictionary] -or $source.clean -isnot [bool] -or -not $source.clean -or
        $source.manifest -isnot [string] -or ($source.inputCount -isnot [int] -and $source.inputCount -isnot [long]) -or
        $source.inputCount -lt 1) {
        throw 'Receipt is missing a clean source snapshot and manifest.'
    }
    $manifestFile = (Resolve-Path -LiteralPath $source.manifest).ProviderPath
    $manifestHash = (Get-FileHash -LiteralPath $manifestFile -Algorithm SHA256).Hash
    if ($manifestHash -ine $ExpectedSourceManifestSha256) { throw 'Source manifest SHA256 does not match the expected candidate.' }

    $binary = $receipt.binary
    if ($binary -isnot [System.Collections.IDictionary] -or $binary.file -isnot [string] -or
        $binary.sha256 -isnot [string] -or
        $binary.sha256 -notmatch '\A[0-9a-fA-F]{64}\z' -or $binary.sha256 -ine $ExpectedBinarySha256) {
        throw 'Built benchmark binary SHA256 does not match the expected candidate.'
    }
    $binaryFile = (Resolve-Path -LiteralPath $binary.file).ProviderPath
    $binaryHash = (Get-FileHash -LiteralPath $binaryFile -Algorithm SHA256).Hash
    if ($binaryHash -ine $ExpectedBinarySha256 -or $binaryHash -ine $binary.sha256) {
        throw 'Built benchmark binary file bytes do not match the expected candidate.'
    }

    # 新验收入口必须传运行清单；旧收据的重放仍保留原有入口程序集契约。
    $runtimeHash = $null
    if ($ExpectedRuntimeManifestSha256) {
        if ($ExpectedRuntimeManifestSha256 -notmatch '\A[0-9a-fA-F]{64}\z') { throw 'Expected runtime identity must be a SHA256 hex digest.' }
        $runtime = $receipt.runtime
        if ($runtime -isnot [System.Collections.IDictionary] -or !$runtime.manifest -or !$runtime.root) {
            throw 'Receipt is missing the complete runtime dependency manifest.'
        }
        $runtimeHash = (Get-FileHash -LiteralPath $runtime.manifest -Algorithm SHA256).Hash
        if ($runtimeHash -ine $ExpectedRuntimeManifestSha256) { throw 'Runtime manifest SHA256 does not match the expected candidate.' }
        $runtimeManifest = ConvertFrom-Json -AsHashtable -InputObject ([System.IO.File]::ReadAllText($runtime.manifest, $strictUtf8))
        $runtimeRoot = (Resolve-Path -LiteralPath $runtime.root).ProviderPath
        if ($runtimeRoot -ine [System.IO.Path]::GetDirectoryName($binaryFile) -or
            $runtimeManifest.root -ine $runtimeRoot -or $runtimeManifest.files -isnot [System.Collections.IDictionary] -or
            $runtimeManifest.files.Count -lt 1 -or $runtimeManifest.count -ne $runtimeManifest.files.Count -or
            $runtime.count -ne $runtimeManifest.count) { throw 'Runtime manifest does not describe the final output directory.' }
        $actualFiles = @(Get-ChildItem -LiteralPath $runtimeRoot -File -Recurse -Force)
        if ($actualFiles.Count -ne $runtimeManifest.count) { throw 'Runtime output file set differs from the expected candidate.' }
        foreach ($file in $actualFiles) {
            $relative = [System.IO.Path]::GetRelativePath($runtimeRoot, $file.FullName).Replace('\', '/')
            $expected = $runtimeManifest.files[$relative]
            if ($expected -notmatch '\A[0-9a-fA-F]{64}\z' -or
                (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ine $expected) {
                throw "Runtime output file differs from the expected candidate: $relative"
            }
        }
    }

    $measurement = $receipt.measurement
    if ($measurement -isnot [System.Collections.IDictionary] -or $measurement.TimedOut -isnot [bool] -or
        ($measurement.ExitCode -isnot [int] -and $measurement.ExitCode -isnot [long]) -or
        -not $measurement.Contains('PeakJobCommittedBytes')) {
        throw 'Receipt is missing exit, timeout, or committed-memory peak measurements.'
    }
    if ($measurement.TimedOut) { throw 'Timed-out build is invalid budget evidence.' }
    if ($measurement.ExitCode -ne 0) { throw 'Clean build did not exit successfully.' }
    $peakRaw = $measurement.PeakJobCommittedBytes
    if ($peakRaw -isnot [ValueType] -or $peakRaw -is [bool]) { throw 'Committed-memory peak must be a finite integer byte count.' }
    $peakBytes = [decimal]$peakRaw
    if ($peakBytes -le 0 -or [decimal]::Truncate($peakBytes) -ne $peakBytes) {
        throw 'Committed-memory peak must be a finite positive integer byte count.'
    }
    $peakMiB = $peakBytes / [decimal]1048576

    $report.receiptSha256 = (Get-FileHash -LiteralPath $receiptFile -Algorithm SHA256).Hash
    $report.candidate = [ordered]@{
        sourceManifestSha256 = $manifestHash.ToUpperInvariant()
        binarySha256 = $binaryHash.ToUpperInvariant()
        identityStatus = 'MATCH'
    }
    if ($runtimeHash) { $report.candidate.runtimeManifestSha256 = $runtimeHash.ToUpperInvariant() }
    $report.budget = [ordered]@{
        approvalReference = $BudgetApprovalId
        referenceVerification = 'caller-supplied; not independently authenticated'
        maxCommittedMiB = $budgetMiB
    }
    $report.observed = [ordered]@{
        peakCommittedBytes = $peakBytes
        peakCommittedMiB = [decimal]::Round($peakMiB, 3, [MidpointRounding]::AwayFromZero)
        buildStatus = $receipt.status
        exitCode = $measurement.ExitCode
        timedOut = $measurement.TimedOut
    }
    $report.violations = @(
        if ($peakMiB -gt $budgetMiB) { 'maxCommittedMiB' }
    )
    $report.status = if ($report.violations.Count) { 'FAIL' } else { 'PASS' }
}
catch { $report.error = $_.Exception.Message }

if ([string]::IsNullOrWhiteSpace($ResultPath)) {
    $repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
    $ResultPath = Join-Path $repositoryRoot ('artifacts/validation/r02/build-gate/gate-' + [Guid]::NewGuid().ToString('N') + '.json')
}
try {
    $resultFile = [System.IO.Path]::GetFullPath($ResultPath)
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($resultFile)) | Out-Null
    $report.resultPath = $resultFile
    $json = $report | ConvertTo-Json -Depth 8
    $bytes = $utf8.GetBytes($json + [Environment]::NewLine)
    $stream = [System.IO.File]::Open($resultFile, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write)
    try { $stream.Write($bytes, 0, $bytes.Length) }
    finally { $stream.Dispose() }
}
catch {
    $report.status = 'INVALID'
    $report.error = "Could not write a new result file: $($_.Exception.Message)"
    $json = $report | ConvertTo-Json -Depth 8
}
Write-Output $json
switch ($report.status) {
    'PASS' { exit 0 }
    'FAIL' { exit 1 }
    default { exit 2 }
}
