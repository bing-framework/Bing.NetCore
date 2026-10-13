# 生产符号到测试方法映射

| 生产符号 | 关键行为 | 测试或证据 |
| --- | --- | --- |
| `CacheManager.ReadHybridRawBatch` / `ReadHybridRawBatchAsync` | 批量脚本参数顺序和原始响应校验保持不变 | `CacheManagerTests` Hybrid 批量路由测试；`RedisCoordinatorIntegrationTest`（Redis 门控） |
| `CacheManager.ReadRawRedisValues` / `ReadRawRedisValuesAsync` | Redis 批量键投影和命中值解码保持顺序 | `CacheManagerTests.GetAllStream_PreservesOrderDuplicatesMissingAndNullValues`；Cluster/Hybrid 集成测试 |
| `CacheBatchBudget.Check<T>` | selector 路径保持 UTF-8/字节聚合上限和 null 语义 | `CacheResourceBoundaryTest.PayloadBudget_IndexSelectorPreservesAggregateLimit` |
| `CacheManager.DecodeRawRedisBatch` | RedisValue 到序列化字节数组映射和命中状态不变 | `CacheManagerTests` 批量读取契约；`RedisCoordinatorIntegrationTest` |
