param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('blue', 'green')]
    [string]$Target,
    [string]$Namespace = 'default'
)

$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$deployment = "bing-web-$Target"
& kubectl -n $Namespace rollout status "deployment/$deployment" --timeout=120s
if ($LASTEXITCODE -ne 0) { throw "Target $Target is not ready." }

$ready = & kubectl -n $Namespace get deployment $deployment -o 'jsonpath={.status.readyReplicas}'
if ($LASTEXITCODE -ne 0 -or [int]$ready -lt 1) { throw "Target $Target has no ready replicas." }

$old = & kubectl -n $Namespace get service bing-web -o 'jsonpath={.spec.selector.color}'
if ($LASTEXITCODE -ne 0 -or $old -notin @('blue', 'green')) { throw 'Current Service selector is invalid.' }

& kubectl -n $Namespace set selector service bing-web app=bing-web "color=$Target"
if ($LASTEXITCODE -ne 0) { throw 'Service patch failed.' }

$actual = & kubectl -n $Namespace get service bing-web -o 'jsonpath={.spec.selector.color}'
if ($LASTEXITCODE -ne 0 -or $actual -ne $Target) {
    & kubectl -n $Namespace set selector service bing-web app=bing-web "color=$old"
    throw 'Service selector verification failed; rollback attempted.'
}

Write-Output "Traffic now targets $Target. Keep bing-web-$old running until in-flight requests drain."
