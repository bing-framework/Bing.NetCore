[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Collector,
    [Parameter(Mandatory)][string]$Fixture,
    [Parameter(Mandatory)][string]$ResultsDirectory
)
[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
# 与 smoke 一样在当前 PowerShell 会话调用，核对原生命令退出码不会泄漏给调用方。
& (Join-Path $PSScriptRoot 'SqlCpuWindow.Integration.Tests.ps1') -Collector $Collector -Fixture $Fixture -ResultsDirectory $ResultsDirectory
if ($LASTEXITCODE -ne 0) { throw "Successful integration leaked native exit code: $LASTEXITCODE" }
Write-Output 'SQL_CPU_WINDOW_EXIT_CODE_PASS'
