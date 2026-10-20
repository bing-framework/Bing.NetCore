# 本轮生产符号到测试方法映射

| 生产符号/行为 | 测试方法 | 证据边界 |
| --- | --- | --- |
| `FreeRedisCacheManager.ReadInputBatchAsync` 单节点直读 | `BatchContractTest.GetAllAsync_PreservesOrderDuplicatesAndMissingDefault_AndEnumeratesOnce`、`GetAllStream_LargeInputMatchesListIncludingDuplicatesAndMissingKeys` | 结果顺序、重复键、缺失值；真实 Redis 门控 |
| `FreeRedisCacheManager.ReadPrefixBatchAsync` 单节点直读 | `BatchContractTest.GetByPrefixStream_CancellationAfterFirstEntry_StopsFurtherConsumption` 及命名空间/扫描契约 | 取消和前缀隔离；SCAN 非快照边界保持 |
| FreeRedis Cluster/分片分组 | `ClusterCacheTest`、`ShardedCacheTest` | 本轮不改变实现；需专用拓扑门控 |
| 性能路径 | CacheBench `batchRead`、`scan`、`batchReadStream`、`scanStream` | 短基准只提供当前绝对值，不能替代固定 baseline 对照 |
