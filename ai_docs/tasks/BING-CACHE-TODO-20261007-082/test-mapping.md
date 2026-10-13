# BING-CACHE-TODO-20261007-082 生产符号到测试方法映射

| 生产符号/路径 | 验证行为 | 测试方法 |
| --- | --- | --- |
| `CacheManager` Cluster 批量读写和原生 SCAN | Redis 跨槽批量、流式读取、全主节点扫描、Clear、命名空间哨兵 | `EasyClusterIntegrationTest.RedisCluster_CrossSlotBatchStreamRemoveAndClear_PreservesNamespaceIsolation` |
| `HybridCacheManager`/`CacheManager.Hybrid` Cluster 路径 | Hybrid 分布式层、本地层、跨槽批量和 Clear 收敛 | `EasyClusterIntegrationTest.HybridCluster_CrossSlotBatchStreamRemoveAndClear_UpdatesDistributedAndLocalLayers` |
| `CacheManager.Cluster` 拓扑发现 | 主节点枚举和槽位完整性 | 测试内部 `ReadClusterMastersAsync` 及上述两个门禁 |
