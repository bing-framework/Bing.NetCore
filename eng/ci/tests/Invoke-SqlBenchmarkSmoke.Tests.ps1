[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$ResultsDirectory = "artifacts/benchmarks/sql-smoke",
    [switch]$NoRestore
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$project = Join-Path $repositoryRoot "framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj"
$parent = if ([System.IO.Path]::IsPathRooted($ResultsDirectory)) { $ResultsDirectory } else { Join-Path $repositoryRoot $ResultsDirectory }
$directory = Join-Path $parent ("run-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$result = [ordered]@{
    evidenceKind = "BuildAndDiscovery"
    performanceVerified = $false
    configuration = $Configuration
    startedAt = [DateTime]::UtcNow.ToString("o")
    status = "FAIL"
    steps = @()
}

Push-Location $repositoryRoot
try {
    # 使用项目默认导入配置，不通过全局属性改变依赖工程的编译合同。
    $buildArguments = @("build", $project, "-c", $Configuration,
        "-p:UseSharedCompilation=false", "-m:1", "-nr:false", "--verbosity", "minimal")
    if ($NoRestore) { $buildArguments += "--no-restore" }
    $buildLog = Join-Path $directory "build.log"
    & dotnet @buildArguments 2>&1 | Out-File -LiteralPath $buildLog -Encoding utf8
    $buildCode = $LASTEXITCODE
    $result.steps += [ordered]@{ name = "BuildWithProjectDefaults"; command = @("dotnet") + $buildArguments; exitCode = $buildCode; log = $buildLog }
    if ($buildCode -ne 0) { throw "Benchmark build failed: $buildLog" }

    $targetPathArguments = @("msbuild", $project, "-nologo", "-getProperty:TargetPath", "-p:Configuration=$Configuration")
    $targetPathLog = Join-Path $directory "target-path.log"
    $binary = & dotnet @targetPathArguments
    $targetPathCode = $LASTEXITCODE
    $binary | Out-File -LiteralPath $targetPathLog -Encoding utf8
    $result.steps += [ordered]@{ name = "ResolveBenchmarkTargetPath"; command = @("dotnet") + $targetPathArguments; exitCode = $targetPathCode; log = $targetPathLog }
    if ($targetPathCode -ne 0 -or $binary -isnot [string] -or !(Test-Path -LiteralPath $binary -PathType Leaf)) {
        throw "Benchmark build did not produce its MSBuild TargetPath: $targetPathLog"
    }

    # 只枚举实际编译产物，不启动测量或数据库操作。
    $discoveryArguments = @($binary, "--list", "flat", "--filter", "*SqlResourceConstructionBenchmarks*", "*SqlGeneratedQueryTemplateBenchmarks*")
    $discoveryLog = Join-Path $directory "discovery.log"
    & dotnet @discoveryArguments 2>&1 | Out-File -LiteralPath $discoveryLog -Encoding utf8
    $discoveryCode = $LASTEXITCODE
    $result.steps += [ordered]@{ name = "DiscoverResourceAndTemplateBenchmarks"; command = @("dotnet") + $discoveryArguments; exitCode = $discoveryCode; log = $discoveryLog }
    if ($discoveryCode -ne 0) { throw "Benchmark discovery failed: $discoveryLog" }
    $expected = @(
        "Bing.Data.Sql.Benchmarks.SqlResourceConstructionBenchmarks.Query",
        "Bing.Data.Sql.Benchmarks.SqlResourceConstructionBenchmarks.Writer",
        "Bing.Data.Sql.Benchmarks.SqlResourceConstructionBenchmarks.Multiple",
        "Bing.Data.Sql.Benchmarks.SqlGeneratedQueryTemplateBenchmarks.StaticBridgeToSql",
        "Bing.Data.Sql.Benchmarks.SqlGeneratedQueryTemplateBenchmarks.TemplateApplyToSql",
        "Bing.Data.Sql.Benchmarks.SqlGeneratedQueryTemplateBenchmarks.StaticBridgeBoundSnapshot",
        "Bing.Data.Sql.Benchmarks.SqlGeneratedQueryTemplateBenchmarks.TemplateApplyBoundSnapshot",
        "Bing.Data.Sql.Benchmarks.SqlGeneratedQueryTemplateBenchmarks.TemplateAggregateBoundSnapshot",
        "Bing.Data.Sql.Benchmarks.SqlGeneratedQueryTemplateBenchmarks.TemplateSameQueryBoundSnapshot"
    )
    $actual = @(Get-Content -LiteralPath $discoveryLog -Encoding utf8 | Where-Object { $_ -match '^Bing\.Data\.Sql\.Benchmarks\.' })
    if (@(Compare-Object -ReferenceObject $expected -DifferenceObject $actual).Count -ne 0) {
        throw "Benchmark discovery differs from the nine expected methods: $discoveryLog"
    }
    $result.benchmarks = $actual
    $result.binary = [ordered]@{ file = $binary; sha256 = (Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash }

    $capacityLoadTests = Join-Path $repositoryRoot "eng/perf/tests/SqlCapacityLoad.Tests/SqlCapacityLoad.Tests.csproj"
    $loadArguments = @("run", "--project", $capacityLoadTests, "-c", $Configuration, "-p:UseSharedCompilation=false")
    if ($NoRestore) { $loadArguments += "--no-restore" }
    $loadLog = Join-Path $directory "capacity-load-tests.log"
    & dotnet @loadArguments 2>&1 | Out-File -LiteralPath $loadLog -Encoding utf8
    $loadCode = $LASTEXITCODE
    $result.steps += [ordered]@{ name = "VerifyCapacityLoad"; command = @("dotnet") + $loadArguments; exitCode = $loadCode; log = $loadLog }
    if ($loadCode -ne 0) { throw "Capacity load tests failed: $loadLog" }

    $mixedLoadTests = Join-Path $repositoryRoot 'eng/perf/tests/SqlMixedCapacity.Tests/SqlMixedCapacity.Tests.csproj'
    # 固定独立工程的恢复位置，便于 -NoRestore 调用方事先准备相同的 assets。
    $mixedBuild = Join-Path $repositoryRoot 'artifacts/validation/sql-smoke/mixed-build'
    $mixedArguments = @('run', '--project', $mixedLoadTests, '-c', $Configuration,
        '--artifacts-path', $mixedBuild, '-p:UseSharedCompilation=false')
    if ($NoRestore) { $mixedArguments += '--no-restore' }
    $mixedLog = Join-Path $directory 'mixed-capacity-load-tests.log'
    & dotnet @mixedArguments -- (Join-Path (Split-Path -Parent $binary) 'Npgsql.dll') 2>&1 | Out-File -LiteralPath $mixedLog -Encoding utf8
    $mixedCode = $LASTEXITCODE
    $result.steps += [ordered]@{ name = 'VerifyMixedCapacityLoad'; command = @('dotnet') + $mixedArguments; exitCode = $mixedCode; log = $mixedLog }
    if ($mixedCode -ne 0) { throw "Mixed capacity load tests failed: $mixedLog" }

    $capacityTests = Join-Path $repositoryRoot "eng/perf/tests/SqlCapacityProbe.Tests.ps1"
    $capacityOutput = Join-Path $directory "capacity-contracts"
    & $capacityTests -BenchmarkAssembly $binary -ResultsDirectory $capacityOutput | Out-Null
    $capacityCode = $LASTEXITCODE
    $result.steps += [ordered]@{ name = "VerifyCapacityDatabaseGate"; exitCode = $capacityCode; results = $capacityOutput }
    if ($capacityCode -ne 0) { throw "Capacity gate tests failed: $capacityOutput" }
    $gateTests = Join-Path $repositoryRoot "eng/perf/tests/SqlCapacityGate.Tests.ps1"
    $gateOutput = Join-Path $directory "capacity-gate"
    & $gateTests -ResultsDirectory $gateOutput | Out-Null
    $gateCode = $LASTEXITCODE
    $result.steps += [ordered]@{ name = "VerifyCapacityThresholdGate"; exitCode = $gateCode; results = $gateOutput }
    if ($gateCode -ne 0) { throw "Capacity threshold tests failed: $gateOutput" }

    $matrixOutput = Join-Path $directory 'capacity-matrix'
    & (Join-Path $repositoryRoot 'eng/perf/tests/SqlCapacityMatrix.Tests.ps1') -ResultsDirectory $matrixOutput | Out-Null
    $result.steps += [ordered]@{ name = 'VerifyCapacityMatrix'; exitCode = $LASTEXITCODE; results = $matrixOutput }
    if ($LASTEXITCODE -ne 0) { throw "Capacity matrix tests failed: $matrixOutput" }

    foreach ($contract in @('SqlCapacityEnvironment', 'SqlCleanBuildBudgetGate', 'SqlMixedCapacityGate', 'SqlLocalCapacity', 'SqlMixedStageDiagnostic', 'SqlContainerResources', 'SqlWalSyncOutput', 'SqlWalSyncProbe')) {
        $contractOutput = Join-Path $directory $contract
        # 在独立进程中读取测试脚本的退出码，避免脚本内预期失败的子命令污染调用方的 LASTEXITCODE。
        & (Get-Command pwsh).Source -NoProfile -File (Join-Path $repositoryRoot "eng/perf/tests/$contract.Tests.ps1") -ResultsDirectory $contractOutput | Out-Null
        $contractCode = $LASTEXITCODE
        $result.steps += [ordered]@{ name = "Verify$contract"; exitCode = $contractCode; results = $contractOutput }
        if ($contractCode -ne 0) { throw "$contract tests failed: $contractOutput" }
    }

    $collector = Join-Path $repositoryRoot 'eng/perf/SqlCpuTrace/SqlCpuTrace.csproj'
    $cpuBuild = Join-Path $repositoryRoot 'artifacts/validation/sql-smoke/cpu-build'
    $cpuArguments = @('run', '--project', $collector, '-c', $Configuration, '--artifacts-path', $cpuBuild,
        "-p:DiagnosticAssemblyDirectory=$(Split-Path -Parent $binary)", '-p:UseSharedCompilation=false')
    if ($NoRestore) { $cpuArguments += '--no-restore' }
    $cpuLog = Join-Path $directory 'cpu-attribution-tests.log'
    & dotnet @cpuArguments -- --self-test 2>&1 | Out-File -LiteralPath $cpuLog -Encoding utf8
    $cpuCode = $LASTEXITCODE
    $result.steps += [ordered]@{ name = 'VerifyCpuAttribution'; exitCode = $cpuCode; log = $cpuLog }
    if ($cpuCode -ne 0) { throw "CPU attribution tests failed: $cpuLog" }

    $fixtureProject = Join-Path $repositoryRoot 'eng/perf/tests/SqlCpuTraceFixture/SqlCpuTraceFixture.csproj'
    $fixtureBuild = Join-Path $repositoryRoot 'artifacts/validation/sql-smoke/cpu-fixture'
    $fixtureArguments = @('build', $fixtureProject, '-c', $Configuration, '--artifacts-path', $fixtureBuild,
        '-p:UseSharedCompilation=false', '-m:1', '-nr:false')
    if ($NoRestore) { $fixtureArguments += '--no-restore' }
    $fixtureLog = Join-Path $directory 'cpu-fixture-build.log'
    & dotnet @fixtureArguments 2>&1 | Out-File -LiteralPath $fixtureLog -Encoding utf8
    $fixtureCode = $LASTEXITCODE
    $result.steps += [ordered]@{ name = 'BuildCpuWindowFixture'; command = @('dotnet') + $fixtureArguments; exitCode = $fixtureCode; log = $fixtureLog }
    if ($fixtureCode -ne 0) { throw "CPU fixture build failed: $fixtureLog" }

    $collectorBinary = Join-Path $cpuBuild "bin/SqlCpuTrace/$($Configuration.ToLowerInvariant())/SqlCpuTrace.dll"
    $fixtureBinary = Join-Path $fixtureBuild "bin/SqlCpuTraceFixture/$($Configuration.ToLowerInvariant())/SqlCpuTraceFixture.dll"
    $windowOutput = Join-Path $directory 'cpu-window'
    $windowLog = Join-Path $directory 'cpu-window-tests.log'
    & (Join-Path $repositoryRoot 'eng/perf/tests/SqlCpuWindow.ExitCode.Tests.ps1') `
        -Collector $collectorBinary -Fixture $fixtureBinary -ResultsDirectory $windowOutput 2>&1 |
        Out-File -LiteralPath $windowLog -Encoding utf8
    $windowCode = $LASTEXITCODE
    $result.steps += [ordered]@{ name = 'VerifyCpuWindowIntegration'; exitCode = $windowCode; log = $windowLog; results = $windowOutput }
    if ($windowCode -ne 0) { throw "CPU window integration failed: $windowLog" }

    # 快照职责测试使用临时源码树，Windows Job 测试启动真实子进程。
    $snapshotOutput = Join-Path $directory 'clean-build'
    & (Join-Path $repositoryRoot 'eng/perf/tests/SqlCleanBuildPeak.Tests.ps1') -ResultsDirectory $snapshotOutput | Out-Null
    $result.steps += [ordered]@{ name = 'VerifyCleanBuildSnapshot'; exitCode = $LASTEXITCODE; results = $snapshotOutput }
    if ($LASTEXITCODE -ne 0) { throw "Clean build snapshot tests failed: $snapshotOutput" }
    if ($IsWindows) {
        $treeOutput = Join-Path $directory 'process-tree'
        $treeLog = Join-Path $directory 'build-process-tree.log'
        & (Join-Path $repositoryRoot 'eng/perf/tests/BuildProcessTree.Tests.ps1') -ResultsDirectory $treeOutput 2>&1 | Out-File -LiteralPath $treeLog -Encoding utf8
        $result.steps += [ordered]@{ name = 'VerifyBuildProcessTree'; exitCode = $LASTEXITCODE; log = $treeLog; results = $treeOutput }
        if ($LASTEXITCODE -ne 0) { throw "Build process tree tests failed: $treeLog" }
    }

    $result.status = "PASS"
}
catch {
    $result.error = $_.Exception.Message
}
finally {
    Pop-Location
    $result.finishedAt = [DateTime]::UtcNow.ToString("o")
    $resultFile = Join-Path $directory "result.json"
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resultFile -Encoding utf8
    Write-Output "SQL_BENCHMARK_SMOKE_$($result.status): $resultFile"
}
if ($result.status -ne "PASS") { exit 1 }
