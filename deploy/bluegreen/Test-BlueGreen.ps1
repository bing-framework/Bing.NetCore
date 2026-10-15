param(
    [ValidateSet('Compose', 'Kubernetes')]
    [string]$Platform = 'Compose',
    [switch]$Live,
    [switch]$RequireConsumer,
    [switch]$RequireTask,
    [string]$Namespace = 'default'
)

$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$compose = Get-Content -LiteralPath (Join-Path $root 'compose.yaml') -Raw -Encoding UTF8
$composeCutover = Get-Content -LiteralPath (Join-Path $root 'Invoke-ComposeBlueGreenCutover.ps1') -Raw -Encoding UTF8
$kubernetesCutover = Get-Content -LiteralPath (Join-Path $root 'Invoke-KubernetesBlueGreenCutover.ps1') -Raw -Encoding UTF8
$kubernetes = Get-Content -LiteralPath (Join-Path $root 'kubernetes.yaml') -Raw -Encoding UTF8
$blue = Get-Content -LiteralPath (Join-Path $root 'nginx/blue.conf') -Raw -Encoding UTF8
$green = Get-Content -LiteralPath (Join-Path $root 'nginx/green.conf') -Raw -Encoding UTF8

function Test-ImmutableImage {
    param(
        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [AllowEmptyString()]
        [string]$Image
    )

    return $Image -match '@sha256:[0-9a-fA-F]{64}$'
}

function Assert-ImmutableImage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [string]$Image
    )

    if (-not (Test-ImmutableImage -Image $Image)) {
        throw "$Name 必须使用带 64 位十六进制 SHA-256 摘要的不可变镜像，例如 registry.example/bing-admin@sha256:...。"
    }
}

foreach ($item in @(
    @{ Name = 'Compose distinct image slots'; Pass = ($compose -match 'BING_BLUE_IMAGE' -and $compose -match 'BING_GREEN_IMAGE') },
    @{ Name = 'Compose Web consumer disabled'; Pass = ($compose -match 'BING_CAP_CONSUMER_ENABLED: "false"') },
    @{ Name = 'Compose consumer role'; Pass = ($compose -match 'BING_HOST_ROLE: consumer' -and $compose -match 'BING_CAP_CONSUMER_ENABLED: "true"') },
    @{ Name = 'Compose consumer group'; Pass = ($compose -match 'BING_CAP_CONSUMER_GROUP: bing-admin') },
    @{ Name = 'Compose task slots'; Pass = ($compose -match 'task-blue:' -and $compose -match 'task-green:') },
    @{ Name = 'Compose task role'; Pass = ($compose -match 'BING_HOST_ROLE: task' -and $compose -match 'BING_CAP_CONSUMER_ENABLED: "false"') },
    @{ Name = 'Compose cutover orchestration'; Pass = ($composeCutover -match '\$SwitchConsumer' -and $composeCutover -match '\$SwitchTask' -and $composeCutover -match 'DrainTimeoutSeconds' -and $composeCutover -match 'Restore-WebTraffic') },
    @{ Name = 'Kubernetes cutover orchestration'; Pass = ($kubernetesCutover -match '\$SwitchConsumer' -and $kubernetesCutover -match '\$SwitchTask' -and $kubernetesCutover -match 'DrainTimeoutSeconds' -and $kubernetesCutover -match 'Restore-WebService') },
    @{ Name = 'Kubernetes Web consumer disabled'; Pass = ($kubernetes -match 'name: BING_CAP_CONSUMER_ENABLED, value: "false"') },
    @{ Name = 'Kubernetes consumer role'; Pass = ($kubernetes -match '(?s)name: BING_HOST_ROLE, value: consumer.*?name: BING_CAP_CONSUMER_ENABLED, value: "true"') },
    @{ Name = 'Kubernetes stable consumer group'; Pass = (([regex]::Matches($kubernetes, 'name: BING_CAP_CONSUMER_GROUP, value: bing-admin')).Count -eq 2) },
    @{ Name = 'Kubernetes green consumer parked'; Pass = ($kubernetes -match '(?s)name: bing-consumer-green\s+spec:\s+replicas: 0') },
    @{ Name = 'Kubernetes task role'; Pass = (([regex]::Matches($kubernetes, 'name: BING_HOST_ROLE, value: task')).Count -eq 2 -and ($kubernetes -match '(?s)name: BING_HOST_ROLE, value: task.*?name: BING_CAP_CONSUMER_ENABLED, value: "false"')) },
    @{ Name = 'Kubernetes green task parked'; Pass = ($kubernetes -match '(?s)name: bing-task-green\s+spec:\s+replicas: 0') },
    @{ Name = 'Kubernetes readiness'; Pass = (([regex]::Matches($kubernetes, 'path: /health/ready')).Count -eq 4) },
    @{ Name = 'Blue upstream'; Pass = ($blue -match 'proxy_pass http://web-blue:8080') },
    @{ Name = 'Green upstream'; Pass = ($green -match 'proxy_pass http://web-green:8080') }
)) {
    if (-not $item.Pass) { throw "FAILED: $($item.Name)" }
    Write-Output "PASS: $($item.Name)"
}

if (-not $Live) {
    Write-Output 'Static checks passed. Live deployment acceptance remains unverified.'
    return
}

if ($Platform -eq 'Compose') {
    Assert-ImmutableImage -Name 'BING_BLUE_IMAGE' -Image $env:BING_BLUE_IMAGE
    Assert-ImmutableImage -Name 'BING_GREEN_IMAGE' -Image $env:BING_GREEN_IMAGE
    & docker compose -f (Join-Path $root 'compose.yaml') config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Compose configuration is invalid.' }
    foreach ($port in @(18081, 18082)) {
        $result = Invoke-WebRequest -Uri "http://127.0.0.1:$port/health/ready" -TimeoutSec 10 -UseBasicParsing
        if ($result.StatusCode -ne 200) { throw "Web on port $port is not ready." }
    }
    if ($RequireConsumer) {
        $runningServices = & docker compose -f (Join-Path $root 'compose.yaml') ps --services --filter status=running
        if ($LASTEXITCODE -ne 0 -or -not ($runningServices -match '(?m)^consumer-(blue|green)$')) {
            throw 'Compose 至少需要一个运行中的蓝/绿消费者服务。'
        }
    }
    if ($RequireTask) {
        $runningServices = & docker compose -f (Join-Path $root 'compose.yaml') ps --services --filter status=running
        if ($LASTEXITCODE -ne 0 -or -not ($runningServices -match '(?m)^task-(blue|green)$')) {
            throw 'Compose 至少需要一个运行中的蓝/绿 Task 服务。'
        }
    }
    $proxy = Invoke-WebRequest -Uri 'http://127.0.0.1:8080/health/ready' -TimeoutSec 10 -UseBasicParsing
    if ($proxy.Headers['X-Bing-Color'] -notin @('blue', 'green')) { throw 'Proxy color header is missing.' }
}
else {
    $images = [regex]::Matches($kubernetes, '(?m)^\s*image:\s*(\S+)') |
        ForEach-Object { $_.Groups[1].Value }
    if ($images.Count -eq 0) { throw 'Kubernetes manifest does not contain image entries.' }
    foreach ($image in $images) {
        if ($image -match 'replace-me|example\.invalid') {
            throw "Kubernetes manifest 仍包含占位镜像：$image。"
        }
        Assert-ImmutableImage -Name 'Kubernetes image' -Image $image
    }
    & kubectl -n $Namespace apply --dry-run=client -f (Join-Path $root 'kubernetes.yaml')
    if ($LASTEXITCODE -ne 0) { throw 'Kubernetes manifest is invalid.' }
    foreach ($color in @('blue', 'green')) {
        & kubectl -n $Namespace rollout status "deployment/bing-web-$color" --timeout=120s
        if ($LASTEXITCODE -ne 0) { throw "Web $color is not ready." }
    }
    if ($RequireConsumer) {
        $consumerReady = 0
        foreach ($color in @('blue', 'green')) {
            $readyText = & kubectl -n $Namespace get deployment "bing-consumer-$color" -o 'jsonpath={.status.readyReplicas}'
            if ($LASTEXITCODE -ne 0) { throw "读取消费者 $color 就绪副本失败。" }
            $ready = 0
            if (-not [int]::TryParse([string]$readyText, [ref]$ready)) { $ready = 0 }
            $consumerReady += $ready
        }
        if ($consumerReady -lt 1) { throw 'Kubernetes 至少需要一个就绪的蓝/绿消费者副本。' }
    }
    if ($RequireTask) {
        $taskReady = 0
        foreach ($color in @('blue', 'green')) {
            $readyText = & kubectl -n $Namespace get deployment "bing-task-$color" -o 'jsonpath={.status.readyReplicas}'
            if ($LASTEXITCODE -ne 0) { throw "读取 Task $color 就绪副本失败。" }
            $ready = 0
            if (-not [int]::TryParse([string]$readyText, [ref]$ready)) { $ready = 0 }
            $taskReady += $ready
        }
        if ($taskReady -lt 1) { throw 'Kubernetes 至少需要一个就绪的蓝/绿 Task 副本。' }
    }
    $selector = & kubectl -n $Namespace get service bing-web -o 'jsonpath={.spec.selector.color}'
    if ($LASTEXITCODE -ne 0 -or $selector -notin @('blue', 'green')) { throw 'Service selector is invalid.' }
}

if ($RequireConsumer -or $RequireTask) {
    Write-Output 'Live Web and requested background-role readiness passed. Long requests, active consumer/task drain, cutover, and rollback still require the runbook exercise.'
}
else {
    Write-Output 'Live Web readiness passed. Consumer readiness, long requests, active consumer drain, cutover, and rollback still require the runbook exercise.'
}
