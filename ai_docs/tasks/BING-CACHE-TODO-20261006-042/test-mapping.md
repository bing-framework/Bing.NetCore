# 生产符号到测试方法映射

| 生产符号 | 测试方法 | 范围 |
| --- | --- | --- |
| `CacheManager.GetByPrefixAsync<T>` | `CacheManagerTests.GetByPrefixAsync_UsesPhysicalPrefixBatchPath` | 字面前缀、命中值、忽略其他前缀和批量读取 |
| `CacheManager.GetByPrefixAsync<T>` | `CacheManagerTests.GetByPrefixAsync_CancellationAfterFirstBatch_DoesNotDispatchNextRead`、`CacheManagerTests.GetByPrefixStream_EnforcesScanKeyBudget`、`PrefixScanCancellationTest` | 扫描预算、首批取消和扫描命令取消语义 |
| `CacheManager.GetByPrefixAsync<T>` | `RedisCoordinatorIntegrationTest.RedisScan_HandlesSpecialKeysWithoutTouchingAnotherNamespace`、`HybridBatchReadAndStream_KeepDistributedAndLocalValuesConsistent` | Redis/Hybrid 隔离和批量回填 |
