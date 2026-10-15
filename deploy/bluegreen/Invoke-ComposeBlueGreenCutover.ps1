[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('blue', 'green')]
    [string]$Target,
    [switch]$SwitchConsumer,
    [switch]$SwitchTask,
    [ValidateRange(1, 1800)]
    [int]$DrainTimeoutSeconds = 300,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$composeFile = Join-Path $root 'compose.yaml'
$active = Join-Path $root 'nginx/active.conf'

function Invoke-Compose {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $display = ($Arguments -join ' ')
    if ($DryRun) {
        Write-Output "DRY-RUN docker compose -f $composeFile $display"
        return @()
    }

    $result = & docker compose -f $composeFile @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose $display 失败：$($result -join [Environment]::NewLine)"
    }
    return $result
}

function Get-ActiveColor {
    $content = Get-Content -LiteralPath $active -Raw -Encoding UTF8
    $match = [regex]::Match($content, 'proxy_pass\s+http://web-(blue|green):8080')
    if (-not $match.Success) {
        throw 'nginx/active.conf 没有可识别的蓝绿后端。'
    }
    return $match.Groups[1].Value
}

function Get-WebPort {
    param([Parameter(Mandatory = $true)][string]$Color)
    if ($Color -eq 'blue') { return 18081 }
    return 18082
}

function Assert-WebReady {
    param([Parameter(Mandatory = $true)][string]$Color)
    if ($DryRun) {
        Write-Output "DRY-RUN 检查 Web $Color 就绪。"
        return
    }

    $port = Get-WebPort -Color $Color
    $response = Invoke-WebRequest -Uri "http://127.0.0.1:$port/health/ready" -TimeoutSec 10 -UseBasicParsing
    if ($response.StatusCode -ne 200) {
        throw "Web $Color 未就绪。"
    }
}

function Assert-ServiceRunning {
    param([Parameter(Mandatory = $true)][string]$Service)
    if ($DryRun) {
        Write-Output "DRY-RUN 检查服务 $Service 已运行。"
        return
    }

    $services = Invoke-Compose @('ps', '--services', '--filter', 'status=running')
    if (-not ($services -match "(?m)^$([regex]::Escape($Service))$")) {
        throw "Compose 服务 $Service 未运行。"
    }
}

function Start-BackgroundRole {
    param(
        [Parameter(Mandatory = $true)][string]$Role,
        [Parameter(Mandatory = $true)][string]$Color
    )
    $service = "$Role-$Color"
    Invoke-Compose @('--profile', $service, 'up', '-d', $service) | Out-Null
    Assert-ServiceRunning -Service $service
}

function Stop-BackgroundRole {
    param(
        [Parameter(Mandatory = $true)][string]$Role,
        [Parameter(Mandatory = $true)][string]$Color
    )
    $service = "$Role-$Color"
    Invoke-Compose @('--profile', $service, 'stop', '-t', ([string]$DrainTimeoutSeconds), $service) | Out-Null
}

function Switch-WebTraffic {
    param([Parameter(Mandatory = $true)][string]$Color)
    if ($DryRun) {
        Write-Output "DRY-RUN 切换 Nginx 到 $Color。"
        return
    }
    & (Join-Path $root 'Switch-ComposeTraffic.ps1') -Target $Color
    if ($LASTEXITCODE -ne 0) {
        throw "Web 流量切换到 $Color 失败。"
    }
}

function Restore-WebTraffic {
    param([Parameter(Mandatory = $true)][string]$Color)
    try {
        if ($DryRun) {
            Write-Warning "DRY-RUN 将恢复 Nginx 到 $Color。"
            return
        }
        & (Join-Path $root 'Switch-ComposeTraffic.ps1') -Target $Color
        if ($LASTEXITCODE -ne 0) {
            throw "恢复 Web 流量到 $Color 失败。"
        }
    }
    catch {
        Write-Warning "恢复 Web 路由失败：$($_.Exception.Message)"
    }
}

$previous = Get-ActiveColor
if ($previous -eq $Target) {
    throw "目标 $Target 已经是当前 Web 路由。"
}

$startedTargetRoles = [System.Collections.Generic.List[string]]::new()
$stoppedPreviousRoles = [System.Collections.Generic.List[string]]::new()
$webSwitched = $false

try {
    Assert-WebReady -Color $Target

    foreach ($role in @('consumer', 'task')) {
        $enabled = ($role -eq 'consumer' -and $SwitchConsumer) -or
            ($role -eq 'task' -and $SwitchTask)
        if (-not $enabled) { continue }
        $startedTargetRoles.Add($role)
        Start-BackgroundRole -Role $role -Color $Target
    }

    Switch-WebTraffic -Color $Target
    $webSwitched = $true

    foreach ($role in @('consumer', 'task')) {
        $enabled = ($role -eq 'consumer' -and $SwitchConsumer) -or
            ($role -eq 'task' -and $SwitchTask)
        if (-not $enabled) { continue }
        $stoppedPreviousRoles.Add($role)
        Stop-BackgroundRole -Role $role -Color $previous
    }

    Write-Output "蓝绿切换完成：Web=$Target，Consumer=$([bool]$SwitchConsumer)，Task=$([bool]$SwitchTask)。"
}
catch {
    $original = $_
    if ($webSwitched) {
        Restore-WebTraffic -Color $previous
    }

    foreach ($role in $stoppedPreviousRoles) {
        try {
            Start-BackgroundRole -Role $role -Color $previous
        }
        catch {
            Write-Warning "恢复旧版 $role 失败：$($_.Exception.Message)"
        }
    }

    foreach ($role in $startedTargetRoles) {
        try {
            Stop-BackgroundRole -Role $role -Color $Target
        }
        catch {
            Write-Warning "停止候选版 $role 失败：$($_.Exception.Message)"
        }
    }

    throw $original
}
