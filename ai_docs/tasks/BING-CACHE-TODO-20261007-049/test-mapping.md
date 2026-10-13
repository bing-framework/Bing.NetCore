# 生产符号到测试方法映射

| 生产符号/行为 | 测试项目 | 测试方法 | 状态 |
| --- | --- | --- | --- |
| `FreeRedisCacheManager.RemoveByPrefixAsync` 扫描删除取消后停止后续 `DEL` | `Bing.Caching.FreeRedis.Tests.Integration` | `ScanDeleteBatchTest.RemoveByPrefixAsync_CancellationAfterFirstDelete_DoesNotDispatchNextDelete` | GATED |
| `CSRedisCacheManager.RemoveByPrefixAsync` 扫描删除取消后停止后续 `DEL` | `Bing.Caching.CSRedis.Tests.Integration` | `ScanDeleteBatchTest.RemoveByPrefixAsync_CancellationAfterFirstDelete_DoesNotDispatchNextDelete` | GATED |
| FreeRedis/CSRedis 扫描删除批量结果、租约键清理和命名空间隔离 | 对应 Provider 集成项目 | `ScanDeleteBatchTest.ScanDelete_RemovesDataAndLeaseKeysAcrossBatchedPrefixPatternAndClear` | GATED |

测试使用真实 Redis `MONITOR` 和 `PING` 排空屏障观察服务端命令；`RUN_INTEGRATION_TESTS` 未开启时只证明测试发现和编译，不计为实机通过。
