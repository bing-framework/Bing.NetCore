# 生产符号到测试方法映射

| 生产符号 | 行为 | 测试/探针 | 状态 |
| --- | --- | --- | --- |
| `CacheManager.GetAllAsync<T>` | 批量读取顺序、缺失和重复 | `RedisBatchReadAndStream_KeepOrderDuplicatesAndMissingConsistent`；CacheBench `batchRead` | 已验证 |
| `CacheManager.GetAllStreamAsync<T>` | 流式批量读取和取消 | `RedisBatchReadAndStream_KeepOrderDuplicatesAndMissingConsistent`、`RedisLargeBatch_ListAndStreamAgreeAcrossTenThousandKeys`；CacheBench `batchReadStream` | 已验证 |
| `CacheManager.GetByPrefixAsync<T>` | 前缀扫描值和命名空间过滤 | `RedisBatchReadAndStream_KeepOrderDuplicatesAndMissingConsistent`、`RedisLargeBatch_ListAndStreamAgreeAcrossTenThousandKeys`；CacheBench `scan` | 已验证 |
| `CacheManager.GetByPrefixStreamAsync<T>` | 流式扫描和取消 | `RedisBatchReadAndStream_KeepOrderDuplicatesAndMissingConsistent`；CacheBench `scanStream` | 已验证 |
| `ReadRawRedisBatchAsync<T>` / `ReadHybridBatchAsync<T>` | 分组、预算、异常和取消 | `RedisAndHybridStreams_PreserveFirstBatchAndPropagateSecondBatchDecodeFailure`、Hybrid 批量一致性回归 | 已验证 |
