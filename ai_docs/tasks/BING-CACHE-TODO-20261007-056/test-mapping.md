# 生产符号到测试方法映射

| 生产符号/行为 | 测试项目 | 测试方法或证据 |
|---|---|---|
| FreeRedis `ForEachKeyAsync` 批次后取消观察 | `Bing.Caching.FreeRedis.Tests.Integration` | `ScanDeleteBatchTest.RemoveByPrefixAsync_CancellationAfterFirstDelete_DoesNotDispatchNextDelete`，RESP 闸门捕获首个目标 `DEL` 后取消，net6/net8 通过 |
| FreeRedis 批量/流式读取、TTL、隔离、租约 | `Bing.Caching.FreeRedis.Tests.Integration` | `BatchContractTest`、`TtlCancellationRecoveryTest`、`NamespaceAndCoordinationTest`、`SharedCacheContractTest`；完整单节点门控 net6/net8 99 通过 |
| CSRedis `VisitAsync` 批次后取消观察 | `Bing.Caching.CSRedis.Tests.Integration` | `ScanDeleteBatchTest.RemoveByPrefixAsync_CancellationAfterFirstDelete_DoesNotDispatchNextDelete`，RESP 闸门捕获首个目标 `DEL` 后取消，net6/net8 通过 |
| CSRedis 批量/流式读取、TTL、隔离、租约 | `Bing.Caching.CSRedis.Tests.Integration` | `BatchContractTest`、`TtlCancellationRecoveryTest`、`NamespaceIsolationTest`、`SharedCacheContractTest`；完整单节点门控 net6/net8 97 通过 |
| 测试代理的 RESP 分帧、目标命令识别和连接清理 | `Bing.Test.Shared` | `RedisDeleteResponseGate` 被两个 Provider 集成测试实际驱动；共享项目 net6/net8 Release 构建通过 |
| EasyCaching Redis/Hybrid 批量、流式和回源协调 | `Bing.EasyCaching.Tests` | `RedisCoordinatorIntegrationTest`、`CacheManagerTests`、`Provider_SatisfiesSharedContract`；本任务未修改，沿用既有证据 |
| 生产构建兼容性 | 对应 `src` 项目 | 本任务未修改生产实现；受影响测试项目 Release 构建及 `git diff --check` 通过 |
