param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('blue', 'green')]
    [string]$Target
)

$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$active = Join-Path $root 'nginx/active.conf'
$candidate = Join-Path $root "nginx/$Target.conf"
$port = if ($Target -eq 'blue') { 18081 } else { 18082 }
$previous = Get-Content -LiteralPath $active -Raw -Encoding UTF8

try {
    $response = Invoke-WebRequest -Uri "http://127.0.0.1:$port/health/ready" -TimeoutSec 10 -UseBasicParsing
    if ($response.StatusCode -ne 200) { throw "Target $Target is not ready." }

    Copy-Item -LiteralPath $candidate -Destination $active -Force
    & docker compose -f (Join-Path $root 'compose.yaml') exec -T nginx nginx -t
    if ($LASTEXITCODE -ne 0) { throw 'Nginx configuration validation failed.' }
    & docker compose -f (Join-Path $root 'compose.yaml') exec -T nginx nginx -s reload
    if ($LASTEXITCODE -ne 0) { throw 'Nginx reload failed.' }
    Write-Output "Traffic now targets $Target. Keep the old Web container running until in-flight requests drain."
}
catch {
    [System.IO.File]::WriteAllText($active, $previous, [System.Text.UTF8Encoding]::new($false))
    & docker compose -f (Join-Path $root 'compose.yaml') exec -T nginx nginx -s reload
    throw
}
