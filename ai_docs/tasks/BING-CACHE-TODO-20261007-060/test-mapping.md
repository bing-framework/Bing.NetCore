# 生产符号到测试方法映射

| 能力 | 测试方法 | 结果 |
| --- | --- | --- |
| FreeRedis `GetAllStreamAsync` 首批后取消 | `Bing.Caching.FreeRedis.Tests.Integration.BatchContractTest.GetAllStream_CancellationAfterFirstBatch_DoesNotDispatchNextMGet` | net6/net8 通过 |
| FreeRedis `GetByPrefixStreamAsync` 首批后取消 | `BatchContractTest.GetByPrefixStream_CancellationAfterFirstBatch_DoesNotDispatchNextMGet` | net6/net8 通过 |
| CSRedis `GetAllStreamAsync` 首批后取消 | `Bing.Caching.CSRedis.Tests.Integration.BatchContractTest.GetAllStream_CancellationAfterFirstBatch_DoesNotDispatchNextMGet` | net6/net8 通过 |
| CSRedis `GetByPrefixStreamAsync` 首批后取消 | `BatchContractTest.GetByPrefixStream_CancellationAfterFirstBatch_DoesNotDispatchNextMGet` | net6/net8 通过 |
| FreeRedis/CSRedis List 与 Stream 一致性 | `BatchContractTest.GetAllStream_10000ItemsMatchesListIncludingDuplicatesMissingOrderAndHits`、`GetByPrefix_10000Items_ListAndStreamRemainConsistentAcrossSpecialKeysAndNamespaces` | 两 Provider net6/net8 通过 |
| FreeRedis/CSRedis 第二批失败 | `BatchContractTest.GetAllStream_PropagatesSecondBatchDeserializationFailureAfterFirstBatch`、`GetByPrefixStream_PropagatesSecondBatchFailureAfterFirstBatch` | 两 Provider net6/net8 通过 |
| FreeRedis/CSRedis 特殊键前缀 Stream | `CacheSpecialKeyTest.GetByPrefixStream_ReturnsSpecialLogicalKeys_AndExcludesSimilarNamespaceSentinel` | 两 Provider net6/net8 通过 |
| EasyCaching Redis/Hybrid mock 取消 | `CacheManagerTests.GetAllStream_CancellationAfterFirstBatch_DoesNotDispatchNextRead`、`GetByPrefixStream_CancellationAfterFirstBatch_DoesNotDispatchNextRead`、`DoesNotDispatchNextBatch` | net6/net8 所属类 40/40 通过 |
| EasyCaching Hybrid 批量实机 | `RedisCoordinatorIntegrationTest.HybridBatchReadAndStream_KeepDistributedAndLocalValuesConsistent`、`HybridBatchOperations_PublishCrossInstanceInvalidationAndConverge`、`HybridBatchRefill_UsesRemainingTtlAndRemovesMissingLocalValue` | net6/net8 集成子集通过 |
| EasyCaching Hybrid 流式失败/特殊键 | `RedisCoordinatorIntegrationTest.RedisAndHybridStreams_PreserveFirstBatchAndPropagateSecondBatchDecodeFailure`、`HybridScan_HandlesSpecialKeysWithoutTouchingAnotherNamespace` | net6/net8 集成子集通过 |

## 外部边界

上述结果使用本机非生产单节点 Redis `localhost:6379`。Cluster、双节点分片、独立总线和双进程证据没有被单节点结果替代。
