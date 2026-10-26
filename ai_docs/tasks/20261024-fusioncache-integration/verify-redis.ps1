[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$ResultsRoot = "output/fusioncache-validation"
)

$ErrorActionPreference = "Stop"
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$container = "bing-fusioncache-" + [guid]::NewGuid().ToString("N").Substring(0, 12)
$variables = @("RUN_INTEGRATION_TESTS", "BING_TEST_FUSIONCACHE_REDIS", "BING_TEST_FUSIONCACHE_CONTAINER", "BING_TEST_FUSIONCACHE_RESULTS")
$original = @{}
foreach ($name in $variables) { $original[$name] = [Environment]::GetEnvironmentVariable($name, "Process") }
$created = $false

Push-Location $repositoryRoot
try {
    # 固定本次容器的宿主端口，stop/start 时不能重新分配随机端口。
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    $listener.Stop()
    & docker run --detach --name $container --publish "127.0.0.1:${port}:6379" redis:7.4-alpine redis-server --save "" --appendonly no
    if ($LASTEXITCODE -ne 0) { throw "Cannot start disposable Redis." }
    $created = $true
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $reply = & docker exec $container redis-cli ping 2>$null
        if ($LASTEXITCODE -eq 0 -and $reply -eq "PONG") { $ready = $true; break }
        Start-Sleep -Milliseconds 200
    }
    if (-not $ready) { throw "Redis readiness timed out." }

    $env:RUN_INTEGRATION_TESTS = "true"
    $env:BING_TEST_FUSIONCACHE_REDIS = "127.0.0.1:${port},abortConnect=false,connectTimeout=1000,syncTimeout=1000,asyncTimeout=1000"
    $env:BING_TEST_FUSIONCACHE_CONTAINER = $container
    $env:BING_TEST_FUSIONCACHE_RESULTS = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $ResultsRoot))
    foreach ($framework in @("net8.0", "net10.0")) {
        & dotnet test framework/tests/Bing.Caching.FusionCache.Tests.Integration/Bing.Caching.FusionCache.Tests.Integration.csproj --configuration $Configuration --framework $framework --verbosity quiet --logger "trx;LogFileName=fusioncache-redis-$framework.trx" --results-directory $ResultsRoot
        if ($LASTEXITCODE -ne 0) { throw "Redis validation failed for $framework." }
    }
}
finally {
    if ($created) {
        & docker rm --force $container
        if ($LASTEXITCODE -ne 0) { Write-Warning "Disposable Redis cleanup failed: $container" }
    }
    foreach ($name in $variables) { [Environment]::SetEnvironmentVariable($name, $original[$name], "Process") }
    Pop-Location
}
