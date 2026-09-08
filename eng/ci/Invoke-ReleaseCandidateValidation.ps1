[CmdletBinding()]
param(
    [ValidateSet("Release")]
    [string]$Configuration = "Release",

    [string]$ResultsRoot = "artifacts/release-candidate",

    [switch]$SelfTest
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
    [ordered]@{ Path = "framework/tests/Bing.Core.Tests/Bing.Core.Tests.csproj"; Frameworks = @("net6.0", "net8.0") },
    [ordered]@{ Path = "framework/tests/Bing.Dapper.Core.Tests/Bing.Dapper.Core.Tests.csproj"; Frameworks = @("net6.0", "net8.0") },
    [ordered]@{ Path = "framework/tests/Bing.Dapper.MySql.Tests/Bing.Dapper.MySql.Tests.csproj"; Frameworks = @("net6.0", "net8.0") },
    [ordered]@{ Path = "framework/tests/Bing.Dapper.PostgreSql.Tests/Bing.Dapper.PostgreSql.Tests.csproj"; Frameworks = @("net6.0", "net8.0") },
    [ordered]@{ Path = "framework/tests/Bing.Dapper.SqlServer.Tests/Bing.Dapper.SqlServer.Tests.csproj"; Frameworks = @("net6.0", "net8.0") },
    [ordered]@{ Path = "framework/tests/Bing.Dapper.Sqlite.Tests/Bing.Dapper.Sqlite.Tests.csproj"; Frameworks = @("net6.0", "net8.0") },
    [ordered]@{ Path = "framework/tests/Bing.Dapper.Oracle.Tests/Bing.Dapper.Oracle.Tests.csproj"; Frameworks = @("net6.0", "net8.0") },
    [ordered]@{ Path = "framework/tests/Bing.Data.Sql.Tests/Bing.Data.Sql.Tests.csproj"; Frameworks = @("net6.0", "net8.0") },
    [ordered]@{ Path = "framework/tests/Bing.Data.Sql.CustomProvider.Tests/Bing.Data.Sql.CustomProvider.Tests.csproj"; Frameworks = @("net6.0", "net8.0") },
    [ordered]@{ Path = "framework/tests/Bing.Data.Sql.Analyzers.Tests/Bing.Data.Sql.Analyzers.Tests.csproj"; Frameworks = @("net8.0") },
    [ordered]@{ Path = "framework/tests/Bing.Test.Shared/Bing.Test.Shared.csproj"; Frameworks = @("net6.0", "net8.0") }
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
    $headOutput = & git -C $repositoryRoot rev-parse HEAD 2>$null
    $headExitCode = $LASTEXITCODE
    $head = ($headOutput | Select-Object -First 1).ToString().Trim()
    if ($headExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($head)) {
        throw "Release Evidence 源码身份校验失败：无法解析 HEAD。"
    }
    $statusOutput = & git -C $repositoryRoot status --porcelain --untracked-files=all 2>$null
    $statusExitCode = $LASTEXITCODE
    $status = ($statusOutput | Out-String).Trim()
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

function Assert-SourceIdentity {
    param(
        [Parameter(Mandatory)]
        [string]$Expected,

        [Parameter(Mandatory)]
        [string]$Stage,

        [scriptblock]$SourceIdentityReader
    )

    $actual = if ($null -eq $SourceIdentityReader) {
        Get-SourceIdentity
    }
    else {
        & $SourceIdentityReader
    }
    $actual = [string]$actual
    if (-not [string]::Equals($actual, $Expected, [StringComparison]::Ordinal)) {
        throw "Release Evidence source identity changed $Stage. Expected '$Expected', actual '$actual'."
    }
    return $actual
}

function Publish-AggregateEvidence {
    param(
        [Parameter(Mandatory)]
        [string]$StagingDirectory,

        [Parameter(Mandatory)]
        [string]$FinalDirectory,

        [Parameter(Mandatory)]
        [string]$ExpectedSourceIdentity,

        [scriptblock]$SourceIdentityReader
    )

    $root = [System.IO.Path]::GetFullPath($repositoryRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    foreach ($path in @($StagingDirectory, $FinalDirectory)) {
        $fullPath = [System.IO.Path]::GetFullPath($path)
        if (-not $fullPath.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
            throw "聚合制品路径必须位于工作区内。"
        }
    }
    if (-not (Test-Path -LiteralPath $StagingDirectory -PathType Container)) {
        throw "Release evidence aggregate staging directory was not produced."
    }
    if (Test-Path -LiteralPath $FinalDirectory) {
        throw "Release evidence aggregate final directory already exists; refusing to reuse stale evidence."
    }

    Assert-SourceIdentity -Expected $ExpectedSourceIdentity -Stage "before aggregate publish" `
        -SourceIdentityReader $SourceIdentityReader | Out-Null
    New-Item -ItemType Directory -Path (Split-Path -Parent $FinalDirectory) -Force | Out-Null
    Move-Item -LiteralPath $StagingDirectory -Destination $FinalDirectory
    try {
        return Assert-SourceIdentity -Expected $ExpectedSourceIdentity -Stage "after aggregate publish" `
            -SourceIdentityReader $SourceIdentityReader
    }
    catch {
        if (Test-Path -LiteralPath $FinalDirectory) {
            Remove-Item -LiteralPath $FinalDirectory -Recurse -Force -ErrorAction SilentlyContinue
        }
        throw
    }
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

    $outputLines = & $FilePath @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $output = $outputLines | Out-String
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

function Get-ValidatedResetAuthorization {
    param([string]$Value)

    if (-not [string]::Equals($Value, "true", [StringComparison]::OrdinalIgnoreCase)) {
        throw "受保护 Provider run 必须由外部显式提供 ALLOW_DATABASE_RESET_FOR_TESTS=true。"
    }
    return "true"
}

function Invoke-ProviderRuns {
    param(
        [string]$SessionId,
        [scriptblock]$RunnerOverride
    )

    $resetAuthorization = Get-ValidatedResetAuthorization (
        [Environment]::GetEnvironmentVariable("ALLOW_DATABASE_RESET_FOR_TESTS"))
    $connections = @{}
    foreach ($provider in $providerConnections.Keys) {
        $connections[$provider] = [Environment]::GetEnvironmentVariable($providerConnections[$provider])
    }
    foreach ($provider in @("MySql", "PostgreSql", "SqlServer")) {
        Clear-ProviderEnvironment
        Set-EnvironmentValue $providerGates[$provider] "true"
        Set-EnvironmentValue $providerConnections[$provider] $connections[$provider]
        Set-EnvironmentValue "ALLOW_DATABASE_RESET_FOR_TESTS" $resetAuthorization
        Set-EnvironmentValue "BING_PROVIDER_EVIDENCE_SESSION_ID" $SessionId
        foreach ($framework in @("net6.0", "net8.0")) {
            if ($null -eq $RunnerOverride) {
                & $providerRunner -Provider $provider -Framework $framework -Configuration $Configuration
            }
            else {
                & $RunnerOverride $provider $framework
            }
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
    param(
        [string]$UnitRoot,
        [string]$SessionId,
        [string]$SourceIdentity
    )

    $existingFiles = @(Get-ChildItem -LiteralPath $UnitRoot -Recurse -File -ErrorAction SilentlyContinue)
    if ($existingFiles.Count -gt 0) {
        throw "Unit 结果目录必须为空，拒绝复用旧 TRX。"
    }
    $startedAtUtc = [DateTimeOffset]::UtcNow
    $entries = @()
    foreach ($unitProject in $unitProjects) {
        $relativeProject = [string]$unitProject.Path
        $project = Join-Path $repositoryRoot ($relativeProject.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        foreach ($framework in $unitProject.Frameworks) {
            $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project)
            $resultDirectory = Join-Path $UnitRoot ("{0}-{1}" -f $projectName, $framework)
            New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
            $trxName = "$projectName-$framework.trx"
            & dotnet test $project -c $Configuration -f $framework --no-restore --nologo `
                "--logger:trx;LogFileName=$trxName" --results-directory $resultDirectory
            if ($LASTEXITCODE -ne 0) {
                throw "Unit test run 失败：$projectName/$framework。"
            }
            $trxPath = Join-Path $resultDirectory $trxName
            if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) {
                throw "Unit test run 未生成预期 TRX：$projectName/$framework。"
            }
            $entries += [ordered]@{
                Project = $projectName
                Framework = $framework
                TrxPath = Get-RelativePath $trxPath
                Sha256 = (Get-FileHash -LiteralPath $trxPath -Algorithm SHA256).Hash.ToLowerInvariant()
                LastWriteTimeUtc = [DateTimeOffset]::new((Get-Item -LiteralPath $trxPath).LastWriteTimeUtc,
                    [TimeSpan]::Zero).ToString("O")
            }
        }
    }
    $completedAtUtc = [DateTimeOffset]::UtcNow
    $currentSourceIdentity = Get-SourceIdentity
    if (-not [string]::Equals($currentSourceIdentity, $SourceIdentity, [StringComparison]::Ordinal)) {
        throw "Unit test run changed the source identity during execution."
    }
    $inventoryPath = Join-Path $UnitRoot "unit-inventory.json"
    ([ordered]@{
        EvidenceSessionId = $SessionId
        SourceIdentity = $SourceIdentity
        SourceState = "clean"
        StartedAtUtc = $startedAtUtc.ToString("O")
        CompletedAtUtc = $completedAtUtc.ToString("O")
        Entries = $entries
    } | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $inventoryPath -Encoding utf8
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
            RS0016 = ([regex]::Matches($result.Output, "RS0016")).Count
            RS0017 = ([regex]::Matches($result.Output, "RS0017")).Count
            RS0018 = ([regex]::Matches($result.Output, "RS0018")).Count
            RS0026 = $rs0026Count
            ErrorLines = $errorCount
            Log = Get-RelativePath $logPath
        }
    }
    $inventoryPath = Join-Path $GateRoot "rs0026-inventory.json"
    $markdownPath = Join-Path $GateRoot "rs0026-inventory.md"
    $inventory = [ordered]@{
        EvidenceSessionId = $SessionId
        SourceIdentity = $SourceIdentity
        SourceState = "clean"
        Entries = $entries
        Artifacts = @()
    }
    $inventory | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $inventoryPath -Encoding utf8
    $markdownLines = @(
        "# Public API Analyzer Inventory",
        "",
        "- EvidenceSessionId: ``$SessionId``",
        "- SourceIdentity: ``$SourceIdentity``",
        "- SourceState: ``clean``",
        "",
        "| Project | ExitCode | RS0016 | RS0017 | RS0018 | RS0026 | ErrorLines | Log |",
        "| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |"
    )
    foreach ($entry in $entries) {
        $markdownLines += "| $($entry.Project) | $($entry.ExitCode) | $($entry.RS0016) | $($entry.RS0017) | $($entry.RS0018) | $($entry.RS0026) | $($entry.ErrorLines) | ``$($entry.Log)`` |"
    }
    $markdownLines | Set-Content -LiteralPath $markdownPath -Encoding utf8
    $inventory.Artifacts = @($entries | ForEach-Object { Get-ArtifactRecord (Join-Path $repositoryRoot $_.Log) }) +
        @(Get-ArtifactRecord $inventoryPath; Get-ArtifactRecord $markdownPath)
    $inventory | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $inventoryPath -Encoding utf8
    return Get-RelativePath $inventoryPath
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

function Get-FormalHostReportExpectations {
    return [ordered]@{
        "Bing.Data.Sql.Benchmarks.SqlAggregateRenderingBenchmarks" = 19
        "Bing.Data.Sql.Benchmarks.SqlBuilderAppendToBenchmarks" = 6
        "Bing.Data.Sql.Benchmarks.SqlBuilderCteAndParameterTokenBenchmarks" = 2
        "Bing.Data.Sql.Benchmarks.SqlDebugSqlBenchmarks" = 3
        "Bing.Data.Sql.Benchmarks.SqliteDapperE2EBenchmarks" = 24
        "Bing.Data.Sql.Benchmarks.SqlLambdaJoinBenchmarks" = 28
        "Bing.Data.Sql.Benchmarks.SqlLambdaRootBenchmarks" = 12
        "Bing.Data.Sql.Benchmarks.SqlMetadataBenchmarks" = 27
        "Bing.Data.Sql.Benchmarks.SqlMutationBenchmarks" = 15
    }
}

function Get-FormalHostReportPrefixes {
    return @((Get-FormalHostReportExpectations).Keys)
}

function New-FormalHostManifest {
    param(
        [string]$FormalRoot,
        [string]$ArtifactDirectory,
        [string]$LogPath,
        [string]$SessionId,
        [string]$SourceIdentity,
        [string[]]$FormalFilters
    )

    if ($SourceIdentity -notmatch '(?i);source=clean$') {
        throw "FormalHost manifest requires a clean source identity."
    }

    $reportFiles = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot $ArtifactDirectory) -Recurse -File |
        Where-Object { $_.Name -match "-report\.(csv|html)$" -or $_.Name -like "*-report-github.md" -or $_.Extension -eq ".log" })
    $csv = @($reportFiles | Where-Object { $_.Name.EndsWith("-report.csv", [StringComparison]::OrdinalIgnoreCase) })
    $markdown = @($reportFiles | Where-Object { $_.Name.EndsWith("-report-github.md", [StringComparison]::OrdinalIgnoreCase) })
    $html = @($reportFiles | Where-Object { $_.Name.EndsWith("-report.html", [StringComparison]::OrdinalIgnoreCase) })
    $expectedReportExpectations = Get-FormalHostReportExpectations
    $expectedReportPrefixes = @($expectedReportExpectations.Keys)
    foreach ($prefix in $expectedReportPrefixes) {
        foreach ($extension in @("-report.csv", "-report-github.md", "-report.html")) {
            if (@($reportFiles | Where-Object { $_.Name -eq "$prefix$extension" }).Count -ne 1) {
                throw "FormalHost report is missing or duplicated: $prefix$extension"
            }
        }
    }
    if ($csv.Count -ne $expectedReportPrefixes.Count -or
        $markdown.Count -ne $expectedReportPrefixes.Count -or
        $html.Count -ne $expectedReportPrefixes.Count -or
        -not (Test-Path -LiteralPath $LogPath -PathType Leaf)) {
        throw "FormalHost reports are incomplete."
    }
    $rows = @($csv | ForEach-Object { Import-Csv -LiteralPath $_.FullName -Encoding utf8 })
    if ($rows.Count -eq 0) {
        throw "FormalHost produced no benchmark rows."
    }
    $expectedBenchmarkCount = [int](($expectedReportExpectations.Values | Measure-Object -Sum).Sum)
    if ($rows.Count -ne $expectedBenchmarkCount) {
        throw "FormalHost benchmark count is $($rows.Count); expected $expectedBenchmarkCount."
    }
    $reportCounts = [ordered]@{}
    foreach ($prefix in $expectedReportPrefixes) {
        $reportPath = @($csv | Where-Object { $_.Name -eq "$prefix-report.csv" })
        if ($reportPath.Count -ne 1) {
            throw "FormalHost CSV is missing or duplicated: $prefix-report.csv"
        }
        $count = @((Import-Csv -LiteralPath $reportPath[0].FullName -Encoding utf8)).Count
        $reportCounts[$prefix] = $count
        if ($count -ne [int]$expectedReportExpectations[$prefix]) {
            throw "FormalHost report count for $prefix is $count; expected $($expectedReportExpectations[$prefix])."
        }
    }
    $invalidRows = @($rows | Where-Object {
        try {
            $_.Job -ne "FormalHost" -or [int]$_.LaunchCount -ne 3 -or
                [int]$_.WarmupCount -ne 6 -or [int]$_.IterationCount -ne 15
        }
        catch {
            $true
        }
    })
    if ($invalidRows.Count -gt 0) {
        throw "FormalHost CSV contains $($invalidRows.Count) rows with an unexpected Job or iteration configuration."
    }
    $groupCounts = [ordered]@{
        Aggregate = @($csv | Where-Object { $_.Name -match "SqlAggregateRendering|SqlBuilderAppendTo|SqlBuilderCteAndParameterToken" } |
            ForEach-Object { @(Import-Csv -LiteralPath $_.FullName -Encoding utf8) }).Count
        Debug = @($csv | Where-Object { $_.Name -match "SqlDebugSqlBenchmarks" } |
            ForEach-Object { @(Import-Csv -LiteralPath $_.FullName -Encoding utf8) }).Count
        SQLite = @($csv | Where-Object { $_.Name -match "SqliteDapperE2EBenchmarks" } |
            ForEach-Object { @(Import-Csv -LiteralPath $_.FullName -Encoding utf8) }).Count
        Lambda = @($csv | Where-Object { $_.Name -match "SqlLambdaJoin|SqlLambdaRoot" } |
            ForEach-Object { @(Import-Csv -LiteralPath $_.FullName -Encoding utf8) }).Count
        MetadataMutation = @($csv | Where-Object { $_.Name -match "SqlMetadata|SqlMutation" } |
            ForEach-Object { @(Import-Csv -LiteralPath $_.FullName -Encoding utf8) }).Count
    }
    $artifactPaths = @($reportFiles | ForEach-Object { $_.FullName }) + @($LogPath)
    $artifactPaths = @($artifactPaths | Sort-Object -Unique)
    $reports = @($artifactPaths | ForEach-Object { Get-RelativePath $_ })
    $artifactRecords = @($artifactPaths | ForEach-Object { Get-ArtifactRecord $_ })
    $manifestPath = Join-Path $FormalRoot "formal-host-complete.json"
    ([ordered]@{
        Status = "Complete"
        Job = "FormalHost"
        Scope = "ApprovedBenchmarkSet"
        ExecutionMode = "SingleFormalHost"
        LaunchCount = 3
        WarmupCount = 6
        IterationCount = 15
        BenchmarkCount = $rows.Count
        ExpectedBenchmarkCount = $expectedBenchmarkCount
        ReportCounts = $reportCounts
        GroupCounts = $groupCounts
        Filters = $FormalFilters
        EvidenceSessionId = $SessionId
        SourceIdentity = $SourceIdentity
        SourceState = "clean"
        Reports = $reports
        Artifacts = $artifactRecords
    } | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $manifestPath -Encoding utf8
    return Get-RelativePath $manifestPath
}

function Invoke-UnitInventorySelfTest {
    $selfTestId = [Guid]::NewGuid().ToString("N")
    $unitRelativeRoot = "artifacts/test-results/release-candidate/unit-inventory-selftest-$selfTestId"
    $providerRelativeRoot = "artifacts/provider-test-results/release-candidate/unit-inventory-selftest-$selfTestId"
    $aggregateRelativeRoot = "$unitRelativeRoot/aggregate"
    $unitRoot = Join-Path $repositoryRoot ($unitRelativeRoot.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
    $providerRoot = Join-Path $repositoryRoot ($providerRelativeRoot.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
    $inventoryRelativePath = "$unitRelativeRoot/unit-inventory.json"
    $inventoryPath = Join-Path $repositoryRoot ($inventoryRelativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
    $aggregatePath = Join-Path $repositoryRoot (($aggregateRelativeRoot + "/provider-capability-matrix.json").Replace('/', [System.IO.Path]::DirectorySeparatorChar))
    $aggregateLogPath = Join-Path $unitRoot "aggregate.log"
    $sessionId = "selftest-unit-$selfTestId"
    $sourceIdentity = "git=selftest;source=clean"
    try {
        New-Item -ItemType Directory -Path $unitRoot,$providerRoot -Force | Out-Null
        $startedAtUtc = [DateTimeOffset]::UtcNow
        $entries = @()
        $updateInventoryHash = {
            param([string]$TrxPath)

            $relativeTrxPath = Get-RelativePath $TrxPath
            $inventory = Get-Content -LiteralPath $inventoryPath -Raw -Encoding utf8 | ConvertFrom-Json
            $matchingEntries = @($inventory.Entries | Where-Object { $_.TrxPath -eq $relativeTrxPath })
            if ($matchingEntries.Count -ne 1) {
                throw "Unit inventory self-test could not locate the modified TRX entry."
            }
            $matchingEntries[0].Sha256 = (Get-FileHash -LiteralPath $TrxPath -Algorithm SHA256).Hash.ToLowerInvariant()
            $inventory | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $inventoryPath -Encoding utf8
        }.GetNewClosure()
        foreach ($unitProject in $unitProjects) {
            $projectPath = [string]$unitProject.Path
            $projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
            foreach ($framework in $unitProject.Frameworks) {
                $resultDirectory = Join-Path $unitRoot ("{0}-{1}" -f $projectName, $framework)
                New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
                $trxPath = Join-Path $resultDirectory ("{0}-{1}.trx" -f $projectName, $framework)
                $trx = @"
<?xml version="1.0" encoding="utf-8"?>
<TestRun><TestDefinitions><UnitTest name="Synthetic.$projectName"><TestMethod codeBase="C:\synthetic\bin\Release\$framework\$projectName.dll" className="Synthetic.$projectName" name="Passes" /></UnitTest></TestDefinitions><ResultSummary><Counters total="1" executed="1" passed="1" failed="0" notExecuted="0" /></ResultSummary><Results><UnitTestResult testName="Synthetic.$projectName" outcome="Passed" /></Results></TestRun>
"@
                [System.IO.File]::WriteAllText($trxPath, $trx, [System.Text.UTF8Encoding]::new($false))
                $entries += [ordered]@{
                    Project = $projectName
                    Framework = $framework
                    TrxPath = Get-RelativePath $trxPath
                    Sha256 = (Get-FileHash -LiteralPath $trxPath -Algorithm SHA256).Hash.ToLowerInvariant()
                    LastWriteTimeUtc = [DateTimeOffset]::new((Get-Item -LiteralPath $trxPath).LastWriteTimeUtc,
                        [TimeSpan]::Zero).ToString("O")
                }
            }
        }
        $completedAtUtc = [DateTimeOffset]::UtcNow
        ([ordered]@{
            EvidenceSessionId = $sessionId
            SourceIdentity = $sourceIdentity
            SourceState = "clean"
            StartedAtUtc = $startedAtUtc.ToString("O")
            CompletedAtUtc = $completedAtUtc.ToString("O")
            Entries = $entries
        } | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $inventoryPath -Encoding utf8

        $aggregateArguments = @(
            "run", "--project", $evidenceCli, "-c", $Configuration, "--no-restore", "--",
            "--workspace-root", $repositoryRoot,
            "--aggregate-output-directory", $aggregateRelativeRoot,
            "--provider-results-directory", $providerRelativeRoot,
            "--unit-results-directory", $unitRelativeRoot,
            "--unit-inventory", $inventoryRelativePath,
            "--evidence-session-id", $sessionId,
            "--source-identity", $sourceIdentity
        )
        $runUnitAggregate = {
            $result = Invoke-Captured -FilePath "dotnet" -Arguments $aggregateArguments -LogPath $aggregateLogPath
            if ($result.ExitCode -ne 0) {
                throw "Unit inventory self-test aggregate failed."
            }
            return Get-Content -LiteralPath $aggregatePath -Raw -Encoding utf8 | ConvertFrom-Json
        }.GetNewClosure()
        $matrix = & $runUnitAggregate
        if (-not [bool]$matrix.ReadinessChecks.UnitEvidenceComplete -or
            -not [bool]$matrix.ReadinessChecks.UnitTestsPassed -or
            [bool]$matrix.ReleaseReady) {
            throw "Unit inventory self-test did not accept the complete synthetic inventory without releasing RC readiness."
        }

        $trxFiles = @(Get-ChildItem -LiteralPath $unitRoot -Recurse -Filter "*.trx" -File | Sort-Object FullName)
        $originalTrxContents = @{}
        foreach ($trxFile in $trxFiles) {
            $originalTrxContents[$trxFile.FullName] = [System.IO.File]::ReadAllText(
                $trxFile.FullName, [System.Text.Encoding]::UTF8)
        }
        $originalInventory = [System.IO.File]::ReadAllText($inventoryPath, [System.Text.Encoding]::UTF8)
        $resetUnitFixture = {
            foreach ($fixturePath in $originalTrxContents.Keys) {
                [System.IO.File]::WriteAllText($fixturePath, $originalTrxContents[$fixturePath],
                    [System.Text.UTF8Encoding]::new($false))
            }
            [System.IO.File]::WriteAllText($inventoryPath, $originalInventory,
                [System.Text.UTF8Encoding]::new($false))
        }.GetNewClosure()

        $wrongAssemblyTrx = $trxFiles[0]
        & $resetUnitFixture
        $wrongAssemblyContent = [System.IO.File]::ReadAllText($wrongAssemblyTrx.FullName, [System.Text.Encoding]::UTF8)
        $wrongAssemblyProject = [System.Text.RegularExpressions.Regex]::Replace($wrongAssemblyTrx.BaseName,
            "-net[0-9]+\.[0-9]+$", "", [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        $wrongAssemblyContent = $wrongAssemblyContent.Replace("$wrongAssemblyProject.dll", "Wrong.Tests.dll", [StringComparison]::Ordinal)
        [System.IO.File]::WriteAllText($wrongAssemblyTrx.FullName, $wrongAssemblyContent,
            [System.Text.UTF8Encoding]::new($false))
        & $updateInventoryHash $wrongAssemblyTrx.FullName
        $matrix = & $runUnitAggregate
        if ([bool]$matrix.ReadinessChecks.UnitEvidenceComplete) {
            throw "Unit inventory self-test accepted a TRX with the wrong assembly identity."
        }

        $wrongTfmTrx = @($trxFiles | Where-Object { $_.BaseName -match "-net6\.0$" })[0]
        & $resetUnitFixture
        $wrongTfmContent = [System.IO.File]::ReadAllText($wrongTfmTrx.FullName, [System.Text.Encoding]::UTF8)
        $wrongTfmContent = $wrongTfmContent.Replace("\net6.0\", "\net9.0\", [StringComparison]::Ordinal)
        [System.IO.File]::WriteAllText($wrongTfmTrx.FullName, $wrongTfmContent,
            [System.Text.UTF8Encoding]::new($false))
        & $updateInventoryHash $wrongTfmTrx.FullName
        $matrix = & $runUnitAggregate
        if ([bool]$matrix.ReadinessChecks.UnitEvidenceComplete) {
            throw "Unit inventory self-test accepted a TRX with the wrong target framework."
        }

        $mixedDefinitionTrx = $trxFiles[1]
        & $resetUnitFixture
        $mixedDefinitionContent = [System.IO.File]::ReadAllText($mixedDefinitionTrx.FullName, [System.Text.Encoding]::UTF8)
        $mixedDefinitionContent = $mixedDefinitionContent.Replace(
            "</TestDefinitions>",
            '<UnitTest name="Synthetic.Mixed"><TestMethod codeBase="C:\synthetic\bin\Release\net8.0\Other.Tests.dll" className="Synthetic.Mixed" name="WrongAssembly" /></UnitTest></TestDefinitions>',
            [StringComparison]::Ordinal)
        [System.IO.File]::WriteAllText($mixedDefinitionTrx.FullName, $mixedDefinitionContent,
            [System.Text.UTF8Encoding]::new($false))
        & $updateInventoryHash $mixedDefinitionTrx.FullName
        $matrix = & $runUnitAggregate
        if ([bool]$matrix.ReadinessChecks.UnitEvidenceComplete) {
            throw "Unit inventory self-test accepted mixed test definitions."
        }

        $missingDefinitionTrx = $trxFiles[2]
        & $resetUnitFixture
        $missingDefinitionContent = [System.IO.File]::ReadAllText($missingDefinitionTrx.FullName, [System.Text.Encoding]::UTF8)
        $missingDefinitionContent = [System.Text.RegularExpressions.Regex]::Replace(
            $missingDefinitionContent, "<TestDefinitions>.*?</TestDefinitions>", "", "Singleline")
        [System.IO.File]::WriteAllText($missingDefinitionTrx.FullName, $missingDefinitionContent,
            [System.Text.UTF8Encoding]::new($false))
        & $updateInventoryHash $missingDefinitionTrx.FullName
        $matrix = & $runUnitAggregate
        if ([bool]$matrix.ReadinessChecks.UnitEvidenceComplete) {
            throw "Unit inventory self-test accepted a TRX without test definitions."
        }

        $counterMismatchTrx = $trxFiles[3]
        & $resetUnitFixture
        $counterMismatchContent = [System.IO.File]::ReadAllText($counterMismatchTrx.FullName, [System.Text.Encoding]::UTF8)
        $counterMismatchContent = $counterMismatchContent.Replace('executed="1"', 'executed="2"', [StringComparison]::Ordinal)
        [System.IO.File]::WriteAllText($counterMismatchTrx.FullName, $counterMismatchContent,
            [System.Text.UTF8Encoding]::new($false))
        & $updateInventoryHash $counterMismatchTrx.FullName
        $matrix = & $runUnitAggregate
        if ([bool]$matrix.ReadinessChecks.UnitEvidenceComplete) {
            throw "Unit inventory self-test accepted inconsistent TRX counters."
        }

        $tamperedTrx = $trxFiles[4]
        & $resetUnitFixture
        $tamperedContent = [System.IO.File]::ReadAllText($tamperedTrx.FullName, [System.Text.Encoding]::UTF8)
        $tamperedContent = $tamperedContent.Replace("Synthetic.", "Tampered.", [StringComparison]::Ordinal)
        [System.IO.File]::WriteAllText($tamperedTrx.FullName, $tamperedContent,
            [System.Text.UTF8Encoding]::new($false))
        $matrix = & $runUnitAggregate
        if ([bool]$matrix.ReadinessChecks.UnitEvidenceComplete) {
            throw "Unit inventory self-test accepted a tampered TRX hash."
        }
        Write-Host "Unit inventory self-test passed."
    }
    finally {
        Remove-Item -LiteralPath $unitRoot,$providerRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-FormalHostSelfTest {
    $selfTestId = [Guid]::NewGuid().ToString("N")
    $formalRoot = Join-Path $repositoryRoot "artifacts/release-candidate/formal-host-selftest-$selfTestId"
    $artifactDirectory = "artifacts/benchmarks/release-candidate/formal-host-selftest-$selfTestId/BenchmarkDotNet"
    $artifactRoot = Join-Path $repositoryRoot $artifactDirectory
    $artifactDirectoryRoot = Split-Path -Parent $artifactRoot
    $logPath = Join-Path $formalRoot "formal-host.log"
    $filters = @("*SqlAggregateRenderingBenchmarks*", "*SqlBuilderAppendToBenchmarks*",
        "*SqlBuilderCteAndParameterTokenBenchmarks*", "*SqlDebugSqlBenchmarks*",
        "*SqliteDapperE2EBenchmarks*", "*SqlLambdaJoinBenchmarks*", "*SqlLambdaRootBenchmarks*",
        "*SqlMetadataBenchmarks*", "*SqlMutationBenchmarks*")
    try {
        New-Item -ItemType Directory -Path $artifactRoot,$formalRoot -Force | Out-Null
        $expectations = Get-FormalHostReportExpectations
        foreach ($prefix in $expectations.Keys) {
            $csvLines = @("Method,Job,LaunchCount,WarmupCount,IterationCount")
            for ($index = 1; $index -le [int]$expectations[$prefix]; $index++) {
                $csvLines += "Synthetic$index,FormalHost,3,6,15"
            }
            $csvLines | Set-Content -LiteralPath (Join-Path $artifactRoot "$prefix-report.csv") -Encoding utf8
            "# $prefix" | Set-Content -LiteralPath (Join-Path $artifactRoot "$prefix-report-github.md") -Encoding utf8
            "<html>$prefix</html>" | Set-Content -LiteralPath (Join-Path $artifactRoot "$prefix-report.html") -Encoding utf8
        }
        "formal host self-test" | Set-Content -LiteralPath $logPath -Encoding utf8
        $manifest = New-FormalHostManifest -FormalRoot $formalRoot -ArtifactDirectory $artifactDirectory `
            -LogPath $logPath -SessionId "selftest-formal-host" -SourceIdentity "git=selftest;source=clean" -FormalFilters $filters
        if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $manifest) -PathType Leaf)) {
            throw "FormalHost self-test did not create a manifest."
        }
        $shortCsvPrefix = "Bing.Data.Sql.Benchmarks.SqlAggregateRenderingBenchmarks"
        $shortCsv = Join-Path $artifactRoot "$shortCsvPrefix-report.csv"
        $shortCsvLines = @("Method,Job,LaunchCount,WarmupCount,IterationCount")
        for ($index = 1; $index -lt [int]$expectations[$shortCsvPrefix]; $index++) {
            $shortCsvLines += "Synthetic$index,FormalHost,3,6,15"
        }
        $shortCsvLines | Set-Content -LiteralPath $shortCsv -Encoding utf8
        $shortCountRejected = $false
        try {
            New-FormalHostManifest -FormalRoot $formalRoot -ArtifactDirectory $artifactDirectory `
                -LogPath $logPath -SessionId "selftest-formal-host" -SourceIdentity "git=selftest;source=clean" -FormalFilters $filters | Out-Null
        }
        catch {
            $shortCountRejected = $_.Exception.Message -match "benchmark count|report count"
        }
        if (-not $shortCountRejected) {
            throw "FormalHost self-test accepted an incomplete approved report count."
        }

        $validCsvLines = @("Method,Job,LaunchCount,WarmupCount,IterationCount")
        for ($index = 1; $index -le [int]$expectations[$shortCsvPrefix]; $index++) {
            $validCsvLines += "Synthetic$index,FormalHost,3,6,15"
        }
        $validCsvLines | Set-Content -LiteralPath $shortCsv -Encoding utf8

        $badCsv = Join-Path $artifactRoot "Bing.Data.Sql.Benchmarks.SqlDebugSqlBenchmarks-report.csv"
        "Method,Job,LaunchCount,WarmupCount,IterationCount`nSynthetic,Dry,3,6,15" |
            Set-Content -LiteralPath $badCsv -Encoding utf8
        $rejected = $false
        try {
            New-FormalHostManifest -FormalRoot $formalRoot -ArtifactDirectory $artifactDirectory `
                -LogPath $logPath -SessionId "selftest-formal-host" -SourceIdentity "git=selftest;source=clean" -FormalFilters $filters | Out-Null
        }
        catch {
            $rejected = $true
        }
        if (-not $rejected) {
            throw "FormalHost self-test accepted a non-FormalHost CSV."
        }
        Write-Host "FormalHost manifest self-test passed."
    }
    finally {
        Remove-Item -LiteralPath $formalRoot -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $artifactDirectoryRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-AppVeyorLaneSelfTest {
    $appVeyorPath = Join-Path $repositoryRoot "appveyor.yml"
    if (-not (Test-Path -LiteralPath $appVeyorPath -PathType Leaf)) {
        throw "AppVeyor 配置不存在。"
    }

    $content = Get-Content -LiteralPath $appVeyorPath -Encoding utf8 -Raw
    foreach ($lane in @("common", "mysql", "postgresql", "sqlserver", "release")) {
        if ($content -notmatch ("(?m)^\s*-\s*PROVIDER_TEST_LANE:\s*" + [regex]::Escape($lane) + "\s*$")) {
            throw "AppVeyor lane self-test 缺少 '$lane' matrix entry。"
        }
    }
    if ($content -match "(?m)^\s*-\s*PROVIDER_TEST_LANE:\s*aggregate\s*$" -or
        $content -match "(?m)^\s*'aggregate'\s*\{") {
        throw "AppVeyor lane self-test 发现不受支持的独立 aggregate lane。"
    }
    if ($content -notmatch "Invoke-ReleaseCandidateValidation\.ps1") {
        throw "AppVeyor lane self-test 未发现 release lane 的 RC orchestrator。"
    }
    Write-Host "AppVeyor lane self-test passed."
}

function Invoke-ResetAuthorizationSelfTest {
    $oldResetAuthorization = [Environment]::GetEnvironmentVariable("ALLOW_DATABASE_RESET_FOR_TESTS")
    $runnerCalls = 0
    $fakeProviderRunner = {
        param([string]$Provider, [string]$Framework)
        $runnerCalls++
    }.GetNewClosure()
    try {
        foreach ($invalidValue in @($null, "false", "1")) {
            [Environment]::SetEnvironmentVariable("ALLOW_DATABASE_RESET_FOR_TESTS", $invalidValue)
            $rejected = $false
            $runnerCalls = 0
            try {
                Invoke-ProviderRuns -SessionId "selftest-reset-authorization" `
                    -RunnerOverride $fakeProviderRunner
            }
            catch {
                $rejected = $_.Exception.Message -match "ALLOW_DATABASE_RESET_FOR_TESTS=true"
            }
            if (-not $rejected -or $runnerCalls -ne 0) {
                throw "Reset authorization self-test accepted '$invalidValue' or invoked a Provider runner."
            }
        }

        [Environment]::SetEnvironmentVariable("ALLOW_DATABASE_RESET_FOR_TESTS", "true")
        if ((Get-ValidatedResetAuthorization "true") -ne "true") {
            throw "Reset authorization self-test rejected an explicit true authorization."
        }
    }
    finally {
        [Environment]::SetEnvironmentVariable("ALLOW_DATABASE_RESET_FOR_TESTS", $oldResetAuthorization)
    }
    Write-Host "Reset authorization self-test passed."
}

function Invoke-SourceIdentitySelfTest {
    $expected = "git=selftest;source=clean"

    $unitDriftState = @{ Calls = 0 }
    $unitDriftReader = {
        $unitDriftState.Calls++
        if ($unitDriftState.Calls -le 2) {
            return $expected
        }
        return "git=selftest-drift;source=clean"
    }.GetNewClosure()
    $unitDriftRejected = $false
    try {
        Assert-SourceIdentity -Expected $expected -Stage "at release start" `
            -SourceIdentityReader $unitDriftReader | Out-Null
        Assert-SourceIdentity -Expected $expected -Stage "after Unit tests" `
            -SourceIdentityReader $unitDriftReader | Out-Null
        Assert-SourceIdentity -Expected $expected -Stage "before aggregate" `
            -SourceIdentityReader $unitDriftReader | Out-Null
    }
    catch {
        $unitDriftRejected = $_.Exception.Message -match "source identity changed before aggregate"
    }
    if (-not $unitDriftRejected) {
        throw "Source identity self-test accepted source drift after Unit tests."
    }

    $readyDriftState = @{ Calls = 0 }
    $readyDriftReader = {
        $readyDriftState.Calls++
        if ($readyDriftState.Calls -le 3) {
            return $expected
        }
        return "git=selftest-drift-before-ready;source=clean"
    }.GetNewClosure()
    $readyDriftRejected = $false
    try {
        Assert-SourceIdentity -Expected $expected -Stage "at release start" `
            -SourceIdentityReader $readyDriftReader | Out-Null
        Assert-SourceIdentity -Expected $expected -Stage "after Unit tests" `
            -SourceIdentityReader $readyDriftReader | Out-Null
        Assert-SourceIdentity -Expected $expected -Stage "before aggregate" `
            -SourceIdentityReader $readyDriftReader | Out-Null
        Assert-SourceIdentity -Expected $expected -Stage "before final READY" `
            -SourceIdentityReader $readyDriftReader | Out-Null
    }
    catch {
        $readyDriftRejected = $_.Exception.Message -match "source identity changed before final READY"
    }
    if (-not $readyDriftRejected) {
        throw "Source identity self-test accepted source drift before final READY."
    }

    $publishSelfTestId = [Guid]::NewGuid().ToString("N")
    $publishSelfTestRoot = Join-Path $repositoryRoot "artifacts/test-results/.rc-publish-selftest-$publishSelfTestId"
    $stableStagingDirectory = Join-Path $publishSelfTestRoot "stable-staging/provider-aggregate"
    $stableFinalDirectory = Join-Path $publishSelfTestRoot "stable-final/provider-aggregate"
    $driftStagingDirectory = Join-Path $publishSelfTestRoot "drift-staging/provider-aggregate"
    $driftFinalDirectory = Join-Path $publishSelfTestRoot "drift-final/provider-aggregate"
    try {
        New-Item -ItemType Directory -Path $stableStagingDirectory,$driftStagingDirectory -Force | Out-Null
        [System.IO.File]::WriteAllText(
            (Join-Path $stableStagingDirectory "provider-capability-matrix.json"),
            '{"ReleaseReady":true}', [System.Text.UTF8Encoding]::new($false))
        $stableIdentity = Publish-AggregateEvidence -StagingDirectory $stableStagingDirectory `
            -FinalDirectory $stableFinalDirectory -ExpectedSourceIdentity $expected `
            -SourceIdentityReader { return $expected }
        if (-not [string]::Equals($stableIdentity, $expected, [StringComparison]::Ordinal) -or
            -not (Test-Path -LiteralPath (Join-Path $stableFinalDirectory "provider-capability-matrix.json") -PathType Leaf)) {
            throw "Source identity self-test did not publish a stable aggregate."
        }

        [System.IO.File]::WriteAllText(
            (Join-Path $driftStagingDirectory "provider-capability-matrix.json"),
            '{"ReleaseReady":true}', [System.Text.UTF8Encoding]::new($false))
        $publishDriftState = @{ Calls = 0 }
        $publishDriftReader = {
            $publishDriftState.Calls++
            if ($publishDriftState.Calls -eq 1) {
                return $expected
            }
            return "git=selftest-drift-after-publish;source=clean"
        }.GetNewClosure()
        $publishDriftRejected = $false
        try {
            Publish-AggregateEvidence -StagingDirectory $driftStagingDirectory `
                -FinalDirectory $driftFinalDirectory -ExpectedSourceIdentity $expected `
                -SourceIdentityReader $publishDriftReader | Out-Null
        }
        catch {
            $publishDriftRejected = $_.Exception.Message -match "source identity changed after aggregate publish"
        }
        if (-not $publishDriftRejected -or (Test-Path -LiteralPath $driftFinalDirectory)) {
            throw "Source identity self-test accepted post-publish drift or retained a positive aggregate."
        }
    }
    finally {
        Remove-Item -LiteralPath $publishSelfTestRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    Write-Host "Source identity self-test passed."
}

function Invoke-FormalHost {
    param([string]$FormalRoot, [string]$SessionId, [string]$SourceIdentity)

    $artifactDirectory = "$formalResultsRelativeRoot/BenchmarkDotNet"
    $logPath = Join-Path $FormalRoot "formal-host.log"
    $formalFilters = @(
        "*SqlAggregateRenderingBenchmarks*",
        "*SqlBuilderAppendToBenchmarks*",
        "*SqlBuilderCteAndParameterTokenBenchmarks*",
        "*SqlDebugSqlBenchmarks*",
        "*SqliteDapperE2EBenchmarks*",
        "*SqlLambdaJoinBenchmarks*",
        "*SqlLambdaRootBenchmarks*",
        "*SqlMetadataBenchmarks*",
        "*SqlMutationBenchmarks*"
    )
    $formalArguments = @(
        "run", "--project", $benchmarkProject, "-c", $Configuration, "--no-build", "--",
        "--artifacts", $artifactDirectory, "--filter"
    ) + $formalFilters
    $result = Invoke-Captured -FilePath "dotnet" -Arguments $formalArguments -LogPath $logPath
    if ($result.ExitCode -ne 0) {
        throw "FormalHost benchmark failed."
    }
    return New-FormalHostManifest -FormalRoot $FormalRoot -ArtifactDirectory $artifactDirectory `
        -LogPath $logPath -SessionId $SessionId -SourceIdentity $SourceIdentity -FormalFilters $formalFilters
}

if ($SelfTest) {
    Invoke-AppVeyorLaneSelfTest
    Invoke-ResetAuthorizationSelfTest
    Invoke-SourceIdentitySelfTest
    Invoke-UnitInventorySelfTest
    Invoke-FormalHostSelfTest
    exit 0
}

if (-not [string]::Equals([Environment]::GetEnvironmentVariable("CI"), "true",
        [StringComparison]::OrdinalIgnoreCase) -or
    [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable("APPVEYOR_BUILD_ID"))) {
    throw "Release Candidate validation must run in protected AppVeyor CI."
}

$sessionId = Get-EvidenceSessionId
$sourceIdentity = Get-SourceIdentity
$aggregateRunToken = [Guid]::NewGuid().ToString("N")
$aggregateStagingRelativeRoot = "artifacts/test-results/.rc-staging-$aggregateRunToken"
$aggregateStagingRoot = Join-Path $repositoryRoot ($aggregateStagingRelativeRoot.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
$aggregateStagingDirectory = Join-Path $aggregateStagingRoot "provider-aggregate"
$aggregateFinalDirectory = Join-Path $repositoryRoot "$aggregateResultsRelativeRoot/provider-aggregate"
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
    Invoke-UnitRuns $unitRoot $sessionId $sourceIdentity
    Invoke-ProviderRuns $sessionId
    Invoke-SqliteRuns $sessionId
    $rs0026Path = New-Rs0026Evidence $rs0026Root $sessionId $sourceIdentity
    $apiGatePath = New-ApiGateEvidence $apiRoot $sessionId $sourceIdentity
    $formalHostDirectory = Invoke-FormalHost $formalRoot $sessionId $sourceIdentity
    $aggregateSourceIdentity = Assert-SourceIdentity -Expected $sourceIdentity -Stage "before aggregate"

    & dotnet run --project $evidenceCli -c $Configuration --no-restore -- `
        --workspace-root $repositoryRoot `
        --aggregate-output-directory "$aggregateStagingRelativeRoot/provider-aggregate" `
        --provider-results-directory "artifacts/provider-test-results" `
        --unit-results-directory $unitResultsRelativeRoot `
        --unit-inventory "$unitResultsRelativeRoot/unit-inventory.json" `
        --formal-host-results-directory $formalHostDirectory `
        --rs0026-inventory $rs0026Path `
        --api-gate-file $apiGatePath `
        --evidence-session-id $sessionId `
        --source-identity $aggregateSourceIdentity
    if ($LASTEXITCODE -ne 0) {
        throw "Release evidence aggregate failed."
    }
    $aggregatePath = Join-Path $aggregateStagingDirectory "provider-capability-matrix.json"
    if (-not (Test-Path -LiteralPath $aggregatePath -PathType Leaf)) {
        throw "Release evidence aggregate matrix was not produced."
    }
    $aggregate = Get-Content -LiteralPath $aggregatePath -Raw -Encoding utf8 | ConvertFrom-Json
    if (-not [bool]$aggregate.ReleaseReady) {
        throw "Release Candidate is not ready; aggregate gate remained fail-closed."
    }
    $finalSourceIdentity = Publish-AggregateEvidence -StagingDirectory $aggregateStagingDirectory `
        -FinalDirectory $aggregateFinalDirectory -ExpectedSourceIdentity $sourceIdentity
    if (-not [string]::Equals($finalSourceIdentity, $aggregateSourceIdentity, [StringComparison]::Ordinal)) {
        throw "Release Evidence source identity changed after aggregate validation."
    }
    $publishedAggregatePath = Join-Path $aggregateFinalDirectory "provider-capability-matrix.json"
    if (-not (Test-Path -LiteralPath $publishedAggregatePath -PathType Leaf)) {
        throw "Release evidence aggregate matrix was not published."
    }
    Write-Host "Release Candidate validation passed: EvidenceSessionId=$sessionId; SourceIdentity=$finalSourceIdentity"
}
finally {
    if ($aggregateStagingRoot -and (Test-Path -LiteralPath $aggregateStagingRoot)) {
        Remove-Item -LiteralPath $aggregateStagingRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    [Environment]::SetEnvironmentVariable("BING_PROVIDER_EVIDENCE_SESSION_ID", $oldSession)
    [Environment]::SetEnvironmentVariable("BING_PROVIDER_SOURCE_IDENTITY", $oldSource)
}
