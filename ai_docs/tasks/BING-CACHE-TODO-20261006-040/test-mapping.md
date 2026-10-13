# 生产符号到测试方法映射

| 生产符号 | 测试方法 | 范围 |
| --- | --- | --- |
| `CacheManager.DecodeRawRedisBatch<T>` | `RedisCoordinatorIntegrationTest.RedisBatchReadAndStream_KeepOrderDuplicatesAndMissingConsistent`、`CacheManagerTests.RawRedisBatchDecode_PreservesOrderAndMissingValues`、`CacheManagerTests.GetAllAsync_ListMatchesStream_ForDuplicatesMissingAndNullValues` | 批量顺序、重复键、缺失值、null/default 和 List/Stream 一致性 |
| `CacheManager.DecodeRawRedisBatch<T>` | `SerializationBoundaryTest`（EasyCaching 相关批量路径） | 复杂序列化和异常传播 |
