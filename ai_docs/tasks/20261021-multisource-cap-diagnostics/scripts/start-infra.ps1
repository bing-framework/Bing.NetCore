$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$taskDirectory = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $taskDirectory 'infra/compose.yaml'
docker compose --project-name bing-multisource-cap-diagnostics --file $composeFile config --quiet
if ($LASTEXITCODE -ne 0) { throw 'Compose 配置校验失败。' }
docker compose --project-name bing-multisource-cap-diagnostics --file $composeFile up -d --wait --wait-timeout 180
if ($LASTEXITCODE -ne 0) { throw '隔离的 MySQL/RabbitMQ 启动失败。' }
docker compose --project-name bing-multisource-cap-diagnostics --file $composeFile ps
