[CmdletBinding()]
param(
    [ValidateSet("Release")]
    [string]$Configuration = "Release",

    [string]$ResultsRoot = "artifacts/release-candidate"
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$providerRunner = Join-Path $repositoryRoot "eng\ci\Invoke-ProviderIntegrationTests.ps1"
$sqliteRunner = Join-Path $repositoryRoot "eng\ci\Invoke-SqliteContractTests.ps1"
$evidenceCli = Join-Path $repositoryRoot "eng\Bing.ProviderEvidence.Cli\Bing.ProviderEvidence.Cli.csproj"
$benchmarkProject = Join-Path $repositoryRoot "framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj"
$unitResultsRelativeRoot = "artifacts/test-results/release-candidate/unit"
$aggregateResultsRelativeRoot = "artifacts/test-results/release-candidate"
$formalResultsRelativeRoot = "artifacts/benchmarks/release-candidate/formal-host"
$normalizedResultsRoot = $ResultsRoot.Replace('\', '/').Trim('/')
if ([System.IO.Path]::IsPathRooted($normalizedResultsRoot) -or
    [string]::IsNullOrWhiteSpace($normalizedResultsRoot) -or
    @($normalizedResultsRoot.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -gt 0 -or
    -not ($normalizedResultsRoot.Equals("artifacts/release-candidate", [StringComparison]::OrdinalIgnoreCase) -or
        $normalizedResultsRoot.StartsWith("artifacts/release-candidate/", [StringComparison]::OrdinalIgnoreCase))) {
    throw "ResultsRoot 必须是 artifacts/release-candidate 下的工作区相对目录。"
}
$unitProjects = @(
    "framework/tests/Bing.Core.Tests/Bing.Core.Tests.csproj",
    "framework/tests/Bing.Dapper.Core.Tests/Bing.Dapper.Core.Tests.csproj",
    "framework/tests/Bing.Data.Sql.Tests/Bing.Data.Sql.Tests.csproj",
    "framework/tests/Bing.Test.Shared/Bing.Test.Shared.csproj"
)
$providerProjects = @{
    MySql = "framework/tests/Bing.Dapper.MySql.Tests.Integration/Bing.Dapper.MySql.Tests.Integration.csproj"
    PostgreSql = "framework/tests/Bing.Dapper.PostgreSql.Tests.Integration/Bing.Dapper.PostgreSql.Tests.Integration.csproj"
    SqlServer = "framework/tests/Bing.Dapper.SqlServer.Tests.Integration/Bing.Dapper.SqlServer.Tests.Integration.csproj"
}
$providerGates = @{
    MySql = "RUN_MYSQL_INTEGRATION_TESTS"
    PostgreSql = "RUN_POSTGRESQL_INTEGRATION_TESTS"
    SqlServer = "RUN_SQLSERVER_INTEGRATION_TESTS"
}
$providerConnections = @{
    MySql = "ConnectionStrings__MySqlConnection"
    PostgreSql = "ConnectionStrings__PostgreSqlConnection"
    SqlServer = "ConnectionStrings__SqlServerConnection"
}

function Get-RelativePath {
    param([string]$Path)

    $root = [System.IO.Path]::GetFullPath($repositoryRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "制品路径必须位于工作区内。"
    }
    return $fullPath.Substring($root.Length).Replace('\', '/')
}

function Get-SourceIdentity {
    $head = (& git -C $repositoryRoot rev-parse HEAD 2>$null | Select-Object -First 1).ToString().Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($head)) {
        throw "Release Evidence 源码身份校验失败：无法解析 HEAD。"
    }
    $status = (& git -C $repositoryRoot status --porcelain --untracked-files=all 2>$null | Out-String).Trim()
    if (-not [string]::IsNullOrWhiteSpace($status)) {
        throw "Release Evidence 必须来自 clean source。"
    }
    foreach ($name in @("APPVEYOR_REPO_COMMIT", "GITHUB_SHA", "BUILD_SOURCEVERSION", "CI_COMMIT_SHA")) {
        $value = [Environment]::GetEnvironmentVariable($name)
        if (-not [string]::IsNullOrWhiteSpace($value) -and
            -not [string]::Equals($head, $value.Trim(), [StringComparison]::OrdinalIgnoreCase)) {
            throw "Release Evidence 源码身份校验失败：$name 与 HEAD 不一致。"
        }
    }
    return "git=$head;source=clean"
}

function Get-EvidenceSessionId {
    $buildId = [Environment]::GetEnvironmentVariable("APPVEYOR_BUILD_ID")
    if ([string]::IsNullOrWhiteSpace($buildId)) {
        throw "Release Evidence 必须绑定受保护 CI build identity。"
    }
    $expected = "ci-$($buildId.Trim())"
    $configured = [Environment]::GetEnvironmentVariable("BING_PROVIDER_EVIDENCE_SESSION_ID")
    if (-not [string]::IsNullOrWhiteSpace($configured) -and
        -not [string]::Equals($configured.Trim(), $expected, [StringComparison]::Ordinal)) {
        throw "Release Evidence session 与 APPVEYOR_BUILD_ID 不一致。"
    }
    return $expected
}

function Invoke-Captured {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$LogPath
    )

    $output = & $FilePath @Arguments 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
    $output | Set-Content -LiteralPath $LogPath -Encoding utf8
    return [pscustomobject]@{ ExitCode = $exitCode; Output = $output }
}

function Get-ArtifactRecord {
    param([string]$Path)

    $relativePath = Get-RelativePath $Path
    return [ordered]@{
        Path = $relativePath
        Sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

function Set-EnvironmentValue {
    param([string]$Name, [string]$Value)

    [Environment]::SetEnvironmentVariable($Name, $Value)
}

function Clear-ProviderEnvironment {
    foreach ($name in @(
        "RUN_INTEGRATION_TESTS", "RUN_MYSQL_INTEGRATION_TESTS", "RUN_POSTGRESQL_INTEGRATION_TESTS",
        "RUN_SQLSERVER_INTEGRATION_TESTS", "RUN_ORACLE_INTEGRATION_TESTS", "RUN_DORIS_INTEGRATION_TESTS",
        "ALLOW_DATABASE_RESET_FOR_TESTS", "ConnectionStrings__DefaultConnection",
        "ConnectionStrings__MySqlConnection", "ConnectionStrings__PostgreSqlConnection",
        "ConnectionStrings__SqlServerConnection", "ConnectionStrings__OracleConnection",
        "ConnectionStrings__DorisConnection"
    )) {
        [Environment]::SetEnvironmentVariable($name, $null)
    }
}

function Invoke-ProviderRuns {
    param([string]$SessionId)

    $connections = @{}
    foreach ($provider in $providerConnections.Keys) {
        $connections[$provider] = [Environment]::GetEnvironmentVariable($providerConnections[$provider])
    }
    foreach ($provider in @("MySql", "PostgreSql", "SqlServer")) {
        Clear-ProviderEnvironment
        Set-EnvironmentValue $providerGates[$provider] "true"
        Set-EnvironmentValue $providerConnections[$provider] $connections[$provider]
        Set-EnvironmentValue "ALLOW_DATABASE_RESET_FOR_TESTS" "true"
        Set-EnvironmentValue "BING_PROVIDER_EVIDENCE_SESSION_ID" $SessionId
        foreach ($framework in @("net6.0", "net8.0")) {
            & $providerRunner -Provider $provider -Framework $framework -Configuration $Configuration
            if ($LASTEXITCODE -ne 0) {
                throw "受保护 Provider run 失败：$provider/$framework。"
            }
        }
    }
}

function Invoke-SqliteRuns {
    param([string]$SessionId)

    Clear-ProviderEnvironment
    Set-EnvironmentValue "BING_PROVIDER_EVIDENCE_SESSION_ID" $SessionId
    foreach ($framework in @("net6.0", "net8.0")) {
        $directory = "artifacts/provider-test-results/sqlite-$framework-release"
        & $sqliteRunner -ResultsDirectory $directory -Framework $framework -Configuration $Configuration `
            -RunName "release" -ReleaseEvidence
        if ($LASTEXITCODE -ne 0) {
            throw "受保护 SQLite Release Evidence run 失败：$framework。"
        }
    }
}

function Invoke-UnitRuns {
    param([string]$UnitRoot)

    foreach ($relativeProject in $unitProjects) {
        $project = Join-Path $repositoryRoot ($relativeProject.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        foreach ($framework in @("net6.0", "net8.0")) {
            $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project)
            $resultDirectory = Join-Path $UnitRoot ("{0}-{1}" -f $projectName, $framework)
            New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
            $trxName = "$projectName-$framework.trx"
            & dotnet test $project -c $Configuration -f $framework --no-restore --nologo `
                "--logger:trx;LogFileName=$trxName" --results-directory $resultDirectory
            if ($LASTEXITCODE -ne 0) {
                throw "Unit test run 失败：$projectName/$framework。"
            }
        }
    }
}

function New-Rs0026Evidence {
    param([string]$GateRoot, [string]$SessionId, [string]$SourceIdentity)

    $projects = @(
        "Bing.Data.Sql", "Bing.Dapper.Core", "Bing.Dapper.MySql", "Bing.Dapper.PostgreSql",
        "Bing.Dapper.SqlServer", "Bing.Dapper.Sqlite", "Bing.Dapper.Oracle"
    )
    $entries = @()
    foreach ($projectName in $projects) {
        $project = Get-ChildItem -Path (Join-Path $repositoryRoot "framework/src") -Filter "$projectName.csproj" -Recurse |
            Select-Object -First 1
        if ($null -eq $project) {
            throw "RS0026 gate project 不存在：$projectName。"
        }
        $logPath = Join-Path $GateRoot "$projectName.log"
        $result = Invoke-Captured -FilePath "dotnet" -Arguments @("build", $project.FullName, "-c", $Configuration, "--no-restore", "--nologo") -LogPath $logPath
        $rs0026Count = ([regex]::Matches($result.Output, "RS0026")).Count
        $errorCount = ([regex]::Matches($result.Output, "(?im)^.*error .*$")).Count
        $entries += [ordered]@{
            Project = $projectName
            ExitCode = $result.ExitCode
            RS0026 = $rs0026Count
            ErrorLines = $errorCount
            Log = Get-RelativePath $logPath
        }
    }
    $artifactRecords = Get-ChildItem -LiteralPath $GateRoot -File | ForEach-Object { Get-ArtifactRecord $_.FullName }
    $inventory = [ordered]@{
        EvidenceSessionId = $SessionId
        SourceIdentity = $SourceIdentity
        SourceState = "clean"
        Entries = $entries
        Artifacts = @($artifactRecords)
    }
    $path = Join-Path $GateRoot "rs0026-inventory.json"
    $inventory | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $path -Encoding utf8
    return Get-RelativePath $path
}

function New-ApiGateEvidence {
    param([string]$GateRoot, [string]$SessionId, [string]$SourceIdentity)

    $project = Join-Path $repositoryRoot "framework/tests/Bing.Data.Sql.Tests/Bing.Data.Sql.Tests.csproj"
    $productionProjectNames = @(
        "Bing.Data.Sql", "Bing.Dapper.Core", "Bing.Dapper.MySql", "Bing.Dapper.PostgreSql",
        "Bing.Dapper.SqlServer", "Bing.Dapper.Sqlite", "Bing.Dapper.Oracle"
    )
    $productionProjects = @(
        foreach ($productionProjectName in $productionProjectNames) {
            $productionProject = Get-ChildItem -Path (Join-Path $repositoryRoot "framework/src") `
                -Filter "$productionProjectName.csproj" -Recurse | Select-Object -First 1
            if ($null -eq $productionProject) {
                throw "API gate production project 不存在：$productionProjectName。"
            }
            $productionProject
        }
    )
    if ($productionProjects.Count -ne $productionProjectNames.Count) {
        throw "API gate production project set is incomplete."
    }
    foreach ($productionProject in $productionProjects) {
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($productionProject.FullName)
        $analyzerLogPath = Join-Path $GateRoot ("{0}-analyzer.log" -f $projectName)
        $analyzerResult = Invoke-Captured -FilePath "dotnet" -Arguments @(
            "build", $productionProject.FullName, "-c", $Configuration, "--no-restore", "--nologo"
        ) -LogPath $analyzerLogPath
        if ($analyzerResult.ExitCode -ne 0 -or $analyzerResult.Output -match "(?im)^.*error .*$") {
            throw "API gate analyzer build failed: $($productionProject.Name)."
        }
    }
    $logPath = Join-Path $GateRoot "api-contract-tests.log"
    $trxDirectory = Join-Path $GateRoot "api-contract-tests"
    New-Item -ItemType Directory -Path $trxDirectory -Force | Out-Null
    $trxName = "api-contract-tests.trx"
    $result = Invoke-Captured -FilePath "dotnet" -Arguments @(
        "test", $project, "-c", $Configuration, "-f", "net8.0", "--no-restore", "--nologo",
        "--filter", "FullyQualifiedName~SqlQueryApiContractTest|FullyQualifiedName~TransactionApiContractTest",
        "--logger:trx;LogFileName=$trxName", "--results-directory", $trxDirectory
    ) -LogPath $logPath
    if ($result.ExitCode -ne 0) {
        throw "API contract tests failed."
    }
    $trxPath = Join-Path $trxDirectory $trxName
    if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) {
        throw "API contract TRX was not produced."
    }
    [xml]$trx = Get-Content -LiteralPath $trxPath -Raw -Encoding utf8
    $counters = $trx.TestRun.ResultSummary.Counters
    if ($null -eq $counters -or [int]$counters.total -le 0 -or [int]$counters.failed -ne 0 -or
        [int]$counters.notExecuted -ne 0) {
        throw "API contract TRX is incomplete."
    }
    $artifacts = @($productionProjects | ForEach-Object {
        Get-ArtifactRecord (Join-Path $GateRoot ("{0}-analyzer.log" -f
            [System.IO.Path]::GetFileNameWithoutExtension($_.FullName)))
    }) + @(
        Get-ArtifactRecord $logPath
        Get-ArtifactRecord $trxPath
    )
    $path = Join-Path $GateRoot "api-gate.json"
    ([ordered]@{
        Status = "PASS"
        PublicApiAnalyzer = "PASS"
        ReflectionContract = "PASS"
        EvidenceSessionId = $SessionId
        SourceIdentity = $SourceIdentity
        SourceState = "clean"
        Artifacts = $artifacts
    } | ConvertTo-Json -Depth 6) | Set-Content -LiteralPath $path -Encoding utf8
    return Get-RelativePath $path
}

function Invoke-FormalHost {
    param([string]$FormalRoot, [string]$SessionId, [string]$SourceIdentity)

    $artifactDirectory = "$formalResultsRelativeRoot/BenchmarkDotNet"
    $logPath = Join-Path $FormalRoot "formal-host.log"
    $result = Invoke-Captured -FilePath "dotnet" -Arguments @(
        "run", "--project", $benchmarkProject, "-c", $Configuration, "--no-build", "--",
        "--artifacts", $artifactDirectory
    ) -LogPath $logPath
    if ($result.ExitCode -ne 0) {
        throw "FormalHost benchmark failed."
    }
    $reportFiles = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot $artifactDirectory) -Recurse -File |
        Where-Object { $_.Name -match "-report\.(csv|html)$" -or $_.Name -like "*-report-github.md" -or $_.Extension -eq ".log" })
    $csv = @($reportFiles | Where-Object { $_.Name.EndsWith("-report.csv", [StringComparison]::OrdinalIgnoreCase) })
    $markdown = @($reportFiles | Where-Object { $_.Name.EndsWith("-report-github.md", [StringComparison]::OrdinalIgnoreCase) })
    $html = @($reportFiles | Where-Object { $_.Name.EndsWith("-report.html", [StringComparison]::OrdinalIgnoreCase) })
    $raw = @(Get-Item -LiteralPath $logPath)
    if ($csv.Count -eq 0 -or $markdown.Count -eq 0 -or $html.Count -eq 0 -or $raw.Count -eq 0) {
        throw "FormalHost reports are incomplete."
    }
    $reports = @($csv + $markdown + $html + $raw + (Get-Item -LiteralPath $logPath) |
        ForEach-Object { Get-RelativePath $_.FullName })
    $artifactRecords = @($reportFiles + $logPath | ForEach-Object {
        $path = if ($_ -is [string]) { $_ } else { $_.FullName }
        Get-ArtifactRecord $path
    })
    $manifestPath = Join-Path $FormalRoot "formal-host-complete.json"
    ([ordered]@{
        Status = "Complete"
        Job = "FormalHost"
        LaunchCount = 3
        WarmupCount = 6
        IterationCount = 15
        EvidenceSessionId = $SessionId
        SourceIdentity = $SourceIdentity
        SourceState = "clean"
        Reports = $reports
        Artifacts = $artifactRecords
    } | ConvertTo-Json -Depth 6) | Set-Content -LiteralPath $manifestPath -Encoding utf8
    return Get-RelativePath $FormalRoot
}

if (-not [string]::Equals([Environment]::GetEnvironmentVariable("CI"), "true",
        [StringComparison]::OrdinalIgnoreCase) -or
    [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable("APPVEYOR_BUILD_ID"))) {
    throw "Release Candidate validation must run in protected AppVeyor CI."
}

$sessionId = Get-EvidenceSessionId
$sourceIdentity = Get-SourceIdentity
$resultsRootPath = Join-Path $repositoryRoot ($normalizedResultsRoot.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
New-Item -ItemType Directory -Path $resultsRootPath -Force | Out-Null
$unitRoot = Join-Path $repositoryRoot ($unitResultsRelativeRoot.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
$rs0026Root = Join-Path $resultsRootPath "rs0026"
$apiRoot = Join-Path $resultsRootPath "api"
$formalRoot = Join-Path $repositoryRoot ($formalResultsRelativeRoot.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
foreach ($directory in @($unitRoot, $rs0026Root, $apiRoot, $formalRoot)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$oldSession = [Environment]::GetEnvironmentVariable("BING_PROVIDER_EVIDENCE_SESSION_ID")
$oldSource = [Environment]::GetEnvironmentVariable("BING_PROVIDER_SOURCE_IDENTITY")
try {
    Set-EnvironmentValue "BING_PROVIDER_EVIDENCE_SESSION_ID" $sessionId
    Set-EnvironmentValue "BING_PROVIDER_SOURCE_IDENTITY" $sourceIdentity
    Invoke-UnitRuns $unitRoot
    Invoke-ProviderRuns $sessionId
    Invoke-SqliteRuns $sessionId
    $rs0026Path = New-Rs0026Evidence $rs0026Root $sessionId $sourceIdentity
    $apiGatePath = New-ApiGateEvidence $apiRoot $sessionId $sourceIdentity
    $formalHostDirectory = Invoke-FormalHost $formalRoot $sessionId $sourceIdentity

    & dotnet run --project $evidenceCli -c $Configuration --no-restore -- `
        --workspace-root $repositoryRoot `
        --aggregate-output-directory "$aggregateResultsRelativeRoot/provider-aggregate" `
        --provider-results-directory "artifacts/provider-test-results" `
        --unit-results-directory $unitResultsRelativeRoot `
        --formal-host-results-directory $formalHostDirectory `
        --rs0026-inventory $rs0026Path `
        --api-gate-file $apiGatePath `
        --evidence-session-id $sessionId `
        --source-identity $sourceIdentity
    if ($LASTEXITCODE -ne 0) {
        throw "Release evidence aggregate failed."
    }
    $aggregatePath = Join-Path $repositoryRoot "$aggregateResultsRelativeRoot/provider-aggregate/provider-capability-matrix.json"
    if (-not (Test-Path -LiteralPath $aggregatePath -PathType Leaf)) {
        throw "Release evidence aggregate matrix was not produced."
    }
    $aggregate = Get-Content -LiteralPath $aggregatePath -Raw -Encoding utf8 | ConvertFrom-Json
    if (-not [bool]$aggregate.ReleaseReady) {
        throw "Release Candidate is not ready; aggregate gate remained fail-closed."
    }
    Write-Host "Release Candidate validation passed: EvidenceSessionId=$sessionId; SourceIdentity=$sourceIdentity"
}
finally {
    [Environment]::SetEnvironmentVariable("BING_PROVIDER_EVIDENCE_SESSION_ID", $oldSession)
    [Environment]::SetEnvironmentVariable("BING_PROVIDER_SOURCE_IDENTITY", $oldSource)
}
