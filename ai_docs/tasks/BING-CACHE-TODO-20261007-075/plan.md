# BING-CACHE-TODO-20261007-075：EasyCaching 分片异步清理有界并发

## 目标

关闭客户端分片清理路径中仍可在本地验证的异步串行瓶颈。`ShardedRedisCacheManager` 的 `RemoveByPrefixAsync`、`RemoveByPatternAsync` 和 `ClearAsync` 目前逐个等待分片，无法使用配置中的 `MaxConcurrency`。本轮为这些异步 API 增加有界分片调度，同时保留同步清理 API 的既有顺序和同步 Provider 调用语义。

## 现状与边界

- 每个分片的 `CacheManager` 已具备异步 SCAN/KeysAsync、批量删除、取消和 Hybrid Redis 协调写回。
- `CacheScanBudget` 已支持并发安全的候选键/扫描页计数以及真实分片完成/失败诊断。
- 分片操作已经在开始前和成功后校验 Redis 主节点身份；失败或取消时不伪造完整成功。
- 删除是扫描后批量操作，不是快照或跨分片事务。部分分片可能已经完成，失败时保留首个可观察异常。
- Hybrid 每个分片独立执行本地层、Redis 层和失效通知；本轮不新增跨分片消息顺序保证。
- 同步 `RemoveByPrefix`、`RemoveByPattern`、`Clear` 保持顺序执行，避免把现有同步 Provider 异常路径改成伪异步或线程池阻塞。

## 实现设计

1. 在 `ShardedRedisCacheManager` 增加共享的异步分片扫描调度器：按 `MaxConcurrency` 分窗派发，每个任务绑定分片上下文，完成/失败写入同一个 `CacheScanBudget`。
2. 调度器使用链接取消令牌。调用方取消或任一分片失败后停止派发后续分片，等待并观察已经派发的任务，再传播原始取消/异常。
3. 三个异步清理 API 复用调度器，保留现有前缀/模式验证、空输入快速返回、模式字面量快速路由和 `RunScanAsync` 的拓扑收尾校验。
4. 保留现有同步清理实现及其 `RunScan` 路径；不新增 `Task.Run`，不改变 Hybrid 的批量失效通知和失败传播。

## 测试与验收

- 分片清理异步路径在 `MaxConcurrency=2` 时能同时进入两个分片，且不超过配置上限。
- 取消只停止后续分片，已经派发的扫描/删除任务被观察；尾部身份探测不执行。
- 单个分片失败后其余已派发任务被观察并传播原始异常；共享预算和失败分片诊断不失真。
- 前缀、通配模式和 Clear 三条路径至少各有一个直接回归覆盖；现有同步失败、预算、Hybrid 失效测试保持通过。
- 运行 L0、`ShardedScanCancellationUnitTest` 精准测试、`Bing.EasyCaching.Tests` 双 TFM 完整单元、受影响生产项目 netstandard2.0 构建；实机双节点门禁仍按外部阻塞记录。

## 不在本轮

- 同步清理 API 并发化；需要单独定义同步调用的并发和异常契约。
- 跨分片删除顺序、跨分片事务、Hybrid 总线背压/重试/outbox。
- 真实双节点、Cluster、网络队列和正式性能预算。
