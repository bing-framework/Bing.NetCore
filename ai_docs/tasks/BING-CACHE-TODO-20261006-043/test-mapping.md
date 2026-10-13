# 生产符号到测试方法映射

| 生产符号 | 测试方法 | 范围 |
| --- | --- | --- |
| `ShardedRedisCacheManager.GetAllAsync<T>` | `ShardedScanCancellationUnitTest.GetAllAsync_ReturnsValuesFromAllShardsInInputOrder` | 分片顺序、直接值批处理和前后身份校验 |
| `ShardedRedisCacheManager.GetByPrefixAsync<T>` | `ShardedScanCancellationUnitTest.GetByPrefixAsync_ReturnsValuesFromAllShardsAndUsesSharedBudget` | 共享扫描预算、分片身份校验和直接值批处理 |
| `CacheManager.GetByPrefixWithBudgetAsync<T>` | `CacheManagerTests.GetByPrefixAsync_UsesPhysicalPrefixBatchPath`、`ShardedScanCancellationUnitTest.GetByPrefixAsync_ReturnsValuesFromAllShardsAndUsesSharedBudget` | 单分片前缀 List 扫描、命名空间隔离和取消入口 |
