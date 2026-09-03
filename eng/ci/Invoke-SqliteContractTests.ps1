[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ResultsDirectory,

    [ValidateSet("net6.0", "net8.0")]
    [string]$Framework = "net8.0",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    [string]$RunName
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$normalizedResultsDirectory = $ResultsDirectory.Replace('\', '/').Trim('/')
$invalidPathSegments = @($normalizedResultsDirectory -split '/' | Where-Object { $_ -in @("", ".", "..") })
if ([System.IO.Path]::IsPathRooted($normalizedResultsDirectory) -or
    [string]::IsNullOrWhiteSpace($normalizedResultsDirectory) -or
    -not $normalizedResultsDirectory.StartsWith("artifacts/test-results/", [StringComparison]::OrdinalIgnoreCase) -or
    $invalidPathSegments.Count -gt 0) {
    throw "ResultsDirectory 必须是工作区内 artifacts/test-results 下的相对目录。"
}

$trxFileName = "$RunName-$Framework.trx"
$artifactFileName = "$RunName-$Framework.json"
$relativeTrxPath = "$normalizedResultsDirectory/$trxFileName"
$relativeArtifactPath = "$normalizedResultsDirectory/$artifactFileName"
$resultsPath = Join-Path $repositoryRoot ($normalizedResultsDirectory.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
$trxPath = Join-Path $resultsPath $trxFileName
$artifactPath = Join-Path $resultsPath $artifactFileName
if ((Test-Path -LiteralPath $trxPath -PathType Leaf) -or
    (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
    throw "SQLite 合同制品已存在，必须使用新的隔离结果目录或 RunName。"
}
New-Item -ItemType Directory -Path $resultsPath -Force | Out-Null

$projectPath = Join-Path $repositoryRoot "framework\tests\Bing.Dapper.Sqlite.Tests.Integration\Bing.Dapper.Sqlite.Tests.Integration.csproj"
$testFilter = "FullyQualifiedName~ProviderContract_WhenSqliteScenariosRun_ShouldRecordRealIntegrationEvidence"
$environmentNames = @(
    "BING_SQLITE_CONTRACT_RESULTS_DIRECTORY",
    "BING_SQLITE_CONTRACT_TRX_FILE_NAME",
    "BING_SQLITE_CONTRACT_ARTIFACT_FILE_NAME"
)
$oldEnvironment = @{}
foreach ($name in $environmentNames) {
    $oldEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}

function Get-RequiredJsonProperty {
    param(
        [object]$Object,
        [string]$Name
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        throw "SQLite Matrix 缺少字段：$Name。"
    }
    return $property.Value
}

function Get-RequiredUtc {
    param(
        [object]$Object,
        [string]$Name
    )

    $value = Get-RequiredJsonProperty $Object $Name
    if ($value -is [DateTime]) {
        return [DateTimeOffset]::new($value).ToUniversalTime()
    }
    $parsed = [DateTimeOffset]::Parse([string]$value).ToUniversalTime()
    return $parsed
}

try {
    [Environment]::SetEnvironmentVariable("BING_SQLITE_CONTRACT_RESULTS_DIRECTORY", $normalizedResultsDirectory)
    [Environment]::SetEnvironmentVariable("BING_SQLITE_CONTRACT_TRX_FILE_NAME", $trxFileName)
    [Environment]::SetEnvironmentVariable("BING_SQLITE_CONTRACT_ARTIFACT_FILE_NAME", $artifactFileName)

    & dotnet test $projectPath -c $Configuration -f $Framework --no-restore --nologo `
        --filter $testFilter "--logger:trx;LogFileName=$trxFileName" --results-directory $resultsPath
    if ($LASTEXITCODE -ne 0) {
        throw "SQLite 合同执行失败：Framework=$Framework。"
    }
}
finally {
    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, $oldEnvironment[$name])
    }
}

if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) {
    throw "SQLite 合同 TRX 不存在：$relativeTrxPath。"
}
if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
    throw "SQLite 合同 Matrix 不存在：$relativeArtifactPath。"
}

$matrix = Get-Content -LiteralPath $artifactPath -Raw -Encoding utf8 | ConvertFrom-Json
if ([bool](Get-RequiredJsonProperty $matrix "ReleaseReady")) {
    throw "SQLite 合同 Matrix 不得标记为 ReleaseReady。"
}
$entries = @($matrix.Entries)
if ($entries.Count -ne 2) {
    throw "SQLite 合同 Matrix 必须包含两个场景。"
}

$sourceIdentities = @()
$matrixStartedAt = $null
$matrixCompletedAt = $null
foreach ($entry in $entries) {
    $integrationEvidence = Get-RequiredJsonProperty $entry "IntegrationEvidence"
    if ((Get-RequiredJsonProperty $entry "State") -ne "RealIntegrationProven") {
        throw "SQLite 合同 Matrix 包含非真实集成状态。"
    }
    if ((Get-RequiredJsonProperty $integrationEvidence "TrxPath") -ne $relativeTrxPath) {
        throw "SQLite Matrix 的 TRX 路径未绑定到当前结果目录。"
    }
    if ((Get-RequiredJsonProperty $integrationEvidence "ArtifactPath") -ne $relativeArtifactPath) {
        throw "SQLite Matrix 的 artifact 路径未绑定到当前结果目录。"
    }
    if ((Get-RequiredJsonProperty $integrationEvidence "ArtifactKind") -ne "TestGenerated") {
        throw "SQLite 合同 Matrix 必须标记为 TestGenerated。"
    }
    $sourceIdentities += [string](Get-RequiredJsonProperty $integrationEvidence "SourceIdentity")
    $entryStartedAt = Get-RequiredUtc $integrationEvidence "StartedAtUtc"
    $entryCompletedAt = Get-RequiredUtc $integrationEvidence "CompletedAtUtc"
    if ($entryCompletedAt -lt $entryStartedAt) {
        throw "SQLite 合同证据完成时间早于开始时间。"
    }
    if ($null -eq $matrixStartedAt -or $entryStartedAt -lt $matrixStartedAt) {
        $matrixStartedAt = $entryStartedAt
    }
    if ($null -eq $matrixCompletedAt -or $entryCompletedAt -gt $matrixCompletedAt) {
        $matrixCompletedAt = $entryCompletedAt
    }
    $testMethod = [string](Get-RequiredJsonProperty $integrationEvidence "TestMethod")
    if (-not $testMethod.StartsWith("ProviderContract_WhenSqliteScenariosRun_ShouldRecordRealIntegrationEvidence[", [StringComparison]::Ordinal)) {
        throw "SQLite 合同 Matrix 的测试方法不符合合同入口。"
    }
}
if ($sourceIdentities.Count -ne 2 -or $sourceIdentities[0] -ne $sourceIdentities[1] -or
    [string]::IsNullOrWhiteSpace($sourceIdentities[0])) {
    throw "SQLite 合同 Matrix 的源码身份不一致或为空。"
}

[xml]$trx = Get-Content -LiteralPath $trxPath -Raw -Encoding utf8
$namespaceManager = [System.Xml.XmlNamespaceManager]::new($trx.NameTable)
$namespaceManager.AddNamespace("t", "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")
$counters = $trx.SelectSingleNode("/t:TestRun/t:ResultSummary/t:Counters", $namespaceManager)
$testResult = $trx.SelectSingleNode("/t:TestRun/t:Results/t:UnitTestResult[@outcome='Passed']", $namespaceManager)
if ($null -eq $counters -or $null -eq $testResult) {
    throw "SQLite 合同 TRX 缺少结果计数或已通过测试结果。"
}
if ([int]$counters.total -ne 1 -or [int]$counters.passed -ne 1 -or [int]$counters.failed -ne 0 -or
    [int]$counters.notExecuted -ne 0) {
    throw "SQLite 合同 TRX 计数不是 total=1/passed=1/failed=0/notExecuted=0。"
}
if (-not ([string]$testResult.testName).Contains("ProviderContract_WhenSqliteScenariosRun_ShouldRecordRealIntegrationEvidence", [StringComparison]::Ordinal)) {
    throw "SQLite 合同 TRX 缺少目标测试方法。"
}
$trxStart = [DateTimeOffset]::Parse($trx.TestRun.Times.start).ToUniversalTime()
$trxFinish = [DateTimeOffset]::Parse($trx.TestRun.Times.finish).ToUniversalTime()
if ($matrixStartedAt.UtcTicks -lt $trxStart.UtcTicks -or
    $matrixCompletedAt.UtcTicks -gt $trxFinish.UtcTicks) {
    throw "SQLite Matrix 时间未落在当前 TRX 执行窗口内。"
}

$artifactText = Get-Content -LiteralPath $artifactPath -Raw -Encoding utf8
if ($artifactText -match "(?i)Password=|User Id=|Data Source=|Server=") {
    throw "SQLite Matrix 包含敏感连接字段。"
}
Write-Host ("SQLite contract passed: Framework={0}; Trx={1}; Matrix={2}; Entries={3}" -f
    $Framework, $relativeTrxPath, $relativeArtifactPath, $entries.Count)
