[CmdletBinding()]
param([switch]$Apply)

$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Text.UTF8Encoding]::new($false)

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sourceRoot = Join-Path $repoRoot 'ai_docs/tasks'
$artifactRoot = Join-Path $repoRoot 'artifacts/ai-tasks'
$sourcePrefix = $sourceRoot + [IO.Path]::DirectorySeparatorChar
$artifactPrefix = $artifactRoot + [IO.Path]::DirectorySeparatorChar
$tracked = @(& git -C $repoRoot ls-files -- 'ai_docs/tasks')
if ($LASTEXITCODE -ne 0) { throw '无法核对 Git 跟踪状态。' }

# 仅归档任务运行 JSON；编译产物由既有 bin/obj 规则管理。
$files = @(Get-ChildItem -LiteralPath $sourceRoot -File -Recurse -Filter '*.json' |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
$moves = @($files | ForEach-Object {
    $source = [IO.Path]::GetFullPath($_.FullName)
    if (-not $source.StartsWith($sourcePrefix, [StringComparison]::OrdinalIgnoreCase) -or
        ($_.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "源文件不在任务目录内或是链接：$source"
    }
    $relative = $source.Substring($sourcePrefix.Length)
    $gitPath = 'ai_docs/tasks/' + $relative.Replace('\', '/')
    if ($tracked -contains $gitPath) {
        throw "文件已被 Git 跟踪，需人工核对，不自动改动暂存区：$gitPath"
    }
    $destination = [IO.Path]::GetFullPath((Join-Path $artifactRoot $relative))
    if (-not $destination.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "目标文件超出归档目录：$destination"
    }
    if (Test-Path -LiteralPath $destination) { throw "归档目标已存在，不覆盖：$destination" }
    [pscustomobject]@{ Source = $source; Destination = $destination; Bytes = $_.Length }
})

# 全部路径和冲突先核对；不覆盖、不清理目录、不改写原始证据。
if ($Apply) {
    foreach ($move in $moves) {
        $null = New-Item -ItemType Directory -Path (Split-Path $move.Destination -Parent) -Force
        Move-Item -LiteralPath $move.Source -Destination $move.Destination
    }
}
[pscustomobject]@{
    Mode = if ($Apply) { 'Moved' } else { 'Preview; use -Apply to move' }
    Files = $moves.Count
    Bytes = ($moves | Measure-Object Bytes -Sum).Sum
    Destination = $artifactRoot
}
