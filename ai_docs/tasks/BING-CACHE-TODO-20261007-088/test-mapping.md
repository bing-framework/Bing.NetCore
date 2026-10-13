# BING-CACHE-TODO-20261007-088 生产符号到测试方法映射

| 生产符号 | 行为 | 测试方法 | 状态 |
| --- | --- | --- | --- |
| `CacheSyncCleanupMode` / `CacheConfiguration` | 默认顺序并保留配置；显式模式通过派生配置启用 | `CacheConfigurationAndKeyFormatTest.SyncCleanupMode_DefaultsToSequential` | PASS：net6.0/net8.0 各 1/1 |
| `ShardedRedisCacheManager.RunSyncShardScans` | BoundedParallel 不超过 `MaxConcurrency` | `ShardedSyncCleanupConcurrencyUnitTest.BoundedParallel_CleanupDoesNotExceedConfiguredConcurrency` | PASS：net6.0/net8.0 各 3/3 精准套件 |
| `ShardedRedisCacheManager.RunSyncShardScans` | 首窗失败停止尾部派发并等待已派发分片 | `ShardedSyncCleanupConcurrencyUnitTest.BoundedParallel_FailureStopsTrailingShardAndObservesWindow` | PASS：net6.0/net8.0 各 3/3 精准套件 |
| `ShardedRedisCacheManager` 同步清理入口 | 前缀、模式、Clear 共享调度器，字面模式保持单键路由 | `ShardedSyncCleanupConcurrencyUnitTest.BoundedParallel_CoversPrefixPatternAndClear` | PASS：net6.0/net8.0 各 3/3 精准套件 |
| 默认 `Sequential` 路径 | 既有同步异常和顺序语义不变 | `ShardedScanCancellationUnitTest.RemoveByPrefix_SyncFailurePreservesPrimaryExceptionAndSkipsTrailingIdentityProbe` | REUSE |
