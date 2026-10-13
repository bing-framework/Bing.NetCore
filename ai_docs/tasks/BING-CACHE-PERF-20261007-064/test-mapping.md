# EasyCaching 协调批量路径生产符号到测试映射

| 生产符号 | 关键行为 | 测试/证据 | 状态 |
| --- | --- | --- | --- |
| `RedisLeaseCoordinator.WriteBatch` | 批量覆盖写入并清理数据键对应租约 | `RedisLeaseCoordinatorBatchTest.WriteBatch_UsesOneLuaCommandAndOneBatchInvalidation`、`ClusterWriteBatch_GroupsKeysBySlot` | 已验证 |
| `RedisLeaseCoordinator.RemoveBatch` | 批量删除数据键和租约键 | `RedisLeaseCoordinatorBatchTest.RemoveBatch_UsesOneDeleteAndOneBatchInvalidation` | 已验证 |
| `CacheManager.SetPhysicalBatch` / `SetPhysicalBatchAsync` | 协调器启用时按批调用并限制在途任务 | `CacheManagerBatchCoordinationTest.SetAllAndRemoveAll_UseCoordinatorBatchCommands` | 已验证 |
| `CacheManager.RemovePhysicalBatch` / `RemovePhysicalBatchAsync` | 扫描删除继续使用批量协调路径 | `CacheManagerBatchCoordinationTest.SetAllAndRemoveAll_UseCoordinatorBatchCommands` | 已验证 |
| Hybrid 批量失效回调 | 本地批量删除并发布包含多个键的失效消息 | `RedisLeaseCoordinator` 的批量失效回调注入点；实机跨进程总线仍由 `RUN_INTEGRATION_TESTS` 门控 | 本地已验证，实机外部门禁 |
| `RedisLeaseCoordinator.WriteBatchAsync` | 取消后停止下一分槽派发并观察已发命令 | `RedisLeaseCoordinatorBatchTest.WriteBatchAsync_CancellationAfterFirstClusterGroupStopsNextDispatch` | 已验证 |
