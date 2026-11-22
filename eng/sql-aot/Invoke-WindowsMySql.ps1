#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Start', 'Stop')][string]$Action = 'Start',
    [string]$BinDirectory,
    [string]$Directory = 'output/sql-aot-mysql',
    [string]$EnvironmentFile = $env:GITHUB_ENV
)

[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'WindowsMySql.ps1')
if ($Action -eq 'Start') {
    Start-SqlAotMySql -BinDirectory $BinDirectory -Directory $Directory -EnvironmentFile $EnvironmentFile | ConvertTo-Json -Depth 5
}
else { Stop-SqlAotMySql -Directory $Directory }
