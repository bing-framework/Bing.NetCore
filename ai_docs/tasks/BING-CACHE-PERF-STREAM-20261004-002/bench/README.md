# 缓存批量路径基准

本目录提供旧版/当前版使用同一份 `Program.cs` 的可审计基准工程。`baseline` 工程通过 `eng/baselines.props` 引用仓库外共享的固定历史基线并定义 `BASELINE`，`current` 工程引用当前工作树。两份工程的运行参数、Redis 端点、运行时和样本策略必须保持一致。

JSON 输出和 checkpoint 统一保存在 `artifacts/ai-tasks/<任务ID>/bench/`，不提交到 Git；旧文件已按原相对路径归档，详见[任务产物归档说明](../../ARTIFACTS.md)。

两个缓存基准任务共用提交 `24c93cb1a5355797223637e2d42ac3dce968d7ef`。在仓库根目录运行以下脚本准备基线；脚本返回/输出基线绝对路径，默认目录为仓库外相邻的 `H:/Bing_Framework/Bing.NetCore.baselines/24c93cb1a5355797223637e2d42ac3dce968d7ef`：

```powershell
./eng/ci/prepare-baseline.ps1 -Commit 24c93cb1a5355797223637e2d42ac3dce968d7ef
```

基准工程不把基线源码复制进 `ai_docs/`，也不自动移除共享基线目录。默认引用可用 `-p:BaselineStore=<统一目录>` 或 `-p:BaselineSourceRoot=<源码目录>` 覆盖；确认两个任务及后续复核均完成后，再由维护者显式清理共享目录。

## 采样方法

- 默认规模为 `1000,10000,100000` 项。
- 每个规模先执行 2 轮预热，随后执行 10 轮正式样本；预热不写入分布统计。
- 每轮按顺序测量批量写入、命中读取、批量读取、前缀扫描、同键回源和删除。
- 每个阶段记录耗时、`GC.GetTotalAllocatedBytes` 分配量和 Redis `INFO stats.total_commands_processed` 增量；`networkRequests` 在当前客户端没有公开请求计数时写 `-1`，并保留该限制，不将 Redis 命令数冒充网络往返数。
- 同键回源记录请求并发数、回源函数最大实际并发数、最大排队请求数和回源函数调用次数。
- 命中读取按请求记录延迟；所有阶段和命中请求分别计算 P50/P95/P99 与平均值。分位数采用最近秩，样本数较小时 P99 只作为描述性数据。
- 同一个 Redis 端点只运行一个基准进程，避免其它流量污染命令计数。每个样本使用唯一命名空间或唯一键前缀，轮次结束清理。
- `RawSamples` 保留每轮阶段数据，便于复核汇总值；输出 JSON 使用 UTF-8 无 BOM。
- 默认在最终输出路径追加 `.checkpoint.json`，也可通过 `--checkpoint-path=<path>` 指定。每个正式样本完成后原子更新一次检查点，记录 `datasetIdentity`、当前规模、已完成样本和原始样本；进程被终止时此前已完成的规模仍可审计。检查点的 `status=RUNNING` 或包含未完成规模时不是完整正式报告，不能直接用于预算结论；正常结束后会写为 `status=COMPLETE`。
- checkpoint 现在同时保存稳定的 `workloadIdentity`/`datasetIdentity` 和本次执行唯一的 `runIdentity`。同一 workload 的重复执行拥有相同数据集身份，但可以通过执行身份区分。每个 warmup/sample 在开始 Redis 操作前记录实际 `namespace`、`logicalPrefix`、`physicalPattern` 和状态；`cleanupStatus=CLEANED` 只在该轮 `ClearAsync` 成功后写入。进程在清理前终止时，最后一个 `RUNNING/PENDING` round 可直接用于定位残留键，不能把未完成 checkpoint 当作正式性能报告。
- 报告和 checkpoint 的 `endpoint` 只保存脱敏摘要，并另存 `endpointFingerprint`；连接串密码不会写入基准产物。`rawSamples` 仍保留每轮阶段数据，输出 JSON 使用 UTF-8 无 BOM。
- 当前版可选 `--allocation-components=true` 增加单节点整数值诊断阶段：`keyFormat`、`rawMGet`、`typedMGet`、`decodeRaw`、`decodeJsonBatch`、`keyParse`。该选项只用于分配归因，不纳入旧版/当前版正式预算对照，默认关闭。
- 可选 `--scan-noise-count=<非负整数>` 在每轮正式计时前写入不匹配前缀的噪声键。目标键仍使用 `item:`（历史基线使用带命名空间的等价前缀），噪声键使用 `noise:`，因此 `scan`/`scanStream` 只应返回目标项。噪声准备不计入 `write` 阶段，但会保留在 checkpoint 的 `noiseLogicalPrefix`/`noiseCount` 和 workload identity 中；默认值为 `0`，密集场景与既有基准保持一致。

## 运行命令

以下命令定义正式重复采样流程。本轮使用固定历史提交基线，并在隔离 Redis 运行了单节点和双节点分片的 100 项、1 轮预热、1 轮样本 smoke；该结果不用于性能预算。端点参数使用分号分隔节点，例如 `127.0.0.1:52782;127.0.0.1:52783`。`--nodes` 必须与端点数量一致。

```powershell
./eng/ci/prepare-baseline.ps1 -Commit 24c93cb1a5355797223637e2d42ac3dce968d7ef
New-Item -ItemType Directory -Force artifacts/ai-tasks/BING-CACHE-PERF-STREAM-20261004-002/bench
dotnet restore ai_docs/tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/baseline/CacheBench.csproj --ignore-failed-sources -p:NuGetAudit=false
dotnet restore ai_docs/tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/current/CacheBench.csproj --ignore-failed-sources -p:NuGetAudit=false
dotnet run --project ai_docs/tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/baseline/CacheBench.csproj --no-restore -- freeredis 127.0.0.1:52782 artifacts/ai-tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/baseline-freeredis-v3.json baseline-v3 --counts=1000,10000,100000 --warmups=2 --samples=10 --requested-concurrency=16
dotnet run --project ai_docs/tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/current/CacheBench.csproj --no-restore -- freeredis 127.0.0.1:52782 artifacts/ai-tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/current-freeredis-v3.json current-v3 --counts=1000,10000,100000 --warmups=2 --samples=10 --requested-concurrency=16
```

稀疏前缀扫描对照应在 baseline/current 使用相同的 `--scan-noise-count`，例如目标 1,000 项、额外 9,000 个无关键：

```powershell
dotnet run --project ai_docs/tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/baseline/CacheBench.csproj --no-restore -- freeredis 127.0.0.1:52782 artifacts/ai-tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/sparse-baseline-freeredis-1k.json sparse-baseline-freeredis --counts=1000 --scan-noise-count=9000 --warmups=2 --samples=10 --requested-concurrency=16
dotnet run --project ai_docs/tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/current/CacheBench.csproj --no-restore -- freeredis 127.0.0.1:52782 artifacts/ai-tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/sparse-current-freeredis-1k.json sparse-current-freeredis --counts=1000 --scan-noise-count=9000 --warmups=2 --samples=10 --requested-concurrency=16
```

该参数只改变扫描数据分布，不改变目标返回数量；报告中的正式预算仍应使用相同运行时、端点、预热、样本和噪声数量比较。噪声数量为零时，结果不能说明服务端 `MATCH` 在稀疏命名空间中的收益。

将两条命令中的 `freeredis` 替换为 `csredis`，并使用不同的输出文件，即可运行 CSRedis。当前版还可以运行 EasyCaching Redis 和 Hybrid；两者使用当前 benchmark 工程的 `Bing.EasyCaching` ProjectReference，配置为单节点 Redis：

```powershell
dotnet run --project ai_docs/tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/current/CacheBench.csproj --no-restore -- easyredis 127.0.0.1:52782 artifacts/ai-tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/current-easyredis-v1.json current-easyredis-v1 --counts=1000,10000,100000 --warmups=2 --samples=10 --requested-concurrency=16
dotnet run --project ai_docs/tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/current/CacheBench.csproj --no-restore -- easyhybrid 127.0.0.1:52782 artifacts/ai-tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/current-easyhybrid-v1.json current-easyhybrid-v1 --counts=1000,10000,100000 --warmups=2 --samples=10 --requested-concurrency=16
```

`easyredis` 测量 EasyCaching Redis Provider；`easyhybrid` 测量 Hybrid Provider 的本地层、Redis 层和失效总线组合。两者的单节点命令使用 `--topology=single --nodes=1`，多节点命令见下文。锁定历史提交中不存在 `Bing.EasyCaching` 工程及等价 Hybrid/stream API，因此 baseline 对这两个 provider 为 `NOT_APPLICABLE`，只报告 current 绝对指标。`--samples`、`--warmups`、`--hit-reads` 和 `--retriever-delay-ms` 必须在可比较的 baseline/current 场景中保持一致。100k 样本若遇到预先批准的资源或时间边界，应保留进程退出状态和原始日志，不填入虚构的 P95/P99 或分配量。

当前版 EasyCaching 也支持客户端分片和 Redis Cluster。客户端分片的每个端点代表一个独立 Redis 节点，必须使用 `--topology=sharded --nodes` 并提供相同数量的分号分隔端点：

```powershell
# EasyCaching Redis 客户端分片。
dotnet run --project ai_docs/tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/current/CacheBench.csproj --no-restore -- easyredis "127.0.0.1:52782;127.0.0.1:52783" artifacts/ai-tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/current-sharded-easyredis.json current-sharded-easyredis --topology=sharded --nodes=2 --counts=1000,10000 --warmups=2 --samples=10 --requested-concurrency=16

# EasyCaching Hybrid 客户端分片。
dotnet run --project ai_docs/tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/current/CacheBench.csproj --no-restore -- easyhybrid "127.0.0.1:52782;127.0.0.1:52783" artifacts/ai-tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/current-sharded-easyhybrid.json current-sharded-easyhybrid --topology=sharded --nodes=2 --counts=1000,10000 --warmups=2 --samples=10 --requested-concurrency=16
```

客户端分片 Hybrid 的失效总线使用第一个分片端点；该端点必须对所有运行基准的进程可达。它是明确的 Pub/Sub 总线位置，不是分片故障转移地址；需要总线高可用时，应在隔离环境中提供独立、可故障转移的 Redis 总线端点，并改用相应的生产注册方式。

Redis Cluster 使用完整的 Cluster 主节点种子列表。Redis Provider 和 Hybrid 的总线都通过完整端点集合构造 `StackExchange.Redis.ConfigurationOptions`，不把 Cluster 模拟成客户端分片：

```powershell
# EasyCaching Redis Cluster；端点应为隔离 Cluster 的主节点种子。
dotnet run --project ai_docs/tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/current/CacheBench.csproj --no-restore -- easyredis "127.0.0.1:7000;127.0.0.1:7001;127.0.0.1:7002" artifacts/ai-tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/current-cluster-easyredis.json current-cluster-easyredis --topology=cluster --nodes=3 --counts=1000,10000 --warmups=2 --samples=10 --requested-concurrency=16

# EasyCaching Hybrid Cluster；Redis 失效总线也使用完整 Cluster 种子列表。
dotnet run --project ai_docs/tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/current/CacheBench.csproj --no-restore -- easyhybrid "127.0.0.1:7000;127.0.0.1:7001;127.0.0.1:7002" artifacts/ai-tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/current-cluster-easyhybrid.json current-cluster-easyhybrid --topology=cluster --nodes=3 --counts=1000,10000 --warmups=2 --samples=10 --requested-concurrency=16
```

历史基线工程仍只接受单节点参数；上述 EasyCaching 分片和 Cluster 命令仅适用于当前版，不能把单节点 baseline 复制成多拓扑 before 数据。多节点命令的端点数量必须与 `--nodes` 一致，且只能使用明确隔离的测试环境。

FreeRedis/CSRedis 当前版还支持以下多节点运行方式：

```powershell
# 客户端分片：两个独立 Redis 主节点，FreeRedis/CSRedis 都按键路由。
dotnet run --project ai_docs/tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/current/CacheBench.csproj --no-restore -- freeredis "127.0.0.1:52782;127.0.0.1:52783" artifacts/ai-tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/current-sharded-freeredis.json current-sharded-freeredis --topology=sharded --nodes=2 --counts=1000,10000 --warmups=2 --samples=10 --requested-concurrency=16

# Redis Cluster：端点应为 Cluster 主节点种子；需要三主节点以上的隔离 Cluster。
dotnet run --project ai_docs/tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/current/CacheBench.csproj --no-restore -- csredis "127.0.0.1:7000;127.0.0.1:7001;127.0.0.1:7002" artifacts/ai-tasks/BING-CACHE-PERF-STREAM-20261004-002/bench/current-cluster-csredis.json current-cluster-csredis --topology=cluster --nodes=3 --counts=1000,10000 --warmups=2 --samples=10 --requested-concurrency=16
```

历史基线工程只接受单节点参数；分片和 Cluster 没有对应的旧版构造路径，因此会显式报“不适用”，不会把单节点结果复制到多节点报告。当前版的多节点命令计数由每个指定节点的 `INFO stats.total_commands_processed` 增量求和，并在报告中写入 `monitoredNodeCount`。`networkRequests` 仍为 `-1`，因为两个客户端没有公开可靠的网络往返计数器，不能用 Redis 命令数代替网络请求数。批量、写入、命中和扫描阶段只能从探针外部观察到一次 API 调用，无法观测 Provider 内部任务并发，因此这些阶段的 `maxActualParallelism/maxQueuedRequests` 为 `-1`，并标记 `API_CALL_ONLY_INTERNAL_PROVIDER_UNAVAILABLE`；同键回源阶段单独记录真实的并发请求和回源函数并发，并标记 `REQUEST_AND_FACTORY_OBSERVED`。

## 流式 API 状态

当前工作树已经提供 `CacheEntry<T>`、`GetAllStreamAsync` 和 `GetByPrefixStreamAsync`。当前版探针实际执行 `batchReadStream`、`scanStream` 阶段，并在运行后写入 `MEASURED_WHEN_RUN`；历史版没有流式 API，标记为 `NOT_APPLICABLE_BASELINE_API_MISSING`。正式对照应保留 List 阶段，并分别报告新增流式能力的绝对指标。

## 输出字段

顶层包含任务、版本标签、Provider、运行时、操作系统、脱敏端点、端点指纹、参数、拓扑、节点数、命令监控节点数、执行身份、稳定 workload/dataset 身份、round 清单和流式 API 状态。每个规模包含预热次数、样本数、阶段汇总、命中请求分布、工作集和全部 `rawSamples`。每个阶段的汇总字段为：

`elapsedMs`、`throughputItemsPerSecond`、`allocatedBytes`、`redisCommands`、`nativeClientCalls`、`commandBreakdown`、`commandMaximumParallelism`、`scanPages`、`scanReturnedItems`、`networkRequests`、`requestedConcurrency`、`maxActualParallelism`、`maxQueuedRequests`、`retrieverCalls` 和 `concurrencyMeasurement`；其中 `commandBreakdown` 按 `Unknown`、`SingleRead`、`BatchRead`、`Scan` 记录已接入探针的逻辑调用次数，`commandMaximumParallelism` 记录同一阶段内各逻辑调用类别的最大重叠数，`scanPages` 记录显式 Redis 客户端报告的 SCAN 页数，`scanReturnedItems` 记录扫描返回的物理键数量。EasyCaching 等隐藏底层分页的 Provider 将 `scanPages` 标为 `-1`，这些字段都不能解释为网络往返、连接池排队或 Redis 服务端实际遍历总量。数值字段都包含 `samples`、`p50`、`p95`、`p99` 和 `mean`。`rawSamples` 保留每轮的同名原始字段。
