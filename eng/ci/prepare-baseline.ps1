[CmdletBinding()]
param(
    [string]$Commit = '24c93cb1a5355797223637e2d42ac3dce968d7ef',
    [string]$StoreRoot
)

$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ([string]::IsNullOrWhiteSpace($StoreRoot)) {
    $StoreRoot = Join-Path (Split-Path $repoRoot -Parent) ((Split-Path $repoRoot -Leaf) + '.baselines')
}
$StoreRoot = [System.IO.Path]::GetFullPath($StoreRoot)
$comparison = if ($env:OS -eq 'Windows_NT') {
    [System.StringComparison]::OrdinalIgnoreCase
} else {
    [System.StringComparison]::Ordinal
}
$repoPrefix = $repoRoot.TrimEnd([char[]]'\/') + [System.IO.Path]::DirectorySeparatorChar
if ($StoreRoot.Equals($repoRoot, $comparison) -or $StoreRoot.StartsWith($repoPrefix, $comparison)) {
    throw '基线存储目录必须位于当前仓库之外。'
}

$resolvedCommit = & git -C $repoRoot rev-parse --verify --end-of-options "$Commit^{commit}"
if ($LASTEXITCODE -ne 0) { throw "无法解析基线提交：$Commit" }
$resolvedCommit = ($resolvedCommit | Select-Object -Last 1).Trim()
$baselinePath = Join-Path $StoreRoot $resolvedCommit

if (Test-Path -LiteralPath $baselinePath) {
    $existingRoot = & git -C $baselinePath rev-parse --show-toplevel
    if ($LASTEXITCODE -ne 0) { throw "目标已存在但不是 Git 工作树：$baselinePath" }
    $existingRoot = [System.IO.Path]::GetFullPath(($existingRoot | Select-Object -Last 1).Trim())
    if (-not $existingRoot.Equals($baselinePath, $comparison)) {
        throw "目标不是独立的基线工作树：$baselinePath"
    }
    $existingCommit = & git -C $baselinePath rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or ($existingCommit | Select-Object -Last 1).Trim() -ne $resolvedCommit) {
        throw "目标工作树与请求的基线提交不符：$baselinePath"
    }
    $changes = & git -C $baselinePath status --porcelain --untracked-files=all
    if ($LASTEXITCODE -ne 0 -or $changes) {
        throw "基线工作树存在未提交内容，请先核对；脚本不会重置：$baselinePath"
    }
} else {
    $null = New-Item -ItemType Directory -Path $StoreRoot -Force
    & git -C $repoRoot worktree add --detach $baselinePath $resolvedCommit
    if ($LASTEXITCODE -ne 0) { throw "创建基线工作树失败：$baselinePath" }
}

# 同一个提交复用同一工作树，不复制源码，也不覆盖基线。
Write-Output $baselinePath
