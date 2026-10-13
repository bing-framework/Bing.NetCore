# 077 生产符号到测试方法映射

| 生产符号 | 关键行为 | 测试项目/方法 | 证据 |
| --- | --- | --- | --- |
| `CacheManager.SetPhysicalBatch<T>` | Hybrid 同步批量提交后回填写入实例本地层 | `Bing.EasyCaching.Tests/CacheManagerBatchCoordinationTest.cs`：`HybridSetAll_RefillsWritingInstanceLocalLayerAfterCoordinatorCommit` | net6/net8 各通过 |
| `CacheManager.SetPhysicalBatchAsync<T>` | Hybrid 异步批量提交后回填写入实例本地层 | `Bing.EasyCaching.Tests/CacheManagerBatchCoordinationTest.cs`：`HybridSetAllAsync_RefillsWritingInstanceLocalLayerAfterCoordinatorCommit` | net6/net8 各通过 |
| `ShardedRedisCacheManager` Redis 路由与批量/流式/扫描/Clear | 双节点客户端分片跨节点行为和命名空间隔离 | `Bing.EasyCaching.Tests/EasyShardedIntegrationTest.cs`：`ShardedRedis_CrossNodeBatchStreamScanClearAndRetriever_Work`、`ShardedRedis_SharesFactoryAdmissionAndPayloadBudgetAcrossNodes` | 双节点 net6/net8 各 4 通过 |
| `ShardedRedisCacheManager` Hybrid 组合 | 双节点 Hybrid 批量、流式、本地回填、失效和 Clear 收敛 | `Bing.EasyCaching.Tests/EasyShardedHybridIntegrationTest.cs`：`ShardedHybrid_CrossInstanceBatchStreamInvalidationAndClearConverge` | 双节点 net6/net8 各 1 通过 |
