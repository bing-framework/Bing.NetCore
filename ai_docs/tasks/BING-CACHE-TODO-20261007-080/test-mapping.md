# BING-CACHE-TODO-20261007-080 生产符号到测试方法映射

本轮没有修改生产源码；以下测试在真实 Redis 7.4 单节点上覆盖生产扫描预算调用链。

| 生产符号/路径 | 验证行为 | 测试方法 |
| --- | --- | --- |
| `FreeRedisCacheManager.EnumerateKeys` | 同步命名空间扫描在第二页前消耗页预算并抛出异常 | `Bing.Caching.FreeRedis.Tests.ScanPageBudgetTest.GetByPrefix_StopsBeforeSecondScanPage`、`RemoveByPrefix_StopsBeforeSecondScanPage`、`Clear_StopsBeforeSecondScanPage` |
| `FreeRedisCacheManager.ForEachKeyAsync` | 异步前缀删除和 Clear 在第二页前停止派发 | `Bing.Caching.FreeRedis.Tests.ScanPageBudgetTest.GetByPrefixAsync_StopsBeforeSecondScanPage`、`RemoveByPrefixAsync_StopsBeforeSecondScanPage`、`ClearAsync_StopsBeforeSecondScanPage` |
| `FreeRedisCacheManager.EnumerateMatchingPhysicalKeysAsync`、`GetByPrefixStreamAsync` | 流式前缀扫描的页预算和异常类型 | `Bing.Caching.FreeRedis.Tests.ScanPageBudgetTest.GetByPrefixStream_StopsBeforeSecondScanPage` |
| `CSRedisCacheManager.Scan` | 同步前缀扫描、删除和 Clear 在第二页前停止派发 | `Bing.Caching.CSRedis.Tests.ScanPageBudgetTest.GetByPrefix_StopsBeforeSecondScanPage`、`RemoveByPrefix_StopsBeforeSecondScanPage`、`Clear_StopsBeforeSecondScanPage` |
| `CSRedisCacheManager.VisitAsync` | 异步前缀扫描、删除和 Clear 在第二页前停止派发 | `Bing.Caching.CSRedis.Tests.ScanPageBudgetTest.GetByPrefixAsync_StopsBeforeSecondScanPage`、`RemoveByPrefixAsync_StopsBeforeSecondScanPage`、`ClearAsync_StopsBeforeSecondScanPage` |
| `CSRedisCacheManager.EnumerateMatchingPhysicalKeysAsync`、`GetByPrefixStreamAsync` | 流式前缀扫描的页预算和异常类型 | `Bing.Caching.CSRedis.Tests.ScanPageBudgetTest.GetByPrefixStream_StopsBeforeSecondScanPage` |

每个方法配置 `MaxScanPages=1`、`batchSize=1`，种入 128 个随机命名空间键，并用 `RedisCommandMonitor` 过滤当前物理命名空间；四个 Provider/TFM 组合均观察到 1 条匹配 `SCAN`，未发送第二页命令。
