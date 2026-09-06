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
    [string]$RunName,

    [switch]$ReleaseEvidence
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$releaseEvidenceProjectPath = Join-Path $repositoryRoot "eng\Bing.ProviderEvidence.Cli\Bing.ProviderEvidence.Cli.csproj"
$configuredEvidenceSessionId = [Environment]::GetEnvironmentVariable("BING_PROVIDER_EVIDENCE_SESSION_ID")
$evidenceSessionId = $configuredEvidenceSessionId
$isCi = [string]::Equals([Environment]::GetEnvironmentVariable("CI"), "true",
    [StringComparison]::OrdinalIgnoreCase)
$buildIdentity = [Environment]::GetEnvironmentVariable("APPVEYOR_BUILD_ID")
if ([string]::IsNullOrWhiteSpace($evidenceSessionId)) {
    if ($isCi -and [string]::IsNullOrWhiteSpace($buildIdentity)) {
        throw "SQLite 证据会话校验失败：受保护 CI 必须提供构建身份。"
    }
    $evidenceSessionId = if ([string]::IsNullOrWhiteSpace($buildIdentity)) {
        "local-" + [Guid]::NewGuid().ToString("N")
    }
    else {
        "ci-$($buildIdentity.Trim())"
    }
}
if ($isCi -and [string]::IsNullOrWhiteSpace($buildIdentity)) {
    throw "SQLite 证据会话校验失败：受保护 CI 必须提供构建身份。"
}
if ($isCi -and -not [string]::Equals($evidenceSessionId, "ci-$($buildIdentity.Trim())",
        [StringComparison]::Ordinal)) {
    throw "SQLite 证据会话标识与当前 CI 构建不一致。"
}
if ($evidenceSessionId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$') {
    throw "SQLite 证据会话标识格式无效。"
}
$gitIdentity = (& git -C $repositoryRoot rev-parse HEAD 2>$null | Select-Object -First 1).ToString().Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($gitIdentity)) {
    throw "SQLite 证据源码身份校验失败：无法解析当前 HEAD。"
}
$sourceState = (& git -C $repositoryRoot status --porcelain --untracked-files=all 2>$null | Out-String).Trim()
$sourceState = if ([string]::IsNullOrWhiteSpace($sourceState)) { "clean" } else { "dirty" }
$sourceIdentity = "git=$gitIdentity;source=$sourceState"
if ($ReleaseEvidence -and $sourceState -ne "clean") {
    throw "SQLite Release Evidence 必须来自 clean source。"
}
if ($ReleaseEvidence -and -not [string]::Equals([Environment]::GetEnvironmentVariable("CI"), "true",
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "SQLite Release Evidence 必须在受保护 CI 中生成。"
}
$normalizedResultsDirectory = $ResultsDirectory.Replace('\', '/').Trim('/')
$invalidPathSegments = @($normalizedResultsDirectory -split '/' | Where-Object { $_ -in @("", ".", "..") })
if ([System.IO.Path]::IsPathRooted($normalizedResultsDirectory) -or
    [string]::IsNullOrWhiteSpace($normalizedResultsDirectory) -or
    -not ($normalizedResultsDirectory.StartsWith("artifacts/test-results/", [StringComparison]::OrdinalIgnoreCase) -or
        $normalizedResultsDirectory.StartsWith("artifacts/provider-test-results/", [StringComparison]::OrdinalIgnoreCase)) -or
    $invalidPathSegments.Count -gt 0) {
    throw "ResultsDirectory 必须是工作区内受控 artifacts 目录下的相对目录。"
}
$releaseRunId = "sqlite-$Framework-$RunName"
$releaseDirectory = "artifacts/provider-test-results/$releaseRunId"
$releaseTrxFileName = "sqlite-$Framework.trx"
$releaseTrxRelativePath = "$releaseDirectory/$releaseTrxFileName"
$trxFileName = if ($ReleaseEvidence) { $releaseTrxFileName } else { "$RunName-$Framework.trx" }
$artifactFileName = if ($ReleaseEvidence) { "$RunName-$Framework.json" } else { "$RunName-$Framework.json" }
if ($ReleaseEvidence -and
    -not [string]::Equals($normalizedResultsDirectory, $releaseDirectory, [StringComparison]::OrdinalIgnoreCase)) {
    throw "SQLite Release Evidence 必须写入 $releaseDirectory。"
}
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
    "BING_SQLITE_CONTRACT_ARTIFACT_FILE_NAME",
    "BING_PROVIDER_EVIDENCE_SESSION_ID",
    "BING_PROVIDER_RUN_METADATA_PATH",
    "BING_PROVIDER_SOURCE_IDENTITY"
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
    [Environment]::SetEnvironmentVariable("BING_PROVIDER_EVIDENCE_SESSION_ID", $evidenceSessionId)
    [Environment]::SetEnvironmentVariable("BING_PROVIDER_SOURCE_IDENTITY", $sourceIdentity)
    $metadataPath = Join-Path $resultsPath "provider-run-metadata.json"
    if ($ReleaseEvidence) {
        & dotnet build $projectPath -c $Configuration -f $Framework --no-restore --nologo
        if ($LASTEXITCODE -ne 0) {
            throw "SQLite Release Evidence build failed：Framework=$Framework。"
        }
        [Environment]::SetEnvironmentVariable("BING_PROVIDER_RUN_METADATA_PATH", $metadataPath)
    }

    $testArguments = @(
        "test", $projectPath, "-c", $Configuration, "-f", $Framework, "--no-restore", "--nologo"
    )
    if (-not $ReleaseEvidence) {
        $testArguments += @("--filter", $testFilter)
    }
    $testArguments += @("--logger:trx;LogFileName=$trxFileName", "--results-directory", $resultsPath)
    & dotnet @testArguments
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

if ($ReleaseEvidence) {
    $releaseTrxPath = Join-Path $resultsPath $releaseTrxFileName
    $releaseTrxRelativePath = "$normalizedResultsDirectory/$releaseTrxFileName"
    if (-not (Test-Path -LiteralPath $releaseTrxPath -PathType Leaf)) {
        throw "SQLite Release Evidence TRX 不存在：$releaseTrxRelativePath。"
    }
    $relativeTrxPath = $releaseTrxRelativePath
    $trxPath = $releaseTrxPath
}

$matrix = Get-Content -LiteralPath $artifactPath -Raw -Encoding utf8 | ConvertFrom-Json
if ([bool](Get-RequiredJsonProperty $matrix "ReleaseReady")) {
    throw "SQLite 合同 Matrix 不得标记为 ReleaseReady。"
}
$entries = @($matrix.Entries)
if (-not $ReleaseEvidence -and $entries.Count -ne 2) {
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
    if (-not $ReleaseEvidence -and (Get-RequiredJsonProperty $integrationEvidence "ArtifactKind") -ne "TestGenerated") {
        throw "SQLite 合同 Matrix 必须标记为 TestGenerated。"
    }
    if ($ReleaseEvidence -and (Get-RequiredJsonProperty $integrationEvidence "ArtifactKind") -ne "TestGenerated") {
        throw "SQLite Release Evidence 的受控合同输入必须先标记为 TestGenerated。"
    }
    if ((Get-RequiredJsonProperty $integrationEvidence "EvidenceSessionId") -ne $evidenceSessionId) {
        throw "SQLite 合同 Matrix 的证据会话标识不一致。"
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
if ($ReleaseEvidence) {
    if ([int]$counters.total -le 0 -or [int]$counters.passed -le 0 -or [int]$counters.failed -ne 0 -or
        [int]$counters.notExecuted -ne 0) {
        throw "SQLite Release Evidence TRX 必须是完整零失败、零跳过运行。"
    }
}
elseif ([int]$counters.total -ne 1 -or [int]$counters.passed -ne 1 -or [int]$counters.failed -ne 0 -or
        [int]$counters.notExecuted -ne 0) {
    throw "SQLite 合同 TRX 计数不是 total=1/passed=1/failed=0/notExecuted=0。"
}
if (-not $ReleaseEvidence -and -not ([string]$testResult.testName).Contains("ProviderContract_WhenSqliteScenariosRun_ShouldRecordRealIntegrationEvidence", [StringComparison]::Ordinal)) {
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

if ($ReleaseEvidence) {
    if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
        throw "SQLite Release Evidence 缺少 testhost 元数据。"
    }
    $metadata = Get-Content -LiteralPath $metadataPath -Raw -Encoding utf8 | ConvertFrom-Json
    foreach ($name in @("ProviderVersion", "DatabaseVersion", "DriverVersion", "RuntimeVersion", "OperatingSystem", "TargetFramework")) {
        $value = [string]$metadata.$name
        if ([string]::IsNullOrWhiteSpace($value) -or $value -in @("not-recorded", "unknown")) {
            throw "SQLite Release Evidence 元数据缺失或为占位值：$name。"
        }
    }
    if (-not [string]::Equals([string]$metadata.TargetFramework, $Framework, [StringComparison]::OrdinalIgnoreCase)) {
        throw "SQLite Release Evidence 的 TFM 与当前运行不一致。"
    }
    $runId = $releaseRunId
    $expectedDirectory = $releaseDirectory
    $binaryOutputDirectory = Join-Path $repositoryRoot ("framework\tests\Bing.Dapper.Sqlite.Tests.Integration\bin\{0}\{1}" -f $Configuration, $Framework)
    $binaryNames = @(
        "Bing.Dapper.Sqlite.Tests.Integration.dll",
        "Bing.Dapper.Sqlite.dll",
        "Bing.Data.Sql.dll"
    )
    $manifestEntries = @()
    foreach ($binaryName in $binaryNames) {
        $builtBinaryPath = Join-Path $binaryOutputDirectory $binaryName
        if (-not (Test-Path -LiteralPath $builtBinaryPath -PathType Leaf)) {
            throw "SQLite Release Evidence 缺少已构建程序集：$binaryName。"
        }
        $artifactBinaryPath = Join-Path $resultsPath $binaryName
        Copy-Item -LiteralPath $builtBinaryPath -Destination $artifactBinaryPath -Force
        $manifestEntries += [ordered]@{
            Path = $normalizedResultsDirectory + "/" + $binaryName
            Sha256 = (Get-FileHash -LiteralPath $artifactBinaryPath -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
    $manifestPath = Join-Path $resultsPath "provider-binary-manifest.json"
    ([ordered]@{
        Provider = "SQLite"
        Framework = $Framework
        RunId = $runId
        EvidenceSessionId = $evidenceSessionId
        SourceIdentity = $sourceIdentity
        SourceState = $sourceState
        Assemblies = $manifestEntries
    } | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $manifestPath -Encoding utf8
    $releaseMatrixJsonPath = Join-Path $resultsPath "provider-capability-matrix.json"
    Copy-Item -LiteralPath $artifactPath -Destination $releaseMatrixJsonPath -Force
    $releaseMatrixMarkdownPath = Join-Path $resultsPath "provider-capability-matrix.md"
    "# SQLite provider capability matrix`n`n> Release Evidence source matrix is generated by the controlled SQLite contract runner." |
        Set-Content -LiteralPath $releaseMatrixMarkdownPath -Encoding utf8
    $summaryPath = Join-Path $resultsPath ("sqlite-{0}.json" -f $Framework)
    $startedAtUtc = $trxStart.ToUniversalTime().ToString("O")
    $completedAtUtc = $trxFinish.ToUniversalTime().ToString("O")
    $total = [int]$counters.total
    $passed = [int]$counters.passed
    $failed = [int]$counters.failed
    $skipped = [int]$counters.notExecuted
    if ($total -le 0 -or $passed -le 0 -or $failed -ne 0 -or $skipped -ne 0) {
        throw "SQLite Release Evidence 必须是完整零失败、零跳过运行。"
    }
    ([ordered]@{
        Provider = "SQLite"
        Framework = $Framework
        Discovered = $total
        Passed = $passed
        Failed = $failed
        Skipped = $skipped
        OptionalSkipped = 0
        CoreSkipped = 0
        Executed = $passed + $failed
        Duration = [string]$trx.TestRun.ResultSummary.duration
        ConfigurationSource = "ProtectedCI"
        GlobalGate = $false
        ProviderGate = $true
        ConnectionConfigured = $true
        DatabaseName = "controlled-file"
        ResetAllowed = $true
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
        StartedAtUtc = $startedAtUtc
        CompletedAtUtc = $completedAtUtc
        SourceIdentity = $sourceIdentity
        BinaryManifestPath = "$expectedDirectory/provider-binary-manifest.json"
        TrxPath = $releaseTrxRelativePath
        ArtifactPath = "$expectedDirectory/sqlite-$Framework.json"
    } | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $summaryPath -Encoding utf8
    if (-not (Test-Path -LiteralPath $releaseEvidenceProjectPath -PathType Leaf)) {
        throw "SQLite Release Evidence CLI 项目不存在。"
    }
    & dotnet run --project $releaseEvidenceProjectPath -c $Configuration --no-restore -- --workspace-root $repositoryRoot `
        --provider SQLite --framework $Framework --run-id $runId --evidence-session-id $evidenceSessionId `
        --trx $releaseTrxRelativePath --summary "$expectedDirectory/sqlite-$Framework.json" `
        --matrix-json "$expectedDirectory/provider-capability-matrix.json" `
        --matrix-markdown "$expectedDirectory/provider-capability-matrix.md" `
        --binary-manifest "$expectedDirectory/provider-binary-manifest.json" `
        --source-identity $sourceIdentity --provider-version ([string]$metadata.ProviderVersion) `
        --database-version ([string]$metadata.DatabaseVersion) --driver-version ([string]$metadata.DriverVersion) `
        --runtime-version ([string]$metadata.RuntimeVersion) --operating-system ([string]$metadata.OperatingSystem) `
        --source-state $sourceState --started-at-utc $startedAtUtc --completed-at-utc $completedAtUtc
    if ($LASTEXITCODE -ne 0) {
        throw "SQLite Release Evidence 验证失败：Framework=$Framework。"
    }
}
Write-Host ("SQLite contract passed: Framework={0}; Trx={1}; Matrix={2}; Entries={3}" -f
    $Framework, $relativeTrxPath, $relativeArtifactPath, $entries.Count)
