# BING-CACHE-TODO-20261007-074：EasyCaching 分片前缀读取有界并发

## 目标

优化 `ShardedRedisCacheManager` 的前缀读取路径。当前 `GetByPrefix`、`GetByPrefixAsync` 和 `GetByPrefixStreamAsync` 按分片顺序执行，未使用配置中的 `MaxConcurrency`。本轮只处理前缀读取，采用有界窗口和共享线程安全扫描预算；删除、模式清理和 Clear 保持现有顺序语义，另列后续设计。

## 现状与证据

- `framework/src/Bing.EasyCaching/Bing/EasyCaching/ShardedRedisCacheManager.cs` 的三个前缀读取方法在 `for (shard = 0; ...)` 中逐个调用内部 `CacheManager`。
- 所有分片共享一个 `CacheScanBudget`，但当前 `Count`、`PageCount`、`Consume` 和分片进度字段没有同步保护，不能安全地直接并发。
- `GetByPrefixStreamAsync` 的公共文档没有承诺前缀结果顺序；`GetAllStreamAsync` 的输入顺序契约不受本轮影响。因此流式前缀结果可以按分片扫描完成顺序交错，仍保持逐项消费和取消语义。
- 现有分片批量读取已经使用 `MaxConcurrency` 窗口和已派发任务观察，可复用其错误/取消处理方式。

## 修改范围

### 生产代码

- `CacheScanBudget` 增加锁、原子候选键/页计数、分片上下文作用域和已完成分片快照；保留现有顺序调用的 `SetShardProgress` 兼容行为。
- `CacheScanLimitExceededException` 增加内部按实际分片索引列表构造的路径，避免并发时把非连续分片误报为已完成。
- `ShardedRedisCacheManager` 增加有界分片前缀 List 调度：每个窗口最多 `MaxConcurrency` 个分片任务，完成后按分片索引合并 List，保持现有 List 的稳定分片顺序。
- `GetByPrefixStreamAsync` 增加有界交错调度：每个分片保持一个在途 `MoveNextAsync`，以完成顺序产出条目，消费速度通过在途任务数量自然背压；取消或首个异常时取消并观察其它任务。
- 同步 `GetByPrefix<T>` 桥接到异步读取实现，避免同步路径继续逐分片串行 MGET/SCAN；不使用 `Task.Run`。
- 不修改 `GetAll` 输入顺序、写入、删除、租约、物理键格式或 Hybrid 总线语义。

### 测试与证据

- 更新分片扫描单元测试，验证共享预算在并发前缀读取下仍只允许全局上限、失败分片诊断有效、提前取消不执行尾部身份探测。
- 保留 EasyCaching Redis/Hybrid 双节点集成测试门控；无两个非生产 Redis 节点时只记录跳过，不宣称真实跨节点并发。
- 更新权威 TODO 索引，将“前缀读取并发”关闭，并将清理扫描并发另列为延后设计。

## 实施步骤

1. 建立 074 任务状态、TODO、执行记录和测试映射。
2. 改造公共扫描预算和异常快照，先补预算单元测试。
3. 实现分片前缀 List 的有界并发和失败观察。
4. 实现分片前缀 Stream 的交错消费、取消和枚举器清理。
5. 将同步前缀读取桥接到异步实现，补回归断言。
6. 运行 L0、EasyCaching 精准单元测试、受影响项目构建和门控集成测试。
7. 更新索引和任务收口，记录真实多节点外部边界。

## 风险与边界

- 前缀 Stream 不保证跨分片顺序；这是接口当前未承诺的行为，调用方需要按键或值自行排序。
- 共享预算达到上限时，已派发的 Redis 命令可能完成；取消只阻止后续命令，不能撤回已发送请求。
- 并发扫描仍不是快照，可能受并发写入影响；失败时只报告首个可观察异常，不宣称跨分片事务性。
- 清理操作仍按分片顺序执行，避免本轮同时改变删除部分成功和异常语义。

## 验收标准

- EasyCaching 前缀 List/Stream 实际使用 `MaxConcurrency`，不再固定逐分片等待。
- `MaxScanKeys` 和 `MaxScanPages` 在并发任务下不会超额消费；异常的 `CompletedShards`/`FailedShard` 反映真实索引。
- List 结果保持既有分片顺序；Stream 保持逐项产出、取消和异常传播，不积累完整全局结果。
- 受影响项目构建无警告/错误，单元测试通过；真实双节点证据仍按环境门控分类。
