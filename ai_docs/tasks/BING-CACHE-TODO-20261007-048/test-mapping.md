# 生产符号到测试方法映射

| 生产符号 | 关键行为 | 测试项目/方法 | 状态 |
| --- | --- | --- | --- |
| `FreeRedisCacheManager.ForEachKeyAsync` | 扫描删除批次、取消、拓扑校验 | `Bing.Caching.FreeRedis.Tests.ScanDeleteBatchTest.ScanDelete_RemovesDataAndLeaseKeysAcrossBatchedPrefixPatternAndClear` | GATED |
| `FreeRedisCacheManager.RemovePhysicalBatch` | 数据键与租约键成批删除 | 同上 | GATED |
| `CSRedisCacheManager.VisitAsync` | 扫描删除批次、取消、拓扑校验 | `Bing.Caching.CSRedis.Tests.ScanDeleteBatchTest.ScanDelete_RemovesDataAndLeaseKeysAcrossBatchedPrefixPatternAndClear` | GATED |
| `CSRedisCacheManager.RemovePhysicalBatch` | 数据键与租约键成批删除 | 同上 | GATED |
