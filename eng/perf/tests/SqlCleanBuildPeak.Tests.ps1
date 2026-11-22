[CmdletBinding()]
param([string]$ResultsDirectory = 'output/round135-tests')

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
Write-FixtureFile 'framework/src/Sql/Sql.xml' '<doc>old generated documentation</doc>'
Write-FixtureFile 'framework/src/Sql/bin/Release/old.dll' 'old binary'
Write-FixtureFile 'framework/src/Sql/obj/project.assets.json' '{}'
Write-FixtureFile 'framework/src/Sql/.codex-build/old/Sql.dll' 'old diagnostic binary'
Write-FixtureFile 'framework/tests/Bing.Data.Sql.Benchmarks/Program.cs' '// benchmark fixture'
Write-FixtureFile 'framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj' '<Project />'
Write-FixtureFile 'framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.xml' '<doc>old generated documentation</doc>'
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
        'framework/src/Sql/Model.cs', 'framework/src/Sql/Sql.csproj',
        'framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj',
        'framework/tests/Bing.Data.Sql.Benchmarks/Program.cs', 'global.json') | Sort-Object
    if (@(Compare-Object $expected $actual).Count -ne 0) { throw 'Snapshot input set differs' }
    if (Get-ChildItem -LiteralPath $destination -Directory -Recurse -Force | Where-Object Name -In @('bin', 'obj', '.codex-build')) {
        throw 'Copied stale build directories'
    }
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

$results | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $resultRoot 'snapshot-tests.json') -Encoding utf8
$results | Format-Table name, status -AutoSize
if (@($results | Where-Object status -EQ 'FAIL').Count -gt 0) { exit 1 }
