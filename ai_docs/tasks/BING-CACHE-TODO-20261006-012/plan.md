# 前缀扫描流取消证据计划

## 目标

补齐 FreeRedis 和 CSRedis `GetByPrefixStreamAsync` 在消费首批结果后取消的真实 Redis 命令证据，确认取消会停止下一批 MGET 派发。该证据覆盖上一轮流式能力计划中的取消语义，不重复已达到上限的性能实验。

## 范围

- 修改 `framework/tests/Bing.Caching.FreeRedis.Tests.Integration/CommandCancellationTest.cs`。
- 修改 `framework/tests/Bing.Caching.CSRedis.Tests.Integration/CommandCancellationTest.cs`。
- 更新本任务 TODO、执行记录和生产符号到测试映射。

## 验收标准

1. 每个 Provider 使用 `batchSize=2` 和至少四个同前缀键。
2. 消费首个流元素后确认首个 MGET 已由 Redis MONITOR 观察到，再取消令牌。
3. 下一次 `MoveNextAsync` 抛出 `OperationCanceledException`。
4. 通过屏障命令等待 MONITOR 消费完成，匹配该命名空间的 MGET 数量严格为 1。
5. net6.0/net8.0 精准集成测试在 Redis 门控开启时通过；门控关闭时保持跳过，不伪造通过。

## 非目标

- 不改变生产实现、批大小、扫描一致性或性能预算。
- 不运行 Cluster、100k 矩阵或历史 baseline。
