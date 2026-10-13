# 生产符号到测试方法映射

| 生产符号/行为 | 测试项目 | 测试方法 | 状态 |
| --- | --- | --- | --- |
| `FreeRedisCacheManager.RemoveAllAsync` 按 `BatchSize` 聚合 `DEL`，同时删除数据键和租约键 | `Bing.Caching.FreeRedis.Tests.Integration` | `RemoveAllBatchTest.RemoveAllAsync_UsesBatchSizeAndDeletesDataAndLeaseKeys` | GATED |
| `CSRedisCacheManager.RemoveAllAsync` 按 `BatchSize` 聚合 `DEL`，同时删除数据键和租约键 | `Bing.Caching.CSRedis.Tests.Integration` | `RemoveAllBatchTest.RemoveAllAsync_UsesBatchSizeAndDeletesDataAndLeaseKeys` | GATED |
| FreeRedis/CSRedis 同步 `RemoveAll` 复用物理删除批次路径 | 对应 Provider 集成项目 | 既有 `BatchContractTest` 删除结果断言；生产构建覆盖实现 | BUILD + GATED |
| Cluster/客户端分片删除分组 | 对应 Provider 集成项目 | `ClusterCacheTest`、`ShardedCacheTest` 及既有 RemoveAll 回归 | GATED |

命令聚合测试通过真实 Redis `MONITOR` 和 `PING` 排空屏障观察服务端命令；门控关闭时只证明编译和测试发现，不计为实机通过。
