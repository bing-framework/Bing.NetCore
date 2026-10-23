$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$taskDirectory = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $taskDirectory 'infra/compose.yaml'
docker compose --project-name bing-multisource-cap-diagnostics --file $composeFile down
if ($LASTEXITCODE -ne 0) { throw '停止本任务 Compose 项目失败。' }
