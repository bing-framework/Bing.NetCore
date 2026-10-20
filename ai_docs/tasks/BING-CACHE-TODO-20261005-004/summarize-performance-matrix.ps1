[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$matrixRoot = Join-Path $workspaceRoot 'artifacts/ai-tasks/BING-CACHE-TODO-20261005-004/performance-matrix'
$status = Get-Content -LiteralPath (Join-Path $matrixRoot 'run-status.json') -Encoding UTF8 | ConvertFrom-Json
$rows = [Collections.Generic.List[object]]::new()
foreach ($cell in $status) {
    if ($cell.status -ne 'COMPLETED') {
        # 未完成 cell 仅列边界，不生成任何性能统计。
        $rows.Add([pscustomobject][ordered]@{
            Cell=$cell.cell.Name; Provider=$cell.cell.Provider; Topology=$cell.cell.Topology
            Scale=$cell.cell.Counts; Status=$cell.status; Phase=''; P50Ms=$null; P95Ms=$null
            P99Ms=$null; AllocatedMeanBytes=$null; RedisCommandsMean=$null; NativeCallsMean=$null
            ActualParallelismP95=$null; ObservedQueuedRequestsP95=$null; NetworkRequestsMean=$null
            SampledPhasePeakBytesP95=$null; RequestedConcurrency=$null; RetrieverCallsMean=$null
            ThroughputItemsPerSecondMean=$null; ConcurrencyScope=''
        })
        continue
    }
    # 状态文件保留生成时的路径；归档后按原文件名读取同目录证据。
    $reportPath = Join-Path $matrixRoot (Split-Path $cell.artifact -Leaf)
    $report = Get-Content -LiteralPath $reportPath -Encoding UTF8 | ConvertFrom-Json
    foreach ($result in $report.results) {
        foreach ($phaseName in @('hit','batchRead','batchReadStream','scan','scanStream','remove','write','stampede')) {
            $phase = $result.phases.$phaseName
            if ($null -eq $phase) { continue }
            # 命中延迟采用单次请求分布；其他阶段采用操作样本分布。
            $latency = if ($phaseName -eq 'hit') {$result.hitRequestLatencyMs} else {$phase.elapsedMs}
            $rows.Add([pscustomobject][ordered]@{
                Cell=$cell.cell.Name; Provider=$cell.cell.Provider; Topology=$cell.cell.Topology
                Scale=$result.count; Status=$cell.status; Phase=$phaseName
                P50Ms=$latency.p50; P95Ms=$latency.p95; P99Ms=$latency.p99
                AllocatedMeanBytes=$phase.allocatedBytes.mean; RedisCommandsMean=$phase.redisCommands.mean
                NativeCallsMean=$phase.nativeClientCalls.mean; ActualParallelismP95=$phase.maxActualParallelism.p95
                ObservedQueuedRequestsP95=$phase.maxQueuedRequests.p95; NetworkRequestsMean=$phase.networkRequests.mean
                SampledPhasePeakBytesP95=$phase.sampledPhasePeakWorkingSetBytes.p95
                RequestedConcurrency=$phase.requestedConcurrency.mean; RetrieverCallsMean=$phase.retrieverCalls.mean
                ThroughputItemsPerSecondMean=$phase.throughputItemsPerSecond.mean
                ConcurrencyScope=$phase.concurrencyMeasurement
            })
        }
    }
}
$rows | Export-Csv -LiteralPath (Join-Path $matrixRoot 'summary.csv') -Encoding UTF8 -NoTypeInformation
Write-Output "Summarized $($status.Count) cells / $($rows.Count) rows; unfinished cells have no statistics."
