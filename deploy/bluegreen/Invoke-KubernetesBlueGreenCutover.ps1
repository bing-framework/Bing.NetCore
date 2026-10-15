[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('blue', 'green')]
    [string]$Target,
    [string]$Namespace = 'default',
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

function Invoke-Kubectl {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $display = ($Arguments -join ' ')
    if ($DryRun) {
        Write-Output "DRY-RUN kubectl -n $Namespace $display"
        return @()
    }

    $result = & kubectl -n $Namespace @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "kubectl $display 失败：$($result -join [Environment]::NewLine)"
    }
    return $result
}

function Get-CurrentColor {
    if ($DryRun) {
        return 'blue'
    }
    $value = Invoke-Kubectl @('get', 'service', 'bing-web', '-o', 'jsonpath={.spec.selector.color}')
    $color = ([string]::Join('', $value)).Trim()
    if ($color -notin @('blue', 'green')) {
        throw 'bing-web Service 当前没有有效的蓝绿 selector。'
    }
    return $color
}

function Get-DeploymentName {
    param(
        [Parameter(Mandatory = $true)][string]$Role,
        [Parameter(Mandatory = $true)][string]$Color
    )
    return "bing-$Role-$Color"
}

function Get-DeploymentReplicas {
    param([Parameter(Mandatory = $true)][string]$Deployment)
    if ($DryRun) { return 0 }
    $value = Invoke-Kubectl @('get', 'deployment', $Deployment, '-o', 'jsonpath={.status.replicas}')
    $replicas = 0
    if (-not [int]::TryParse(([string]::Join('', $value)).Trim(), [ref]$replicas)) {
        return 0
    }
    return $replicas
}

function Assert-WebReady {
    param([Parameter(Mandatory = $true)][string]$Color)
    $deployment = Get-DeploymentName -Role 'web' -Color $Color
    if ($DryRun) {
        Write-Output "DRY-RUN 等待 $deployment Web 就绪。"
        return
    }
    Invoke-Kubectl @('rollout', 'status', "deployment/$deployment", "--timeout=$($DrainTimeoutSeconds)s") | Out-Null
    if ((Get-DeploymentReplicas -Deployment $deployment) -lt 1) {
        throw "目标 Web $deployment 没有就绪副本。"
    }
}

function Start-BackgroundRole {
    param(
        [Parameter(Mandatory = $true)][string]$Role,
        [Parameter(Mandatory = $true)][string]$Color
    )
    $deployment = Get-DeploymentName -Role $Role -Color $Color
    Invoke-Kubectl @('scale', "deployment/$deployment", '--replicas=1') | Out-Null
    if (-not $DryRun) {
        Invoke-Kubectl @('rollout', 'status', "deployment/$deployment", "--timeout=$($DrainTimeoutSeconds)s") | Out-Null
    }
}

function Stop-BackgroundRole {
    param(
        [Parameter(Mandatory = $true)][string]$Role,
        [Parameter(Mandatory = $true)][string]$Color
    )
    $deployment = Get-DeploymentName -Role $Role -Color $Color
    Invoke-Kubectl @('scale', "deployment/$deployment", '--replicas=0') | Out-Null
    if ($DryRun) {
        Write-Output "DRY-RUN 等待 $deployment 排空。"
        return
    }

    $deadline = [DateTime]::UtcNow.AddSeconds($DrainTimeoutSeconds)
    do {
        if ((Get-DeploymentReplicas -Deployment $deployment) -eq 0) {
            return
        }
        if ([DateTime]::UtcNow -ge $deadline) {
            throw "$deployment 在 $DrainTimeoutSeconds 秒内未完成排空。"
        }
        Start-Sleep -Seconds 2
    } while ($true)
}

function Switch-WebService {
    param([Parameter(Mandatory = $true)][string]$Color)
    Invoke-Kubectl @('set', 'selector', 'service/bing-web', 'app=bing-web', "color=$Color") | Out-Null
    if ($DryRun) { return }
    $actual = ([string]::Join('', (Invoke-Kubectl @('get', 'service', 'bing-web', '-o', 'jsonpath={.spec.selector.color}')))).Trim()
    if ($actual -ne $Color) {
        throw "bing-web Service selector 切换到 $Color 后校验失败。"
    }
}

function Restore-WebService {
    param([Parameter(Mandatory = $true)][string]$Color)
    try {
        Switch-WebService -Color $Color
    }
    catch {
        Write-Warning "恢复 Kubernetes Web Service 失败：$($_.Exception.Message)"
    }
}

$previous = Get-CurrentColor
if ($previous -eq $Target) {
    throw "目标 $Target 已经是当前 Web 路由。"
}

$startedTargetRoles = [System.Collections.Generic.List[string]]::new()
$stoppedPreviousRoles = [System.Collections.Generic.List[string]]::new()
$serviceSwitchAttempted = $false

try {
    Assert-WebReady -Color $Target

    foreach ($role in @('consumer', 'task')) {
        $enabled = ($role -eq 'consumer' -and $SwitchConsumer) -or
            ($role -eq 'task' -and $SwitchTask)
        if (-not $enabled) { continue }
        $startedTargetRoles.Add($role)
        Start-BackgroundRole -Role $role -Color $Target
    }

    $serviceSwitchAttempted = $true
    Switch-WebService -Color $Target

    foreach ($role in @('consumer', 'task')) {
        $enabled = ($role -eq 'consumer' -and $SwitchConsumer) -or
            ($role -eq 'task' -and $SwitchTask)
        if (-not $enabled) { continue }
        $stoppedPreviousRoles.Add($role)
        Stop-BackgroundRole -Role $role -Color $previous
    }

    Write-Output "Kubernetes 蓝绿切换完成：Web=$Target，Consumer=$([bool]$SwitchConsumer)，Task=$([bool]$SwitchTask)。"
}
catch {
    $original = $_
    if ($serviceSwitchAttempted) {
        Restore-WebService -Color $previous
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
