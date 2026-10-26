# FusionCache 本地性能与资源测量

环境：Windows 本机、SDK 10.0.400、.NET 8.0.30 / 10.0.11、Docker Redis 7.4 Alpine，回环网络，Release 构建，Review Fix Round 1。复现命令：`pwsh -NoProfile -File ai_docs/tasks/20261024-fusioncache-integration/verify-redis.ps1 -Configuration Release`。本轮 TRX 与原始 JSON 位于 `output/fusioncache-validation/review-fix-1/`。

工作负载：每种命中场景先预热 20 次；L1 读取 2000 次、L2 读取 200 次，均顺序执行；同键和不同键冷启动各并发 64 次，业务工厂含 5ms 可取消延迟。故障场景停启临时 Redis，验证前台失败、旧值后备和恢复队列重放。每个 TFM 仅测量一轮，不预设提升比例。

延迟为每个请求 Stopwatch 测量；分配为整个进程 GC.GetTotalAllocatedBytes 的场景差值，含测试框架和后台活动，不是精确单次 API 分配。工厂/队列峰值来自实际 Meter 事件。故障恢复只有一个样本，延迟包含容器停止、错误等待、重启和恢复；P50/P95/P99 留空，不作为统计分布。此结果不是正式 BenchmarkDotNet 基线、生产容量或跨 Provider 优劣结论。

资源采样新增进程累计 CPU 时间差，以及 WorkingSet 起始、结束和峰值；峰值由独立 10ms 定时采样取得，因此短于采样间隔的瞬时峰值可能漏采。它们是测试进程级数据，若测试进程中有并行工作，也会包含其他工作的资源消耗。P50/P95/P99 在样本少于 2 时写为 null；单样本只保留 elapsed 和 mean，不伪装为分布统计。Hit、Miss、Fail-Safe 次数来自 FusionCache 原生事件，只统计带 `:d:` 边界的数据键，不按请求数推算；准入等待直方图来自 `bing.cache.factory.wait.duration`。指标维度不得携带业务 key/tag。FusionCache 原生活动可能包含编码后的 key；宿主在导出 trace 前需按自身敏感数据策略清理。

以下为本轮实际测量。总耗时包含采样器停止和测试活动；CPU 是全进程多线程累计时间，可能大于墙钟耗时。WorkingSet 是进程驻留量而非缓存载荷；命中/未命中采用同步原生事件计数，排除内部元数据。

| Runtime | 场景 | 样本 | 总耗时 ms | P50 / P95 / P99 μs | 分配 bytes | CPU ms | WorkingSet 起/终/峰 MiB | Hit/Miss/Fail-Safe | 回源 | 活跃/排队峰值 |
| --- | --- | ---: | ---: | --- | ---: | ---: | --- | --- | ---: | --- |
| .NET 8.0.30 | l1-hit | 2000 | 31.72 | 2.60 / 5.60 / 6.90 | 2876552 | 31.25 | 67.11 / 70.14 / 70.14 | 2000/0/0 | 0 | 0/0 |
| .NET 8.0.30 | l2-hit | 200 | 99.37 | 394.20 / 610.30 / 741.80 | 866800 | 171.88 | 72.20 / 73.12 / 73.12 | 200/0/0 | 0 | 0/0 |
| .NET 8.0.30 | same-key-cold | 64 | 31.82 | 11941.30 / 12057.00 / 12553.80 | 214224 | 31.25 | 74.04 / 74.39 / 74.39 | 63/1/0 | 1 | 1/0 |
| .NET 8.0.30 | different-keys-cold | 64 | 144.96 | 66692.50 / 126750.80 / 126774.20 | 1061976 | 109.38 | 75.50 / 76.84 / 77.78 | 0/64/0 | 64 | 8/56 |
| .NET 8.0.30 | redis-fault-recovery | 1 | 4491.10 | — / — / — | 9423560 | 2250.00 | 83.57 / 98.72 / 100.23 | 1/0/1 | 1 | 1/0 |
| .NET 10.0.11 | redis-fault-recovery | 1 | 4764.10 | — / — / — | 8156000 | 3671.88 | 90.63 / 100.72 / 102.23 | 1/0/1 | 1 | 1/0 |
| .NET 10.0.11 | l1-hit | 2000 | 23.30 | 2.20 / 3.20 / 5.80 | 2858168 | 15.62 | 97.71 / 97.71 / 97.71 | 2000/0/0 | 0 | 0/0 |
| .NET 10.0.11 | l2-hit | 200 | 95.57 | 363.40 / 568.20 / 675.50 | 862576 | 140.62 | 97.74 / 97.76 / 97.76 | 200/0/0 | 0 | 0/0 |
| .NET 10.0.11 | same-key-cold | 64 | 38.07 | 14524.20 / 14614.10 / 15070.30 | 197184 | 15.62 | 97.88 / 98.09 / 98.09 | 63/1/0 | 1 | 1/0 |
| .NET 10.0.11 | different-keys-cold | 64 | 143.43 | 62995.40 / 124166.80 / 124183.70 | 1041552 | 140.62 | 98.74 / 99.07 / 100.59 | 0/64/0 | 64 | 8/56 |

原始数据：[net8](../../../artifacts/ai-tasks/20261024-fusioncache-integration/performance-net8.json)、[net10](../../../artifacts/ai-tasks/20261024-fusioncache-integration/performance-net10.json)。历史基线没有 IHybridCache/FusionCache 同构实现，before/after 对照为 NOT_APPLICABLE。生产负载容量与正式外部性能门禁为 NOT_VERIFIED。

## 后续优化：只读 L1 热路径

2026-10-24，沿用 Review 3 的纯 L1 探针与环境：Windows、Release、.NET 8.0.30 / 10.0.11；每个方法预热 10,000 次，测量 20,000 次；键为 hot，值为 int 42，关闭外部遥测监听与 Redis。每次断言同步完成并返回正确值，使用 GC.GetAllocatedBytesForCurrentThread 统计分配。原始 before 文件保留不覆盖。

| Runtime | API | Before B/op | After B/op | 减少 | Before / After ns/op |
| --- | --- | ---: | ---: | ---: | ---: |
| .NET 8.0.30 | adapter.TryGetAsync | 424 | 208 | 216（50.94%） | 1772.14 / 1512.52 |
| .NET 10.0.11 | adapter.TryGetAsync | 424 | 208 | 216（50.94%） | 2071.72 / 1766.99 |
| .NET 8.0.30 | adapter.GetOrCreateAsync | 608 | 608 | 0 | 1622.97 / 2128.34 |
| .NET 10.0.11 | adapter.GetOrCreateAsync | 608 | 608 | 0 | 1971.55 / 1947.38 |

本轮仅减少 TryGetAsync 每次复制 FusionCacheEntryOptions 的分配，改为私有只读快照。CacheEntry<T> 是值类型；其包装本身不能视为独立堆对象。结果仍有 Task、键编码及原生调用等开销，不是零分配。GetOrCreateAsync 仍按次复制选项并建立工厂闭包，保留配置、提前刷新及回源隔离；未增加一次预读来规避这些开销，以免在 miss 时增加缓存读取或改变原生事件和刷新行为。

单轮短探针的延迟受 JIT、调度和噪声影响，尤其原生对照也发生波动；只把稳定的分配差异作为本轮优化结论，不据此承诺吞吐或生产响应时间提升。没有批准正式性能阈值；未改动冷启动准入算法，复用前文冷启动数据，不重建该基线。

复现：`dotnet run --project output/fusioncache-review-round3/ReviewProbe.csproj -c Release -f <net8.0|net10.0>`。探针源码：[Program.cs](../../../output/fusioncache-review-round3/Program.cs)。Before：[net8](../../../output/fusioncache-review-round3/probe-net8.0.log)、[net10](../../../output/fusioncache-review-round3/probe-net10.0.log)；After：[net8](../../../output/fusioncache-optimization/probe-net8.0.log)、[net10](../../../output/fusioncache-optimization/probe-net10.0.log)。当前候选为本轮 FusionCacheManager 私有读取快照实现，文件指纹与依赖版本记录在 output/fusioncache-optimization/candidate.json。
