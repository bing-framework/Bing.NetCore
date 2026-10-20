[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$resultRoot = Join-Path $workspaceRoot 'artifacts/ai-tasks/BING-CACHE-TODO-20261005-004/test-results/gates'
New-Item -ItemType Directory -Path $resultRoot -Force | Out-Null
$filter = 'FullyQualifiedName~CoordinationBoundaryTest|FullyQualifiedName~SerializationBoundaryTest|FullyQualifiedName~PrefixScanCancellationTest'
$projects = @('Bing.Caching.FreeRedis.Tests.Integration','Bing.Caching.CSRedis.Tests.Integration')
foreach ($enabled in @($false,$true)) {
    $env:RUN_INTEGRATION_TESTS = $enabled.ToString().ToLowerInvariant()
    # 门控关闭时使用不可用的测试地址：若错误地运行了测试，应明确失败，绝不回退默认数据库。
    $env:ConnectionStrings__RedisConnection = if ($enabled) {'127.0.0.1:52800'} else {'127.0.0.1:52899,connectTimeout=200'}
    $mode = if ($enabled) {'enabled'} else {'disabled'}
    foreach ($project in $projects) {
        foreach ($framework in @('net8.0','net6.0')) {
            $projectPath = Join-Path $workspaceRoot "framework/tests/$project/$project.csproj"
            $logName = "$project-$framework-$mode.trx"
            & dotnet test $projectPath --no-restore --framework $framework --filter $filter --logger "trx;LogFileName=$logName" --results-directory $resultRoot
            if ($LASTEXITCODE -ne 0) { throw "Integration gate verification failed: $project $framework $mode" }
        }
    }
}
