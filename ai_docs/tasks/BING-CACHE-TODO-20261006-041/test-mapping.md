# 生产符号到测试方法映射

| 生产符号 | 测试方法 | 范围 |
| --- | --- | --- |
| `CacheManager.GetAllAsync<T>(IEnumerable<string>, CancellationToken)` | `CacheManagerTests.GetAllAsync_ListMatchesStream_ForDuplicatesMissingAndNullValues` | List/Stream 值一致、重复、缺失和 null/default |
| `CacheManager.GetAllAsync<T>(IEnumerable<string>, CancellationToken)` | `RedisCoordinatorIntegrationTest.RedisBatchReadAndStream_KeepOrderDuplicatesAndMissingConsistent`、`HybridBatchReadAndStream_KeepDistributedAndLocalValuesConsistent` | Redis/Hybrid 批量路由与本地回填 |
