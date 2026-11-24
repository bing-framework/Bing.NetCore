[CmdletBinding()]
param(
    [string]$ResultsDirectory = 'artifacts/benchmarks/sql-build',
    [string]$RestoreConfigFile,
    [int]$TimeoutSeconds = 900
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

function New-SqlBuildSnapshot {
    [CmdletBinding()]
    param([string]$SourceRoot, [string]$DestinationRoot)

    # 每次使用新目录；不清理或复用调用者已有的构建产物。
    if (Test-Path -LiteralPath $DestinationRoot) { throw "Snapshot directory already exists: $DestinationRoot" }
    New-Item -ItemType Directory -Path $DestinationRoot -ErrorAction Stop | Out-Null
    $inputs = [ordered]@{}
    function Copy-SnapshotInput([System.IO.FileInfo]$File) {
        $relative = [System.IO.Path]::GetRelativePath($SourceRoot, $File.FullName).Replace('\', '/')
        $destination = Join-Path $DestinationRoot $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $File.FullName -Destination $destination -ErrorAction Stop
        $inputs[$relative] = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    }
    function Copy-SnapshotDirectory([string]$Directory) {
        foreach ($item in Get-ChildItem -LiteralPath $Directory -Force -ErrorAction Stop) {
            if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) { continue }
            if ($item.PSIsContainer) {
                if ($item.Name -notin @('bin', 'obj', '.git', '.vs', '.codex-build', 'output', 'BenchmarkDotNet.Artifacts')) {
                    Copy-SnapshotDirectory $item.FullName
                }
            }
            else {
                # common.props 把同名 XML 文档写到工程目录，它是输出，不是构建输入。
                if ($item.Extension -eq '.xml' -and
                    (Test-Path -LiteralPath (Join-Path $item.DirectoryName ($item.BaseName + '.csproj')) -PathType Leaf)) { continue }
                Copy-SnapshotInput $item
            }
        }
    }
    foreach ($file in Get-ChildItem -LiteralPath $SourceRoot -File -Force) {
        if ($file.Name -match '(?i)\.props$|\.targets$|^global\.json$|^nuget\.config$|^license$|^\.editorconfig$|\.globalconfig$') {
            Copy-SnapshotInput $file
        }
    }
    foreach ($relative in @('asset', 'framework/src', 'framework/tests/Bing.Data.Sql.Benchmarks',
        'framework/tests/Bing.Data.Sql.Benchmarks.Models')) {
        Copy-SnapshotDirectory (Join-Path $SourceRoot $relative)
    }
    return [ordered]@{ inputs = $inputs; count = $inputs.Count }
}

function Get-SqlBenchmarkBinary {
    param([string]$SourceRoot)

    # obj 下还存在实现与引用程序集，收据只绑定最终输出。
    $binaries = @(Get-ChildItem -LiteralPath $SourceRoot -Recurse -File -Filter Bing.Data.Sql.Benchmarks.dll |
        Where-Object FullName -NotMatch '[\\/](obj|ref|refint)[\\/]')
    if ($binaries.Count -ne 1) { throw 'Expected one benchmark entry assembly in the fresh source tree.' }
    return $binaries[0]
}

function Invoke-SqlCleanBuildPeak {
    [CmdletBinding()]
    param([string]$ResultsDirectory, [string]$RestoreConfigFile, [int]$TimeoutSeconds)

    $ErrorActionPreference = 'Stop'
    if (-not $IsWindows) { throw 'This probe requires Windows Job Objects.' }
    $repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
    $parent = if ([System.IO.Path]::IsPathRooted($ResultsDirectory)) { $ResultsDirectory } else { Join-Path $repositoryRoot $ResultsDirectory }
    $runId = 'run-' + [Guid]::NewGuid().ToString('N')
    $directory = Join-Path $parent $runId
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $result = [ordered]@{
        status = 'FAIL'; evidenceKind = 'CleanBuildProcessTree'; startedAt = [DateTime]::UtcNow.ToString('o')
        peakMetric = 'WindowsJobCommittedBytes'; includes = 'root and all descendants, including short-lived processes'
        includesRestore = $true; nugetCache = 'existing host cache'; configuration = 'Release'
        physicalMemoryVerified = $false; methodCpuVerified = $false; productionCapacityVerified = $false
        thresholds = 'not configured; measurement only'
        environment = [ordered]@{ os = [System.Environment]::OSVersion.VersionString
            architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
            logicalProcessors = [System.Environment]::ProcessorCount }
    }
    try {
        # 报告与可重建内容分开，避免源码副本混入长期保留的测量证据。
        $snapshot = Join-Path $repositoryRoot "artifacts/validation/sql-build/$runId/source"
        $manifest = New-SqlBuildSnapshot -SourceRoot $repositoryRoot -DestinationRoot $snapshot
        $manifestFile = Join-Path $directory 'source-manifest.json'
        $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestFile -Encoding utf8
        $result.source = [ordered]@{ root = $snapshot; inputCount = $manifest.count; manifest = $manifestFile; clean = $true }
        $project = Join-Path $snapshot 'framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj'
        $arguments = @('build', $project, '-c', 'Release', '--disable-build-servers', '-p:UseSharedCompilation=false',
            '-m:1', '-nr:false', '-p:NuGetAudit=false', '--verbosity', 'minimal')
        if ($RestoreConfigFile) {
            $restoreConfig = (Resolve-Path -LiteralPath $RestoreConfigFile).Path
            $arguments += '-p:RestoreConfigFile=' + $restoreConfig
            $result.restoreConfig = [ordered]@{ file = $restoreConfig
                sha256 = (Get-FileHash -LiteralPath $restoreConfig -Algorithm SHA256).Hash }
        }
        $executable = (Get-Command dotnet -ErrorAction Stop).Source
        $result.sdk = (& $executable --version | Out-String).Trim()
        if ($LASTEXITCODE -ne 0) { throw 'Cannot determine dotnet SDK version.' }
        $result.command = @($executable) + $arguments
        $result.buildLog = Join-Path $directory 'build.log'
        $result.tool = [ordered]@{ driverSha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
            jobSha256 = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'BuildProcessTree.cs') -Algorithm SHA256).Hash }
        if (-not ('Bing.Sql.Performance.BuildProcessTree' -as [type])) {
            Add-Type -Path (Join-Path $PSScriptRoot 'BuildProcessTree.cs')
        }
        $result.measurement = [Bing.Sql.Performance.BuildProcessTree]::Run(
            $executable, [string[]]$arguments, $snapshot, $result.buildLog, $TimeoutSeconds * 1000)
        if ($result.measurement.TimedOut) { $result.status = 'TIMEOUT' }
        elseif ($result.measurement.ExitCode -eq 0) {
            $binary = Get-SqlBenchmarkBinary -SourceRoot $snapshot
            $result.binary = [ordered]@{ file = $binary.FullName; sha256 = (Get-FileHash -LiteralPath $binary.FullName -Algorithm SHA256).Hash }
            $result.status = 'PASS'
        }
    }
    catch { $result.error = $_.Exception.Message }
    finally {
        $result.finishedAt = [DateTime]::UtcNow.ToString('o')
        $resultFile = Join-Path $directory 'result.json'
        $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resultFile -Encoding utf8
        Write-Host "SQL_CLEAN_BUILD_$($result.status): $resultFile"
    }
    return [pscustomobject]$result
}

if ($MyInvocation.InvocationName -ne '.') {
    $report = Invoke-SqlCleanBuildPeak -ResultsDirectory $ResultsDirectory -RestoreConfigFile $RestoreConfigFile -TimeoutSeconds $TimeoutSeconds
    if ($report.status -ne 'PASS') { exit 1 }
}
