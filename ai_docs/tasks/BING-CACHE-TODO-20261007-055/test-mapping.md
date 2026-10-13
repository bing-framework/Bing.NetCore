# 生产符号到测试方法映射

| 生产符号/行为 | 测试项目 | 测试方法或证据 |
|---|---|---|
| FreeRedis `ForEachKeyAsync` 批次后取消观察 | `Bing.Caching.FreeRedis.Tests.Integration` | `ScanDeleteBatchTest.RemoveByPrefixAsync_CancellationAfterFirstDelete_DoesNotDispatchNextDelete`（当前受 MONITOR 时序竞态影响） |
| FreeRedis 批量/流式读取、TTL、隔离、租约 | `Bing.Caching.FreeRedis.Tests.Integration` | `BatchContractTest`、`TtlCancellationRecoveryTest`、`NamespaceAndCoordinationTest`、`SharedCacheContractTest` |
| CSRedis `VisitAsync` 批次后取消观察 | `Bing.Caching.CSRedis.Tests.Integration` | `ScanDeleteBatchTest.RemoveByPrefixAsync_CancellationAfterFirstDelete_DoesNotDispatchNextDelete`（当前受 MONITOR 时序竞态影响） |
| CSRedis 批量/流式读取、TTL、隔离、租约 | `Bing.Caching.CSRedis.Tests.Integration` | `BatchContractTest`、`TtlCancellationRecoveryTest`、`NamespaceIsolationTest`、`SharedCacheContractTest` |
| EasyCaching Redis/Hybrid 批量、流式和回源协调 | `Bing.EasyCaching.Tests` | `RedisCoordinatorIntegrationTest`、`CacheManagerTests`、`Provider_SatisfiesSharedContract` |
| 生产构建兼容性 | 对应 `src` 项目 | `dotnet build --no-restore --configuration Release`，FreeRedis/CSRedis netstandard2.0，0 警告/0 错误 |
