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

function Get-ProviderSettings {
    param([string]$Name)

    $repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
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
    }
}

function Get-TrxSummary {
    param(
        [string]$TrxPath,
        [object]$Settings,
        [string]$TargetFramework
    )

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

    $Summary | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $SummaryPath -Encoding utf8
    Write-Host ("Provider={0}; Framework={1}; Discovered={2}; Passed={3}; Failed={4}; Skipped={5}; OptionalSkipped={6}; Executed={7}" -f
        $Summary.Provider, $Summary.Framework, $Summary.Discovered, $Summary.Passed, $Summary.Failed,
        $Summary.Skipped, $Summary.OptionalSkipped, $Summary.Executed)
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

    $settings = Get-ProviderSettings "PostgreSql"
    $mySqlSettings = Get-ProviderSettings "MySql"
    $temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("bing-provider-runner-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $temporaryDirectory -Force | Out-Null
    try {
        $passedTrxPath = Join-Path $temporaryDirectory "passed.trx"
        @"
<?xml version="1.0" encoding="utf-8"?>
<TestRun>
  <Results><UnitTestResult testName="Provider.Core" outcome="Passed" /></Results>
  <ResultSummary duration="00:00:01"><Counters total="1" passed="1" failed="0" /></ResultSummary>
</TestRun>
"@ | Set-Content -LiteralPath $passedTrxPath -Encoding utf8
        $summary = Get-TrxSummary -TrxPath $passedTrxPath -Settings $settings -TargetFramework "net8.0"
        if ($summary.Executed -ne 1 -or $summary.Skipped -ne 0) {
            throw "Self-test failed: passed TRX was not summarized correctly."
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

        $oldGlobalGate = Get-EnvironmentValue "RUN_INTEGRATION_TESTS"
        $oldPostgreSqlGate = Get-EnvironmentValue $settings.GateVariable
        $oldResetGate = Get-EnvironmentValue "ALLOW_DATABASE_RESET_FOR_TESTS"
        $oldPostgreSqlConnection = Get-EnvironmentValue $settings.ConnectionVariable
        try {
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
        }
        finally {
            [Environment]::SetEnvironmentVariable("RUN_INTEGRATION_TESTS", $oldGlobalGate)
            [Environment]::SetEnvironmentVariable($settings.GateVariable, $oldPostgreSqlGate)
            [Environment]::SetEnvironmentVariable("ALLOW_DATABASE_RESET_FOR_TESTS", $oldResetGate)
            [Environment]::SetEnvironmentVariable($settings.ConnectionVariable, $oldPostgreSqlConnection)
        }
    }
    finally {
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

$resultsPath = Join-Path ((Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path) $ResultsDirectory
New-Item -ItemType Directory -Path $resultsPath -Force | Out-Null
$trxFileName = "{0}-{1}.trx" -f $Provider.ToLowerInvariant(), $Framework
$trxPath = Join-Path $resultsPath $trxFileName
Remove-Item -LiteralPath $trxPath -Force -ErrorAction SilentlyContinue

& dotnet test $settings.ProjectPath -c $Configuration --no-build -f $Framework --no-restore --nologo `
    "--logger:trx;LogFileName=$trxFileName" --results-directory $resultsPath
if ($LASTEXITCODE -ne 0) {
    throw "Provider test execution failed for $Provider ($Framework)."
}
if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) {
    throw "TRX validation failed: the provider test run did not produce an expected TRX file."
}

$summary = Get-TrxSummary -TrxPath $trxPath -Settings $settings -TargetFramework $Framework
Write-Summary -Summary $summary -SummaryPath (Join-Path $resultsPath ("{0}-{1}.json" -f $Provider.ToLowerInvariant(), $Framework))