[CmdletBinding()]
param([string]$ResultsDirectory = 'artifacts/validation/sql-build-tests')

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$resultRoot = [System.IO.Path]::GetFullPath($ResultsDirectory, $repositoryRoot)
New-Item -ItemType Directory -Path $resultRoot -Force | Out-Null
$testRoot = Join-Path $resultRoot ('snapshot-' + [Guid]::NewGuid().ToString('N'))
$fixture = Join-Path $testRoot 'original'
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
$utf8 = [System.Text.UTF8Encoding]::new($false)

function Write-FixtureFile([string]$Relative, [string]$Content) {
    $file = Join-Path $fixture $Relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $file) -Force | Out-Null
    [System.IO.File]::WriteAllText($file, $Content, $utf8)
}

Write-FixtureFile 'common.props' '<Project />'
Write-FixtureFile 'global.json' '{"sdk":{"version":"10.0.400"}}'
Write-FixtureFile 'LICENSE' 'license fixture'
Write-FixtureFile '.editorconfig' "root = true`n"
Write-FixtureFile 'asset/props/test.props' '<Project />'
Write-FixtureFile 'asset/images/icon.png' 'binary fixture'
Write-FixtureFile 'framework/src/Sql/Model.cs' '// 中文模型'
Write-FixtureFile 'framework/src/Sql/Sql.csproj' '<Project />'
Write-FixtureFile 'framework/src/Generator/Generator.csproj' '<Project />'
Write-FixtureFile 'framework/src/Generator/Generator.cs' '// analyzer fixture'
Write-FixtureFile 'framework/src/Sql/Sql.xml' '<doc>old generated documentation</doc>'
Write-FixtureFile 'framework/src/Sql/bin/Release/old.dll' 'old binary'
Write-FixtureFile 'framework/src/Sql/obj/project.assets.json' '{}'
Write-FixtureFile 'framework/src/Sql/.codex-build/old/Sql.dll' 'old diagnostic binary'
Write-FixtureFile 'framework/tests/Bing.Data.Sql.Benchmarks/Program.cs' '// benchmark fixture'
Write-FixtureFile 'framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj' '<Project><ItemGroup><ProjectReference Include="../Bing.Data.Sql.Benchmarks.Models/Bing.Data.Sql.Benchmarks.Models.csproj" /></ItemGroup></Project>'
Write-FixtureFile 'framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.xml' '<doc>old generated documentation</doc>'
Write-FixtureFile 'framework/tests/Bing.Data.Sql.Benchmarks.Models/Bing.Data.Sql.Benchmarks.Models.csproj' '<Project><ItemGroup><ProjectReference Include="../../src/Sql/Sql.csproj" /><ProjectReference Include="../../src/Generator/Generator.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" /></ItemGroup></Project>'
Write-FixtureFile 'framework/tests/Bing.Data.Sql.Benchmarks.Models/ReturningProjectionContext.cs' '// generated projection fixture'
Write-FixtureFile 'framework/tests/Bing.Data.Sql.Benchmarks.Models/Bing.Data.Sql.Benchmarks.Models.xml' '<doc>old generated model documentation</doc>'
Write-FixtureFile 'framework/tests/Bing.Data.Sql.Benchmarks.Models/bin/Release/old.dll' 'old model binary'
Write-FixtureFile 'framework/tests/Bing.Data.Sql.Benchmarks.Models/obj/project.assets.json' '{}'
Write-FixtureFile 'framework/tests/OtherTests/Unexpected.cs' '// unrelated test'
Write-FixtureFile 'output/old/result.json' '{}'

$results = @()
$loadError = $null
try { . (Join-Path $PSScriptRoot '../Invoke-SqlCleanBuildPeak.ps1') } catch { $loadError = $_ }
function Test-Case([string]$Name, [scriptblock]$Body) {
    try {
        if ($loadError) { throw $loadError }
        & $Body
        $script:results += [pscustomobject]@{ name = $Name; status = 'PASS' }
    }
    catch { $script:results += [pscustomobject]@{ name = $Name; status = 'FAIL'; error = $_.Exception.Message } }
}

Test-Case 'Snapshot_ContainsRequiredBuildInputs_AndExcludesArtifactsAndOtherTests' {
    $destination = Join-Path $testRoot 'selected'
    $manifest = New-SqlBuildSnapshot -SourceRoot $fixture -DestinationRoot $destination
    $actual = @($manifest.inputs.Keys | Sort-Object)
    $expected = @('.editorconfig', 'LICENSE', 'asset/images/icon.png', 'asset/props/test.props', 'common.props',
        'framework/src/Generator/Generator.cs', 'framework/src/Generator/Generator.csproj',
        'framework/src/Sql/Model.cs', 'framework/src/Sql/Sql.csproj',
        'framework/tests/Bing.Data.Sql.Benchmarks.Models/Bing.Data.Sql.Benchmarks.Models.csproj',
        'framework/tests/Bing.Data.Sql.Benchmarks.Models/ReturningProjectionContext.cs',
        'framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj',
        'framework/tests/Bing.Data.Sql.Benchmarks/Program.cs', 'global.json') | Sort-Object
    if (@(Compare-Object $expected $actual).Count -ne 0) { throw 'Snapshot input set differs' }
    if (Get-ChildItem -LiteralPath $destination -Directory -Recurse -Force | Where-Object Name -In @('bin', 'obj', '.codex-build')) {
        throw 'Copied stale build directories'
    }
}

Test-Case 'Snapshot_ContainsBenchmarkModelAndAnalyzerProjectReferences' {
    $destination = Join-Path $testRoot 'project-graph'
    $manifest = New-SqlBuildSnapshot -SourceRoot $fixture -DestinationRoot $destination
    foreach ($relative in $manifest.inputs.Keys | Where-Object { $_.EndsWith('.csproj') }) {
        $projectFile = Join-Path $destination $relative
        $project = [xml][System.IO.File]::ReadAllText($projectFile, $utf8)
        foreach ($reference in $project.SelectNodes('//ProjectReference')) {
            $target = [System.IO.Path]::GetFullPath($reference.Include, (Split-Path -Parent $projectFile))
            if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
                throw "Missing snapshot project reference: $relative -> $($reference.Include)"
            }
        }
    }
}

Test-Case 'CleanBuild_KeepsRebuildableSourceOutsideMeasurementReports' {
    # 在构建前截取快照目标，避免为目录职责测试启动真实恢复或编译。
    $originalSnapshot = (Get-Item Function:New-SqlBuildSnapshot).ScriptBlock
    $script:capturedSnapshotRoot = $null
    try {
        Set-Item Function:New-SqlBuildSnapshot -Value {
            param([string]$SourceRoot, [string]$DestinationRoot)
            $script:capturedSnapshotRoot = $DestinationRoot
            throw 'Snapshot destination captured before build'
        }
        $reports = Join-Path $testRoot 'measurement-reports'
        $report = Invoke-SqlCleanBuildPeak -ResultsDirectory $reports -TimeoutSeconds 1
        if ($report.error -ne 'Snapshot destination captured before build') { throw 'Snapshot capture did not run' }
        $validation = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/validation/sql-build'))
        if (-not $script:capturedSnapshotRoot.StartsWith($validation + [System.IO.Path]::DirectorySeparatorChar,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'Rebuildable source must use artifacts/validation/sql-build'
        }
        if ($script:capturedSnapshotRoot.StartsWith($reports + [System.IO.Path]::DirectorySeparatorChar,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'Source snapshot polluted the measurement report directory'
        }
    }
    finally { Set-Item Function:New-SqlBuildSnapshot -Value $originalSnapshot }
}

Test-Case 'Snapshot_PreservesUtf8BytesAndHashes_AndDoesNotFollowSourceMutation' {
    $destination = Join-Path $testRoot 'isolated'
    $manifest = New-SqlBuildSnapshot -SourceRoot $fixture -DestinationRoot $destination
    $relative = 'framework/src/Sql/Model.cs'
    $sourceHash = (Get-FileHash -LiteralPath (Join-Path $fixture $relative) -Algorithm SHA256).Hash
    if ($manifest.inputs[$relative] -ne $sourceHash) { throw 'Source hash differs' }
    Write-FixtureFile $relative '// modified original'
    $captured = [System.IO.File]::ReadAllText((Join-Path $destination $relative), $utf8)
    if ($captured -ne '// 中文模型') { throw 'Snapshot changed with original' }
    if ((Get-FileHash -LiteralPath (Join-Path $destination $relative) -Algorithm SHA256).Hash -ne $sourceHash) {
        throw 'Captured bytes differ'
    }
}

Test-Case 'Snapshot_RejectsExistingDestination_WithoutOverwritingIt' {
    $destination = Join-Path $testRoot 'existing'
    New-Item -ItemType Directory -Path $destination | Out-Null
    $sentinel = Join-Path $destination 'sentinel.txt'
    [System.IO.File]::WriteAllText($sentinel, 'keep', $utf8)
    $rejected = $false
    try { New-SqlBuildSnapshot -SourceRoot $fixture -DestinationRoot $destination | Out-Null } catch { $rejected = $true }
    if (-not $rejected -or [System.IO.File]::ReadAllText($sentinel, $utf8) -ne 'keep') {
        throw 'Existing destination was reused or overwritten'
    }
}

Test-Case 'BuildOutput_SelectsFinalAssembly_AndIgnoresObjAndReferenceCopies' {
    $directory = Join-Path $testRoot 'build-output'
    $expected = Join-Path $directory 'output/release/net8.0/Bing.Data.Sql.Benchmarks.dll'
    foreach ($relative in @('output/release/net8.0/Bing.Data.Sql.Benchmarks.dll',
        'framework/tests/Benchmark/obj/Release/net8.0/Bing.Data.Sql.Benchmarks.dll',
        'framework/tests/Benchmark/obj/Release/net8.0/ref/Bing.Data.Sql.Benchmarks.dll',
        'framework/tests/Benchmark/obj/Release/net8.0/refint/Bing.Data.Sql.Benchmarks.dll')) {
        $file = Join-Path $directory $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $file) -Force | Out-Null
        [System.IO.File]::WriteAllText($file, 'assembly fixture', $utf8)
    }
    $binary = Get-SqlBenchmarkBinary -SourceRoot $directory
    if ($binary.FullName -ne $expected) { throw 'Selected an intermediate assembly instead of the final output' }
}

Test-Case 'RuntimeManifest_ContainsAllFilesInFinalOutputDirectory' {
    $runtimeRoot = Join-Path $testRoot 'runtime-manifest-complete'
    $runtimeFiles = [ordered]@{
        'Bing.Data.Sql.Benchmarks.dll' = 'entry assembly'
        'Bing.Data.Sql.Benchmarks.deps.json' = '{"runtimeTarget":{"name":".NETCoreApp,Version=v8.0"}}'
        'Bing.Data.Sql.Benchmarks.runtimeconfig.json' = '{"runtimeOptions":{"tfm":"net8.0"}}'
        'Bing.Data.Sql.dll' = 'dependency assembly'
        'runtimes/win-x64/native/sqlnative.dll' = 'native dependency'
    }
    foreach ($relative in $runtimeFiles.Keys) {
        $file = Join-Path $runtimeRoot $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $file) -Force | Out-Null
        [System.IO.File]::WriteAllText($file, $runtimeFiles[$relative], $utf8)
    }

    $binary = Get-Item -LiteralPath (Join-Path $runtimeRoot 'Bing.Data.Sql.Benchmarks.dll')
    $manifest = Get-SqlBenchmarkRuntimeManifest -BinaryFile $binary
    $expected = @($runtimeFiles.Keys | Sort-Object)
    $actual = @($manifest.files.Keys | Sort-Object)
    if (@(Compare-Object $expected $actual).Count -ne 0) { throw 'Runtime manifest file set differs' }
    if ($manifest.count -ne $expected.Count) { throw 'Runtime manifest count differs' }
    if ([System.IO.Path]::GetFullPath($manifest.root) -ne [System.IO.Path]::GetFullPath($runtimeRoot)) {
        throw 'Runtime manifest root differs from final output directory'
    }
    foreach ($relative in $expected) {
        $expectedHash = (Get-FileHash -LiteralPath (Join-Path $runtimeRoot $relative) -Algorithm SHA256).Hash
        if ($manifest.files[$relative] -ine $expectedHash) { throw "Runtime manifest hash differs: $relative" }
    }
}

Test-Case 'RuntimeManifest_ChangesDependencyHashWhenEntryAssemblyIsUnchanged' {
    $runtimeRoot = Join-Path $testRoot 'runtime-manifest-dependency-change'
    New-Item -ItemType Directory -Path $runtimeRoot -Force | Out-Null
    $binaryFile = Join-Path $runtimeRoot 'Bing.Data.Sql.Benchmarks.dll'
    $dependencyFile = Join-Path $runtimeRoot 'Bing.Data.Sql.dll'
    [System.IO.File]::WriteAllText($binaryFile, 'stable entry assembly', $utf8)
    [System.IO.File]::WriteAllText($dependencyFile, 'dependency version one', $utf8)

    $binary = Get-Item -LiteralPath $binaryFile
    $first = Get-SqlBenchmarkRuntimeManifest -BinaryFile $binary
    [System.IO.File]::WriteAllText($dependencyFile, 'dependency version two', $utf8)
    $second = Get-SqlBenchmarkRuntimeManifest -BinaryFile $binary
    if ($first.files['Bing.Data.Sql.Benchmarks.dll'] -ine $second.files['Bing.Data.Sql.Benchmarks.dll']) {
        throw 'Entry assembly hash changed unexpectedly'
    }
    if ($first.files['Bing.Data.Sql.dll'] -ieq $second.files['Bing.Data.Sql.dll']) {
        throw 'Dependency content change did not change its manifest hash'
    }
}

$results | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $resultRoot 'snapshot-tests.json') -Encoding utf8
$results | Format-Table name, status -AutoSize
if (@($results | Where-Object status -EQ 'FAIL').Count -gt 0) { exit 1 }
