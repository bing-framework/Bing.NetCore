# 测试映射

本表只引用本任务或当前候选最终回归中的实际方法。历史测试用于兼容边界，不能替代新增证据。

| 生产符号/能力 | 关键行为 | 测试方法 | 项目/证据 |
|---|---|---|---|
| `BuiltInResilienceProvider` / `EnginePipeline<T>` | 八策略、取消、结果释放、三 Provider 一致性 | 共用 Provider 矩阵 88 项；`HttpReplayCountsAndResponseOwnershipMatchEveryProvider` | Bing.Resilience.Tests; Bing.Resilience.Extensions.Tests / ex11-http-matrix |
| `ResilienceRegistry` | 稳定句柄、CAS 切换、候选代资源释放、首次执行前手动控制 | `PartiallyBuiltCandidateIsDisposedAndActivePipelineRemainsUsable`; `HttpContentKeepsRetiredGenerationUntilContentIsDisposed`; `DynamicPartitionCanBeControlledBeforeFirstExecution` | Extensions / ex03 lifecycle、dynamic partition |
| `ResilienceAdmissionResources` | 跨代并发/速率配额和队列总预算 | `QueuedCallUsesExecutionBudgetAndDoesNotEnterCallback`; `RuntimeSwitchPreservesSharedAdmissionResources` | Extensions |
| `ResilienceCircuitResources` | 熔断隔离、半开、手动控制和 Provider 事件归属 | `CircuitStateIsSharedButProviderEventsFollowActiveGeneration`; Redis 5 项；HTTP 合成 3 次调用 | Resilience.Tests; Extensions / Redis evidence |
| `ResilienceAttemptObservations<T>` | 观察上报失败不覆盖业务结果 | `IndependentClientsShareOpenStateAndDuplicateReportsCountOnce`; Provider HTTP 矩阵 | Extensions |
| `DistributedCircuitObserver<T>` | Endpoint/Service 键隔离、失联策略、取消等待 | `EndpointScopeUsesDistinctStableKeysAndReportsDoNotCrossEndpoints`; `PausedRedisHonorsExplicitFailureModesAndRecoversWithoutLosingIsolation` | Extensions / ex05-outage-fixed |
| `RedisDistributedCircuitStateStore` | Lua epoch、探测租约、重复上报、准入后辅助键 TTL、Redis 中断重接 | Redis 5/5、`AbandonedAdmissionAuxiliaryKeysExpireWithoutAReport`、3 独立进程、pause/unpause 2/2 | evidence/redis-process; ex05 |
| `ConfigurationServiceEndpointSource` | CAS、撤销、TTL、身份 incarnation、退休身份安全窗口 | `ConfigurationUsesCasRejectsIdentityReuseAndHonorsEmptyAndExpiry`; `ConfigurationRevokeRetiredIdentityBlocksOldSnapshotAndExpiresWithStaleWindow` | Extensions / ex04 |
| `HttpDirectoryServiceEndpointSource` | ETag、刷新、绑定请求、过期/失败回退、退休身份撤销 | `HttpRetryKeepsSnapshotWhileRefreshChangesFutureCallsAndRefreshesAuthentication`; `DirectoryBoundsContentSupportsEtagAndFailsClosedAfterStaleBudget`; `DirectoryRevokeRetiredIdentityStillBlocksOldSnapshot` | Extensions |
| `BingHttpDiscoveryExtensions.Routing` | 两客户端动态路由、候选切换、端点分区 | `DiscoveryAndSwitchAffectCachedClientAndRefreshEverySignature`（SDK/Runtime） | Extensions / ex11-client-loopback |
| `HttpWriteReplayStrategy` / `HttpReplaySnapshot` | 写许可、独立请求体、逐次签名、响应释放 | `PermittedPostRetriesWithFreshMessageContentAndSignatureAndCopiedOptions`; 10 项 HTTP Write | Extensions |
| `IdempotencyCoordinator` | 同键同体、同键异体冲突、事务回滚、提交结果损坏时配置介入 | HTTP Write 10 项；Relational 8 项；`CorruptCompletedIdempotencyResultRequiresConfigurationIntervention` | evidence/http-write; relational |
| `RelationalOperationStore` | SQLite/MySQL 锁、租约、CAS、attempt ledger | 8/8 双框架，MySQL 多 Worker | evidence/relational |
| `ReplayWorker` / `SagaWorker` | Unknown 确认、租约、历史密钥、补偿 DAG/事务 | Durable/Saga 16/16；真实 kill/restart 四场景 | evidence/durable-tests; durable-process |
| `HttpSyntheticResponseExecutor` | 只读 200、可靠 202、取消、来源观察、一次释放、观察器异常隔离 | Synthetic/Manifest 20/20；`SyntheticDtoPreservesClientResultAndDoesNotHideCircuitFailures`; `ObserverFailureDoesNotDiscardSelectedResponse` | Extensions |
| `HedgedDefinitionSource` | 完整正文竞速、独立 Scope、拒绝/签名、失败驱逐 | DefinitionRecovery 10/10；真实 SDK/Runtime 描述竞速 | Extensions |
| `HttpEndpointManifestSource` / `ManifestServiceEndpointSource` | 签名、revision、防回滚、发现交集 | ManifestTests 全部方法 | Extensions |
| `AesGcmOperationDataProtector` / `DurableOperationReceipt` | 版本密钥轮换、提交证明、scope/key/body 一致性、损坏数据配置错误 | Durable 重启、Synthetic 202 receipt；`InvalidProtectedDataIsReportedAsConfigurationFailure` | Extensions / DurableReplayTests |
| `ReplayCoordinator` / `SagaCoordinator` | 损坏 Journal 不重试业务处理器，进入人工终态 | `CorruptJournalDataMovesReplayToManualInterventionWithoutRetryingHandler`; `CorruptSagaJournalMovesParentToManualInterventionWithoutRunningSteps` | Extensions / DurableReplayTests、SagaTests |
| `BingDynamicApiClientResilienceExtensions` | Legacy 兼容、旧重试互斥、预算和流/写限制 | `ColdAndHotCallsUseBoundNameAndDoNotStackLegacyRetries`; 34/34 双框架 | DynamicApi.Client.Resilience.Tests |
| `BingHttpResilienceHandler` | Retry/Hedging 资格、Retry-After、SDK 错误包装、生成代租约 | 71/76 兼容；HTTP Provider 27/27 双框架；Loopback | Http/DynamicApi/Extensions |
| `UseBingIdempotency` | ASP.NET 请求/响应缓冲、重放、409/410/413 | Idempotency ASP.NET 10 项 | Extensions |
| 三 Provider nupkg | 无引擎泄漏、独立运行/发布和依赖边界 | 6 consumer run/publish + assets/nuspec/deps/audit | evidence/package-consumption/results.md |

## 最终回归

`evidence/final-tests/latest.json` 的候选在 net6/net8 共运行 12 个受影响项目目标：**共 723 项，723 通过，0 失败，0 未执行**。本次使用 `--local-services` 启用任务专属 Redis/MySQL；测试 runner、TRX 和命令保存在同目录。
