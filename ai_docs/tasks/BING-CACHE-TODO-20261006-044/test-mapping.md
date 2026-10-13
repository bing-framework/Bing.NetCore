# 生产符号到测试方法映射

| 生产符号 | 测试方法 | 范围 |
| --- | --- | --- |
| `FreeRedisCacheManager.GetAll<T>` | `ClusterCacheTest.ThreeMasters_CrossSlotReadScanAndClear` | Cluster 门控下跨槽同步批量读取、输入顺序和缺失值 |
| `FreeRedisCacheManager.ReadScannedBatch<T>` | `ClusterCacheTest.ThreeMasters_CrossSlotReadScanAndClear` | Cluster 门控下同步前缀扫描顺序和批量读取 |
| `CSRedisCacheManager.GetAll<T>` | `ClusterCacheTest.ThreeMasters_CrossSlotReadScanAndClear` | Cluster 门控下跨槽同步批量读取、输入顺序和缺失值 |
| `CSRedisCacheManager.ReadScannedBatch<T>` | `ClusterCacheTest.ThreeMasters_CrossSlotReadScanAndClear` | Cluster 门控下同步前缀扫描顺序和批量读取 |
