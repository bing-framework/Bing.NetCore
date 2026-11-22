#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Start', 'Stop')][string]$Action = 'Start',
    [string]$BinDirectory = $env:PGBIN,
    [string]$Directory = 'output/sql-aot-postgresql',
    [string]$EnvironmentFile = $env:GITHUB_ENV
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'WindowsPostgreSql.ps1')
if ($Action -eq 'Start') {
    Start-SqlAotPostgreSql -BinDirectory $BinDirectory -Directory $Directory -EnvironmentFile $EnvironmentFile | ConvertTo-Json -Depth 5
}
else { Stop-SqlAotPostgreSql -Directory $Directory }
