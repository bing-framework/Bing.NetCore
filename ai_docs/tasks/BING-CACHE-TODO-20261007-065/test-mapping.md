# EasyCaching 协调删除预算生产符号到测试映射

| 生产符号 | 关键行为 | 测试/证据 | 状态 |
| --- | --- | --- | --- |
| `RedisLeaseCoordinator.RemoveBatch` | 按槽位、BatchSize 和命令字节预算切分同步删除 | `RedisLeaseCoordinatorBatchTest.RemoveBatch_RespectsCommandByteBudget`、`ClusterRemoveBatch_PartitionsEachSlotWithinCommandBudget` | 已验证 |
| `RedisLeaseCoordinator.RemoveBatchAsync` | 异步批次遵守预算和最大并发 | `RedisLeaseCoordinatorBatchTest.RemoveBatchAsync_RespectsCommandByteBudget` | 已验证 |
| `CacheDeleteBatchBudget.Partition` 接入协调删除 | 单键超预算在 Redis 命令前失败 | `RedisLeaseCoordinatorBatchTest.RemoveBatch_RejectsSingleKeyOverBudget` | 已验证 |
| EasyCaching README 删除语义 | 文档反映批量协调和预算切分 | `framework/src/Bing.EasyCaching/README.md` 删除行为段落 | 已验证 |
