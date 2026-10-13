# 生产符号到测试映射

| 生产符号/行为 | 测试方法 | 测试项目 | 证据状态 |
| --- | --- | --- | --- |
| `CacheResourceOptions.MaxBatchCommandBytes` 参数校验 | `ResourceOptions_RejectNonPositiveDeleteCommandBudget` | `Bing.Caching.Tests` | 通过 net6/net8 |
| `CacheDeleteBatchBudget.Partition` UTF-8/数量切分 | `DeleteBatchBudget_SplitsByUtf8BytesAndKeepsLeasePairTogether`、`DeleteBatchBudget_HonorsCountAndRejectsSingleOversizedKey` | `Bing.Caching.Tests` | 通过 net6/net8 |
| FreeRedis 删除批次预算 | `RemoveAllAsync_SplitsLongUtf8KeysBeforeCommandBudgetAndPreservesSibling`、`RemoveAllAsync_RejectsSingleKeyOverCommandBudgetBeforeDispatch` | `Bing.Caching.FreeRedis.Tests.Integration` | 编译通过，Redis 门控跳过 |
| CSRedis 删除批次预算 | `RemoveAllAsync_SplitsLongUtf8KeysBeforeCommandBudgetAndPreservesSibling`、`RemoveAllAsync_RejectsSingleKeyOverCommandBudgetBeforeDispatch` | `Bing.Caching.CSRedis.Tests.Integration` | 编译通过，Redis 门控跳过 |
| EasyCaching Redis/Cluster 删除分批 | `RemoveAll_SplitsPhysicalBatchesByUtf8CommandBudget`、`RemoveAll_SplitsCommandsAtConfiguredBatchSize` | `Bing.EasyCaching.Tests` | 通过 net6/net8 |
| Hybrid 协调逐键删除保持租约语义 | `HybridBatchOperations_PublishCrossInstanceInvalidationAndConverge` 及相关集成测试 | `Bing.EasyCaching.Tests` | Redis/总线门控跳过 |
