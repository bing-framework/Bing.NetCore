# BING-CACHE-TODO-20261007-075 生产符号到测试方法映射

| 生产符号/行为 | 测试方法 | 测试项目 | 状态 |
| --- | --- | --- | --- |
| `ShardedRedisCacheManager.RemoveByPrefixAsync` 分片并发、预算和取消 | `RemoveByPrefixAsync_DispatchesShardsWithinConfiguredLimit`、`RemoveByPrefixAsync_CancellationStopsTrailingShardDispatch` | `Bing.EasyCaching.Tests` | CLOSED |
| `ShardedRedisCacheManager.RemoveByPatternAsync` 分片并发 | `RemoveByPatternAsync_DispatchesShardsWithinConfiguredLimit`、`RemoveByPatternAsync_PropagatesFailureAfterObservingIssuedShards` | `Bing.EasyCaching.Tests` | CLOSED |
| `ShardedRedisCacheManager.ClearAsync` 分片并发 | `ClearAsync_DispatchesShardsWithinConfiguredLimit` | `Bing.EasyCaching.Tests` | CLOSED |
| 已派发任务观察、失败传播和尾部拓扑校验 | `ShardedCleanupConcurrencyUnitTest`、`ShardedScanCancellationUnitTest` | `Bing.EasyCaching.Tests` | CLOSED |
| 同步清理 API 保持既有顺序/异常语义 | 现有 `ShardedScanCancellationUnitTest` | `Bing.EasyCaching.Tests` | REUSE |
