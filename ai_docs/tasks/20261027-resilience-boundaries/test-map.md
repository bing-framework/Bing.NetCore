# 生产符号与验证映射

任务：20261027-resilience-boundaries。职责测试位于 `framework/tests/Bing.Resilience.Extensions.Tests`，目标框架为 net6/net8。此前阶段的跨项目有效覆盖汇总为 805 项，详见 `evidence/validation-summary.json`；FIX-BND-R01～R04 后续定向复验各 29/29，扩展测试项目完整回归各 207/207（net6/net8）。这些 TRX 与先前汇总存在重复覆盖，因此本文件不把它们相加成新的全局唯一测试总数。

| 最终生产符号 / 关键行为 | 具体测试方法（类名.方法名） |
|---|---|
| HttpRegistryEndpointOptions、HttpRegistryServiceEndpointSource 构造、配置冻结/可信传输 | RegistryAdapterTests.ConsulUsesNodeIdentityAddressFallbackAndFrozenOptions；SizeSignatureAndTransportGuardsRejectUnsafeDocuments |
| HttpRegistryServiceEndpointSource.GetSnapshotAsync；单飞刷新/取消隔离/过期 | RegistryAdapterTests.OneCancelledWaiterDoesNotCancelSharedRefresh；TransportFailureUsesOnlyBoundedStaleSnapshot |
| HTTP 认证、ETag、PrepareRequestAsync 不可改变目标 | RegistryAdapterTests.NotModifiedKeepsVersionAndAuthCannotRedirectCredentials；KubernetesPaginatesAuthenticatesAndDeduplicatesStablePodIdentity |
| ParsePage / 非法协议字段、Origin、最大正文、永久错误；不使用旧快照 | RegistryAdapterTests.InvalidRegistryDocumentInvalidatesPreviouslyValidSnapshot；AuthorizationLossOrAclFilteredListInvalidatesStaleCache；SizeSignatureAndTransportGuardsRejectUnsafeDocuments |
| HttpRegistryServiceEndpointSource.Revoke / IsRevoked / 历史身份复用 | RegistryAdapterTests.RevocationAndOriginReuseRequireNewIncarnation |
| HttpRegistryServiceEndpointSource.Dispose / timer/取消/传输所有权 | RegistryAdapterTests.DisposeCancelsRefreshAndHonorsTransportOwnership |
| ConsulEndpointOptions / ConsulServiceEndpointSource；node+service、地址回退、健康与元数据 | RegistryAdapterTests.ConsulUsesNodeIdentityAddressFallbackAndFrozenOptions；ConsulFiltersFailingChecksAndRequiresServiceIdentity |
| NacosEndpointOptions / NacosServiceEndpointSource；兼容 v1、健康/禁用/零权重、incarnation | RegistryAdapterTests.NacosFiltersDisabledUnhealthyAndZeroWeightAndRequiresIncarnation |
| KubernetesEndpointSliceOptions / KubernetesEndpointSliceSource；分页、Pod UID、ready/terminating、冲突 | RegistryAdapterTests.KubernetesPaginatesAuthenticatesAndDeduplicatesStablePodIdentity；KubernetesDoesNotRouteUnreadyOrTerminatingPods；KubernetesRejectsInconsistentOrUnboundedPaginationWithoutPublishingPartialSnapshot |
| RelationalOperationStore.SchemaVersion / GetMigrationScript / 嵌入 SQLite、MySQL v1 SQL | RelationalBoundaryTests.EmbeddedMigrationCreatesExpectedSchemaAndValidationIsReadOnly；MySqlMigrationAndReadOnlyValidationPreserveAtomicJournalRollback |
| RelationalOperationStore.ValidateSchemaAsync；列类型/空值/键/索引/InnoDB/字符集；只读失败 | RelationalBoundaryTests.SchemaValidationRejectsMissingUniqueKeyAndWrongColumnType；MySqlMigrationAndReadOnlyValidationPreserveAtomicJournalRollback；EmbeddedMigrationCreatesExpectedSchemaAndValidationIsReadOnly |
| ValidateSqliteCollationsAsync / ReadIndexesAsync；SQLite 身份与状态保持 BINARY，接受注释及引号、不误判默认字符串 | RelationalBoundaryTests.SqliteSchemaRejectsCaseInsensitiveIdentityAndState（3 种 NOCASE 写法）；EmbeddedMigrationCreatesExpectedSchemaAndValidationIsReadOnly；独立 review-storage 探针的 NOCASE 唯一索引 |
| RelationalOperationStore.InitializeAsync 显式 InnoDB；现有 v1 接管 | RelationalBoundaryTests.MySqlMigrationAndReadOnlyValidationPreserveAtomicJournalRollback；既有 RelationalOperationTests 双框架全回归 |
| EntityFrameworkOperationExtensions.EnlistEntityFramework；所有者提交/回滚、缺失事务拒绝 | RelationalBoundaryTests.EfBridgeSharesTransactionAndNeverCommitsOwner |
| FreeSqlOperationExtensions.EnlistFreeSql；真实 IUnitOfWork 事务、连接存活 | RelationalBoundaryTests.FreeSqlBridgeUsesExistingWorkUnitAndPreservesOwner |
| 现有 RelationalOperationStore.Enlist / Dapper 业务与 Journal 同事务 | RelationalBoundaryTests.DapperReusesSameAdoTransactionForBusinessAndJournal |
| ResilienceOperationsEndpointOptions / AddBingResilienceOperationsManagement / MapBingResilienceOperations；冻结、认证授权、路由、前置服务 | OperationsManagementTests.RegistrationRejectsMissingAuthorizationAndInvalidRoutes；AntiforgeryTokenIsBoundToOperatorAndRegistrationOptionsAreFrozen；ManagementRequiresAuthenticatedAuthorizedTenantAndRedactsPayload |
| ResilienceOperationStatus / ResilienceSagaStatus；载荷脱敏与租户隔离 | OperationsManagementTests.ManagementRequiresAuthenticatedAuthorizedTenantAndRedactsPayload；SagaManualActionUsesCoordinatorCasAndUnknownConfirmation |
| CancelReplayRequest / Replay cancel handler；CSRF、CAS、actor/reason 审计 | OperationsManagementTests.CancelRequiresAntiforgeryAndCasAndPersistsAuthenticatedAudit |
| SagaManualRequest / Saga manual handler；Unknown 确认 | OperationsManagementTests.SagaManualActionUsesCoordinatorCasAndUnknownConfirmation |
| Saga manual 补偿重复提交；业务只执行一次、审计只有一条 | OperationsManagementTests.ManualCompensationRequiresCsrfUsesCasAndAuditsOnlyOnce |
| OperationsConsole.Render / Console.html；默认关闭、宿主身份、CSP、textContent | OperationsManagementTests.ConsoleIsOptInAuthenticatedAndProtectedByCsp；浏览器验收限制见 execution.md |
| 样例 SqliteOrderWorkflow.Order / InitializeAsync / ForwardAsync / ConfirmAsync / CompensateAsync | OrderWorkflowSampleTests.PersistedOrderSurvivesRestartConfirmsLostPaymentAndCompensatesInReverse；DuplicateForwardAndCompensationDoNotRepeatBusinessEffectsAndConfirmUsesCorrectLedger |
| ManagementDemo / DurableDemo / Program 新运行入口 | 样例双框架构建运行日志；管理真实服务行为由 OperationsManagementTests 验证 |

## FIX-BND-R01～R04 增量映射

以下方法对应当前最终实现的定向测试。最新证据位于 `evidence/fix-round1/tests/fix-targeted-confirm-net6.trx`、`fix-targeted-confirm-net8.trx`（各 29/29），以及 `evidence/fix-round1/regression-20261027T084302Z/Bing.Resilience.Extensions.Tests-net6.0.trx`、`...net8.0.trx`（各 207/207）。

| 最终生产符号 / 关键行为 | 具体测试方法（类名.方法名） |
|---|---|
| WeightedRoundRobinServiceEndpointSelector.Select；平滑加权比例与等权轮转 | WeightedDiscoveryTests.WeightThreeToOneProducesSeventyFiveTwentyFivePrimaryDistribution；EqualWeightsRotateFairly |
| WeightedRoundRobinServiceEndpointSelector.Select；并发比例、成员/权重变化重置、空快照 | WeightedDiscoveryTests.ConcurrentSelectionsPreserveConfiguredWeightRatio；ChangingWeightsResetsServiceRotationState；ChangingMembershipResetsServiceRotationState；EmptySnapshotReturnsNoCandidatesAndClearsPreviousState |
| WeightedRoundRobinServiceEndpointSelector；TTL、LRU、端点容量与请求候选快照 | WeightedDiscoveryTests.ExpiredRotationStateIsRecreatedAfterRetentionTtl；CapacityEvictsLeastRecentlyUsedServiceRotationState；EndpointCapacityRejectsOversizedSnapshotWithoutPoisoningLaterSelection；PreviouslyReturnedCandidateOrderIsAnImmutableCallSnapshot |
| BingHttpDiscoveryExtensions.AddBingHttpDiscovery；默认 Weighted、选择前契约过滤、请求首次发送比例 | WeightedDiscoveryTests.HttpClientUsesWeightedFirstSendAndFiltersIncompatibleHighWeightEndpoint；该测试通过实际 `HttpClient` 管线和记录型 `HttpMessageHandler` 计数，不是网络 loopback 测试 |
| AddBingHttpDiscovery 显式选择有序主备策略 | WeightedDiscoveryTests.ExplicitOrderedSelectorRemainsAvailableWhenHttpDiscoveryRegistersDefaults |
| RelationalOperationStore.ValidateSchemaAsync；MySQL `{prefix}_due` 名称和 `kind,next_at,id` 列序，与 ClaimDueAsync 的 FORCE INDEX 一致 | MySqlSchemaIndexTests.SchemaValidationAcceptsExpectedDueIndexAndClaimUsesIt；SchemaValidationRejectsEquivalentDueIndexUnderDifferentName；SchemaValidationRejectsMissingDueIndex；SchemaValidationRejectsDueIndexWithWrongColumnOrder |
| ResilienceDiagnostics.RecordLifecycle；Generation 切换/排空、发现刷新/陈旧读取、分布式准入事件及稳定标签 | LifecycleDiagnosticsTests.RuntimeSwitchReportsSuccessFailureCancellationAndDrain；DiscoveryReportsRefreshFailureAndServedStaleSnapshot；DistributedAdmissionReportsAllowedRejectedFailureAndCancellation |
| ReplayWorker、SagaWorker 和 ResilienceDiagnostics；提交后的状态事件、无敏感值标签、诊断监听器异常隔离 | LifecycleDiagnosticsTests.ReplayAndSagaDiagnosticsFollowCommittedSqliteState；ThrowingDiagnosticListenersDoNotChangePublishedBusinessState |
| OperationRetentionPolicy.Validate；ReplayOptions.Retention / SagaOptions.Retention 显式策略校验；ReplayWorker.CleanupOnceAsync / SagaWorker.CleanupOnceAsync | OperationRetentionTests.RetentionRejectsUnboundedOrNonPositiveConfiguration；ReplayCleanupRetainsAuthorizationAndDeduplicationButRemovesPayloadAndResult；SagaCleanupPreservesQueriesAndRejectsReactivation |
| IOperationRetentionStore.CleanupAsync / RelationalOperationStore.CleanupAsync / OperationRetentionResult；终态、Deadline 保护窗口、载荷与尝试/审计分别保留、有界批次 | OperationRetentionTests.SqliteRetentionPreservesProtectedStatesAndBoundsBatches；MySqlRetentionPreservesProtectedStatesAndBoundsBatches；SqliteRetentionRollsBackCancellationAndRedactorFailure；MySqlRetentionRollsBackCancellationAndRedactorFailure |
| OperationRetainedData.Prefix / IsRedacted / Unwrap；幂等摘要和状态保留，已清理载荷不可重新执行 | OperationRetentionTests.ReplayCleanupRetainsAuthorizationAndDeduplicationButRemovesPayloadAndResult；SagaCleanupPreservesQueriesAndRejectsReactivation |
| RelationalOperationStore.CleanupAsync；终态状态门控保留 Replay 的 Unknown/ManualIntervention，Saga 父子同事务且 Unknown 子步骤阻止清理 | OperationRetentionTests.SqliteRetentionPreservesProtectedStatesAndBoundsBatches；MySqlRetentionPreservesProtectedStatesAndBoundsBatches；SqliteSagaCleanupIsAtomicAndProtectsUnknownChildren；MySqlSagaCleanupIsAtomicAndProtectsUnknownChildren；SqliteCleanupAndClaimDoNotEraseInFlightData；MySqlCleanupAndClaimDoNotEraseInFlightData |

保留清理写入 `retained:v1:` 标记。停用清理不恢复已脱敏载荷或已删除历史；回滚版本需继续识别该标记，不能把保留态数据交给不兼容的旧解密/执行路径。

`ReplayOptions.Retention` 与 `SagaOptions.Retention` 的 `null` 默认值及 Worker 的 no-op 分支由生产源码核对；当前上述职责测试覆盖显式启用策略，没有单独断言默认 no-op 的测试方法，因此不把该默认值描述为测试已直接证明。

Replay 的 `Unknown` / `ManualIntervention` 终态保护由 retention state 表测试直接覆盖。Saga `Unknown` 子步骤由 Saga 清理测试直接覆盖；`ManualIntervention` Saga 父记录由生产查询的终态白名单排除，目前没有专门针对该父状态的清理断言。

## 外部证据与保持范围

- Redis 生产代码未修改：`evidence/redis-ha/README.md` 的 14 个阶段使用真实 CSRedisClient/RedisDistributedCircuitStateStore；单宿主 Sentinel + Cluster 限制明确登记。
- 新可选包和所有 Provider：`verify-packages.mjs` 生成独立 nupkg 消费/发布、assets/nuspec/deps 和漏洞审计；消费者不含 ProjectReference。
- 性能：`evidence/performance/Program.cs` 与 `results.md`，三 Provider + 两框架共 78 轮测量，不设生产阈值。
- 完整回归：`verify-tests.mjs` 输出六项目 × 两框架的命令/TRX；变化后的扩展项目最终重验以及旧核心时间敏感用例复验见 `evidence/validation-summary.json`。
- 新包不改变两个客户端主调用链、Provider 算法、热切换、旧流/错误契约；这些范围使用本轮相关项目回归及旧任务不变资源探针证据，不因文档编辑重复执行。

## Round 1 最终验收证据

- 六项目双框架本次完整回归 869/869，0 跳过；明确 12 份 TRX 见 [汇总](evidence/fix-round1/regression-summary.json)，不与旧 805 项相加。
- R05 新增 120 轮正式采样及口径见 [性能报告](evidence/fix-round1/performance-results.md)；两个探针目录保留源代码、运行脚本和原始 JSON。
- 最终 [34 包独立消费](evidence/fix-round1/package-consumption/results.md) 验证新增 WeightedRoundRobinServiceEndpointSelector、RecordLifecycle、IOperationRetentionStore、OperationRetentionPolicy 和 OperationRetainedData 公共入口；三 Provider × 双框架、无 ProjectReference，6 次运行和 6 次发布通过。
- 静态检查 40 个本轮相关文件 UTF-8/空白通过；最终文档修改不影响生产证据，沿用本轮回归。
