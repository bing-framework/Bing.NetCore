# BING-CACHE-TODO-20261007-079 生产符号到测试方法映射

本轮没有修改生产源码；以下映射记录在三主三从 Redis Cluster 实机上验证的现有生产路径。

| 生产符号/路径 | 验证行为 | 测试方法 |
| --- | --- | --- |
| `FreeRedisCacheManager.ValidateScanTopology`、`ValidateScanTopologyAsync` | 扫描前拓扑完整性、主节点缺失和拓扑签名变化显式失败 | `Bing.Caching.FreeRedis.Tests.ClusterFailureTest.OmittedOnlineMaster_RejectsScanAndClearBeforePartialSuccess`、`MissingScanMaster_FailsInsteadOfReturningPartialResults`、`StaleScanClients_RejectScanAndClearAfterReplicaTakeover` |
| `FreeRedisCacheManager.EnumerateMatchingKeysAsync`、`EnumerateMatchingPhysicalKeysAsync` | 三主全节点扫描、前缀流式消费、扫描在途响应丢失 | `Bing.Caching.FreeRedis.Tests.ClusterCacheTest.ThreeMasters_CrossSlotReadScanAndClear`、`Bing.Caching.FreeRedis.Tests.ClusterScanInFlightFailureTest.ClusterScan_ResponseLostAfterExecution_PropagatesFailure` |
| `FreeRedisCacheManager.GetAllStreamAsync`、`GetByPrefixStreamAsync`、`ClearAsync` | 跨槽批量读取、流式顺序、清理和拓扑变化后的旧客户端拒绝 | `Bing.Caching.FreeRedis.Tests.ClusterCacheTest.ThreeMasters_CrossSlotReadScanAndClear`、`Bing.Caching.FreeRedis.Tests.ClusterFailureTest.StaleScanClients_RejectScanAndClearAfterReplicaTakeover` |
| `CSRedisCacheManager.ValidateScanTopology`、`ValidateScanTopologyAsync` | 扫描前拓扑完整性、主节点缺失和拓扑签名变化显式失败 | `Bing.Caching.CSRedis.Tests.ClusterFailureTest.MissingScanMaster_FailsInsteadOfReturningPartialResults`、`TopologyChangeAfterConstruction_RejectsOldScanAndClearWithoutPartialSuccess` |
| `CSRedisCacheManager.EnumerateMatchingKeysAsync`、`EnumerateMatchingPhysicalKeysAsync` | 三主全节点扫描、前缀流式消费、扫描在途响应丢失 | `Bing.Caching.CSRedis.Tests.ClusterCacheTest.ThreeMasters_CrossSlotReadScanAndClear`、`Bing.Caching.CSRedis.Tests.ClusterScanInFlightFailureTest.ClusterScan_ResponseLostAfterExecution_PropagatesFailure` |
| `CSRedisCacheManager.GetAllStreamAsync`、`GetByPrefixStreamAsync`、`ClearAsync` | 跨槽批量读取、流式顺序、清理和副本接管后的旧客户端拒绝 | `Bing.Caching.CSRedis.Tests.ClusterCacheTest.ThreeMasters_CrossSlotReadScanAndClear`、`Bing.Caching.CSRedis.Tests.ClusterFailureTest.TopologyChangeAfterConstruction_RejectsOldScanAndClearWithoutPartialSuccess` |
| FreeRedis/CSRedis Cluster 重定向处理 | 槽迁移、ASK/MOVED、扫描期间槽位归属变化及异常传播 | FreeRedis `ClusterMigrationTest.MigratingSlot_BatchReadAndScanPreserveExistingValuesOrPropagateRedirectFailure`、`MovedSlot_BatchReadAndScanPreserveValueOrPropagateRedirectFailure`、`SlotOwnershipChangeDuringStreamingScan_IsRejectedWithStableMasterIds`；CSRedis `ClusterMigrationTest.MovedRedirect_DoesNotReportNewOwnerValueAsCacheMiss`、`AskRedirect_DoesNotReportMigratedKeyAsCacheMiss`、`SlotOwnerChangesDuringScan_RejectsCompletedStream` |
| FreeRedis/CSRedis Cluster 路由和副本接管后的重新发现 | 7000 主节点停止、在线副本晋升、全槽读取与清理 | FreeRedis `ClusterFailureTest.PromotedReplica_UpdatedTopologyReadsAndClearsAllSlots`；CSRedis 同名方法 |

## 运行矩阵

- 正常跨槽门禁：FreeRedis/CSRedis `net6.0`、`net8.0`。
- 槽迁移门禁：FreeRedis/CSRedis `net6.0`、`net8.0`。
- 扫描在途失败门禁：FreeRedis/CSRedis `net6.0`、`net8.0`。
- 故障转移门禁：FreeRedis/CSRedis `net6.0`、`net8.0`。
- 拓扑转换门禁：FreeRedis/CSRedis `net6.0`、`net8.0`。

所有门禁使用任务专用 Redis 7.4 三主三从拓扑；不把单节点或客户端分片结果当作 Cluster 证据。
