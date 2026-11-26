#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('1C2G','2C4G','4C8G','8C16G')][string]$Profile = '2C4G',
    [string]$Image,
    [string]$PublishDirectory,
    [string]$ResultsDirectory,
    [switch]$CheckHostOnly
)
[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Text.UTF8Encoding]::new($false)

function Get-SqlCapacityProfile([string]$Name) {
    $cpu = switch ($Name) { '1C2G' {1}; '2C4G' {2}; '4C8G' {4}; '8C16G' {8}; default {throw 'Unknown hardware profile.'} }
    return @{name=$Name; cpu=$cpu; memoryBytes=[long]$cpu * 2GB}
}
function Assert-SqlCapacityHost($Engine, $Limits) {
    if ($Engine.OSType -cne 'linux') { throw 'A Linux Docker engine with cgroup v2 is required.' }
    if ($Engine.NCPU -lt $Limits.cpu -or $Engine.MemTotal -lt $Limits.memoryBytes) {
        throw 'Docker host has insufficient CPU or memory for this profile; resize the host before measuring.'
    }
}
function Assert-SqlCapacityLimits([string]$CpuMax, [string]$MemoryMax, $Limits) {
    $parts = $CpuMax.Trim() -split '\s+'
    $quota=0L; $period=0L; $memory=0L
    if ($parts.Count -ne 2 -or ![long]::TryParse($parts[0], [ref]$quota) -or
        ![long]::TryParse($parts[1], [ref]$period) -or $period -le 0 -or $quota -le 0 -or
        $quota / [double]$period -ne $Limits.cpu -or
        ![long]::TryParse($MemoryMax.Trim(), [ref]$memory) -or $memory -ne $Limits.memoryBytes) {
        throw 'Effective cgroup limits do not match the requested profile.'
    }
}
function Invoke-SqlCapacityDocker([string[]]$Arguments) {
    $value = & docker @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Docker command failed: $($Arguments[0])" }
    return ($value -join "`n")
}
function New-SqlCapacityEnvironment {
    $ErrorActionPreference='Stop'
    if (!$ResultsDirectory) { throw 'ResultsDirectory is required.' }
    $root=[IO.Path]::GetFullPath($ResultsDirectory)
    if (Test-Path -LiteralPath $root) { throw 'ResultsDirectory must be a new directory.' }
    [IO.Directory]::CreateDirectory($root) | Out-Null
    $limits=Get-SqlCapacityProfile $Profile
    $receipt=[ordered]@{schema='SqlCapacityEnvironment-v1'; status='FAIL'; profile=$limits
        startedAt=[DateTime]::UtcNow.ToString('o'); effectiveLimitsVerified=$false; productionCapacityVerified=$false}
    $container=$null
    try {
        $engine=Invoke-SqlCapacityDocker @('info','--format','{{json .}}') | ConvertFrom-Json
        $receipt.host=@{os=$engine.OSType; cpu=$engine.NCPU; memoryBytes=$engine.MemTotal; version=$engine.ServerVersion}
        Assert-SqlCapacityHost $engine $limits
        if ($CheckHostOnly) { $receipt.status='HOST_CAPACITY_AVAILABLE'; return }
        if (!$Image -or !$PublishDirectory) { throw 'Image and PublishDirectory are required to provision.' }
        $publish=(Resolve-Path -LiteralPath $PublishDirectory -ErrorAction Stop).Path
        if (!(Test-Path -LiteralPath $publish -PathType Container)) { throw 'PublishDirectory must be a directory.' }
        # 仅使用本地已存在镜像；应用挂载只读，结果挂载统一验证目录。
        $container=Invoke-SqlCapacityDocker @('create','--pull','never','--cpus',"$($limits.cpu)",
            '--memory',"$($limits.memoryBytes)",'--memory-swap',"$($limits.memoryBytes)",
            '--read-only','--tmpfs','/tmp','--mount',"type=bind,source=$publish,target=/app,readonly",
            '--mount',"type=bind,source=$root,target=/results",'--entrypoint','sh',$Image,'-c','exec sleep infinity')
        $receipt.containerId=$container.Trim()
        Invoke-SqlCapacityDocker @('start',$container) | Out-Null
        $details=Invoke-SqlCapacityDocker @('inspect',$container) | ConvertFrom-Json
        $receipt.imageId=$details[0].Image
        $receipt.configuredLimits=@{nanoCpus=$details[0].HostConfig.NanoCpus; memoryBytes=$details[0].HostConfig.Memory}
        $cpu=Invoke-SqlCapacityDocker @('exec',$container,'cat','/sys/fs/cgroup/cpu.max')
        $memory=Invoke-SqlCapacityDocker @('exec',$container,'cat','/sys/fs/cgroup/memory.max')
        Assert-SqlCapacityLimits $cpu $memory $limits
        $swap=Invoke-SqlCapacityDocker @('exec',$container,'cat','/sys/fs/cgroup/memory.swap.max')
        if ($swap.Trim() -ne '0') { throw 'Container swap must be disabled.' }
        $receipt.cgroup=@{cpuMax=$cpu; memoryMax=$memory; swapMax=$swap}
        $receipt.effectiveLimitsVerified=$true
        $receipt.status='READY'
        # READY 仅表示环境就绪；调用方使用 containerId 执行工作负载，结束后显式 docker rm -f。
    }
    catch {
        $receipt.error=$_.Exception.Message
        if ($container) { & docker rm -f $container | Out-Null }
    }
    finally {
        $receipt.finishedAt=[DateTime]::UtcNow.ToString('o')
        $receipt | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root 'environment.json') -Encoding utf8
        $receipt | ConvertTo-Json -Depth 8
    }
    if ($receipt.status -eq 'FAIL') { exit 2 }
}
if ($MyInvocation.InvocationName -ne '.') { New-SqlCapacityEnvironment }
