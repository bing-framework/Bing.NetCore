# 生产符号到测试方法

| 生产符号 | 测试方法 | 结果 |
| --- | --- | --- |
| `CacheResourceOptions.MaxInvalidationMessageBytes` | `CacheResourceBoundaryTest.ResourceOptions_InvalidationMessageBudgetDefaultsToUnlimited`、`ResourceOptions_RejectsNegativeInvalidationMessageBudget` | net6.0/net8.0 通过 |
| 单条消息载荷预算异常 | `HybridInvalidationMessageBatcherTest.Publish_RejectsSingleKeyThatCannotFit` | net6.0/net8.0 通过 |
| 同步消息拆分 | `HybridInvalidationMessageBatcherTest.Publish_SplitsBySerializedBytesAndPreservesDuplicates` | net6.0/net8.0 通过 |
| 异步消息拆分与取消 | `HybridInvalidationMessageBatcherTest.PublishAsync_CancellationStopsSubsequentMessages` | net6.0/net8.0 通过 |
| 总线失败传播 | `HybridInvalidationMessageBatcherTest.Publish_PropagatesBusFailureWithoutSwallowing` | net6.0/net8.0 通过 |
| Hybrid/分片 Hybrid 回调 | `HybridCacheManager`、`CachingServiceCollectionExtensions` 调用点检查；EasyCaching 完整套件 | 调用点检查通过；完整套件双 TFM 各 92 通过、54 门控跳过 |
