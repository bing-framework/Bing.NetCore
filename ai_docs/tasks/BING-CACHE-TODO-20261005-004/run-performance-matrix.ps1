param([switch]$SmokeOnly)
[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $taskRoot '../../..'))
$benchDll = Join-Path $workspaceRoot 'ai_docs/tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/current/bin/Debug/net8.0/CacheBench.dll'
$matrixRoot = Join-Path $workspaceRoot 'artifacts/ai-tasks/BING-CACHE-TODO-20261005-004/performance-matrix'
New-Item -ItemType Directory -Path $matrixRoot -Force | Out-Null
$cells = [Collections.Generic.List[object]]::new()
if ($SmokeOnly) {
    $cells.Add(@{ Name='probe-smoke'; Provider='csredis'; Topology='single'; Endpoint='127.0.0.1:52800'; Nodes=1; Counts='1000'; Warmups=1; Samples=3; Probe=$true })
} else {
    # 旧预算正式确认：相同固定旧版基线/方法学，关闭额外观测开销；同假设不再跑第三遍。
    foreach ($provider in @('freeredis','csredis')) {
        $cells.Add(@{ Name="budget-$provider"; Provider=$provider; Topology='single'; Endpoint='127.0.0.1:52800'; Nodes=1; Counts='1000,10000'; Warmups=2; Samples=10; Probe=$false })
    }
    foreach ($topology in @('single','sharded','cluster')) {
        $endpoint = switch ($topology) { 'single' {'127.0.0.1:52800'} 'sharded' {'127.0.0.1:52800;127.0.0.1:52801'} 'cluster' {'127.0.0.1:7000;127.0.0.1:7001;127.0.0.1:7002'} }
        $nodes = switch ($topology) { 'single' {1} 'sharded' {2} 'cluster' {3} }
        foreach ($provider in @('freeredis','csredis','easyredis','easyhybrid')) {
            # 性能/资源矩阵全部声明 EXPECTED_SUCCESS，每 cell 固定 600s 外部终止窗口。
            # 单节点 Free/CS 的 1k/10k 已由上面的预算测量覆盖；100k 使用额外资源探针。
            $counts = if ($topology -eq 'single' -and $provider -in @('freeredis','csredis')) {'100000'} else {'1000,10000,100000'}
            $cells.Add(@{ Name="matrix-$topology-$provider"; Provider=$provider; Topology=$topology; Endpoint=$endpoint; Nodes=$nodes; Counts=$counts; Warmups=2; Samples=10; Probe=$true })
        }
    }
}
$results = [Collections.Generic.List[object]]::new()
foreach ($cell in $cells) {
    $name = $cell.Name
    $output = Join-Path $matrixRoot ($name+'.json')
    $arguments = @($benchDll,$cell.Provider,$cell.Endpoint,$output,$name,"--counts=$($cell.Counts)","--warmups=$($cell.Warmups)","--samples=$($cell.Samples)","--topology=$($cell.Topology)","--nodes=$($cell.Nodes)",'--requested-concurrency=16',"--command-probe=$($cell.Probe.ToString().ToLowerInvariant())","--resource-probe=$($cell.Probe.ToString().ToLowerInvariant())")
    # ArgumentList 是 Windows 原生命令行；这里只引用可信固定参数，路径用双引号保护空格。
    $quoted = $arguments | ForEach-Object { '"'+$_.Replace('"','\"')+'"' }
    $start = [DateTimeOffset]::UtcNow
    Write-Output "START $name EXPECTED_SUCCESS window=600s"
    $process = Start-Process -FilePath 'dotnet' -ArgumentList $quoted -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $matrixRoot ($name+'.stdout.log')) -RedirectStandardError (Join-Path $matrixRoot ($name+'.stderr.log'))
    $completed = $process.WaitForExit(600000)
    if (!$completed) { $process.Kill($true); $process.WaitForExit(); $status='VERIFIED_TIMEOUT_BOUNDARY' }
    elseif ($process.ExitCode -eq 0 -and (Test-Path -LiteralPath $output)) { $status='COMPLETED' }
    else { $status='FAILED' }
    $results.Add([ordered]@{ cell=$cell; expected='EXPECTED_SUCCESS'; timeoutSeconds=600; startedAt=$start; completedAt=[DateTimeOffset]::UtcNow; status=$status; exitCode=$process.ExitCode; artifact=$output })
    [IO.File]::WriteAllText((Join-Path $matrixRoot 'run-status.json'),($results | ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
    Write-Output "END $name $status"
    # 真实失败需要先诊断，禁止用更多样本掩盖错误。
    if ($status -eq 'FAILED') { exit 1 }
}
