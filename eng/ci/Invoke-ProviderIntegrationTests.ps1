[CmdletBinding()]
param(
    [ValidateSet("MySql", "PostgreSql", "SqlServer")]
    [string]$Provider,

    [ValidateSet("net6.0", "net8.0")]
    [string]$Framework = "net8.0",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$ResultsDirectory = "artifacts/provider-test-results",

    [switch]$ValidateOnly,

    [switch]$SelfTest
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$releaseEvidenceProjectPath = Join-Path $repositoryRoot "eng\Bing.ProviderEvidence.Cli\Bing.ProviderEvidence.Cli.csproj"

function Get-ProviderSettings {
    param([string]$Name)

    switch ($Name) {
        "MySql" {
            return [pscustomobject]@{
                Name = "MySql"
                GateVariable = "RUN_MYSQL_INTEGRATION_TESTS"
                ConnectionVariable = "ConnectionStrings__MySqlConnection"
                ProjectPath = (Join-Path $repositoryRoot "framework\tests\Bing.Dapper.MySql.Tests.Integration\Bing.Dapper.MySql.Tests.Integration.csproj")
                OptionalSkippedTestPatterns = @("MySqlCrossDatabaseQueryTest")
            }
        }
        "PostgreSql" {
            return [pscustomobject]@{
                Name = "PostgreSql"
                GateVariable = "RUN_POSTGRESQL_INTEGRATION_TESTS"
                ConnectionVariable = "ConnectionStrings__PostgreSqlConnection"
                ProjectPath = (Join-Path $repositoryRoot "framework\tests\Bing.Dapper.PostgreSql.Tests.Integration\Bing.Dapper.PostgreSql.Tests.Integration.csproj")
                OptionalSkippedTestPatterns = @()
            }
        }
        "SqlServer" {
            return [pscustomobject]@{
                Name = "SqlServer"
                GateVariable = "RUN_SQLSERVER_INTEGRATION_TESTS"
                ConnectionVariable = "ConnectionStrings__SqlServerConnection"
                ProjectPath = (Join-Path $repositoryRoot "framework\tests\Bing.Dapper.SqlServer.Tests.Integration\Bing.Dapper.SqlServer.Tests.Integration.csproj")
                OptionalSkippedTestPatterns = @("MultiProviderQueryTest")
            }
        }
    }
}

function Get-EnvironmentValue {
    param([string]$Name)

    return [Environment]::GetEnvironmentVariable($Name)
}

function Get-SourceIdentity {
    $sourceState = Get-SourceState
    $gitIdentity = (& git -C $repositoryRoot rev-parse HEAD 2>$null | Select-Object -First 1)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($gitIdentity)) {
        throw "Source identity validation failed: git HEAD could not be resolved."
    }
    $gitIdentity = $gitIdentity.ToString().Trim()
    foreach ($name in @("GITHUB_SHA", "BUILD_SOURCEVERSION", "APPVEYOR_REPO_COMMIT", "CI_COMMIT_SHA")) {
        $value = Get-EnvironmentValue $name
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            if (-not [string]::Equals($value.Trim(), $gitIdentity, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Source identity validation failed: $name does not match the checked-out HEAD."
            }
        }
    }
    return "git=$gitIdentity;source=$sourceState"
}

function Get-SourceState {
    $status = (& git -C $repositoryRoot status --porcelain --untracked-files=all 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -eq 0 -and [string]::IsNullOrWhiteSpace($status)) {
        return "clean"
    }
    return "dirty"
}

function Get-EvidenceSessionId {
    $configured = Get-EnvironmentValue "BING_PROVIDER_EVIDENCE_SESSION_ID"
    $isCi = Test-EnabledValue (Get-EnvironmentValue "CI")
    $buildIdentity = $null
    foreach ($name in @("APPVEYOR_BUILD_ID", "GITHUB_RUN_ID", "BUILD_BUILDID", "CI_PIPELINE_ID")) {
        $value = Get-EnvironmentValue $name
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            $buildIdentity = $value.Trim()
            break
        }
    }
    if ($isCi -and [string]::IsNullOrWhiteSpace($buildIdentity)) {
        throw "Evidence session validation failed: a protected CI build identity is required."
    }
    if (-not [string]::IsNullOrWhiteSpace($configured)) {
        $sessionId = $configured.Trim()
        if ($isCi -and
            -not [string]::Equals($sessionId, "ci-$buildIdentity", [StringComparison]::Ordinal)) {
            throw "Evidence session validation failed: configured session does not match the CI build identity."
        }
    }
    else {
        $sessionId = if ([string]::IsNullOrWhiteSpace($buildIdentity)) {
            "local-" + [Guid]::NewGuid().ToString("N")
        }
        else {
            "ci-" + $buildIdentity
        }
    }
    if ($sessionId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$') {
        throw "Evidence session validation failed: the session identity format is invalid."
    }
    return $sessionId
}

function Get-WorkspaceRelativePath {
    param([string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $rootPath = [System.IO.Path]::GetFullPath($repositoryRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($rootPath, [StringComparison]::OrdinalIgnoreCase)) {
        throw "制品路径必须位于工作区内。"
    }
    return $fullPath.Substring($rootPath.Length).Replace('\', '/')
}

function Resolve-ResultsRoot {
    param([string]$RelativePath)

    $normalizedPath = $RelativePath.Replace('\', '/').Trim('/')
    $segments = @($normalizedPath -split '/' | Where-Object { $_ -in @('', '.', '..') })
    if ([string]::IsNullOrWhiteSpace($normalizedPath) -or
        [System.IO.Path]::IsPathRooted($normalizedPath) -or
        $segments.Count -gt 0 -or
        -not ($normalizedPath.Equals('artifacts/provider-test-results', [StringComparison]::OrdinalIgnoreCase) -or
            $normalizedPath.StartsWith('artifacts/provider-test-results/', [StringComparison]::OrdinalIgnoreCase))) {
        throw "ResultsDirectory 必须是工作区内 artifacts/provider-test-results 下的相对目录。"
    }

    $resultsInputPath = Join-Path $repositoryRoot $normalizedPath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    $resultsRoot = [System.IO.Path]::GetFullPath($resultsInputPath)
    $allowedInputPath = Join-Path $repositoryRoot 'artifacts/provider-test-results'
    $allowedRoot = [System.IO.Path]::GetFullPath($allowedInputPath).TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    if (-not $resultsRoot.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase) -and
        -not $resultsRoot.Equals($allowedRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar),
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "ResultsDirectory 必须位于 artifacts/provider-test-results 下。"
    }
    return [pscustomobject]@{
        Path = $resultsRoot
        RelativePath = $normalizedPath
    }
}

function Test-EnabledValue {
    param([string]$Value)

    return [string]::Equals($Value, "true", [StringComparison]::OrdinalIgnoreCase)
}

function Get-DatabaseName {
    param([string]$ConnectionString)

    $match = [regex]::Match($ConnectionString,
        '(?i)(?:^|;)\s*(?:database|initial\s+catalog)\s*=\s*(?:"(?<value>[^"]*)"|''(?<value>[^'']*)''|(?<value>[^;]*))')
    if ($match.Success) {
        $databaseName = $match.Groups["value"].Value.Trim()
        if (-not [string]::IsNullOrWhiteSpace($databaseName)) {
            return $databaseName
        }
    }
    return $null
}

function Test-SafeTestDatabaseName {
    param([string]$DatabaseName)

    if ([string]::IsNullOrWhiteSpace($DatabaseName)) {
        return $false
    }

    $normalizedName = $DatabaseName.Trim()
    $systemNames = @("information_schema", "master", "model", "msdb", "mysql", "performance_schema", "postgres", "sys", "tempdb", "template0", "template1")
    if ($systemNames -contains $normalizedName.ToLowerInvariant()) {
        return $false
    }

    foreach ($token in ($normalizedName -split "[_-]")) {
        if ($token -in @("prod", "production", "development")) {
            return $false
        }
    }

    return $normalizedName.EndsWith("_test", [StringComparison]::OrdinalIgnoreCase) -or
        $normalizedName.EndsWith("_tests", [StringComparison]::OrdinalIgnoreCase) -or
        $normalizedName.EndsWith("_integration", [StringComparison]::OrdinalIgnoreCase) -or
        $normalizedName.EndsWith("_integration_test", [StringComparison]::OrdinalIgnoreCase)
}

function Invoke-Preflight {
    param([object]$Settings)

    if (Test-EnabledValue (Get-EnvironmentValue "RUN_INTEGRATION_TESTS")) {
        throw "Provider preflight failed: RUN_INTEGRATION_TESTS must not enable a protected provider lane."
    }
    foreach ($otherProvider in @("MySql", "PostgreSql", "SqlServer")) {
        $otherSettings = Get-ProviderSettings $otherProvider
        if ($otherSettings.Name -eq $Settings.Name) {
            continue
        }
        if (Test-EnabledValue (Get-EnvironmentValue $otherSettings.GateVariable)) {
            throw "Provider preflight failed: $($otherSettings.GateVariable) must not enable the $($Settings.Name) protected lane."
        }
        if (-not [string]::IsNullOrWhiteSpace((Get-EnvironmentValue $otherSettings.ConnectionVariable))) {
            throw "Provider preflight failed: $($otherSettings.ConnectionVariable) is not allowed in the $($Settings.Name) protected lane."
        }
    }
    if (-not (Test-EnabledValue (Get-EnvironmentValue $Settings.GateVariable))) {
        throw "Provider preflight failed: $($Settings.GateVariable) must be true."
    }
    if (-not (Test-EnabledValue (Get-EnvironmentValue "ALLOW_DATABASE_RESET_FOR_TESTS"))) {
        throw "Provider preflight failed: ALLOW_DATABASE_RESET_FOR_TESTS must be true."
    }
    if (-not [string]::IsNullOrWhiteSpace((Get-EnvironmentValue "ConnectionStrings__DefaultConnection"))) {
        throw "Provider preflight failed: ConnectionStrings__DefaultConnection is forbidden in a protected provider lane."
    }
    if (-not (Test-Path -LiteralPath $Settings.ProjectPath -PathType Leaf)) {
        throw "Provider preflight failed: the configured test project was not found."
    }

    $connectionString = Get-EnvironmentValue $Settings.ConnectionVariable
    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        throw "Provider preflight failed: $($Settings.ConnectionVariable) must be configured."
    }
    $databaseName = Get-DatabaseName $connectionString
    if (-not (Test-SafeTestDatabaseName $databaseName)) {
        throw "Provider preflight failed: the configured database is not a dedicated test database."
    }

    return [pscustomobject]@{
        Provider = $Settings.Name
        DatabaseName = $databaseName
        ProjectPath = $Settings.ProjectPath
        ConfigurationSource = if (Test-EnabledValue (Get-EnvironmentValue "CI")) {
            "CIEnvironment"
        }
        else {
            "ProcessEnvironment"
        }
        GlobalGate = Test-EnabledValue (Get-EnvironmentValue "RUN_INTEGRATION_TESTS")
        ProviderGate = Test-EnabledValue (Get-EnvironmentValue $Settings.GateVariable)
        ConnectionConfigured = $true
        ResetAllowed = Test-EnabledValue (Get-EnvironmentValue "ALLOW_DATABASE_RESET_FOR_TESTS")
        DatabaseReachable = $false
        ExecutionStatus = "PreflightPassed"
        BlockedReason = $null
    }
}

function Get-TrxSummary {
    param(
        [string]$TrxPath,
        [object]$Settings,
        [string]$TargetFramework
    )

    if (-not (Test-Path -LiteralPath $TrxPath -PathType Leaf)) {
        throw "TRX validation failed: the expected TRX file does not exist."
    }
    [xml]$trx = Get-Content -LiteralPath $TrxPath -Raw -Encoding utf8
    $counters = $trx.TestRun.ResultSummary.Counters
    if ($null -eq $counters) {
        throw "TRX validation failed: result counters are missing."
    }

    $total = [int]$counters.total
    $passed = [int]$counters.passed
    $failed = [int]$counters.failed
    $results = @($trx.TestRun.Results.UnitTestResult)
    $skippedResults = @($results | Where-Object { $_.outcome -eq "NotExecuted" })
    $optionalSkippedResults = @()
    if ($Settings.OptionalSkippedTestPatterns.Count -gt 0) {
        $optionalSkippedResults = @($skippedResults | Where-Object {
                $testName = $_.testName
                $Settings.OptionalSkippedTestPatterns | Where-Object { $testName -like "*$_*" }
            })
    }
    $coreSkippedResults = @($skippedResults | Where-Object {
            $optionalSkippedResults.testName -notcontains $_.testName
        })

    if ($total -le 0) {
        throw "TRX validation failed: no provider tests were discovered."
    }
    if ($failed -gt 0) {
        throw "TRX validation failed: provider tests failed."
    }
    if (($passed + $failed) -le 0) {
        throw "TRX validation failed: no provider tests executed."
    }
    if ($coreSkippedResults.Count -gt 0) {
        throw "TRX validation failed: core provider tests were skipped."
    }

    return [pscustomobject]@{
        Provider = $Settings.Name
        Framework = $TargetFramework
        Discovered = $total
        Passed = $passed
        Failed = $failed
        Skipped = $skippedResults.Count
        OptionalSkipped = $optionalSkippedResults.Count
        Executed = $passed + $failed
        Duration = [string]$trx.TestRun.ResultSummary.duration
        TrxPath = $TrxPath
    }
}

function Write-Summary {
    param(
        [object]$Summary,
        [string]$SummaryPath
    )

    $json = $Summary | ConvertTo-Json -Depth 3
    if ($json -match '(?i)Password=|User Id=|Data Source=|Server=') {
        throw "Provider 摘要不得包含连接信息。"
    }
    $json | Set-Content -LiteralPath $SummaryPath -Encoding utf8
    Write-Host ("Provider={0}; Framework={1}; Discovered={2}; Passed={3}; Failed={4}; Skipped={5}; OptionalSkipped={6}; Executed={7}" -f
        $Summary.Provider, $Summary.Framework, $Summary.Discovered, $Summary.Passed, $Summary.Failed,
        $Summary.Skipped, $Summary.OptionalSkipped, $Summary.Executed)
}

function Write-BinaryManifest {
    param(
        [string]$Provider,
        [string]$Framework,
        [string]$RunId,
        [string]$EvidenceSessionId,
        [string]$SourceIdentity,
        [string]$SourceState,
        [string]$ProjectPath,
        [string]$ManifestPath
    )

    $outputDirectory = Join-Path (Split-Path -Parent $ProjectPath) ("bin\{0}\{1}" -f $Configuration, $Framework)
    $providerAssembly = "Bing.Dapper.{0}.dll" -f $Provider
    $testAssembly = "Bing.Dapper.{0}.Tests.Integration.dll" -f $Provider
    $assemblyNames = @($testAssembly, $providerAssembly, "Bing.Data.Sql.dll")
    $entries = @()
    foreach ($assemblyName in $assemblyNames) {
        $assemblyPath = Join-Path $outputDirectory $assemblyName
        if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
            throw "Binary manifest generation failed: required assembly was not built: $assemblyName."
        }
        $artifactPath = Join-Path (Split-Path -Parent $ManifestPath) $assemblyName
        Copy-Item -LiteralPath $assemblyPath -Destination $artifactPath -Force
        $entries += [ordered]@{
            Path = Get-WorkspaceRelativePath $artifactPath
            Sha256 = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
    $manifest = [ordered]@{
        Provider = $Provider
        Framework = $Framework
        RunId = $RunId
        EvidenceSessionId = $EvidenceSessionId
        SourceIdentity = $SourceIdentity
        SourceState = $SourceState
        Assemblies = $entries
    }
    ($manifest | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $ManifestPath -Encoding utf8
}

function Write-ReleaseMatrix {
    param(
        [object]$Summary,
        [string]$SummaryPath,
        [string]$MatrixJsonPath,
        [string]$MatrixMarkdownPath
    )

    if (-not (Test-Path -LiteralPath $releaseEvidenceProjectPath -PathType Leaf)) {
        throw "Provider release evidence CLI project was not found."
    }
    $startedAtUtc = ([DateTimeOffset]$Summary.StartedAtUtc).ToUniversalTime().ToString("O")
    $completedAtUtc = ([DateTimeOffset]$Summary.CompletedAtUtc).ToUniversalTime().ToString("O")
    $arguments = @(
        "run", "--project", $releaseEvidenceProjectPath, "-c", $Configuration, "--no-restore", "--",
        "--workspace-root", $repositoryRoot,
        "--provider", $Summary.Provider,
        "--framework", $Summary.Framework,
        "--run-id", $Summary.RunId,
        "--evidence-session-id", $Summary.EvidenceSessionId,
        "--trx", $Summary.TrxPath,
        "--summary", (Get-WorkspaceRelativePath $SummaryPath),
        "--matrix-json", (Get-WorkspaceRelativePath $MatrixJsonPath),
        "--matrix-markdown", (Get-WorkspaceRelativePath $MatrixMarkdownPath),
        "--binary-manifest", $Summary.BinaryManifestPath,
        "--source-identity", $Summary.SourceIdentity,
        "--provider-version", $Summary.ProviderVersion,
        "--database-version", $Summary.DatabaseVersion,
        "--driver-version", $Summary.DriverVersion,
        "--runtime-version", $Summary.RuntimeVersion,
        "--operating-system", $Summary.OperatingSystem,
        "--source-state", $Summary.SourceState,
        "--started-at-utc", $startedAtUtc,
        "--completed-at-utc", $completedAtUtc
    )
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Provider release evidence validation failed for $($Summary.Provider) ($($Summary.Framework))."
    }
    if (-not (Test-Path -LiteralPath $MatrixJsonPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $MatrixMarkdownPath -PathType Leaf)) {
        throw "Provider release evidence validation did not produce both matrix artifacts."
    }
}

function Invoke-SelfTest {
    if (-not (Test-SafeTestDatabaseName "bing_provider_test")) {
        throw "Self-test failed: safe test database was rejected."
    }
    if (Test-SafeTestDatabaseName "bing_prod_test") {
        throw "Self-test failed: unsafe database was accepted."
    }
    if ((Get-DatabaseName "Server=localhost;Database=bing_provider_test") -ne "bing_provider_test") {
        throw "Self-test failed: database name was not parsed."
    }
    if (-not (Test-EnabledValue "TrUe") -or (Test-EnabledValue "yes")) {
        throw "Self-test failed: gate value parsing is incorrect."
    }

    $oldGithubSha = Get-EnvironmentValue "GITHUB_SHA"
    try {
        [Environment]::SetEnvironmentVariable("GITHUB_SHA", "not-the-checked-out-head")
        $sourceIdentityRejected = $false
        try {
            Get-SourceIdentity | Out-Null
        }
        catch {
            $sourceIdentityRejected = $_.Exception.Message -match "does not match the checked-out HEAD"
        }
        if (-not $sourceIdentityRejected) {
            throw "Self-test failed: mismatched CI source identity was accepted."
        }
    }
    finally {
        [Environment]::SetEnvironmentVariable("GITHUB_SHA", $oldGithubSha)
    }

    $settings = Get-ProviderSettings "PostgreSql"
    $mySqlSettings = Get-ProviderSettings "MySql"
    $temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("bing-provider-runner-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $temporaryDirectory -Force | Out-Null
    try {
        $passedTrxPath = Join-Path $temporaryDirectory "passed.trx"
        @"
<?xml version="1.0" encoding="utf-8"?>
<TestRun>
    <Results><UnitTestResult testName="Bing.Dapper.Tests.SqlQuery.PostgreSqlQueryTest.ExecuteScalar_ShouldReturnActualCount" outcome="Passed" /></Results>
  <ResultSummary duration="00:00:01"><Counters total="1" passed="1" failed="0" /></ResultSummary>
</TestRun>
"@ | Set-Content -LiteralPath $passedTrxPath -Encoding utf8
        $summary = Get-TrxSummary -TrxPath $passedTrxPath -Settings $settings -TargetFramework "net8.0"
        if ($summary.Executed -ne 1 -or $summary.Skipped -ne 0) {
            throw "Self-test failed: passed TRX was not summarized correctly."
        }

        $failedTrxPath = Join-Path $temporaryDirectory "failed.trx"
        $failedSummaryPath = Join-Path $temporaryDirectory "failed.json"
        $failedMatrixJsonPath = Join-Path $temporaryDirectory "failed-provider-capability-matrix.json"
        $failedMatrixMarkdownPath = Join-Path $temporaryDirectory "failed-provider-capability-matrix.md"
        @"
<?xml version="1.0" encoding="utf-8"?>
<TestRun>
  <Results><UnitTestResult testName="Provider.Core" outcome="Failed" /></Results>
  <ResultSummary duration="00:00:01"><Counters total="1" passed="0" failed="1" /></ResultSummary>
</TestRun>
"@ | Set-Content -LiteralPath $failedTrxPath -Encoding utf8
        $failedTrxRejected = $false
        try {
            Get-TrxSummary -TrxPath $failedTrxPath -Settings $settings -TargetFramework "net8.0" | Out-Null
        }
        catch {
            $failedTrxRejected = $_.Exception.Message -match "provider tests failed"
        }
        if (-not $failedTrxRejected) {
            throw "Self-test failed: failed TRX was accepted."
        }
        if ((Test-Path -LiteralPath $failedSummaryPath -PathType Leaf) -or
            (Test-Path -LiteralPath $failedMatrixJsonPath -PathType Leaf) -or
            (Test-Path -LiteralPath $failedMatrixMarkdownPath -PathType Leaf)) {
            throw "Self-test failed: failed TRX produced a summary or release matrix."
        }

        $allSkippedTrxPath = Join-Path $temporaryDirectory "all-skipped.trx"
        @"
<?xml version="1.0" encoding="utf-8"?>
<TestRun>
  <Results><UnitTestResult testName="Provider.Core" outcome="NotExecuted" /></Results>
  <ResultSummary duration="00:00:01"><Counters total="1" passed="0" failed="0" /></ResultSummary>
</TestRun>
"@ | Set-Content -LiteralPath $allSkippedTrxPath -Encoding utf8
        $allSkippedRejected = $false
        try {
            Get-TrxSummary -TrxPath $allSkippedTrxPath -Settings $settings -TargetFramework "net8.0" | Out-Null
        }
        catch {
            $allSkippedRejected = $true
        }
        if (-not $allSkippedRejected) {
            throw "Self-test failed: all-skipped TRX was accepted."
        }

        $zeroTestTrxPath = Join-Path $temporaryDirectory "zero-test.trx"
        @"
<?xml version="1.0" encoding="utf-8"?>
<TestRun>
  <Results />
  <ResultSummary duration="00:00:01"><Counters total="0" passed="0" failed="0" /></ResultSummary>
</TestRun>
"@ | Set-Content -LiteralPath $zeroTestTrxPath -Encoding utf8
        $zeroTestRejected = $false
        try {
            Get-TrxSummary -TrxPath $zeroTestTrxPath -Settings $settings -TargetFramework "net8.0" | Out-Null
        }
        catch {
            $zeroTestRejected = $_.Exception.Message -match "no provider tests were discovered"
        }
        if (-not $zeroTestRejected) {
            throw "Self-test failed: zero-test TRX was accepted."
        }

        $coreSkippedTrxPath = Join-Path $temporaryDirectory "core-skipped.trx"
        @"
<?xml version="1.0" encoding="utf-8"?>
<TestRun>
  <Results>
    <UnitTestResult testName="Provider.Core" outcome="Passed" />
    <UnitTestResult testName="Provider.Required" outcome="NotExecuted" />
  </Results>
  <ResultSummary duration="00:00:01"><Counters total="2" passed="1" failed="0" /></ResultSummary>
</TestRun>
"@ | Set-Content -LiteralPath $coreSkippedTrxPath -Encoding utf8
        $coreSkippedRejected = $false
        try {
            Get-TrxSummary -TrxPath $coreSkippedTrxPath -Settings $settings -TargetFramework "net8.0" | Out-Null
        }
        catch {
            $coreSkippedRejected = $_.Exception.Message -match "core provider tests were skipped"
        }
        if (-not $coreSkippedRejected) {
            throw "Self-test failed: core-skipped TRX was accepted."
        }

        $optionalSkippedTrxPath = Join-Path $temporaryDirectory "optional-skipped.trx"
        @"
<?xml version="1.0" encoding="utf-8"?>
<TestRun>
  <Results>
    <UnitTestResult testName="Provider.Core" outcome="Passed" />
    <UnitTestResult testName="Bing.Dapper.Tests.SqlQuery.MySqlCrossDatabaseQueryTest.Query" outcome="NotExecuted" />
  </Results>
  <ResultSummary duration="00:00:01"><Counters total="2" passed="1" failed="0" /></ResultSummary>
</TestRun>
"@ | Set-Content -LiteralPath $optionalSkippedTrxPath -Encoding utf8
        $optionalSummary = Get-TrxSummary -TrxPath $optionalSkippedTrxPath -Settings $mySqlSettings -TargetFramework "net8.0"
        if ($optionalSummary.Executed -ne 1 -or $optionalSummary.OptionalSkipped -ne 1) {
            throw "Self-test failed: optional MySQL skip was not summarized correctly."
        }

        $missingTrxRejected = $false
        try {
            Get-TrxSummary -TrxPath (Join-Path $temporaryDirectory "missing.trx") -Settings $settings -TargetFramework "net8.0" |
                Out-Null
        }
        catch {
            $missingTrxRejected = $_.Exception.Message -match "expected TRX file does not exist"
        }
        if (-not $missingTrxRejected) {
            throw "Self-test failed: missing TRX was accepted."
        }

        $releaseRunId = "selftest-" + [Guid]::NewGuid().ToString("N")
        $releaseDirectory = Join-Path $repositoryRoot ("artifacts\provider-test-results\" + $releaseRunId)
        New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
        $releaseTrxPath = Join-Path $releaseDirectory "postgresql-net8.0.trx"
        Copy-Item -LiteralPath $passedTrxPath -Destination $releaseTrxPath
        $binaryManifestPath = Join-Path $releaseDirectory "provider-binary-manifest.json"
        $binaryNames = @(
            "Bing.Dapper.PostgreSql.Tests.Integration.dll",
            "Bing.Dapper.PostgreSql.dll",
            "Bing.Data.Sql.dll"
        )
        $binaryEntries = @()
        foreach ($binaryName in $binaryNames) {
            $binaryPath = Join-Path $releaseDirectory $binaryName
            [System.IO.File]::WriteAllText($binaryPath, $binaryName, [System.Text.UTF8Encoding]::new($false))
            $binaryEntries += [ordered]@{
                Path = Get-WorkspaceRelativePath $binaryPath
                Sha256 = (Get-FileHash -LiteralPath $binaryPath -Algorithm SHA256).Hash.ToLowerInvariant()
            }
        }
        ([ordered]@{
            Provider = "PostgreSql"
            Framework = "net8.0"
            RunId = $releaseRunId
            EvidenceSessionId = "self-test-session"
            SourceIdentity = "self-test"
            SourceState = "clean"
            Assemblies = $binaryEntries
        } | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $binaryManifestPath -Encoding utf8
        $summaryPath = Join-Path $releaseDirectory "postgresql-net8.0.json"
        $releaseStartedAtUtc = [DateTimeOffset]::UtcNow
        $releaseCompletedAtUtc = [DateTimeOffset]::UtcNow
        Write-Summary -Summary ([pscustomobject]@{
                Provider = "PostgreSql"
                Framework = "net8.0"
                Discovered = 1
                Passed = 1
                Failed = 0
                Skipped = 0
                OptionalSkipped = 0
                CoreSkipped = 0
                Executed = 1
                RunId = $releaseRunId
                EvidenceSessionId = "self-test-session"
                StartedAtUtc = $releaseStartedAtUtc.ToString("O")
                CompletedAtUtc = $releaseCompletedAtUtc.ToString("O")
                SourceIdentity = "self-test"
                ProviderVersion = "7.0.0"
                DatabaseVersion = "16.4"
                DriverVersion = "6.0.11"
                RuntimeVersion = "8.0.11"
                OperatingSystem = "Windows"
                SourceState = "clean"
                TrxPath = Get-WorkspaceRelativePath $releaseTrxPath
                ArtifactPath = Get-WorkspaceRelativePath $summaryPath
                BinaryManifestPath = Get-WorkspaceRelativePath $binaryManifestPath
            }) -SummaryPath $summaryPath
        $summaryText = Get-Content -LiteralPath $summaryPath -Raw -Encoding utf8
        if ($summaryText -match '(?i)Password=|User Id=|Data Source=|Server=') {
            throw "Self-test failed: summary contains sensitive connection fields."
        }
        $matrixJsonPath = Join-Path $releaseDirectory "provider-capability-matrix.json"
        $matrixMarkdownPath = Join-Path $releaseDirectory "provider-capability-matrix.md"
        $releaseSummary = Get-Content -LiteralPath $summaryPath -Raw -Encoding utf8 | ConvertFrom-Json
        Write-ReleaseMatrix -Summary $releaseSummary -SummaryPath $summaryPath -MatrixJsonPath $matrixJsonPath `
            -MatrixMarkdownPath $matrixMarkdownPath
        $matrixJson = Get-Content -LiteralPath $matrixJsonPath -Raw -Encoding utf8
        if ($matrixJson -notmatch 'ReleaseEvidence' -or $matrixJson -notmatch $releaseRunId) {
            throw "Self-test failed: trusted release matrix was not generated with the current run binding."
        }

        $oldGlobalGate = Get-EnvironmentValue "RUN_INTEGRATION_TESTS"
        $oldPostgreSqlGate = Get-EnvironmentValue $settings.GateVariable
        $oldResetGate = Get-EnvironmentValue "ALLOW_DATABASE_RESET_FOR_TESTS"
        $oldPostgreSqlConnection = Get-EnvironmentValue $settings.ConnectionVariable
        $oldDefaultConnection = Get-EnvironmentValue "ConnectionStrings__DefaultConnection"
        $oldMySqlGate = Get-EnvironmentValue $mySqlSettings.GateVariable
        $oldMySqlConnection = Get-EnvironmentValue $mySqlSettings.ConnectionVariable
        $sqlServerSettings = Get-ProviderSettings "SqlServer"
        $oldSqlServerGate = Get-EnvironmentValue $sqlServerSettings.GateVariable
        $oldSqlServerConnection = Get-EnvironmentValue $sqlServerSettings.ConnectionVariable
        try {
            foreach ($providerSettings in @($mySqlSettings, $settings, $sqlServerSettings)) {
                [Environment]::SetEnvironmentVariable($providerSettings.GateVariable, $null)
                [Environment]::SetEnvironmentVariable($providerSettings.ConnectionVariable, $null)
            }
            [Environment]::SetEnvironmentVariable("RUN_INTEGRATION_TESTS", "true")
            [Environment]::SetEnvironmentVariable($settings.GateVariable, "true")
            [Environment]::SetEnvironmentVariable("ALLOW_DATABASE_RESET_FOR_TESTS", "true")
            [Environment]::SetEnvironmentVariable($settings.ConnectionVariable,
                "Host=localhost;Database=bing_provider_test;Username=test")
            $globalGateRejected = $false
            try {
                Invoke-Preflight $settings | Out-Null
            }
            catch {
                $globalGateRejected = $_.Exception.Message -match "RUN_INTEGRATION_TESTS"
            }
            if (-not $globalGateRejected) {
                throw "Self-test failed: global gate was accepted by a protected provider lane."
            }

            [Environment]::SetEnvironmentVariable("RUN_INTEGRATION_TESTS", $null)
            [Environment]::SetEnvironmentVariable($settings.ConnectionVariable, $null)
            [Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection",
                "Host=localhost;Database=bing_provider_test;Username=test")
            $defaultConnectionRejected = $false
            try {
                Invoke-Preflight $settings | Out-Null
            }
            catch {
                $defaultConnectionRejected = $_.Exception.Message -match "DefaultConnection"
            }
            if (-not $defaultConnectionRejected) {
                throw "Self-test failed: default connection fallback was accepted."
            }

            [Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", $null)
            [Environment]::SetEnvironmentVariable($settings.ConnectionVariable, $null)
            $missingConnectionRejected = $false
            try {
                Invoke-Preflight $settings | Out-Null
            }
            catch {
                $missingConnectionRejected = $_.Exception.Message -match $settings.ConnectionVariable
            }
            if (-not $missingConnectionRejected) {
                throw "Self-test failed: missing provider connection was accepted."
            }

            [Environment]::SetEnvironmentVariable("$($mySqlSettings.GateVariable)", "true")
            [Environment]::SetEnvironmentVariable("$($mySqlSettings.ConnectionVariable)",
                "Server=localhost;Database=bing_provider_test;Username=test")
            $otherProviderRejected = $false
            try {
                Invoke-Preflight $settings | Out-Null
            }
            catch {
                $otherProviderRejected = $_.Exception.Message -match $mySqlSettings.GateVariable
            }
            if (-not $otherProviderRejected) {
                throw "Self-test failed: another provider lane was accepted."
            }
        }
        finally {
            [Environment]::SetEnvironmentVariable("RUN_INTEGRATION_TESTS", $oldGlobalGate)
            [Environment]::SetEnvironmentVariable($settings.GateVariable, $oldPostgreSqlGate)
            [Environment]::SetEnvironmentVariable("ALLOW_DATABASE_RESET_FOR_TESTS", $oldResetGate)
            [Environment]::SetEnvironmentVariable($settings.ConnectionVariable, $oldPostgreSqlConnection)
            [Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", $oldDefaultConnection)
            [Environment]::SetEnvironmentVariable($mySqlSettings.GateVariable, $oldMySqlGate)
            [Environment]::SetEnvironmentVariable($mySqlSettings.ConnectionVariable, $oldMySqlConnection)
            [Environment]::SetEnvironmentVariable($sqlServerSettings.GateVariable, $oldSqlServerGate)
            [Environment]::SetEnvironmentVariable($sqlServerSettings.ConnectionVariable, $oldSqlServerConnection)
        }
    }
    finally {
        if ($null -ne $releaseDirectory) {
            Remove-Item -LiteralPath $releaseDirectory -Recurse -Force -ErrorAction SilentlyContinue
        }
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
    Write-Host "Provider runner self-test passed."
}

if ($SelfTest) {
    Invoke-SelfTest
    exit 0
}

$settings = Get-ProviderSettings $Provider
$preflight = Invoke-Preflight $settings
Write-Host "Provider preflight passed: Provider=$($preflight.Provider); Database=$($preflight.DatabaseName)."
if ($ValidateOnly) {
    exit 0
}

$resultsRoot = Resolve-ResultsRoot $ResultsDirectory
$startedAtUtc = [DateTimeOffset]::UtcNow
$runId = "{0}-{1}-{2}" -f $Provider.ToLowerInvariant(), $Framework,
    ($startedAtUtc.ToString("yyyyMMddTHHmmssfffZ") + "-" + [Guid]::NewGuid().ToString("N"))
$resultsPath = Join-Path $resultsRoot.Path $runId
New-Item -ItemType Directory -Path $resultsPath -Force | Out-Null
$trxFileName = "{0}-{1}.trx" -f $Provider.ToLowerInvariant(), $Framework
$trxPath = Join-Path $resultsPath $trxFileName
$metadataPath = Join-Path $resultsPath "provider-run-metadata.json"
$binaryManifestPath = Join-Path $resultsPath "provider-binary-manifest.json"
$oldMetadataPath = Get-EnvironmentValue "BING_PROVIDER_RUN_METADATA_PATH"
$sourceState = Get-SourceState
$sourceIdentity = Get-SourceIdentity
$evidenceSessionId = Get-EvidenceSessionId

try {
    & dotnet build $settings.ProjectPath -c $Configuration -p:CI=true -f $Framework --no-restore --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Provider test build failed for $Provider ($Framework)."
    }
    Write-BinaryManifest -Provider $Provider -Framework $Framework -RunId $runId -EvidenceSessionId $evidenceSessionId `
        -SourceIdentity $sourceIdentity `
        -SourceState $sourceState -ProjectPath $settings.ProjectPath -ManifestPath $binaryManifestPath
    [Environment]::SetEnvironmentVariable("BING_PROVIDER_RUN_METADATA_PATH", $metadataPath)
    & dotnet test $settings.ProjectPath -c $Configuration -p:CI=true --no-build -f $Framework --no-restore --nologo `
        "--logger:trx;LogFileName=$trxFileName" --results-directory $resultsPath
    if ($LASTEXITCODE -ne 0) {
        throw "Provider test execution failed for $Provider ($Framework)."
    }
}
finally {
    [Environment]::SetEnvironmentVariable("BING_PROVIDER_RUN_METADATA_PATH", $oldMetadataPath)
}
if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) {
    throw "TRX validation failed: the provider test run did not produce an expected TRX file."
}
if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
    throw "Provider metadata validation failed: the testhost did not produce provider-run-metadata.json."
}
$metadataText = Get-Content -LiteralPath $metadataPath -Raw -Encoding utf8
if ($metadataText -match '(?i)Password=|User Id=|Data Source=|Server=') {
    throw "Provider metadata validation failed: metadata contains connection information."
}
$metadata = $metadataText | ConvertFrom-Json
foreach ($name in @("ProviderVersion", "DatabaseVersion", "DriverVersion", "RuntimeVersion", "OperatingSystem", "TargetFramework")) {
    $value = $metadata.$name
    if ([string]::IsNullOrWhiteSpace([string]$value) -or [string]::Equals([string]$value, "not-recorded", [StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals([string]$value, "unknown", [StringComparison]::OrdinalIgnoreCase)) {
        throw "Provider metadata validation failed: $name is missing or a placeholder."
    }
}
if (-not [string]::Equals([string]$metadata.TargetFramework, $Framework, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Provider metadata validation failed: TargetFramework does not match the runner framework."
}

$summary = Get-TrxSummary -TrxPath $trxPath -Settings $settings -TargetFramework $Framework
$completedAtUtc = [DateTimeOffset]::UtcNow
$summaryPath = Join-Path $resultsPath ("{0}-{1}.json" -f $Provider.ToLowerInvariant(), $Framework)
$matrixJsonPath = Join-Path $resultsPath "provider-capability-matrix.json"
$matrixMarkdownPath = Join-Path $resultsPath "provider-capability-matrix.md"
$summary = [pscustomobject]@{
    Provider = $summary.Provider
    Framework = $summary.Framework
    Discovered = $summary.Discovered
    Passed = $summary.Passed
    Failed = $summary.Failed
    Skipped = $summary.Skipped
    OptionalSkipped = $summary.OptionalSkipped
    CoreSkipped = $summary.Skipped - $summary.OptionalSkipped
    Executed = $summary.Executed
    Duration = $summary.Duration
    ConfigurationSource = $preflight.ConfigurationSource
    GlobalGate = $preflight.GlobalGate
    ProviderGate = $preflight.ProviderGate
    ConnectionConfigured = $preflight.ConnectionConfigured
    DatabaseName = $preflight.DatabaseName
    ResetAllowed = $preflight.ResetAllowed
    DatabaseReachable = $true
    ExecutionStatus = "Executed"
    BlockedReason = $null
    SourceState = $sourceState
    ProviderVersion = [string]$metadata.ProviderVersion
    DatabaseVersion = [string]$metadata.DatabaseVersion
    DriverVersion = [string]$metadata.DriverVersion
    RuntimeVersion = [string]$metadata.RuntimeVersion
    OperatingSystem = [string]$metadata.OperatingSystem
    RunId = $runId
    EvidenceSessionId = $evidenceSessionId
    StartedAtUtc = $startedAtUtc.ToString("O")
    CompletedAtUtc = $completedAtUtc.ToString("O")
    SourceIdentity = $sourceIdentity
    BinaryManifestPath = Get-WorkspaceRelativePath $binaryManifestPath
    TrxPath = Get-WorkspaceRelativePath $trxPath
    ArtifactPath = Get-WorkspaceRelativePath $summaryPath
}
Write-Summary -Summary $summary -SummaryPath $summaryPath
Write-ReleaseMatrix -Summary $summary -SummaryPath $summaryPath -MatrixJsonPath $matrixJsonPath `
    -MatrixMarkdownPath $matrixMarkdownPath