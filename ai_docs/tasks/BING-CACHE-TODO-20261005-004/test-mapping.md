# 生产符号 → 直接测试方法

本表记录 task004 的关键最终路径；task001/task002/task003 已有隔离、TTL、Cluster/分片和 Core 快照映射继续有效。

| 生产符号/行为 | 测试项目 | 方法 |
| --- | --- | --- |
| CacheConfiguration / CacheKeyFormat 缓存派生前缀与直接 hex，v2 格式不变 | Bing.Caching.Tests | CacheConfigurationAndKeyFormatTest.PhysicalKey_RemainsByteForByteCompatibleWithOriginalV2Format；PhysicalKeys_BatchMatchesSingleKeyFormat |
| CacheCoordinationOptions 毫秒上取整与 Timer 上界 | Bing.Caching.Tests | CacheFactoryExecutionTest.CoordinationOptions_RoundPositiveSubMillisecondDurationsUp；CoordinationOptions_RejectUnsupportedTimerRanges |
| CacheFactoryExecution.InvokeAsync/AwaitAsync，总超时、调用方取消与晚异常 | Bing.Caching.Tests | Timeout_ReturnsWhileUncooperativeFactoryStillRuns；CallerCancellation_PreservesCancellationToken；FactoryFault_IsPreserved |
| CacheFactoryExecution.Cleanup/CleanupAsync，异常身份、二次错误与清理等待边界 | Bing.Caching.Tests / Bing.EasyCaching.Tests | CleanupFailure_IsAttachedToOriginalFault；CleanupTimeout_IsBoundedAndReportsSecondaryFault；RedisLeaseCleanupTest.FactoryFault_ReleaseFailureDoesNotReplacePrimaryAsyncOrSync |
| CacheFactoryAdmission，独立键总并发/有界队列/取消恢复 | Bing.Caching.Tests | CacheResourceBoundaryTest.Admission_BoundsDifferentKeysQueueAndRecoversAfterCancellation；Admission_TimeoutDoesNotLeakQueueCapacity |
| CacheBatchBudget，UTF-8 字节累计与单值超限 | Bing.Caching.Tests | PayloadBudget_CountsUtf8AndRejectsAggregateOversize |
| Free/CS GetOrCreate，gate 在 admission 前、失租取消生命周期 | 两个 Redis Integration 项目 | CoordinationBoundaryTest.SameKeyMergeWithZeroQueue_AndLeaseLossCancelsFactory（共享 VerifySameKeyMergeAndLeaseLossAsync） |
| Free/CS 原子写删撤销租约、拒绝旧提交 | 两个 Redis Integration 项目 | CoordinationBoundaryTest.ExplicitMutations_RejectOlderFactoryCommit（共享 VerifyMutationsWinAsync，6 写删路径） |
| Free/CS 超时退出与晚结果拒写 | 两个 Redis Integration 项目 | FactoryTimeout_ReleasesGateAndRejectsLateCommit |
| Free/CS 实际回源准入及 List/Stream 原始响应预算 | 两个 Redis Integration 项目 | DifferentKeyFactoriesAndBatchPayload_RespectConfiguredResourceLimits（共享 VerifyResourceLimitsAsync） |
| Free/CS 各值独立 JSON reference/nullable/root-null/miss | 两个 Redis Integration 项目 | SerializationBoundaryTest.IndependentJsonValuesRemainConsistentAcrossReadPaths |
| Free/CS 前缀枚举取消停止 SCAN 派发 | 两个 Redis Integration 项目 | PrefixScanCancellationTest.GetByPrefixStream_CancellationStopsFurtherScanCommands（真实 MONITOR + PING 栅栏） |
| Easy Redis/Hybrid gate 顺序与及时失租取消 | Bing.EasyCaching.Tests | RedisAndHybrid_SameKeyMergeWithZeroQueueAndLeaseLossCancelsFactory |
| Easy Redis/Hybrid 明确写删与回源顺序 | Bing.EasyCaching.Tests | ExplicitMutations_RejectOlderFactoryCommitForRedisAndHybrid；FactoryTimeout_ReleasesGateAndRejectsLateCommit |
| Easy Redis/Hybrid 字节预算与不同键限流 | Bing.EasyCaching.Tests | RedisAndHybrid_DifferentKeyFactoriesAndBatchPayloadRespectResourceLimits |
| ShardedRedisCacheManager.BuildShards 传播同一 Resources 对象 | Bing.EasyCaching.Tests | ShardedRedis_SharesFactoryAdmissionAndPayloadBudgetAcrossNodes（真实双节点、跨分片键、最大工厂 1、响应超限） |
| ReadHybridBatch / ReadHybridBatchAsync，原子 GET/PTTL 剩余 TTL 回填 | Bing.EasyCaching.Tests | HybridBatchRefill_UsesRemainingTtlAndRemovesMissingLocalValue（500ms/1500ms）；HybridBatchReadAndStream_KeepDistributedAndLocalValuesConsistent |
| Hybrid WriteValue / own publisher 回环 / peer 失效 | Bing.EasyCaching.Tests | HybridCluster_CrossSlotBatchStreamRemoveAndClear_UpdatesDistributedAndLocalLayers；HybridBatchOperations_PublishCrossInstanceInvalidationAndConverge；Hybrid_RemoveByPrefix_SyncAndAsyncClearLocalAndDistributedLayers |
| HybridBusFailureTracker 订阅断线清空、恢复丢失更新/删除 | Bing.EasyCaching.Tests | SubscriptionReconnect_ClearsMissedUpdateAndDeleteWithoutRepublish；HybridBus_SubscriptionDisconnect_ReconnectsAndInvalidatesPeer |
| RedisLeaseCoordinator / bus 发布失败不能吞错 | Bing.EasyCaching.Tests | HybridBus_PublishDuringPause_ReportsFailureAndRecovers；HybridBus_SubscriptionAndPauseFailures_ReconnectThenRepublishAndInvalidatePeer |
| Easy 跨批错误传播，已产出值不撤回 | Bing.EasyCaching.Tests | RedisAndHybridStreams_PreserveFirstBatchAndPropagateSecondBatchDecodeFailure |
| Easy 原生批读取消，观察已发任务且停止派发下一组 | Bing.EasyCaching.Tests | ClusterBatch_CancellationDuringDispatchStopsFurtherReadsAndObservesIssuedTask |
| Easy Redis/Hybrid 前缀取消 SCAN | Bing.EasyCaching.Tests | PrefixScanCancellationTest.Redis_GetByPrefixStream_CancellationStopsFurtherScanCommands；Hybrid_GetByPrefixStream_CancellationStopsFurtherScanCommands |
| Easy Cluster List/Stream/Prefix 大规模值一致 | Bing.EasyCaching.Tests | RedisCluster_10000ItemListStreamAndPrefixRemainConsistent；HybridCluster_10000ItemListStreamAndPrefixRemainConsistent；对应两个 100000 方法 |
| CacheCommandProbe 并发/失败/嵌套 scope 恢复 | Bing.Caching.Tests | CacheCommandProbeTest.Probe_TracksOverlappingCallsAndRestoresOuterScopeOnFailure |

集成测试只有明确的非生产环境和 RUN_INTEGRATION_TESTS 门控下执行。故障/大规模用例保留额外门控；跳过不计通过。性能探针 smoke/formal 证据单独记录于 performance.md，不把正确性测试耗时当性能统计。
