# 生产符号到直接测试方法

## 有序收件箱安全留存清理（2026-10-07）

| 生产符号 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `DbOrderedEventStore.DeleteProcessedBeforeAsync` | `Bing.EventBus.Tests.Ordering.DbOrderedEventStoreTest.DeleteProcessedBeforeAsync_ShouldKeepPendingAndFailedRecords` | `framework/tests/Bing.EventBus.Tests/TestResults/ordered-retention-net6-v2.trx`，net6.0 1/1；`ordered-retention-net8-retry.trx`，net8.0 1/1 |
| `OrderedEventCoordinator.DeleteProcessedBeforeAsync` | `Bing.EventBus.Tests.Ordering.OrderedEventCoordinatorTest.DeleteProcessedBeforeAsync_ShouldDeleteCompletedRecordsOnly` | `framework/tests/Bing.EventBus.Tests/TestResults/ordered-retention-coordinator-only.trx`，net6.0 1/1；`ordered-retention-net8-retry.trx`，net8.0 1/1 |

测试覆盖已处理记录删除、待处理和失败记录保留、协调器事务路径、游标可查询性，以及默认关闭、多消费者调度、租约未获取和无消费者配置拒绝。归档历史表和清理后重复事件检查见下方映射；长期容量预算、历史重放窗口和归档表外部清理仍属于宿主部署边界。

| `OrderedEventRetentionHostedService` / `AddBingOrderedEventRetentionScheduler` | `Bing.EventBus.Tests.Ordering.OrderedEventRetentionHostedServiceTest.Scheduler_WhenDisabled_ShouldNotRun`、`Scheduler_WhenEnabled_ShouldDeleteProcessedRecordsForAllConsumers`、`Scheduler_WhenCoordinationLeaseUnavailable_ShouldSkipBatch`、`Scheduler_WhenCoordinationProviderMissing_ShouldRejectStartup`、`Scheduler_WhenNoConsumerIsConfigured_ShouldRejectConfiguration`、`Scheduler_WhenCurrentCleanupIgnoresCancellation_ShouldWaitForCompletion` | `framework/tests/Bing.EventBus.Tests/TestResults/ordered-retention-scheduler-net6-v5.trx` 和 `ordered-retention-scheduler-net8-v5.trx`，两个目标框架各 6/6；`ValidateScopes=true` 覆盖 scoped 租约不被单例捕获 |

## 有序收件历史显式留存清理（2026-10-07）

| 生产符号 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `IOrderedEventStoreArchiveRetention` / `DbOrderedEventStore.DeleteArchivedBeforeAsync` | `Bing.EventBus.Tests.Ordering.DbOrderedEventStoreTest.DeleteArchivedBeforeAsync_ShouldDeleteOnlyExpiredHistory` | `ordered-archive-retention-net6-v1.trx`、`ordered-archive-retention-net8-v1.trx`，覆盖 UTC 截止时间、批量删除、历史表与收件箱隔离 |
| `OrderedEventCoordinator.DeleteArchivedBeforeAsync` | `Bing.EventBus.Tests.Ordering.OrderedEventCoordinatorTest.DeleteArchivedBeforeAsync_ShouldDeleteExpiredHistoryOnly` | 同上，覆盖协调器独立事务和可选契约路径 |
| `DbOrderedEventStore` / `OrderedEventCoordinator` 历史清理的取消与回滚 | `DbOrderedEventStoreTest.DeleteArchivedBeforeAsync_WhenTransactionRollsBack_ShouldKeepHistory`、`DeleteArchivedBeforeAsync_WhenCanceled_ShouldKeepHistory`、`OrderedEventCoordinatorTest.DeleteArchivedBeforeAsync_WhenCanceled_ShouldNotBeginTransaction` | 包含原有正常路径在内的五项筛选测试 net6/net8 各 5/5；`ordered-archive-cancel-rollback-net6-v2.trx`、`ordered-archive-cancel-rollback-net8-v2.trx`，覆盖事务回滚、底层取消预检和协调器开启事务前取消 |
| `Bing.EventBus.Tests` 历史清理取消补测后的完整回归 | `Bing.EventBus.Tests` 全量回归 | net6.0/net8.0 各 331/331；`ordered-archive-history-full-net6-v2.trx`、`ordered-archive-history-full-net8-v2.trx` |
| 新增历史清理契约的整体回归 | `Bing.EventBus.Tests` 全量 328/328（net6.0、net8.0） | `ordered-archive-history-full-net6-v1.trx`、`ordered-archive-history-full-net8-v1.trx` |

历史清理没有接入默认后台调度器；MySQL 真实语法、跨节点租约和业务重放窗口仍需部署验收。

组合回归：`DeleteProcessedBeforeAsync` 存储、协调器和调度器筛选在 net6.0/net8.0 各 8/8 通过，证据为 `framework/tests/Bing.EventBus.Tests/TestResults/ordered-retention-full-net6-v2.trx` 和 `ordered-retention-full-net8-v2.trx`。

| 生产符号 | 测试项目与方法 |
| --- | --- |
| `LocalLock.LockTakeAsync` / `LockRelease` | `Bing.Core.Tests`: `LockTakeAsync_ShouldReleaseAfterThreadSwitch`, `LockRelease_ShouldCheckOwnerAndWaiter` |
| `LockExtensions.LockTak/LockRelease` 与 `ILockLeaseProvider` | `Bing.Core.Tests`: `LockTakAndRelease_WhenOwnerGenerationChanges_ShouldNotReleaseSuccessor`, `TryAcquireLeaseAsync_WhenWaitingIsCancelled_ShouldReclaimWaiter`, `TryAcquireLeaseAsync_WhenLockIsResolvedAsInterface_ShouldUseProvider`, `AddLocalLock_CustomImplementationWithoutLease_ShouldKeepLeaseOptional` | `locks-net8-registration-boundary.trx`, `locks-net6-registration-boundary.trx`，net8/net6 各 21/21 |
| `LocalLock.TryAcquireLeaseAsync` | `Bing.Core.Tests`: `TryAcquireLeaseAsync_ShouldCancelAndReleaseOnce` |
| `CSRedisDistributedLock.LockTakeAsync` / `LockReleaseAsync` | `Bing.Caching.CSRedis.Tests.Integration`: `LockTakeAsync_ConcurrentOwners_ShouldHaveOneWinner`, `LockReleaseAsync_ShouldCheckOwnerAfterExpiry`, `LockTakeAsync_LeaseOverMinute_ShouldRemainHeld` |
| `CSRedisDistributedLock.TryAcquireLeaseAsync` | `Bing.Caching.CSRedis.Tests.Integration`: `TryAcquireLeaseAsync_ShouldWaitAndCancel` |
| `LocalEventBus.PublishAsync` | `Bing.EventBus.Tests`: `PublishAsync_ConcurrentSubscriptionChanges_UsesInitialSnapshot` |
| `LocalTokenBucketRateLimiter.TryAcquire` / `TryAcquireAsync` | `Bing.Core.Tests`: `TryAcquire_BurstAndRefill_ReturnsPreciseRetryAfter`, `TryAcquire_ConcurrentCalls_GrantOnlyAvailableTokens`, `TryAcquire_KeysAreIndependent_AndMaximumKeysIsBounded`, `TryAcquireAsync_CancelledRequest_ShouldNotConsumePermit` |
| `RedisTokenBucketRateLimiter.TryAcquireAsync` | `Bing.Caching.CSRedis.Tests.Integration`: `TryAcquireAsync_MultipleInstances_ShouldShareQuota`, `TryAcquireAsync_ConcurrentCalls_ShouldAllowOnlyCapacity`, `TryAcquireAsync_IdleKey_ShouldBeReclaimed` |
| `RedisRateLimitExtensions.AddBingRedisRateLimiting` | `Bing.Caching.CSRedis.Tests.Integration`: `AddBingRedisRateLimiting_ShouldRegisterOptionsAndLimiter` |
| `BingRateLimitMiddleware.InvokeAsync` | `Bing.AspNetCore.Mvc.Tests`: `InvokeAsync_Rejected_ShouldReturnRetryAfter`, `InvokeAsync_AuthenticatedUser_ShouldUseSubjectId`, `InvokeAsync_TenantClaim_ShouldIsolateTenantBucket`, `InvokeAsync_RouteKeySelector_ShouldUseStableRoute`, `InvokeAsync_RouteEndpoint_ShouldUseRoutePattern`, `InvokeAsync_LimiterFailure_ShouldReturnUnavailable`, `InvokeAsync_SeparatorInCustomKey_ShouldKeepBucketsSeparate` |
| `BingHttpContextExtensions.GetRemoteIpAddress` | `Bing.AspNetCore.Mvc.Tests`: `GetRemoteIpAddress_WhenForwardedHeaderIsUntrusted_ShouldUseConnectionAddress`, `GetRemoteIpAddress_WhenConnectionAddressIsMissing_ShouldReturnNull` |
| `AuditingManager.BeginScope` / `AuditHandle.SaveAsync` | `Bing.Auditing.Tests`: `BeginScope_Nested_ShouldRestoreParent`, `SaveAsync_ConcurrentCalls_ShouldStoreOnce`, `BeginScope_ParallelFlows_ShouldNotShareLog` |
| `SimpleLogAuditingStore.SaveAsync` | `Bing.Auditing.Tests`: `DefaultStore_ShouldNotLogSensitiveContents` |
| `LocalTokenBucketRateLimiter.PruneIdleCore` | `Bing.Core.Tests`: `PruneIdle_ExhaustedBucket_ShouldRetainQuotaUntilRefilled` |
| `RedisTokenBucketRateLimiter.TryAcquireAsync` 的闲置回收与策略一致性 | `Bing.Caching.CSRedis.Tests.Integration`: `TryAcquireAsync_IdleBeforeRefill_ShouldKeepExhaustedQuota`, `TryAcquireAsync_OtherBucket_ShouldNotShortenSharedRedisExpiry`, `TryAcquireAsync_SamePolicyWithDifferentSettings_ShouldFailFast` |
| `CSRedisDistributedLock.ExecuteWithLock` 的原异常保留 | `Bing.Caching.CSRedis.Tests.Integration`: `ExecuteWithLock_WhenCallbackAndReleaseFail_ShouldKeepCallbackError`, `ExecuteWithLockAsync_WhenCallbackAndReleaseFail_ShouldKeepCallbackError` |
| `AuditingManagerExtensions.BeginAsyncScope` / `AuditHandle.DisposeAsync` | `Bing.Auditing.Tests`: `BeginAsyncScope_InFlightSave_ShouldWaitBeforeDisposeCompletes`, `BeginAsyncScope_SaveFailure_ShouldPropagateOnDispose`, `BeginScope_InFlightSave_ShouldRequireAsyncDispose` |
| `ClusterEventHeaders.Write` / `Read`、`MessageEventBus.PublishAsync` | `Bing.EventBus.Tests`: `PublishAsync_OrderedEvent_ShouldWriteMetadataOnce`, `Read_LegacyAndInvalidOrderedHeaders_ShouldHandleExplicitly` |
| `LegacyCapMessageEventBusAdapter.PublishAsync` | `Bing.EventBus.Tests`: `PublishAsync_LegacyEvent_ShouldPreserveIdAndPublishOnce`, `PublishAsync_LegacyImmediateEvent_ShouldPublishToCapOnce`, `PublishAsync_LegacyDeferredEvent_ShouldWaitForTransactionAction` |
| `AddLegacyCapMessageEventBusAdapter` | `Bing.EventBus.Tests`: `AddLegacyCapMessageEventBusAdapter_ShouldBeExplicitAndIdempotent`, `AddLegacyCapMessageEventBusAdapter_ExistingBus_ShouldRemainSelected` |
| `ClusterEventHeaders` 的真实出箱与消费链路 | `Bing.Admin.EFCore.Tests`、`Bing.Admin.FreeSQL.Tests`: `SaveChangesAsync_OrderedEvent_ShouldPersistHeadersAndReachSubscriber` |
| `TransactionActionManager.CommitAsync` 的分轮、上限与失败清理 | `Bing.Data.Tests`: `CommitAsync_ActionRegistersAnotherAction_ShouldRunNextRound`, `CommitAsync_ActionKeepsRegistering_ShouldStopAtRoundLimit`, `CommitAsync_ActionThrows_ShouldDiscardPendingActions`, `CommitAsync_ConcurrentCall_ShouldRejectSecondCommit` |
| 事务中派生 CAP 消息的两 ORM 路径 | `Bing.Admin.EFCore.Tests`、`Bing.Admin.FreeSQL.Tests`: `SaveChangesAsync_DerivedMessage_ShouldPublishInSameTransaction` |
| `PostCommitNotificationException`、EFCore `UnitOfWorkBase.SaveChanges/SaveChangesAsync` 的保存后失败语义 | `Bing.EntityFrameworkCore.Tests`: `SaveChanges_WhenPostCommitNotificationFails_ShouldReportCommittedData`, `SaveChangesAsync_WhenPostCommitNotificationFails_ShouldReportCommittedData`, `SaveChangesAsync_WhenExternalTransactionExists_ShouldNotClaimFinalCommit` |
| FreeSQL `UnitOfWorkBase.SaveChanges/SaveChangesAsync`、`SaveChangesAfter`、`PublishEventsAsync` | `Bing.FreeSQL.Tests`: `SaveChanges_WhenSuccessful_ShouldNotifyOnce`, `SaveChangesAsync_WhenTransactionSucceeds_ShouldNotifyOnceAfterCommit`, `SaveChanges_WhenNotificationFails_ShouldPreserveCommittedData`, `SaveChangesAsync_WhenNotificationFails_ShouldPreserveCommittedData`, `SaveChangesAsync_WhenCancelledBeforeTransactionActions_ShouldNotCommitActions` |
| `IHasDomainEvents`、`BasicAggregateRoot` 事件契约 | `Bing.Ddd.Domain.Tests`: `IHasDomainEvents_ShouldExposeAggregateEvents` |
| `DomainEventDispatchSession.DispatchAsync/Complete` | `Bing.Ddd.Domain.Tests`: `DispatchAsync_DuplicateEventInstance_ShouldDispatchEachOccurrence`, `DispatchAsync_WhenHandlerReplacesEvent_ShouldRejectMutation`; `Bing.EntityFrameworkCore.Tests`: `SaveChangesAsync_WhenDomainEventsRepeat_ShouldStopAtRoundLimit` |
| EFCore `UnitOfWorkBase.PrepareDomainEvents`、事务绑定顺序 | `Bing.EntityFrameworkCore.Tests`: `SaveChanges_WithDomainEvents_ShouldDispatchInTransaction`, `SaveChangesAsync_WithDomainEvents_ShouldDispatchDerivedEventsInTransaction`, `SaveChangesAsync_WhenDomainDispatchFails_ShouldRollbackAndRetainEvents`, `SaveChangesAsync_WithDomainEventsAndExternalTransaction_ShouldRejectBeforeDispatch` |
| FreeSQL `UnitOfWorkBase.RegisterDomainEventSource`、事务分发及清理 | `Bing.FreeSQL.Tests`: `SaveChanges_WithDomainEventSource_ShouldDispatchOnce`, `SaveChangesAsync_WithDomainEventSource_ShouldDispatchOnce`, `SaveChangesAsync_WhenDomainEventDispatchFails_ShouldRollback`, `SaveChangesAsync_WithDomainEventAndExternalTransaction_ShouldRejectBeforeDispatch`, `SaveChangesAsync_WhenDomainEventRemovalFails_ShouldReportPostCommitFailure` |
| Admin MySQL/CAP 验收脚本的 TRX 计数判定 | `eng/ci/tests/Invoke-AdminMySqlCapIntegrationTests.Tests.ps1`: `Test-TrxCounters` 的 EFCore 13 项、FreeSQL 15 项达标及不足、失败、跳过断言 |
| `EventConsumptionDatabase`、`IEventConsumptionStore.TryRecordAsync`、`EventConsumptionStore.TryRecordAsync` | `Bing.EventBus.Tests`: `TryRecordAsync_SameConsumerAndEvent_ShouldReturnFalseOnDuplicate`, `TryRecordAsync_DifferentConsumers_ShouldRecordIndependently`, `TryRecordAsync_BusinessRollback_ShouldRemoveReceiptAndAllowRetry`, `TryRecordAsync_InvalidKeyOrCancellation_ShouldNotWriteReceipt` |
| MySQL/CAP 消费幂等及事务回滚 | `Bing.Admin.EFCore.Tests`、`Bing.Admin.FreeSQL.Tests`: `DuplicateDelivery_ShouldCommitBusinessAndReceiptOnce`, `ConsumerFailure_ShouldRollbackReceiptAndAllowRetry`; `Bing.Admin.EFCore.Tests`: `ConcurrentConsumers_ShouldAllowOnlyOneReceipt` |
| EFCore/FreeSQL `UnitOfWorkBase.TransactionCommit`、`SaveChangesCoreAsync` 与 Admin CAP 事务钩子 | `Bing.EntityFrameworkCore.Tests`: `SaveChanges_WithDomainEvents_ShouldDispatchInTransaction`, `SaveChangesAsync_WithDomainEvents_ShouldDispatchDerivedEventsInTransaction`; `Bing.FreeSQL.Tests`: `SaveChanges_WithDomainEventSource_ShouldDispatchOnce`, `SaveChangesAsync_WithDomainEventSource_ShouldDispatchOnce`; 两个 Admin 测试项目：`DomainEvent_ShouldPublishToCapAfterBusinessCommit`, `DomainEvent_WhenBusinessFails_ShouldRollbackCapMessage`, `DomainEvent_SyncSave_ShouldPublishInCapTransaction` |
| CAP 测试宿主订阅就绪探针 | `Bing.Admin.EFCore.Tests`、`Bing.Admin.FreeSQL.Tests`: `SaveChangesAsync_OrderedEvent_ShouldPersistHeadersAndReachSubscriber`, `DuplicateDelivery_ShouldCommitBusinessAndReceiptOnce` |

P1 的发布侧元数据、旧接口适配、两 ORM 的显式出箱及领域事件到真实 CAP 消费路径、事务内领域事件分发、业务消费幂等及保存后通知失败语义已有直接测试；P2/P3/P4 余项尚未实现，不把模板静态检查列为动态行为测试。

## 有序消费与提交诊断新增映射

测试存在与测试通过分别记录；实际结果见 execution.md。MySQL 与 CAP 用例必须启用专用环境门控，不以 SQLite 代替。

有序收件清理还要求 `bing_event_inbox_archive` 历史表与收件箱同事务迁移；SQLite 直接测试覆盖归档后重复事件 ID 的幂等检查，MySQL 迁移脚本和真实语法验证仍需专用环境。

| 生产符号 | 测试项目与直接方法 |
| --- | --- |
| `OrderedEventSchema.GetCreateStatements`、`DbOrderedEventStore.AllocateSequenceAsync` | `Bing.EventBus.Tests`: `AllocateSequenceAsync_WhenRolledBack_ShouldNotLeaveGap`; `Bing.FreeSQL.Tests`: `AllocateSequence_TwoProcesses_ShouldCommitContiguousNumbers` |
| `OrderedEventSchema` 历史表脚本、`DbOrderedEventStore.DeleteProcessedBeforeAsync` 的归档与重复检查 | `Bing.EventBus.Tests`: `OrderedEventSchemaTest`（MySQL/SQLite 完整 SQL）、`DbOrderedEventStoreTest.DeleteProcessedBeforeAsync_ShouldKeepPendingAndFailedRecords`（归档数量、清理后原事件幂等、复用事件 ID 冲突拒绝）；net6.0/net8.0 各 10/10，证据见 `ordered-retention-archive-net6.trx`、`ordered-retention-archive-net8.trx` |
| 有序收件历史改动的整体回归 | `Bing.EventBus.Tests` 全量 326/326；`ordered-archive-eventbus-full-net6.trx`、`ordered-archive-eventbus-full-net8.trx` |
| `OrderedEventStream`、`OrderedEventMessage`、`DbOrderedEventStore.GetStreamKey/EnqueueAsync` | `Bing.EventBus.Tests`: `EnqueueAsync_WhenKeysDiffer_ShouldKeepStreamsAndConsumersIsolated`, `EnqueueAsync_WhenInvalidOrCanceled_ShouldRejectWithoutNewMessage` |
| `DbOrderedEventStore.ProcessNextAsync/GetStateAsync` | `Bing.EventBus.Tests`: `ProcessNextAsync_WhenArrivingOutOfOrder_ShouldProcessOnceInSequence`, `ProcessNextAsync_WhenTransactionRollsBack_ShouldRetryBusinessAndCursor`; `Bing.FreeSQL.Tests`: `Consume_TwoProcesses_ShouldCommitFortyEventsInOrder`, `ProcessNextAsync_RepeatableReadSnapshot_ShouldSeeCommittedCursor` |
| `DbOrderedEventStore.RecordFailureAsync/RetryAsync/GetPendingStreamsAsync`、`OrderedEventStreamState` | `Bing.EventBus.Tests`: `ProcessNextAsync_WhenPredecessorFails_ShouldRequireRetryOnlyForThatStream`, `RecordFailureAsync_WhenSequenceAlreadyProcessed_ShouldNotFailSuccessor`, `GetPendingStreamsAsync_WhenFirstStreamFails_ShouldPageToOtherStreams` |
| `DbInventoryStore.TryDeductAsync` / `InventorySchema` | `Bing.EventBus.Tests`: `TryDeductAsync_WhenRequestRepeats_ShouldApplyOnce`, `TryDeductAsync_WhenQuantityIsInsufficient_ShouldRemainNonNegative`, `TryDeductAsync_WhenRequestIdentityConflicts_ShouldReject`, `TryDeductAsync_WhenCallerRollsBack_ShouldNotConsumeOrReserveRequest` |
| `AddBingInventoryStore` | `Bing.EventBus.Tests`: `AddBingInventoryStore_ShouldRegisterSingletonWithoutReplacement` |
| `InventoryBatchOperations.DeductAsync` | `Bing.EventBus.Tests`: `InventoryBatch_WhenRequestsAreUnsorted_ShouldUseStableOrder`、`InventoryBatch_WhenOneRequestIsInsufficient_ShouldStopAndThrow`、`InventoryBatch_WhenRequestIdsMatchAcrossTenants_ShouldProcessBoth` |
| `EfCoreInventoryExtensions.TryDeductInventoryAsync` | `Bing.EntityFrameworkCore.Tests`: `TryDeductInventoryAsync_ShouldReuseCurrentEfCoreTransaction` |
| `FreeSqlInventoryExtensions.TryDeductInventoryAsync` | `Bing.FreeSQL.Tests`: `TryDeductInventoryAsync_ShouldReuseCurrentFreeSqlTransaction` |
| `StoreBase<TEntity,TKey>` 的 FreeSQL 审计字段入口 | `Bing.FreeSQL.Tests`: `StoreBase_WhenAuditPropertySetterIsRegistered_ShouldSetCreateAndModifyProperties` |
| `IAuditPropertySetter` 契约及旧程序集转发 | `Bing.Auditing.Tests`: `IAuditPropertySetter_LegacyAssemblyQualifiedName_ShouldResolve` |
| `OrderedEventCoordinator.ReceiveAsync/ProcessNextAsync/RetryAsync` | `Bing.EventBus.Tests`: `ReceiveAsync_WhenMessagesArriveThreeOneTwo_ShouldPersistAndProcessInOrder`, `ProcessNextAsync_WhenHandlerFails_ShouldRecordFailureAndRetry`, `ProcessNextAsync_WhenCanceled_ShouldNotRecordFailure`, `RetryAsync_WhenAuthorizationIsMissing_ShouldRejectBeforeOpeningTransaction`, `RetryAsync_WhenRecoveryIsDenied_ShouldRejectBeforeOpeningTransaction`, `RetryAsync_WhenCommitted_ShouldRecordRetryOnce`, `RetryAsync_WhenCommitOutcomeIsUnknown_ShouldNotRecordRetry`, `RetryAsync_WhenDisposeFailsAfterCommit_ShouldRecordRetry` |
| `IOrderedEventRecoveryAuthorizer` / `AddBingOrderedEventRecoveryAuthorization` | `Bing.EventBus.Tests`: `AddBingOrderedEventRecoveryAuthorization_ShouldRegisterScopedAuthorizer`、`RetryAsync_WhenRecoveryIsDenied_ShouldRejectBeforeOpeningTransaction` |
| `OrderedEventRecoveryService` 的授权后审计前置检查 | `Bing.EventBus.Tests`: `GetStateAsync_WhenAuditIsMissing_ShouldRejectAfterAuthorization`, `RetryAsync_WhenAuditIsMissing_ShouldRejectBeforeRetry`, `RetryAsync_WhenCommitted_ShouldWriteSuccessfulAudit`, `RetryAsync_WhenAuditFailsAfterCommit_ShouldPreserveCommittedOutcome` |
| `OrderedEventCoordinator` 事务结果及清理诊断 | `Bing.EventBus.Tests`: `ProcessNextAsync_WhenCommitThrows_ShouldKeepOriginalAndUnknownOutcome`, `ProcessNextAsync_WhenDisposeFailsAfterCommit_ShouldReportCommitted` |
| `OrderedEventPublisher.PublishAsync` | `Bing.EventBus.Tests`: `PublishAsync_WhenCapTransactionMatches_ShouldAllocateAndPublishOnce`, `PublishAsync_WhenCapFailsAndCallerRollsBack_ShouldReuseSequence`, `PublishAsync_WhenTransactionIsUnrelatedOrMessageAllocated_ShouldRejectBeforeWrite` |
| `OrderedEventConsumerExtensions.AddBingOrderedConsumer`、`IOrderedEventHandler`、后台消费者 | `Bing.EventBus.Tests`: `HostedWorker_WhenBlockedStreamsFillFirstPage_ShouldProcessOthersInSeparateScopes`, `HostedWorker_WhenEventIsDeliveredTwice_ShouldHandleOnceAndStop`, `AddBingOrderedConsumer_WhenSameConsumerRepeats_ShouldReject` |
| `EfCoreOrderedEventTransactionFactory.BeginAsync`、`IOrderedEventTransaction` 所有权与释放 | `Bing.EntityFrameworkCore.Tests`: `BeginAsync_WhenCommitted_ShouldPersistEventBusinessAndCursor`, `BeginAsync_WhenExternalTransactionExists_ShouldLeaveItOwnedByCaller`, `DisposeAsync_ConcurrentCalls_ShouldAwaitOneNativeCleanup`, `DisposeAsync_WhenRollbackAndNativeDisposeFail_ShouldPreserveBothOnce`, `DisposeAsync_WhenCalledTwice_ShouldRejectFurtherOperationsAndKeepContext` |
| `FreeSqlOrderedEventTransactionFactory.BeginAsync`、事务终态和唯一释放 | `Bing.FreeSQL.Tests`: `BeginAsync_WhenCommitted_ShouldPersistEventBusinessAndCursor`, `BeginAsync_WhenRolledBackOrDisposed_ShouldLeaveNoWrites`, `DisposeAsync_WhenRollbackAndNativeDisposeFail_ShouldPreserveBothOnce`, `DisposeAsync_WhenCalledTwice_ShouldRejectFurtherOperationsAndKeepOrm` |
| 有序业务中断恢复 | `Bing.FreeSQL.Tests`: `Consume_WhenProcessDiesBeforeCommit_ShouldRollbackAndRetry`, `Consume_WhenProcessDiesAfterCommit_ShouldNotRepeatBusiness` |
| `EventConsumptionStore.TryRecordAsync` 的目标重复判定 | `Bing.EventBus.Tests`: `TryRecordAsync_WhenOtherConstraintFails_ShouldPropagateStorageError`, `TryRecordAsync_WhenSchemaInvalid_ShouldPropagateStorageError`; `Bing.FreeSQL.Tests`: `TryRecordAsync_WhenTargetExists_ShouldReturnFalse`, `TryRecordAsync_WhenCheckFails_ShouldPropagateError`, `TryRecordAsync_WhenOtherUniqueKeyConflicts_ShouldPropagateError`, `TryRecordAsync_WhenSnapshotIsStale_ShouldReturnFalseForCommittedTarget` |
| `TransactionOutcome`、`TransactionOutcomeDiagnostics`、`PostCommitNotificationException` | `Bing.Data.Tests`: `GetTransactionOutcome_UnmarkedException_ShouldBeUnknown`, `SetTransactionOutcome_ShouldPreserveOriginalException`, `Diagnostics_InvalidArguments_ShouldFail`, `PostCommitNotificationException_ShouldExposeExplicitOutcome` |
| `DomainEventDispatchSession.CompleteAfterCommitFailure` | `Bing.Ddd.Domain.Tests`: `CompleteAfterCommitFailure_WhenRemovalSucceeds_ShouldClearDispatchedEvents`, `CompleteAfterCommitFailure_WhenRemovalFails_ShouldAttachCleanupError`, `CompleteAfterCommitFailure_WhenExceptionIsNull_ShouldReject` |
| FreeSQL `DeferredCommitUnitOfWork` 保存阶段不提前提交 | `Bing.FreeSQL.Tests`: `SaveChangesAsync_WhenTransactionActionFails_ShouldRollbackActionChanges`, `SaveChangesAsync_WhenCancelledAfterTransactionAction_ShouldRollbackActionChanges`, `Commit_WhenTransactionActionFails_ShouldRollbackActionChanges` |
| `OrderedEventPublisher`、真实 CAP 订阅与后台消费闭环 | `Bing.Admin.FreeSQL.Tests`: `PublishAsync_WhenCommitted_ShouldReceiveAndProcessOnceInOrder`, `PublishAsync_WhenRolledBack_ShouldRetryFromFirstSequence`, `ReceiveAsync_WhenCapMessagesArriveOutOfOrder_ShouldProcessInSequence`；门控 MySQL/CAP/RabbitMQ 测试，结果另记 |

当前发布器 SQLite 测试中的 CAP 是边界替身，只证明分配及调用契约，不能替代真实 CAP/RabbitMQ 链路；后者另列环境测试和执行结果。

本轮验证事实（仅对应已运行批次）：Admin EFCore 有序 CAP 16/16、Admin FreeSQL 有序 CAP 15/15；FreeSQL 有序 MySQL 集成测试串行化后 68/68；Sample Lazy 冒烟 1/1。`OrderedEventPublisher` 对 EFCore `IDbContextTransaction` 包装事务的兼容行为有 4 项 net6 定向测试。上述计数不扩展为全仓库最新回归或蓝绿部署通过。

| 生产符号与兼容行为 | 测试项目与直接方法 |
| --- | --- |
| `OrderedEventPublisher.PublishAsync` 使用 EFCore `IDbContextTransaction` 包装并保持原生事务身份 | `Bing.EventBus.Tests`（net6）: `PublishAsync_WhenCapTransactionMatches_ShouldAllocateAndPublishOnce`, `PublishAsync_WhenCapTransactionWrapsDatabaseTransaction_ShouldAllocateAndPublish`, `PublishAsync_WhenCapFailsAndCallerRollsBack_ShouldReuseSequence`, `PublishAsync_WhenTransactionIsUnrelatedOrMessageAllocated_ShouldRejectBeforeWrite` |
| Admin EFCore 真实 CAP 有序发布、重复与乱序处理 | `Bing.Admin.EFCore.Tests`: 该轮新增有序 CAP 16 项门控测试，实际方法映射与 TRX 以门控产物为准 |
| Admin FreeSQL 真实 CAP 有序发布、重复与乱序处理 | `Bing.Admin.FreeSQL.Tests`: `PublishAsync_WhenCommitted_ShouldReceiveAndProcessOnceInOrder`, `PublishAsync_WhenRolledBack_ShouldRetryFromFirstSequence`, `ReceiveAsync_WhenCapMessagesArriveOutOfOrder_ShouldProcessInSequence`，该轮门控 15 项 |
| FreeSQL MySQL 有序并发、进程中断与当前读 | `Bing.FreeSQL.Tests`: `OrderedEventMySqlProcessTest` 相关串行集成批次共 68 项 |
| Sample Lazy 业务解析与正常停止 | `Bing.Samples.WebApi.Tests`: `SampleWebApiSmokeTest.LazyEndpoint_ShouldResolveBusinessValueAndReleasePortOnStop`，1/1 |

### 有序失败原子性与只读状态查询

以下为新增直接测试映射，方法存在不代表已经通过当前候选回归；实际运行结果以新的 TRX 和 execution.md 为准。

| 生产符号与关键行为 | 测试项目与直接方法 |
| --- | --- |
| `OrderedEventCoordinator.ExecuteBusinessAsync/RunAsync` 保存点回滚、同事务提交失败标记 | `Bing.EventBus.Tests`: `ProcessNextAsync_WhenBusinessFails_ShouldCommitOnlyFailureMarker` |
| `OrderedEventCoordinator.ExecuteBusinessAsync` 执行中取消不记录失败 | `Bing.EventBus.Tests`: `ProcessNextAsync_WhenCanceledDuringBusiness_ShouldRollbackWithoutFailureMarker` |
| `OrderedEventCoordinator.RunAsync` 失败标记提交前、提交后异常与 Unknown 诊断 | `Bing.EventBus.Tests`: `ProcessNextAsync_WhenFailureMarkerCommitThrowsBeforeNativeCommit_ShouldRemainPending`, `ProcessNextAsync_WhenFailureMarkerCommitThrowsAfterNativeCommit_ShouldRetainRecordedState` |
| `OrderedEventCoordinator.RunAsync` 失败标记已提交后的释放异常 | `Bing.EventBus.Tests`: `ProcessNextAsync_WhenDisposeThrowsAfterFailureMarkerCommit_ShouldRetainBusinessFailure` |
| `OrderedEventCoordinator.ExecuteBusinessAsync` 重复使用异常对象时的本轮标志 | `Bing.EventBus.Tests`: `ProcessNextAsync_WhenSameExceptionIsReused_ShouldReportOnlyCurrentFailureMarkerOutcome` |
| `DbOrderedEventStore.GetStateAsync` 单条只读查询、未知流不建游标、缺口和消费者隔离 | `Bing.EventBus.Tests`: `GetStateAsync_WhenConnectionIsReadOnly_ShouldReturnStateWithoutWriting`, `GetStateAsync_WhenStreamIsUnknown_ShouldLeaveAllTablesEmpty`, `GetStateAsync_ShouldDescribeGapFailureCompletionAndIsolation` |
| `OrderedEventCoordinator.ApplySavepointAsync` 与两套 ORM 的真实 SQLite 事务 | `Bing.EntityFrameworkCore.Tests`、`Bing.FreeSQL.Tests`: `Coordinator_WhenBusinessFails_ShouldRollbackSavepointAndBlockStream` |
| `OrderedEventCoordinator.ApplySavepointAsync` 自定义存储无原生保存点的预拒绝和回滚 | `Bing.FreeSQL.Tests`: `Coordinator_WhenCustomStoreLacksNativeSavepoints_ShouldRollbackBeforeBusiness`；真实SQLite，验证结果另记 |
| `OrderedEventCoordinator` 保持游标锁提交失败标记及 `DbOrderedEventStore.GetStateAsync` 并发只读 | `Bing.FreeSQL.Tests`: `ProcessNextAsync_WhenFailureIsWaitingToCommit_ShouldKeepCursorLocked`, `GetStateAsync_WhenBusinessHoldsWriteLock_ShouldReadCommittedSnapshotWithoutWriting`；MySQL 专用环境门控 |

### 入箱死锁有界重试

以下七项位于 `Bing.FreeSQL.Tests` 的 `OrderedEventReceiveRetryMySqlTest`，均由 `IntegrationFact("MySql")` 门控；测试使用真实 MySqlConnector 事务和 `SIGNAL` 注入原生错误形状，并非制造真实 InnoDB 死锁或提交断连。七项测试在net6/net8均7/7通过、0跳过，见`artifacts/ordered-receive-retry-regressions/receive-retry-direct-net6.trx`及`receive-retry-direct-net8.trx`；受影响全量另记execution.md。`MySql.Data`分支仍无直接覆盖。

| 生产符号与关键行为 | 直接测试方法 |
| --- | --- |
| `OrderedEventCoordinator.ReceiveAsync/IsRetryableReceiveFailure` 1213 已回滚时重试一次并避免重复入箱 | `ReceiveAsync_WhenFirstAttemptDeadlocks_ShouldRetryAndInsertOnce` |
| `OrderedEventCoordinator.ReceiveAsync` 最多四次尝试并保留最后原始驱动异常 | `ReceiveAsync_WhenAllAttemptsDeadlock_ShouldThrowLastNativeError` |
| `IsRetryableReceiveFailure` 拒绝附带清理异常的回滚结果 | `ReceiveAsync_WhenCleanupFails_ShouldNotRetry` |
| `OrderedEventCoordinator.ReceiveAsync` 在回滚释放后取消退避等待 | `ReceiveAsync_WhenCanceledAfterRollback_ShouldNotStartAnotherAttempt` |
| `IsRetryableReceiveFailure` 拒绝提交阶段结果未知的 1213 | `ReceiveAsync_WhenCommitSignalsDeadlock_ShouldNotRetryUnknownOutcome` |
| `IsRetryableReceiveFailure` 拒绝原生 MySQL 1205 锁等待超时 | `ReceiveAsync_WhenLockWaitTimesOut_ShouldNotRetry` |
| `IsRetryableReceiveFailure` 仅支持内置 `DbOrderedEventStore` | `ReceiveAsync_WhenStoreIsCustom_ShouldNotRetry` |

## CAP 宿主释放与失败验收诊断

| 生产符号 | 测试项目与直接方法 |
| --- | --- |
| `Extensions.AddCapEventBus/ConfigureHostedLifecycle` | `Bing.EventBus.Tests.Cap.CapHostedLifecycleTest`: `AddCapEventBus_WhenRepeated_ShouldPreserveOtherServicesAndSingleCapHost`, `StartAsync_WhenProcessorIsStarting_ShouldWaitForCompletion` |
| `CapHostedLifecycleService.StartAsync/StartCoreAsync` | `CapHostedLifecycleTest`: `StartAsync_WhenAlreadyCancelled_ShouldNotStartProcessor`, `StartAsync_WhenCallerCancels_ShouldWaitForSafeCleanup`, `StartAndStopAsync_WhenConcurrent_ShouldExecuteOnce` |
| `CapHostedLifecycleService.StopAsync/EnsureShutdownAsync/ShutdownCoreAsync` | `CapHostedLifecycleTest`: `StopAsync_WhenProcessorIsStarting_ShouldNotDisposeActiveProcessor`, `StopAsync_WhenWaitIsCancelled_ShouldKeepStartupAndCleanupRunning`, `StopAsync_WhenProcessorDisposeBlocks_ShouldCancelWaitingAndFinishCleanupLater`；真实 Admin 宿主：`DomainEvent_WhenBusinessFails_ShouldRollbackCapMessage` |
| `CapHostedLifecycleService.Dispose/DisposeAsync` 及双失败 | `CapHostedLifecycleTest`: `DisposeAsync_WhenStarting_ShouldWaitAndShareCleanup`, `StartAsync_WhenBootstrapAndCleanupFail_ShouldKeepBothErrorsAndShutdownOnce`、以上两容器释放路径；原生边界：`NativeConsumer_WhenStartedWithCancelledToken_ShouldExposeDisposedCancellationSource` |
| `New-TestFailureDiagnostic/Get-ExistingTrxPaths` | `eng/ci/tests/Invoke-AdminMySqlCapIntegrationTests.Tests.ps1`: 两项目TRX、无TRX、测试退出码17/9、TRX门槛失败但测试退出码0的三项诊断断言；原预检及13/15门槛保持 |

CAP 原生行为特征测试故意断言直接调用已取消消费者会失败，证明限制仍存在；Bing 宿主入口的启动/关闭测试断言安全路径。实际通过结果另记 execution.md，不据此承诺 CAP 的全部监听线程已经排空。




### HTTP 审计与流量排空新增映射

| 生产符号与关键行为 | 测试项目与直接方法 |
| --- | --- |
| `BingHttpAuditingMiddleware.InvokeAsync` 请求元数据采集、正文不读取、端点禁用标记 | `Bing.AspNetCore.Mvc.Tests`: `BingHttpAuditingMiddlewareTest.InvokeAsync_ShouldSaveRequestMetadata`、`InvokeAsync_WhenEndpointDisablesAuditing_ShouldSkipStore` |
| `BingHttpAuditingMiddleware` 保存失败和业务异常优先级 | `Bing.AspNetCore.Mvc.Tests`: `InvokeAsync_WhenRequestAndStoreFail_ShouldKeepRequestException`、`InvokeAsync_WhenStoreFailsAndThrowEnabled_ShouldThrowStoreException` |
| `AddBingHttpAuditing` 配置与默认管理器注册 | `Bing.AspNetCore.Mvc.Tests`: `AddBingHttpAuditing_ShouldApplyOptions` |
| `BingTrafficGate` CAS 状态、原子在途计数、重复释放和显式就绪 | `Bing.AspNetCore.Mvc.Tests`: `TryEnter_WhenLeaseDisposedTwice_ShouldReleaseOnce`、`StartReadyDisabled_ShouldRequireExplicitReadyMark` |
| `BingTrafficGateMiddleware` 就绪探针、排空拒绝和 Retry-After | `Bing.AspNetCore.Mvc.Tests`: `Middleware_ShouldExposeReadinessAndRejectDrainingRequest` |
| `BingTrafficGateHostedService` 停止取消不提前释放活动请求 | `Bing.AspNetCore.Mvc.Tests`: `HostedService_WhenStopWaitIsCancelled_ShouldKeepActiveRequest` |
| `AddBingTrafficGate` 幂等注册和配置 | `Bing.AspNetCore.Mvc.Tests`: `AddBingTrafficGate_ShouldRegisterConfiguredSingleton` |

本批 HTTP 审计与流量闸门定向测试在 net6.0、net8.0 均通过；Admin 两宿主已接线并构建，但 CAP 消费角色切换和动态蓝绿演练仍未验证。


| `BingOrderedEventMetrics` 与 `AddBingOrderedConsumer` 指标绑定 | `Bing.EventBus.Tests`: `OrderedEventMetricsTest.Snapshot_ShouldExposeAggregatedCounters`、`Snapshot_WhenCompletionIsRepeated_ShouldKeepNonNegativeInFlight`、`Constructor_WhenConsumerIsMissing_ShouldThrow`；net6/net8 各 3/3 |
| `Bing.EntityFrameworkCore.UnitOfWorkBase` 实体创建/修改审计字段填充 | `Bing.EntityFrameworkCore.Tests.Integration.EfCoreAuditPropertySetterIntegrationTest.SaveChangesAsync_ShouldPersistCreationAndModificationAuditFields`；net6.0 1/1 |
| `Bing.FreeSQL.StoreBase` 可选 `IAuditPropertySetter` 接入 | `Bing.FreeSQL.Tests.Data.Stores.QueryStoreBaseTest.StoreBase_WhenAuditPropertySetterIsRegistered_ShouldSetCreateAndModifyProperties`；net6.0 1/1，测试类 net6/net8 各 22/22 |
| `IAuditPropertySetter` 契约及旧程序集类型转发 | `Bing.Auditing.Tests.Bing.Auditing.AuditingManagerTest.IAuditPropertySetter_LegacyAssemblyQualifiedName_ShouldResolve`；net6/net8 各 1/1，审计全量各 131/131 |

### HTTP 限流路由与可信身份补充映射

| 生产符号与关键行为 | 测试项目与直接方法 |
| --- | --- |
| `BingRateLimitMiddleware.InvokeAsync` 未匹配路径共享分桶 | `Bing.AspNetCore.Mvc.Tests.RateLimiting.BingRateLimitMiddlewareTest.InvokeAsync_UnmatchedPaths_ShouldShareBucket`；`BingRateLimitPipelineTest.Pipeline_UnmatchedPaths_ShouldShareBucket` |
| `BingRateLimitOptions.UserIdClaimType` 与认证声明选择 | `BingRateLimitMiddlewareTest.InvokeAsync_CustomUserIdClaim_ShouldUseAuthenticatedSubject`、`InvokeAsync_UnauthenticatedClaims_ShouldUseClientAddress` |
| 路由、认证、限流中间件全链路 | `BingRateLimitPipelineTest.Pipeline_AuthenticatedTenant_ShouldIsolateAndUseRouteTemplate` |
| 代理名单与匿名客户端配额 | `BingRateLimitPipelineTest.Pipeline_UntrustedForwardedHeaders_ShouldShareClientBucket`、`Pipeline_TrustedProxy_ShouldUseForwardedClientAddress` |

以上限流定向组 net6/net8 各 17/17；MVC 全量各 87 通过、1 项现有跳过，0 失败。测试使用真实 TestServer 管道和本地令牌桶，不替代 Redis 双实例、故障切换或 Admin 生产环境验收。

| 生产符号与关键行为 | 测试项目与直接方法 |
| --- | --- |
| Admin 两宿主 `AppModule.ConfigureRateLimiting` 显式启用、稳定策略及声明配置 | `Bing.Admin.EFCore.Tests` / `Bing.Admin.FreeSQL.Tests` 的 `AdminRateLimitingRegistrationTest.AddServices_WhenRateLimitingDisabled_ShouldNotRegisterLimiter`、`AddServices_WhenRateLimitingEnabled_ShouldRegisterSharedPolicy` |
| Admin 限流参数注册阶段预检 | 上述两类的 `AddServices_WhenRateLimitingEnabledWithoutCapacity_ShouldRejectConfiguration`、`AddServices_WhenRefillIsTooSmall_ShouldRejectAtRegistration` |
| Admin `ConfigureTrustedProxies` 明确信任列表 | 上述两类的 `AddServices_WithKnownProxy_ShouldTrustOnlyConfiguredAddress`、`AddServices_WithInvalidProxy_ShouldRejectConfiguration` |
| Redis 跨独立 DI 容器并发配额 | `Bing.Caching.CSRedis.Tests.RedisTokenBucketRateLimiterTest.TryAcquireAsync_IndependentProviders_ShouldShareConcurrentQuota` |

Admin 注册组各 6/6；EFCore 本地回归 14 通过/16 外部门控跳过，FreeSQL 34 通过/15 外部门控跳过。未以此宣称实际 Admin HTTP、CAP 或 MySQL 外部链路在本轮通过。

真实 Redis 令牌桶定向 net6/net8 最终各 9/9、0 跳过，包含独立 DI 容器的 64 次并发竞争及租户配额隔离；只清理自身策略。两实例属于同进程独立容器，不能替代跨进程或故障切换验收。

### 数据库审计持久化与事务适配

| 最终生产符号与关键行为 | 测试项目与直接方法 |
| --- | --- |
| `DbAuditingStore.SaveAsync(AuditLogInfo)` 自有连接、异常与释放 | `Bing.Auditing.Tests.DbAuditingStoreTest`: `SaveAsync_WhenUsingOwnConnection_ShouldPersistAndDisposeConnection`、`SaveAsync_WhenTableIsMissing_ShouldPropagateOriginalErrorAndDisposeConnection`、`SaveAsync_WhenConnectionFactoryThrows_ShouldPreserveOriginalException`、`SaveAsync_WhenFactoryReturnsOpenConnection_ShouldNotDisposeBorrowedConnection` |
| `ITransactionalAuditingStore` / `DbAuditingStore.SaveAsync(DbTransaction,...)` 事务借用、提交/回滚与取消 | 同类：`TransactionalSave_WhenCommitted_ShouldPersistBusinessAndAudit`、`TransactionalSave_WhenRolledBack_ShouldLeaveNoRecords`、`TransactionalSave_WhenAuditInsertFails_ShouldPreserveErrorAndAllowBusinessRollback`、`TransactionalSave_WhenCancelledBeforeInsert_ShouldNotWrite` |
| `AuditLogPersistenceOptions` 白名单、构造快照、字段/字节/明细上限 | 同类：`SaveAsync_ShouldApplyAllowlistAndExcludeSensitiveDetails`、`SaveAsync_WhenOptionsMutate_ShouldUseConstructionSnapshot`、`SaveAsync_WhenPayloadExceedsLimit_ShouldNotPersistOversizedDetails`、`SaveAsync_WhenDetailCountExceedsLimit_ShouldNotPersist` |
| `AuditLogDatabase` / `AuditLogSchema.GetCreateStatements` Provider 完整 SQL 与拒绝 | `Bing.Auditing.Tests.AuditLogSchemaTest`: `GetCreateStatements_WhenSqlite_ShouldMatchCompleteStatement`、`GetCreateStatements_WhenMySql_ShouldMatchCompleteStatement`、`GetCreateStatements_WhenDatabaseIsUnknown_ShouldThrow` |
| `AuditingPersistenceExtensions.AddBingDatabaseAuditing` 实例共享、替换、配置快照及失败不改集合 | `Bing.Auditing.Tests.AuditingPersistenceExtensionsTest`: `AddBingDatabaseAuditing_ShouldReplaceDefaultStoreAndShareInstance`、`AddBingDatabaseAuditing_WhenRepeated_ShouldKeepSingleLatestStore`、`AddBingDatabaseAuditing_WhenConfiguredOptionsMutate_ShouldKeepSnapshot`、`AddBingDatabaseAuditing_WhenInvalid_ShouldNotMutateServices` |
| `EfCoreAuditingExtensions.SaveTransactionalAuditAsync` 活动事务和调用方所有权 | `Bing.EntityFrameworkCore.Tests.Integration.EfCoreTransactionalAuditingTest`: `SaveTransactionalAuditAsync_ShouldShareBusinessTransaction`（提交/回滚）、`SaveTransactionalAuditAsync_WhenNoTransaction_ShouldReject`、`SaveTransactionalAuditAsync_WhenAuditWriteFails_ShouldAllowBusinessRollback` |
| `FreeSqlAuditingExtensions.SaveTransactionalAuditAsync` 活动事务和调用方所有权 | `Bing.FreeSQL.Tests.Events.FreeSqlTransactionalAuditingTest`: 同名三个方法，提交/回滚两条数据 |

审计存储/Schema/注册定向 net6/net8 各 19/19、审计全量各 150/150；EF 事务 4/4、EF 全量 87/87；FreeSQL 事务各 4/4 的历史 TRX 位于 `artifacts/audit-persistence/`。这些 SQL/事务证据基于 SQLite，不证明完整 MySQL 库存/审计/CAP 业务。最新 FreeSQL 全量另记执行报告。

### FIX-014～019 请求作用域、停止诊断、指标和库存恢复

| 最终生产符号与关键行为 | 测试项目与直接方法 |
| --- | --- |
| `BingHttpAuditingMiddleware.InvokeAsync` 请求管理器共享及并发隔离 | `Bing.AspNetCore.Mvc.Tests.BingHttpAuditingPipelineTest`: `Pipeline_WhenScopesAreValidated_ShouldShareRequestAudit`、`Pipeline_ConcurrentRequests_ShouldKeepAuditsIsolated`；各覆盖 Microsoft DI/AspectCore |
| `BingHttpAuditingOptions.AllowedQueryParameters`、Claim 配置及中间件安全错误日志 | `BingHttpAuditingMiddlewareTest`: `InvokeAsync_ShouldRecordOnlyAllowedQueryParameters`、`InvokeAsync_ShouldIgnoreUnauthenticatedIdentityClaims`、`InvokeAsync_ShouldReadConfiguredClaimsFromAuthenticatedIdentity`；`BingHttpAuditingPipelineTest.Pipeline_WhenStoreFails_ShouldLogOnlySafeMetadata` |
| `BingTrafficGateHostedService.StopAsync` 超时/取消及活动资源不提前释放 | `BingTrafficGateTest`: `HostedService_WhenStopWaitIsCancelled_ShouldKeepActiveRequest`、`HostedService_WhenDrainTimesOut_ShouldReportIncompleteDrain`；`BingTrafficGateHostTest`: `HostStop_WhenRequestIsActive_ShouldReportTimeoutAndKeepRequestResource`、`HostStop_ShouldWaitForRequestCompletion` |
| `BingTrafficGate.TryEnter/BeginDrain/WaitForDrainAsync` 竞争、终态和句柄释放 | `BingTrafficGateTest.BeginDrain_WhenEntriesRace_ShouldRemainTerminalAndReleaseEveryLease`、`BeginDrain_ShouldRejectNewRequestAndWaitForExisting`、`WaitForDrainAsync_WhenCancelled_ShouldReturnFalse`；剩余 Delay 取消已接入，长期 GC 探针仍待补 |
| `OrderedEventConsumerExtensions.ConsumerWorker` 解析与释放故障的计数退出及继续处理 | `Bing.EventBus.Tests.OrderedEventConsumerTest`: `HostedWorker_WhenHandlerConstructionFails_ShouldClearInFlightAndProcessNextEvent`、`HostedWorker_WhenScopeDisposalFails_ShouldClearInFlightAndProcessNextStream` |
| `OrderedEventCoordinator.RetryAsync` / `BingOrderedEventMetrics.RetryCount` 已确认提交口径 | `OrderedEventCoordinatorTest`: `RetryAsync_WhenCommitted_ShouldRecordRetryOnce`、`RetryAsync_WhenRecoveryIsDenied_ShouldRejectBeforeOpeningTransaction`、`RetryAsync_WhenCommitOutcomeIsUnknown_ShouldNotRecordRetry`、`RetryAsync_WhenDisposeFailsAfterCommit_ShouldRecordRetry` |
| `DbInventoryStore.TryDeductAsync` 唯一冲突后的 MySQL 当前读 | `Bing.FreeSQL.Tests.Events.InventoryRepeatableReadMySqlTest`: `TryDeductAsync_WhenRepeatableReadSnapshotPredatesCommit_ShouldReturnCommittedResult`、`TryDeductAsync_WhenReadCommittedSnapshotPredatesCommit_ShouldReturnCommittedResult`、`TryDeductAsync_WhenStaleSnapshotRequestHasDifferentQuantity_ShouldRejectIdentityConflict`；两个真实交错事务 |
| 两 Admin `AppModule` 的审计声明配置 | 两测试项目的 `AdminRateLimitingRegistrationTest.AddServices_ShouldUseHostClaimTypesForHttpAuditing` |
| EFCore Admin 默认日志采集范围 | `Bing.Admin.EFCore.Tests.AdminRateLimitingRegistrationTest.AddServices_ShouldUseSafeHttpLoggingAndDisableLegacyRequestResponseLog` |
| 两 Admin `CapModule` RabbitMQ 受控配置与 Dashboard 默认关闭 | 两项目 `AdminCapRegistrationTest`: `AddServices_WhenRabbitFieldIsMissing_ShouldRejectWithoutExposingValues`、`AddServices_WhenRabbitPortIsInvalid_ShouldReject`、`AddServices_ShouldUseConfiguredRabbitAndDisableDashboardByDefault`、`AddServices_WhenDashboardIsExplicitlyEnabled_ShouldRegisterConfiguredTransport` |

最新 TRX 位于 `artifacts/cluster-latest-review-fix/`；旧快照失败与修复证据位于 `artifacts/inventory-snapshot-review-fix/`；HTTP 定向位于 `artifacts/http-audit-review-fix/`。注册测试不建立 RabbitMQ 连接，Dashboard 注册不等于访问授权通过。停止错误在 net6 的 Generic Host 聚合、net8 的直接传播均保留超时与资源有效断言。

### 整单库存、作用域与限流身份修复

| 最终生产符号与关键行为 | 测试项目与直接方法 |
| --- | --- |
| `InventoryBatchOperations.DeductAsync` 结构化租户/请求键、真正重复与失败整批回滚 | `Bing.EventBus.Tests.Ordering.InventoryStoreTest`: `InventoryBatch_WhenTenantAndRequestKeysContainDelimiter_ShouldProcessBoth`、`InventoryBatch_WhenSameTenantRequestIdsRepeat_ShouldRejectAndRollback`、`InventoryBatch_WhenOneRequestIsInsufficient_ShouldRollbackPreviousDeductions` |
| `InventoryOrderRequest` / `InventoryOrderLine` 正数校验、不可变快照、排序、身份摘要、拒绝无效代理字符 | `InventoryOrderStoreTest.Request_ShouldValidateLinesAndKeepImmutableSnapshot`、`ExecuteAsync_WhenLinesAreReordered_ShouldRecognizeSameRequest`、`ExecuteAsync_WhenOrderContentConflicts_ShouldRejectWithoutNewWrites` |
| `InventoryOrderSchema.GetCreateStatements` 两 Provider 完整 SQL 与非法枚举 | `InventoryOrderSchemaTest`: `GetCreateStatements_WhenMySql_ShouldMatchCompleteSql`、`GetCreateStatements_WhenSqlite_ShouldMatchCompleteSql`、`GetCreateStatements_WhenUnknown_ShouldReject` |
| `InventoryOrderResult` / `IInventoryOrderStore.ExecuteAsync` / `DbInventoryOrderStore` 首次结果、重复、不足、取消和回调失败原子性 | `InventoryOrderStoreTest`: `ExecuteAsync_WhenRepeated_ShouldReturnFirstResultAndRunCallbackOnce`、`ExecuteAsync_WhenLaterSkuIsInsufficient_ShouldRollbackAllItemsAndRememberResult`、`ExecuteAsync_WhenCallbackFails_ShouldRollbackInventoryOrderAuditAndOutbox`、`ExecuteAsync_WhenCancelledBeforeStart_ShouldNotWrite`、`ExecuteAsync_WhenOrderIdsDifferByCaseOrTrailingSpace_ShouldKeepSeparateResults` |
| `IInventoryOrderStore.FindAsync` / `DbInventoryOrderStore.FindAsync` 独立只读查询及内容冲突 | `InventoryOrderStoreTest.FindAsync_ShouldReadCommittedFirstResultWithoutNewWrites`；两 Admin 的 `InventoryOrder_WhenRepeated_ShouldCommitInventoryAuditAndOutboxOnce` 也使用新的独立连接和查询作用域 |
| `AddBingInventoryOrders` 默认 Scoped、调用方 Scoped 依赖、作用域隔离与释放一次（FIX-021） | `InventoryOrderStoreTest.AddBingInventoryOrders_WithScopedInventoryStore_ShouldIsolateScopesAndDisposeOnce`、`AddBingInventoryOrders_ShouldKeepCustomStoreAndSingleRegistration`；真实 SQLite 扣减与 Microsoft DI ValidateScopes/ValidateOnBuild |
| 两 ORM `AddAdminInventory` / `CapModule` 可选启用、自定义关键审计与幂等注册 | 两项目 `AdminCapRegistrationTest`: `AddServices_ShouldKeepInventoryDisabledByDefault`、`AddServices_WhenInventoryEnabledSettingIsApplied_ShouldRegisterInventoryServicesAccordingly`、`AddAdminInventory_WhenCalledTwice_ShouldKeepOneBusinessServiceAndCustomAuditingStore` |
| `AdminInventoryService.ExecuteAsync` / `CompletedTopic` / 两 ORM 事务访问器：库存、整单结果、关键审计、真实 CAP 出箱同事务 | 两项目的 `AdminMySqlCapIntegrationTest` 共享 `AdminInventoryIntegrationCases.cs`：`InventoryOrder_WhenRepeated_ShouldCommitInventoryAuditAndOutboxOnce`、`InventoryOrder_WhenLaterSkuIsInsufficient_ShouldRememberResultWithoutPartialDeduction`、`InventoryOrder_WhenCriticalAuditFails_ShouldRollbackAllBusinessWrites`、`InventoryOrder_WhenCancelledAfterOutbox_ShouldRollbackAllWrites` |
| `AdminInventoryService` 独立真实工作单元并发及整单不超卖 | 两项目同类：`InventoryOrder_WhenScopesCompeteForSameOrder_ShouldCommitExactlyOnce`、`InventoryOrder_WhenDifferentOrdersCompete_ShouldKeepNonNegativeAtomicStock`；同进程独立作用域，不能代替双进程 |
| `BingRateLimitMiddleware.InvokeAsync` / `EncodeIdentityPart` 默认租户/用户身份无歧义（FIX-022） | `BingRateLimitMiddlewareTest`: `InvokeAsync_TenantAndUserSeparators_ShouldKeepBucketsSeparate`、`InvokeAsync_SameTenantAndUser_ShouldReuseBucket`、`InvokeAsync_LongTenantAndUserClaims_ShouldKeepDigestBucketsSeparate`；无租户、自定义主体与路由既有回归保留 |

修复前后的 TRX 和日志在 `artifacts/cluster-scope-identity-fix/`；实际通过数量及回归范围在 execution.md 追加记录。SQLite 的 audit_test/outbox_test 是事务可观察记录，不能当作 CAP 集成；真实 Admin 用例使用专用 MySQL/RabbitMQ 和实际工作单元，不使用生产数据库。Unknown 正常查询覆盖不等于提交确认丢失故障验收。

### 库存精确身份、限流长度与审计异常完整性

| 最终生产符号与关键行为 | 测试项目与直接方法 |
| --- | --- |
| `InventorySchema.GetCreateStatements` MySQL 二进制身份/InnoDB、SQLite BINARY 文本及非法语法 | `Bing.EventBus.Tests.Ordering.InventorySchemaTest`: `GetCreateStatements_WhenMySql_ShouldMatchCompleteSql`、`GetCreateStatements_WhenSqlite_ShouldMatchCompleteSql`、`GetCreateStatements_WhenUnknown_ShouldReject`；完整 SQL 字符串断言 |
| `InventoryDeductionRequest` / `ValidateKey` 无损 UTF-8、UTF-16 长度及原值保留 | `InventoryDeductionRequestTest`: `Constructor_WhenIdentityContainsUnpairedSurrogate_ShouldReject`、`Constructor_ShouldKeepExactIdentifiersAndAcceptCharacterLimits`、`Constructor_WhenIdentityExceedsCharacterLimit_ShouldReject`；四种身份分别验证 |
| `DbInventoryStore.AddIdentityParameter` / `ReadIdentity` / `TryDeductAsync` UTF-8 字节绑定、精确身份及首次结果 | `Bing.FreeSQL.Tests.Events.InventoryIdentityMySqlTest.TryDeductAsync_WhenIdentifiersDifferInCaseWhitespaceOrUnicode_ShouldRemainIsolated`；真实 MySQL 四类身份、20 个请求与重复结果 |
| `InventorySchema` 显式事务引擎不受会话 MyISAM 默认值影响 | 同类 `Schema_WhenServerDefaultsAreNonTransactional_ShouldUseExactInnoDbTables`；逐表/逐列核对 information_schema |
| `deploy/migrations/inventory/002-exact-identities.mysql.sql` 旧字符集先转 UTF-8，再二进制，显式迁移恢复回滚 | 同类 `Migration_WhenLegacyLatin1TablesExist_ShouldPreserveReceiptsAndRollbackNewWrites`；直接执行部署脚本，核对旧收据、不同大小写请求、真实回滚。IF NOT EXISTS 不迁移也有断言 |
| `BingRateLimitMiddleware` 构造预检、`MinimumMaxKeyLength` / `HashKeyPrefix`（FIX-023） | `BingRateLimitMiddlewareTest.Constructor_WhenMaxKeyLengthIsBelowMinimum_ShouldThrowArgumentOutOfRangeException`、`Constructor_WhenMaxKeyLengthMeetsMinimum_ShouldSucceed`，及 `InvokeAsync_LongTenantAndUserClaims_ShouldKeepDigestBucketsSeparate`；保留合理长度下旧键协议 |
| `BingHttpAuditingMiddleware.InvokeAsync` / `AddCleanupError` / `AttachCleanupErrors` 保存/释放失败的原异常与完整诊断（FIX-009） | `BingHttpAuditingMiddlewareTest`: `InvokeAsync_WhenStoreAndDisposeFail_ShouldThrowFirstFailureAndPreserveCleanupErrors`、`InvokeAsync_WhenRequestStoreAndDisposeFail_ShouldKeepRequestExceptionAndCleanupErrors`、`InvokeAsync_WhenDisposeFailsWithThrowEnabled_ShouldThrowDisposeFailure`、`InvokeAsync_WhenStoreAndDisposeFailWithDefaultPolicy_ShouldForwardRequest`；真实 `AuditingManager` 的 `InvokeAsync_WhenStoreFailsAndThrowEnabled_ShouldThrowStoreException` 验证同一异常引用去重 |

本批失败基线、定向及全量结果见 `artifacts/inventory-identity-length-fix/`。随机 MySQL 数据库测试与两套 Admin 真实 CAP 业务分别验收；它们不能替代跨进程库存、提交确认丢失和长时间容量证据。



### 本轮身份契约修复映射

| 最终生产符号与关键行为 | 测试项目与直接方法 |
| --- | --- |
| `DbAuditingStore.TenantIdentity` 严格 UTF-8、保留原始空格和 Unicode | `Bing.Auditing.Tests.DbAuditingStoreTest`: `TransactionalSave_WhenTenantIdentitiesDiffer_ShouldPreserveExactValues`、`TransactionalSave_WhenTenantHasUnpairedSurrogate_ShouldReject` |
| `AuditLogSchema` MySQL 租户二进制列 | `Bing.Auditing.Tests.AuditLogSchemaTest.GetCreateStatements_WhenMySql_ShouldMatchCompleteStatement` |
| MySQL 审计租户迁移脚本 | `Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest.InventoryOrder_WhenTenantIdentityContainsWhitespace_ShouldKeepAuditAndOutboxIsolated`、`Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest.InventoryOrder_WhenTenantIdentityContainsWhitespace_ShouldKeepAuditAndOutboxIsolated`；迁移后真实专用 MySQL/RabbitMQ，各1/1 |
| `BingRateLimitMiddleware` 多身份可信声明选择 | `Bing.AspNetCore.Mvc.Tests.RateLimiting.BingRateLimitMiddlewareTest`: `InvokeAsync_AuthenticatedIdentityAfterUnauthenticated_ShouldUseFirstAuthenticatedIdentity`、`InvokeAsync_UnauthenticatedClaimsOnAnotherIdentity_ShouldNotBeUsed`、`InvokeAsync_MultipleAuthenticatedIdentities_ShouldKeepClaimsInFirstIdentity`；`BingRateLimitPipelineTest` 三个多 scheme 方法，net6/net8各35/35 |


### 库存执行器、真实库存进程与统一审计身份

| 最终生产符号与关键行为 | 测试项目与直接方法 |
| --- | --- |
| `AdminInventoryOrderExecutor.ExecuteAsync` / `ExecuteCoreAsync` / `IsRetryableDeadlock` / `HasCleanupFailure`：统一清理判断，有界新作用域重试与异步清理 | 两Admin测试项目共享`AdminInventoryOrderExecutorTest`：`ExecuteAsync_WhenDeadlockHasCleanupFailure_ShouldNotRetry`（三诊断键）、`ExecuteAsync_WhenDeadlockPersists_ShouldBoundAttemptsAndAwaitEachCleanup`、`ExecuteAsync_WhenDeadlockThenSuccess_ShouldReturnNewAttemptResult`、`ExecuteAsync_WhenRetryIsUnsafe_ShouldKeepOriginalFailure`；原生驱动异常形状的分派测试，不替代真实数据库死锁 |
| `AdminInventoryOrderExecutor.ExecuteWithRecoveryAsync`：原请求查询、清理失败不吞、原异常保留 | 同类`ExecuteWithRecoveryAsync_WhenBusinessAndCleanupFail_ShouldPreserveBothFailures`、`ExecuteWithRecoveryAsync_WhenOnlyCleanupFails_ShouldPreserveCommittedResultWithoutRecovery`、`ExecuteWithRecoveryAsync_WhenRecoveryReturnsNull_ShouldPreserveOriginalFailure`、`ExecuteWithRecoveryAsync_WhenRecoveryThrows_ShouldPreserveOriginalAndRecoveryFailures`、`ExecuteWithRecoveryAsync_WhenOutcomeIsUnknown_ShouldRecoverWithoutRetry` |
| 执行器取消/异步作用域释放等待 | 同类`ExecuteWithRecoveryAsync_WhenCancelled_ShouldAwaitCleanupAndSkipRecovery`、`ExecuteWithRecoveryAsync_WhenCleanupIsPending_ShouldWaitBeforeRecovery`、`ExecuteAsync_WhenCleanupIsPending_ShouldWaitBeforeReturningCommittedResult`、`ExecuteWithRecoveryAsync_WhenOutcomeIsRolledBack_ShouldNotRecoverOrRetry` |
| 两ORM`AddAdminInventory`注册的执行器及真实`AdminInventoryService`库存/关键审计/CAP事务闭环 | 两Admin的`AdminMySqlCapIntegrationTest.InventoryOrder_WhenProcessesCompeteForSameOrder_ShouldCommitExactlyOnce`、`InventoryOrder_WhenProcessesCompeteForDifferentOrders_ShouldKeepStockAtomic`；独立进程及两PID，不代替确定性事务重叠/真实死锁验证 |
| 两ORM库存提交前后进程中断后的原请求恢复 | 同类`InventoryOrder_WhenProcessDiesBeforeCommit_ShouldRollbackAndRetryOriginalOrder`、`InventoryOrder_WhenProcessDiesAfterCommit_ShouldRecoverOriginalResultWithoutDuplicateWrites`；提交前暂停在审计之后、出箱之前，提交后暂停在业务返回之后，不证明COMMIT确认丢失 |
| `BingHttpAuditingMiddleware.PopulateRequest` / `GetFirstAuthenticatedIdentity` / `FindIdentityClaim`：全部身份字段来自首个已认证身份 | `BingHttpAuditingMiddlewareTest.InvokeAsync_WhenFirstAuthenticatedIdentityHasNoUser_ShouldNotBorrowLaterUser`、`InvokeAsync_WhenFirstAuthenticatedIdentityHasNoTenant_ShouldNotBorrowLaterTenant`、`InvokeAsync_WhenSelectedIdentityHasNoNameOrClient_ShouldNotBorrowLaterClaims`、`InvokeAsync_WhenAuthenticatedIdentitiesAreReordered_ShouldUseFirstAuthenticatedIdentity` |
| HTTP审计可信身份实际容器/请求链路 | `BingHttpAuditingPipelineTest.Pipeline_WhenSelectedIdentityLacksClaims_ShouldNotMixAuthenticatedIdentities`：Microsoft DI与AspectCore各一路 |

结果和本轮失败基线见execution.md。每组定向和完整回归会重叠，不累计为独立覆盖。锁定SDK、真实提交确认丢失、真实1213及七组完整交付仍分别验收。


测试探针资源证据：`AdminInventoryProcessProbe.Program.CleanupHostAsync`由两套`AdminInventoryProbeCleanupTest`直接调用编译后的实际探针，覆盖`CleanupHostAsync_WhenExecutionAndCleanupFail_ShouldPreserveOriginalAndBothCleanupFailures`、`CleanupHostAsync_WhenOnlyCleanupFails_ShouldThrowStopFailureAndPreserveDisposeFailure`、`CleanupHostAsync_WhenAsyncDisposeIsPending_ShouldAwaitItWithoutSyncDispose`、`CleanupHostAsync_WhenHostSupportsOnlySyncDispose_ShouldUseSyncPathOnce`。此处只验证测试宿主清理算法，不替代真实CAP取消/提交确认丢失验收。

CI交付映射：`Test-TrxRequiredMethods`及两ORM14个`RequiredTestMethods` → `eng/ci/tests/Invoke-AdminMySqlCapIntegrationTests.Tests.ps1`，真实声明AST、完整方法集合、逐方法缺失/非Passed拒绝；最低门槛27/26依据本轮完整TRX。


### 真实库存死锁映射

| 最终生产符号及关键行为 | 直接测试方法与证据 |
| --- | --- |
| AdminInventoryOrderExecutor.ExecuteAsync/ExecuteCoreAsync：真实1213回滚、作用域丢弃后整单重试 | 两ORM AdminMySqlCapIntegrationTest.InventoryOrder_WhenInnoDbDeadlocks_ShouldAwaitCleanupAndRetryFreshScope；真实相反行锁顺序，2个独立工作单元/事务、上一轮作用域释放、整单/审计/出箱一次 |
| 同执行器：持续真实1213仅四次、原始异常及无部分提交 | InventoryOrder_WhenInnoDbDeadlocksPersist_ShouldStopAfterFourCleanAttempts；实际InnoDB牺牲者，不是SIGNAL；4次/4释放、全RolledBack、库存恢复20、0整单/审计/出箱 |
| 两测试CreateHost和共享WithInventoryHostAsync：默认库存注册后独立配置回调 | 上述两方法，两套各2/2定向；完整29/28门控验证默认调用兼容 |
| CI必需方法集合含真实死锁测试 | eng/ci/tests/Invoke-AdminMySqlCapIntegrationTests.Tests.ps1；当前16个必需方法/套，最低29/28，84拒绝断言；真实完整TRX全部通过 |

前文14项/27与26是上一实施批次历史数据，本节为本批当前数量。FIX-007/013的偶发根因、真实提交故障及其余工作包仍独立验收。

### 真实提交确认丢失与 CAP 后台恢复映射

| 最终生产符号及关键行为 | 直接测试方法与证据 |
| --- | --- |
| 两套 Admin 工作单元事务提交、回滚/释放诊断与 AdminInventoryOrderExecutor.ExecuteAsync | 两ORM AdminMySqlCapIntegrationTest.InventoryOrder_WhenCommitAcknowledgementIsLost_ShouldRecoverOriginalCommittedResult；服务器真实COMMIT OK丢弃后Unknown、单次尝试、权威写库原请求查询、Duplicate且库存/订单/关键审计/出箱不重复 |
| AdminInventoryOrderExecutor.ExecuteWithRecoveryAsync、HasCleanupFailure 与真实断连清理 | InventoryOrder_WhenCommitAcknowledgementIsLost_ShouldPreserveRecoveryAndCleanupFailures；保留实际清理错误与原异常，恢复结果附于Bing.InventoryRecoveredResult并标注Committed；两套定向各2/2 |
| AddCapEventBus、真实CAP8.0.1后台扫描、AdminInventoryService.CompletedTopic | InventoryOrder_WhenCommitAcknowledgementIsLost_ShouldRecoverOutboxThroughBackgroundScan；故障宿主停止后读取唯一Scheduled原Id，随后独立直连恢复宿主原生扫描至同Id Succeeded，正常宿主RabbitMQ真实订阅回调；两套各1/1，不手动发布/Flush/修改出箱行 |
| IInventoryOrderStore.FindAsync 与原请求幂等 | 上述三个方法；独立权威写库核对同一请求身份/内容，重复执行返回首次余额，库存14、订单/关键审计/出箱各1 |
| CI Test-TrxRequiredMethods 和最新完整必需集合 | eng/ci/tests/Invoke-AdminMySqlCapIntegrationTests.Tests.ps1；19方法/套，最低32/31，126个缺失或非Passed拒绝断言；真实完整门控最终结果见execution.md |

测试夹具符号单独列出：InventoryCommitAckLossProxy 控制loopback连接的成功COMMIT OK转发，不改变服务器结果；CommitLossAudits 使用用户变量只标记目标事务连接。测试连接显式AllowUserVariables，避免未绑定参数错误在提交前终止，失败诊断仅输出类型/Number/结果/调用栈。此处不把测试代理列为生产插件或网络能力。

后台恢复验证证明最终投递和库存单次写入，不将简单订阅回调计数解释为CAP恰好一次保证；业务消费收据、CAP取消重投及其他组合失败继续独立验收。前文14/16/18方法和27/26、29/28、31/30门槛分别是历史批次，本节19/32/31为最新集合。

### 真实 CAP 消费取消与夹具安全清理映射（FIX-030）

本节只追加本批覆盖，不改写前文历史验证记录。两个真实取消方法来自共享 `modules/admin/tests/Shared/AdminCapCancellationCases.cs`，链接到 `Bing.Admin.EFCore.Tests` 与 `Bing.Admin.FreeSQL.Tests`。结果以 execution.md 实际记录为准；本节不预先声明真实消费专项或完整门控通过。

| 最终生产符号及关键行为 | 测试项目与方法、覆盖边界 |
| --- | --- |
| `EventConsumptionStore.TryRecordAsync`：消费收据与业务 SQL 同事务；提交前取消回滚后允许原事件重试，提交后取消由原消费者/事件收据去重 | 两项目 `AdminMySqlCapIntegrationTest.Consumption_WhenCancelledBeforeBusinessCommit_ShouldRetryAndCommitOnce`、`Consumption_WhenCancelledAfterBusinessCommit_ShouldRetryWithoutDuplicateWrites`；断言目标事件仅发布一次、实际取消异常、至少两次回调、原消息头标识一致、同一唯一 `cap.received` 记录最终 Succeeded 且 Retries >= 1、收据与业务行各一条 |
| `AddCapEventBus` / `CapHostedLifecycleService`：真实 AspectCore Admin 宿主的 CAP 注册、启动及停止链路 | 上述两方法自行构造既有 `CreateHost`，保留两 ORM 的真实 Admin 宿主配置，并使用真实 MySQL/RabbitMQ；仅测试配置 `FailedRetryInterval=1`、`FallbackWindowLookbackSeconds=5`，等待原生 CAP 持久化消费重试，不手工重发目标事件或调用 Dispatcher |

业务边界是直接 MySQL 事务中的收据与测试 SQL 写入，不能代表两 ORM 全部工作单元取消行为已经验收。取消发生在业务事务提交前或提交后，不等于宿主停止。CAP 持久化重试及原记录状态变化，不等于 RabbitMQ 重入队、basic.nack 或 broker 重投验收；此处也不声明 CAP 恰好一次投递保证。

以下仅为测试专属符号，不列为生产能力：

| 测试专属符号与职责 | 两测试项目的直接职责测试方法 |
| --- | --- |
| `CapCancellationCleanup.RunAsync` / `RunCoreAsync`：实际 Stop 使用 CancellationToken.None，等待超时或 Stop 抛错后保留数据与资源，保留原始异常；重复调用共享同一任务 | `AdminCapCancellationCleanupTest.Cleanup_WhenStopWaitTimesOut_ShouldRetainDataAndResources`、`Cleanup_WhenStopThrows_ShouldPreserveOriginalAndRetainResources` |
| `CapCancellationObservation.TryEnterCallback` / `ExitCallback` / `CloseCallbackAdmission`，及 `CapCancellationSubscriber.ConsumeAsync` 的关闭后拒绝路径：原子关闭新业务回调入场并等待已入场者实际退出 | `Cleanup_WhenCallbackIsActive_ShouldWaitForExitAndCloseAdmission`、`Cleanup_WhenCallbackDrainTimesOut_ShouldRetainDataAndResources`；关闭后目标回调明确抛取消，不静默成功；排空未确认时不删除独占表/收据、不释放根容器/ORM |
| `CapCancellationCleanup.DisposeResourceAsync`：单次选择 async 或 sync 释放路径，区分已失败完成与实际仍在执行 | `Cleanup_WhenResourcesSupportAsyncDispose_ShouldShareTaskAndDisposeOnce`、`Cleanup_WhenResourcesSupportOnlySyncDispose_ShouldDisposeOnce`、`Cleanup_WhenHostDisposeCompletesWithFailure_ShouldStillDisposeOrm`、`Cleanup_WhenHostDisposeIsStillPending_ShouldRetainOrmAndOwner` |
| `CapCancellationCleanup.RecordFailure` / `RetainResources`：保留原始验证异常、附加清理异常，未安全完成时由异常 Data 的 pending-cleanup owner 持有资源并提供安全 GUID 诊断 | `Cleanup_WhenDataCleanupFails_ShouldPreserveOriginalAndDisposeSafeResources`；停止失败及释放 pending 的原异常/owner 断言由上述相应方法直接覆盖 |

九项直接职责测试使用 fake host，不连接外部数据库。当前已知实际结果仅为 EFCore cleanup 9/9 通过；其他套件和真实消费结果以 execution.md 实际记录为准。宿主停止完成与业务回调排空是独立确认条件，不以 Started == Ended 的瞬时快照替代入场关闭协议；未确认安全的失败路径不会自动重试删除或释放。

CI 当前声明门槛为两 ORM 每套 21 个必需方法、最低 EFCore 34 / FreeSQL 33，校验器自测包含 154 个缺失或非 Passed 拒绝断言。这些是当前门槛和校验器覆盖声明，不代表本批完整真实门控或全部校验器自测已通过；实际执行结果仍以 execution.md 为准。

### CAP 8.0.1 消费取消兼容适配及直接覆盖映射

本节追加生产适配完成后的映射，前文 FIX-030 初次结果保留为历史记录。原生 `SubscribeInvoker.InvokeAsync` 将未处理的 OCE 传播出去；吞 OCE 的位置在上层 `SubscribeExecutor.InvokeConsumerMethodAsync`，其后外层写 Succeeded。原生调用器的 `InvokeAsync` 是接口的 final virtual 实现，不能直接 override；`BingSubscribeInvoker` 显式重新实现 `ISubscribeInvoker.InvokeAsync`，先 await 原生调用链，再将逃出的真实 OCE 包装成普通 InvalidOperationException，保留同一 InnerException 和 `Data["Bing.CapConsumerCancellation"]` 标记，使上层进入原生失败路径。适配不手工重发或调度。

以下均属于 `Bing.EventBus.Tests.Cap` 的职责直接测试，共 17 个方法、18 个测试数据项；同步/异步取消理论方法包含两行数据。

| 最终生产符号及关键行为 | 直接测试类与方法 |
| --- | --- |
| `BingSubscribeInvoker.ISubscribeInvoker.InvokeAsync`：接口派发实际进入适配，保留原 OCE 对象、业务令牌与调用令牌；原生调用器默认行为不变 | `BingSubscribeInvokerTest.InvokeAsync_WhenConsumerCancels_ShouldPreserveOriginalAndUseDerivedInterface`（同步/异步两行）、`InvokeAsync_WhenNativeInvokerIsUsed_ShouldKeepNativeCancellation` |
| 同接口及继承的 `GetInstance`：原生成功结果、消息标识、回调名称、同一上下文与一次实例选择扩展点；普通异常不转换 | `InvokeAsync_WhenConsumerSucceeds_ShouldKeepNativeResultAndInstanceOverride`、`InvokeAsync_WhenConsumerFailsWithoutCancellation_ShouldKeepOriginalException` |
| 同接口与原生 `ISubscribeFilter`：未处理 OCE 才包装；已处理 OCE 保留过滤器恢复结果；改写普通异常保留其原对象 | `InvokeAsync_WhenFilterLeavesCancellationUnhandled_ShouldWrapOriginal`、`InvokeAsync_WhenFilterHandlesCancellation_ShouldKeepFilterResult`、`InvokeAsync_WhenFilterRewritesCancellation_ShouldKeepReplacementException` |
| 同接口的调用令牌语义：开始时已取消保留原生提前检查、不执行业务；业务入场后取消保留传入令牌与实际 OCE | `InvokeAsync_WhenInvocationTokenIsAlreadyCancelled_ShouldKeepNativePrecheck`、`InvokeAsync_WhenInvocationTokenCancelsDuringConsumer_ShouldKeepTokenAndOriginal` |
| `Extensions.EnableBingCapConsumerCancellationRetry`：显式添加、重复启用幂等、替换原生注册一次、只处理实际生效的最后注册 | `BingCapConsumerCancellationRegistrationTest.Enable_WhenCalledRepeatedly_ShouldKeepSingleDescriptor`、`Enable_WhenNativeInvokerIsRegistered_ShouldReplaceItOnce`、`Enable_WhenMultipleInvokersExist_ShouldReplaceEffectiveLastOnly` |
| 同扩展：未知自定义类型/工厂拒绝且不覆盖或探测执行，空参数拒绝；已适配派生类型/实例保持原描述符与生命周期 | `Enable_WhenCustomInvokerExists_ShouldRejectWithoutChangingIt`、`Enable_WhenFactoryExists_ShouldRejectWithoutInvokingIt`、`Enable_WhenServicesAreNull_ShouldReject`、`Enable_WhenAdaptedDerivedInvokerExists_ShouldKeepOriginalDescriptor`、`Enable_WhenAdaptedInvokerInstanceExists_ShouldKeepInstanceAndDescriptor` |

这些调用器测试使用真实 CAP ConsumerContext、ObjectMethodExecutor、原生 SubscribeInvoker 与 Filter 链路，不以替身调用器代替原生调用。调用前取消测试断言原生取消检查产生的 OCE 被适配包装；入场后取消测试断言业务收到同一令牌，finally 放行等待后保留实际抛出的同一取消对象。此处验证调用器契约，不将其解释为宿主停止或全部 CAP 在途业务排空验收。

`AddCapEventBus` 本身不会自动启用该适配。自定义调用器需显式选择兼容基类，并通过 ISubscribeInvoker 接口派发；原生单 Filter 的解析与已处理异常语义保留，不新增过滤器组合协议。未知自定义工厂不会被替换或为检查类型而执行。

| 两套 Admin 生产接线及组合行为 | 测试项目与方法 |
| --- | --- |
| `CapModule.AddServices` 显式启用适配，`CapSubscribeInvoker : BingSubscribeInvoker` 保留自身实例选择/跟踪扩展点及注册 | `Bing.Admin.EFCore.Tests.AdminCapRegistrationTest.AddServices_ShouldEnableConsumerCancellationRetryOnce`、`Bing.Admin.FreeSQL.Tests.AdminCapRegistrationTest.AddServices_ShouldEnableConsumerCancellationRetryOnce`；两套直接核对已适配类型和重复启用保持原描述符 |
| `BingSubscribeInvoker`、`AddCapEventBus`、`CapHostedLifecycleService` 与 `EventConsumptionStore.TryRecordAsync` 的真实宿主闭环 | 两套 `AdminMySqlCapIntegrationTest.Consumption_WhenCancelledBeforeBusinessCommit_ShouldRetryAndCommitOnce`、`Consumption_WhenCancelledAfterBusinessCommit_ShouldRetryWithoutDuplicateWrites`；目标事件一次 Publish，实际事务边界取消，原生 CAP 失败/重试，同一唯一 cap.received 原记录到 Succeeded 且 Retries >= 1，收据与测试业务行各一 |

真实消费用例仍使用直接 MySQL 事务中的测试 SQL 与收据，不能外推为两 ORM 全部工作单元取消验收；CAP 持久化消费重试不能外推为 RabbitMQ 重入队或严格一次投递。

当前已核对的执行证据：

- net6 适配/注册直接测试 18/18，`artifacts/cap-consumer-cancellation-fix/cap-adapter-direct-final-net6.trx`；初始 11/11 是补边界前的历史覆盖，不与最终结果累加。
- 两 Admin net6 专项各 12/12：`cap-cancel-ef-after-net6.trx`、`cap-cancel-freesql-after-net6.trx`（同一 artifacts 目录）；每套实际组成是 2 项真实取消、1 项 Admin 注册、9 项夹具清理职责测试，不能把 12 项全称为数据库集成用例。
- 适配前 EFCore 两项取消失败的 `cap-cancel-ef-net6.trx` 保留为失败基线；其观察到的 Succeeded/Retries=0 不属于重试通过证据。
- net8 及当前完整 21 必需方法、最低 34/33 门控尚未取得本批完整全通过验收；最终状态以 execution.md 实际记录为准，不预先声明通过。

### 有序事务状态与 CAP 适配（2026-10-05）

| 最终生产符号及关键行为 | 测试项目与直接方法、证据 |
| --- | --- |
| `IOrderedEventTransactionState.ActivateAmbient`、`Outcome`、`ResetBusinessStateAsync`：异步开启或保存点回滚后恢复调用方 CAP/ORM 状态 | `Bing.EventBus.Tests.Ordering.CapOrderedEventTransactionFactoryTest`：`BeginAsync_WhenInnerCompletesAsynchronously_ShouldActivateInCallerContext`、`ResetBusinessStateAsync_WhenHostCompletesAsynchronously_ShouldReplaceAndDiscardOldBuffer`、`ResetBusinessStateAsync_WhenResetFails_ShouldKeepOriginalErrorAndRollbackWithoutFlush`；`artifacts/ordered-cap-state-fix/cap-ordered-current-net6-final.trx` 与 `cap-ordered-current-net8.trx` |
| `EfCoreOrderedEventTransactionFactory<TContext>.TransactionScope`：清理 ChangeTracker、暴露提交结果、重复释放一次 | `Bing.EntityFrameworkCore.Tests.Events.EfCoreOrderedEventTransactionFactoryTest.Coordinator_WhenTrackedBusinessFails_ShouldClearAllFailedEntities`、`TransactionState_WhenFinished_ShouldExposeOutcomeAndRejectReset`；`artifacts/ordered-cap-state-fix/ef-state-after3-net6.trx`，3/3 |
| `FreeSqlOrderedEventTransactionFactory.TransactionScope`：清理 EntityChangeReport、暴露提交结果并保持 ORM 所有权 | `Bing.FreeSQL.Tests.Events.FreeSqlOrderedEventTransactionFactoryTest.ResetBusinessStateAsync_ShouldDiscardChangeReportWithoutCommitting`、`TransactionState_WhenCommitted_ShouldKeepOutcomeAndOrmOwnership`；`artifacts/ordered-cap-state-fix/freesql-state-after-net6.trx`，2/2 |
| `CapOrderedEventTransactionFactory`：物理事务唯一所有者、同事务 CAP 缓冲、提交后 Flush 错误保留 Committed、回滚/释放聚合错误 | `Bing.EventBus.Tests.Ordering.CapOrderedEventTransactionFactoryTest` 19 个用例项，net6/net8；最新注册入口加入后定向结果各21/21，环境仍未运行 Admin 真实 CAP 方法 |
| `AddBingCapOrderedEventTransaction<TTransactionFactory>`：显式包装入口、重复注册幂等、空服务拒绝并保留底层工厂 | `Bing.EventBus.Tests.Ordering.CapOrderedEventTransactionRegistrationTest.Register_WhenCalledRepeatedly_ShouldKeepOneDescriptor`、`Register_WhenServicesAreNull_ShouldReject`；net6 定向2/2，`artifacts/ordered-cap-state-fix/cap-ordered-registration-net6.trx` |

该批仅证明通用适配器和两 ORM 状态清理契约。Admin 生产模块没有业务有序消费者，专用 MySQL/CAP/RabbitMQ 门控仍需在真实 `3→1→2`、重复、失败恢复和派生出箱场景中验证，不能把编译或门控跳过当作生产接线通过。

### 有序恢复服务与关键审计入口（2026-10-05）

| 最终生产符号及关键行为 | 直接测试方法与证据 |
| --- | --- |
| `IOrderedEventRecoveryService.GetStateAsync`：状态查询先执行所有授权器，再开启协调器事务 | `Bing.EventBus.Tests.Ordering.OrderedEventRecoveryServiceTest.GetStateAsync_WhenAuthorizationFails_ShouldNotQueryState`；授权拒绝路径无查询副作用 |
| `OrderedEventRecoveryService.RetryAsync`：沿用协调器恢复授权、提交后写成功审计 | `OrderedEventRecoveryServiceTest.RetryAsync_WhenCommitted_ShouldWriteSuccessfulAudit`；SQLite 失败记录经人工重试恢复并产生单条安全审计 |
| `IOrderedEventRecoveryAudit`：审计失败保留原异常和提交结果 | `OrderedEventRecoveryServiceTest.RetryAsync_WhenAuditFailsAfterCommit_ShouldPreserveCommittedOutcome`；断言 `TransactionOutcome.Committed` 与 `Bing.OrderedEventRecoveryAuditCommitted` |
| `AddBingOrderedEventRecovery`：恢复入口显式注册且使用 `TryAddScoped` | `OrderedEventRecoveryService` 直接构造测试覆盖行为；宿主注册应另以生产授权器和持久化审计实现补齐 |

本批只完成通用恢复服务和审计契约，未把日志记录器当可靠落库，也未宣称 Admin 生产模块已有有序业务消费者、租户授权或补偿接口。实际执行：net6/net8 各3/3，证据为 `artifacts/ordered-cap-state-fix/ordered-recovery-service-net6.trx`、`ordered-recovery-service-net8.trx`。

### EFCore 自动实体审计快照（2026-10-05）

| 最终生产符号及关键行为 | 直接测试方法与证据 |
| --- | --- |
| `Bing.Datas.EntityFramework.Core.UnitOfWorkBase.CaptureAuditChanges`：按新增、修改、删除捕获属性变化并合并同一实体 | `Bing.EntityFrameworkCore.Tests.Integration.EfCoreAuditPropertySetterIntegrationTest.SaveChangesAsync_ShouldPersistCreationAndModificationAuditFields`；SQLite 真实工作单元覆盖三种状态及作用域隔离 |
| 自动快照的敏感字段过滤与有界值 | 同一测试设置 `Password`，断言审计明细不包含密码或令牌属性；生产存储仍通过 `AuditLogPersistenceOptions.AllowedPropertyNames` 做第二层白名单 |
| 无活动审计作用域的兼容路径 | `Bing.EntityFrameworkCore.Tests` net6 全量90/90，现有无审计作用域测试回归通过；证据 `artifacts/audit-auto-snapshot/efcore-auto-audit-full-net6.trx` |

本批只完成 EFCore 自动捕获；FreeSQL `EntityChangeReport` 接入、普通审计可靠落库、保存预算和慢存储释放仍不能从上述测试推断为已完成。

### FreeSQL 自动实体审计快照（2026-10-05）

| 最终生产符号及关键行为 | 直接测试方法与证据 |
| --- | --- |
| `Bing.Uow.UnitOfWorkBase.RegisterAuditChangeCallback`：仅在活动审计作用域接入 FreeSQL 报告，保留报告或配置回调 | `Bing.FreeSQL.Tests.Core.UnitOfWorkEntityAuditSnapshotTest.SaveChangesAsync_WhenNoAuditScope_ShouldPreserveHostCallbackAndSkipCapture`、`SaveChangesAsync_WhenHostCallbackExists_ShouldPreserveItAndMergeRepeatedUpdates`；net6/net8 快照定向证据位于 `artifacts/freesql-entity-audit-snapshot/` |
| `Bing.Uow.UnitOfWorkBase.CaptureAuditChanges`：真实 SQLite 工作单元捕获新增、修改、删除、主键、租户和属性前后值 | `UnitOfWorkEntityAuditSnapshotTest.SaveChanges_WhenEntityChanges_ShouldCaptureCreatedUpdatedAndDeletedSnapshots`（同步/异步两行）；net6 快照定向 7/7，net8 快照定向 7/7 |
| FreeSQL 快照安全边界：敏感属性和忽略列跳过，数值使用固定区域格式，文本限制 512 字符，同一实体重复保存合并 | `UnitOfWorkEntityAuditSnapshotTest.SaveChangesAsync_WhenOnlySensitivePropertiesChange_ShouldNotAppendEntityChanges`、`SaveChangesAsync_WhenCultureUsesDecimalComma_ShouldFormatValuesInvariantly`、`SaveChangesAsync_WhenHostCallbackExists_ShouldPreserveItAndMergeRepeatedUpdates`；net6 快照与取消/事务审计组合 25/25 |

FreeSQL 报告通知位于物理提交后；这些测试证明自动收集与回调兼容，不证明普通审计已与业务事务原子落库。关键审计仍使用现有 `SaveTransactionalAuditAsync` 入口，普通数据库审计配置、保存预算/取消和慢存储释放另行验收。

专用容器门控已补齐上述测试夹具：`Bing.Admin.EFCore.Tests.OrderedEventCapIntegrationTest` 三项与 `Bing.Admin.FreeSQL.Tests.OrderedEventCapIntegrationTest` 三项均通过；证据为 `artifacts/ordered-cap-state-fix/admin-efcore-cap-ordered-state-net6-final.trx`、`admin-freesql-cap-ordered-state-net6-final.trx`。这验证的是测试宿主显式接线，不等于 `modules/admin/src` 已注册生产有序业务消费者或恢复授权器。

### 有序消费者全局积压统计（2026-10-06）

| 生产符号 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `IOrderedEventStoreStatistics.GetPendingEventCountAsync` | `DbOrderedEventStoreTest.GetPendingEventCountAsync_ShouldCountAllUnprocessedEvents` | `Bing.EventBus.Tests` net6/net8；`artifacts/ordered-statistics/net6/ordered-statistics-net6.trx`、`artifacts/ordered-statistics/net8/ordered-statistics-net8.trx` |
| `BingOrderedEventMetricsSnapshot.PendingEvents` | `OrderedEventMetricsTest.Snapshot_ShouldExposeAggregatedCounters` | `Bing.EventBus.Tests` net6/net8；同上 |
| `OrderedEventCoordinator.GetPendingEventCountAsync` 与消费者统计回退 | `OrderedEventConsumerTest` 既有后台消费者回归，统计存储契约由上述 SQLite 直接测试覆盖 | `Bing.EventBus.Tests` 有序定向集；自定义存储未实现新契约时保留分页回退 |

### FreeSQL 提交后自动审计快照异常隔离（2026-10-06）

| 生产符号 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `Bing.Uow.UnitOfWorkBase.CaptureAuditChanges` 的提交后异常隔离 | `UnitOfWorkEntityAuditSnapshotTest.SaveChanges_WhenAuditGetterThrowsAfterCommit_ShouldKeepBusinessSuccess`（同步/异步） | `Bing.FreeSQL.Tests` net6/net8 各 10/10；`artifacts/freesql-entity-audit-snapshot/freesql-entity-audit-hardened-net6.trx`、`freesql-entity-audit-hardened-net8.trx` |
| 宿主实体变化回调异常保留 | `UnitOfWorkEntityAuditSnapshotTest.SaveChangesAsync_WhenHostCallbackThrows_ShouldPreserveOriginalError` | 同上；原始异常仍传播，自动采集不吞宿主回调 |
| FreeSQL 全量回归 | `Bing.FreeSQL.Tests` 全部测试方法 | net6/net8 各 69 Passed、24 NotExecuted（外部 MySQL 门控）；`artifacts/freesql-entity-audit-snapshot/full-hardening-net6/`、`full-hardening-net8/` |

### CAP 仅发布宿主角色（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法与证据 |
| --- | --- |
| `EnableBingCapPublisherOnly`：移除 CAP 消费注册器别名和接收重试处理器，保留 Dispatcher 出箱发布 | `Bing.EventBus.Tests.Cap.CapHostRoleRegistrationTest.EnableBingCapPublisherOnly_ShouldKeepDispatcherAndRemoveConsumers`；net6/net8 各3/3，`artifacts/cap-host-role/net6/cap-host-role-net6.trx`、`artifacts/cap-host-role/net8/cap-host-role-net8.trx` |
| 角色扩展重复启用幂等且不误删自定义 `IProcessingServer` | `CapHostRoleRegistrationTest.EnableBingCapPublisherOnly_WhenRepeated_ShouldBeIdempotent`、`EnableBingCapPublisherOnly_ShouldPreserveCustomProcessor`；同上 |
| Admin `CapModule` 读取 `BING_CAP_CONSUMER_ENABLED`，默认兼容，显式 false 进入仅发布模式 | `modules/admin/src/Bing.Admin/Modules/CapModule.cs`、`modules/admin/src/Bing.Admin.FreeSQL/Modules/CapModule.cs`；行为由两个宿主构建验证，真实角色切流需 RabbitMQ/部署环境专项验收 |

### 宿主角色模块隔离（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法与证据 |
| --- | --- |
| `BingHostRoleExtensions.GetBingHostRole`：默认 Web、大小写不敏感解析 `web/consumer/task`、拒绝数值和未知配置 | `BingHostRoleTest.GetBingHostRole_WhenMissing_ShouldReturnWeb`、`GetBingHostRole_ShouldParseSupportedValues`、`GetBingHostRole_WhenInvalid_ShouldFailWithConfigurationKey`；`Bing.AspNetCore.Mvc.Tests` net6 6/6，`artifacts/host-role/task-strict-net6/bing-host-role-task-strict-net6.trx` |
| Admin consumer 角色跳过 Swagger、Hangfire、HTTP 业务管道和 CAP Dashboard，并强制保留 CAP 消费 | `modules/admin/src/Bing.Admin/Startup.cs`、`modules/admin/src/Bing.Admin.FreeSQL/Startup.cs`、两个 `AppModule` 与 `CapModule`；EFCore/FreeSQL net6 构建均 0 错误；未把构建结果当作真实 Worker/RabbitMQ 切换验收 |
| Admin task 角色跳过 HTTP/Swagger，两套宿主均启用 Hangfire Server 但不挂 Dashboard，CAP 消费默认关闭且可显式开启 | `BingHostRoleTest.GetBingHostRole_ShouldParseSupportedValues`（Task 解析与后台角色判断）；两个 `HangfireModule.cs`、两个 `AppModule`/`Startup.cs` 与 `CapModule.cs`；EFCore/FreeSQL 构建 0 错误；TRX `artifacts/host-role/task-net6/bing-host-role-task-net6.trx`，FreeSQL 新增注册与排空证据见文档末尾 |
| Task/Consumer 都跳过 Swagger 的启动条件修正 | 两个 Admin `Startup.ConfigureServices` 使用 `IsBingBackgroundRole`；修正后 EFCore/FreeSQL net6 构建均 0 错误、55 警告，保留前一行历史构建记录 |

### HTTP 审计保存等待预算（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法与证据 |
| --- | --- |
| `BingHttpAuditingOptions.SaveTimeout`：默认无限等待，非法负值在中间件构造阶段拒绝 | `BingHttpAuditingMiddlewareTest.Constructor_WhenSaveTimeoutIsInvalid_ShouldThrow`；`BingHttpAuditingMiddlewareTest` 职责集 net6/net8 各 20/20，证据为 `artifacts/audit-save-budget/full-net6/auditing-save-budget-full-net6.trx`、`artifacts/audit-save-budget/full-net8/auditing-save-budget-full-net8.trx` |
| `BingHttpAuditingMiddleware.SaveWithBudgetAsync`：预算耗尽只附加超时诊断，继续等待原始保存任务，随后才释放审计作用域 | `BingHttpAuditingMiddlewareTest.InvokeAsync_WhenStoreExceedsSaveBudget_ShouldWaitForActualCompletion`；直接预算用例 net6/net8 各 1/1，证据为 `artifacts/audit-save-budget/net6/auditing-save-budget-net6.trx`、`artifacts/audit-save-budget/net8/auditing-save-budget-net8.trx` |
| `ThrowOnStoreFailure` 与保存预算组合：实际保存完成后才传播超时诊断，不把预算耗尽误报为提前释放 | `BingHttpAuditingMiddlewareTest.InvokeAsync_WhenStoreExceedsBudgetAndThrowEnabled_ShouldPropagateTimeoutAfterCompletion`；包含在上述 net6/net8 20/20 职责集 |

有限预算的计时器在保存任务提前完成或超时诊断产生后取消，避免长预算在请求结束后保留定时任务；该资源路径由上述 net6/net8 20/20 职责集间接回归，仍未覆盖调用方取消或宿主强制停止。

该预算不是取消令牌或后台脱离保存机制。普通存储没有调用方取消契约，生产慢存储、宿主停止和取消策略仍需独立验收；`ThrowOnStoreFailure=true` 时预算诊断会在实际保存和释放完成后参与失败传播。

### Admin 有序事件生产基础接线（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| EFCore `CapModule` 在 `OrderedEvents:Enabled=true` 时注册 `IOrderedEventStore` 和 `EfCoreOrderedEventTransactionFactory<AdminUnitOfWork>` | `Bing.Admin.EFCore.Tests.AdminCapRegistrationTest.AddServices_WhenOrderedEventsEnabled_ShouldRegisterFoundation` | `Bing.Admin.EFCore.Tests` net6 14/14；`artifacts/admin-ordered-registration/efcore/admin-ordered-registration-efcore-net6.trx` |
| FreeSQL `CapModule` 借用 `FreeSqlWrapper.Orm` 注册 `FreeSqlOrderedEventTransactionFactory`，并注册 CAP 事务适配器 | `Bing.Admin.FreeSQL.Tests.AdminCapRegistrationTest.AddServices_WhenOrderedEventsEnabled_ShouldRegisterFoundation` | `Bing.Admin.FreeSQL.Tests` net6 14/14；`artifacts/admin-ordered-registration/freesql/admin-ordered-registration-freesql-net6.trx` |
| 关闭开关保持兼容，不注册有序基础设施 | `AdminCapRegistrationTest.AddServices_ShouldKeepInventoryDisabledByDefault` 及默认配置路径 | 上述两个注册职责集；默认路径无 `OrderedEvents:Enabled` |

该批只证明生产模块服务描述已接线，不能替代真实 CAP/RabbitMQ 消费、业务处理器、租户授权、恢复审计和有序表迁移验收。实际 SDK 为 8.0.425，锁定 8.0.424 未验证。

### 有序事件恢复授权缺口修复（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `OrderedEventRecoveryService.AuthorizeAsync`：没有授权器时拒绝状态查询和人工重试 | `OrderedEventRecoveryServiceTest.GetStateAsync_WhenAuthorizationIsMissing_ShouldReject`、`RetryAsync_WhenAuthorizationIsMissing_ShouldReject` | `Bing.EventBus.Tests` net6/net8 各 5/5；`artifacts/ordered-recovery-auth/final-net6/ordered-recovery-auth-final-net6.trx`、`artifacts/ordered-recovery-auth/final-net8/ordered-recovery-auth-final-net8.trx` |
| `OrderedEventRecoveryService.RetryAsync`：授权完成后调用协调器无授权内部路径，避免授权器重复执行 | `OrderedEventRecoveryServiceTest.RetryAsync_WhenCommitted_ShouldWriteSuccessfulAudit` | 同上；成功审计、事务提交和授权路径均覆盖 |
| 提交后审计失败保留 `Committed` 结果 | `OrderedEventRecoveryServiceTest.RetryAsync_WhenAuditFailsAfterCommit_ShouldPreserveCommittedOutcome` | 同上；原有异常与审计诊断断言继续通过 |

该测试只验证框架默认拒绝未授权恢复；实际租户授权器、审计持久化及管理端身份链仍属于宿主交付项。

### Generic Host 后台模块初始化接线（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法与证据 |
| --- | --- |
| `EnableBingHostedInitialization`：在已有 Bing 模块注册记录上启用 Host 启动初始化，不提前构建容器 | `BingHostedLifecycleTest.HostedLifecycle_ShouldInitializeAndShutdownModules`；Core net6/net8 各 2/2，`artifacts/hosted-lifecycle/net6/hosted-lifecycle-net6.trx`、`artifacts/hosted-lifecycle/net8/hosted-lifecycle-net8.trx` |
| `BingModuleHostedService.StartAsync`：启用后台模式时调用 `IBingModuleManager.InitializeAsync`，默认模式保持 Web 初始化时机 | `BingHostedLifecycleTest.HostedLifecycle_ShouldInitializeAndShutdownModules`、`HostedLifecycle_WhenNotEnabled_ShouldPreserveWebInitializationTiming`；同上 |
| `BingModuleHostedService.StopAsync`：复用统一异步关闭路径 | `BingHostedLifecycleTest.HostedLifecycle_ShouldInitializeAndShutdownModules`；停止后模块关闭计数为 1；同上 |

该映射只覆盖 Core 生命周期入口。Admin 的后台角色接线由 EFCore/FreeSQL net6 生产构建验证为 0 错误；真实无 HTTP Worker、CAP/Hangfire 排空和部署切流仍需环境测试。

| Admin 宿主角色分支 | `Bing.Admin.Program.CreateHostBuilder`、`Bing.Admin.FreeSQL.Program.CreateHostBuilder`：后台角色进入 Generic Host，Web 角色进入 `ConfigureWebHostDefaults` | 两个 Admin net6 生产工程构建均 0 错误；当前没有把构建结果当作 RabbitMQ 消费排空或部署切流验收 |

| EFCore Task Hangfire 接线 | `HangfireModule.AddServices` 在 Task 注册 `AddHangfireServer`；`OnApplicationInitialization` 配置 DI 激活器和定时任务 | `AdminHangfireRegistrationTest.AddServices_WhenTaskRole_ShouldRegisterHostedServer`、`AddServices_WhenWebRole_ShouldNotRegisterHostedServerEarly`；2/2，`artifacts/host-role/hangfire/admin-hangfire-role-net6.trx`；真实 Hangfire 任务执行、停止排空和切流仍需运行环境测试 |

### 有序事件最老积压年龄指标（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法与证据 |
| --- | --- |
| `IOrderedEventStorePendingAgeStatistics`：为自定义存储提供可选的最老入箱时间查询，不破坏旧存储契约 | `DbOrderedEventStoreTest.GetOldestPendingEventUtcAsync_ShouldReturnOldestPendingRecord`；`Bing.EventBus.Tests` net6/net8 各 16/16，`artifacts/ordered-age-index/net6/ordered-age-index-net6.trx`、`artifacts/ordered-age-index/net8/ordered-age-index-net8.trx` |
| `DbOrderedEventStore.EnqueueAsync` 与 `OrderedEventSchema`：写入 `CreatedAtUtc` 并为 MySQL/SQLite 生成对应字段和年龄索引 | `DbOrderedEventStoreTest.GetOldestPendingEventUtcAsync_ShouldReturnOldestPendingRecord`、`OrderedEventSchemaTest.GetCreateStatements_WhenMySql_ShouldIncludePendingAgeIndex`、`GetCreateStatements_WhenSqlite_ShouldAddPendingAgeIndex`；同上，覆盖入箱、处理一条后仍能查询剩余积压和完整 SQL |
| `OrderedEventCoordinator.GetOldestPendingEventAgeAsync`：按 UTC 当前时间计算年龄并钳制时钟回拨 | `OrderedEventMetricsTest.Snapshot_WhenPendingAgeIsMissing_ShouldReturnNull` 及有序消费者指标职责集；同上 |
| `BingOrderedEventMetricsSnapshot.OldestPendingEventAge`：记录年龄、空值和负值归零，不增加高基数标签 | `OrderedEventMetricsTest` 中年龄聚合与空值/负值用例；同上 |

| `deploy/migrations/ordered-events/001-created-at-utc.mysql.sql`：为旧收件箱补字段、历史 UTC 基线和查询索引 | `OrderedEventSchemaTest` 固定新表索引 SQL；迁移脚本需在专用 MySQL 环境执行并保存 DDL/回填证据，本轮未对生产库运行 |

该映射只证明进程内年龄采集、SQLite 归档行为和 MySQL 建表 SQL 形态的直接行为。已部署收件箱和历史表的真实迁移执行、MySQL 归档 INSERT/DELETE、指标导出/告警、权限控制及长期 GC/容量测量仍未验收。

### 标准 Meter 指标出口（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `BingOrderedEventMetrics`：注册 `Bing.Events.Ordering` Meter，公开固定 `consumer` 标签和聚合观察仪表 | `OrderedEventMetricsTest.Metrics_ShouldExposeObservableMeasurements` | `Bing.EventBus.Tests` net6/net8 各 5/5；`artifacts/ordered-meter/net6/ordered-meter-net6.trx`、`artifacts/ordered-meter/net8/ordered-meter-net8.trx` |
| `BingOrderedEventMetrics.Dispose`：重复释放只释放本实例 Meter，不保留静态注册引用 | 同一 `MeterListener` 用例使用 `using` 释放指标实例 | 同上；仅验证实例资源释放，不替代插件上下文或长期 GC 回收测试 |

本批只提供标准进程内 Meter 采集契约。OpenTelemetry/Prometheus exporter、远程认证、跨进程聚合、告警规则、积压归档和长期分配/存活对象测量仍未验收。

### 普通审计分批保留清理（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `IAuditLogRetentionStore.DeleteBeforeAsync`：要求 UTC 截止时间和有限批量，尊重取消并返回实际删除数 | `DbAuditingStoreTest.DeleteBeforeAsync_WhenArgumentsAreInvalid_ShouldRejectBeforeOpeningConnection`、`DeleteBeforeAsync_WhenBatchIsLimited_ShouldDeleteAtMostBatchSize` | `Bing.Auditing.Tests` 最终职责集 net6/net8 各 27/27；`artifacts/auditing-retention/final2-net6/auditing-retention-final2-net6.trx`、`artifacts/auditing-retention/final2-net8/auditing-retention-final2-net8.trx` |
| `DbAuditingStore` 的 `IsCritical` 隔离：普通清理只删除过期普通审计，关键事务审计保留 | `DbAuditingStoreTest.DeleteBeforeAsync_ShouldDeleteOnlyExpiredOrdinaryAudits` | 同上；SQLite 真实建表、写入和独立清理事务 |
| `AuditLogSchema` 与 `deploy/migrations/auditing/001-critical-flag.mysql.sql`：关键标记和保留索引 | `AuditLogSchemaTest.GetCreateStatements_WhenSqlite_ShouldMatchCompleteStatement`、`GetCreateStatements_WhenMySql_ShouldMatchCompleteStatement` | 同上；迁移脚本需在专用 MySQL 环境执行并保存 DDL/历史分类复核证据，本轮未对生产库运行 |

本批没有实现冷归档、删除授权、租户差异化留存或自动定时器；生产宿主必须以受控运维任务调用分批接口，并保留清理批次和失败恢复记录。

Bing.Auditing.Tests 完整回归在 net6/net8 均 158/158 通过，证据为 `artifacts/auditing-retention/full-final-net6/auditing-full-retention-final-net6.trx` 和 `artifacts/auditing-retention/full-final-net8/auditing-full-retention-final-net8.trx`。

### Admin 有序恢复授权与审计接线（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `CapModule`：恢复开关显式启用且基础设施开启时注册受控恢复服务、授权器和审计适配器 | `AdminOrderedEventRecoveryTest.AddServices_WhenRecoveryIsEnabled_ShouldRegisterExplicitAdapters` | Admin EFCore/FreeSQL net6 各 3/3；`artifacts/admin-recovery/efcore3/admin-recovery-efcore3.trx`、`artifacts/admin-recovery/freesql/admin-recovery-freesql.trx` |
| `AdminOrderedEventRecoveryAuthorizer`：认证、角色、消费者和租户边界 | `AdminOrderedEventRecoveryTest.Authorizer_ShouldRejectUnauthorizedConsumerAndTenant` | 同上 |
| `AdminOrderedEventRecoveryAudit`：仅保存安全摘要并在释放后恢复审计上下文 | `AdminOrderedEventRecoveryTest.Audit_ShouldPersistSafeRecoveryProjection` | 同上 |

该映射只证明注册和适配器直接行为；实际管理端点、跨节点恢复竞争及生产租户策略仍需宿主环境验收。

### 审计保存取消传递（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `ICancellableAuditingStore`、`ICancellableAuditLogSaveHandle`：可选传递调用方取消，旧存储仍走兼容入口 | `BingHttpAuditingMiddlewareTest.InvokeAsync_WhenRequestIsAborted_ShouldForwardCancellationToStore` | `Bing.AspNetCore.Mvc.Tests` net6/net8 各 21/21；`artifacts/audit-cancellation/net6-final/audit-cancellation-net6-final.trx`、`artifacts/audit-cancellation/net8-final/audit-cancellation-net8-final.trx` |
| `AuditingManager`、`AuditingManagerExtensions`：句柄只启动一次保存并保持异步释放等待 | `Bing.Auditing.Tests` 完整职责集 | net6/net8 各 158/158；`artifacts/audit-cancellation/auditing-net6/auditing-cancellation-net6.trx`、`artifacts/audit-cancellation/auditing-net8/auditing-cancellation-net8.trx` |
| `DbAuditingStore.SaveAsync(AuditLogInfo, CancellationToken)`：数据库打开、执行和回滚路径尊重调用方令牌 | `DbAuditingStoreTest.TransactionalSave_WhenCancelledBeforeInsert_ShouldNotWrite` 及现有连接所有权用例 | 上述 Auditing 完整回归；实际生产 MySQL 停止演练仍未执行 |

取消传递只允许存储结束自身等待；中间件在预算或取消后仍等待保存任务和作用域释放，不把“停止等待”报告为资源已清理。普通自定义存储若未实现可选接口，继续沿用无令牌契约。

### 审计元数据查询（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `IAuditLogQueryStore.QueryAsync`：必须显式租户、有界分页、过滤和稳定排序 | `DbAuditingStoreTest.QueryAsync_ShouldFilterByTenantAndReturnStableMetadata`、`QueryAsync_WhenArgumentsAreInvalid_ShouldRejectBeforeOpeningConnection`、`QueryAsync_WhenFactoryReturnsOpenConnection_ShouldNotDisposeBorrowedConnection` | `Bing.Auditing.Tests` net6/net8 各 27/27；`artifacts/audit-query/final-net6`、`artifacts/audit-query/final-net8` 对应 TRX |
| `DbAuditingStore` SQLite/MySQL 查询租户身份解码及关键标记投影 | 同上，另覆盖关键事务记录查询 | SQLite 直接证据；MySQL SQL/VARBINARY 路径需专用数据库环境验证 |
| `AddBingDatabaseAuditing` 查询契约与存储共享单例、重复注册清理旧契约 | `AuditingPersistenceExtensionsTest.AddBingDatabaseAuditing_ShouldReplaceDefaultStoreAndShareInstance`、`AddBingDatabaseAuditing_WhenRepeated_ShouldKeepSingleLatestStore` | 同上 |

查询层只提供不含 `DetailsJson` 的元数据，不证明 HTTP 端点、角色/租户授权、明细脱敏、删除授权、冷归档或长期容量策略已完成。

### Redis 限流分桶回收等待时间（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `RedisTokenBucketRateLimiter`：`maxKeys` 已满时按 Redis 服务端时间返回最早可回收分桶的等待时间；调用方预先取消不访问 Redis | `RedisTokenBucketRateLimiterTest.TryAcquireAsync_WhenMaxKeysIsFull_ShouldReturnReclaimWait`、`TryAcquireAsync_WhenCancelledBeforeRedisCall_ShouldNotConsumePermit` | `Bing.Caching.CSRedis.Tests.Integration` net6.0/net8.0 各 11/11；`artifacts/rate-limit-reclaim/net6-enabled/redis-rate-limit-net6-enabled.trx`、`artifacts/rate-limit-reclaim/net8-enabled/redis-rate-limit-net8-enabled.trx` |
| Redis 脚本仍保持每秒一次的扫描节流和原子额度更新 | `RedisTokenBucketRateLimiterTest.TryAcquireAsync_ConcurrentCalls_ShouldAllowOnlyCapacity`、`TryAcquireAsync_IdleKey_ShouldBeReclaimed` | 同一集成职责集；未设置环境变量时 10 项跳过，主从切换和断连恢复未验收 |

该修复只改进拒绝响应中的等待时间，不改变 Redis 故障默认返回 503、显式降级放行和取消传播规则；两个独立进程的真实额度隔离仍需部署环境测试。

### 有序消费者扫描失败指标（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `BingOrderedEventMetricsSnapshot.ScanFailureCount`、`BingOrderedEventMetrics.RecordScanFailure`：保留旧积压快照并累计扫描或统计失败 | `OrderedEventMetricsTest.Snapshot_ShouldExposeAggregatedCounters`、`OrderedEventMetricsTest.Metrics_ShouldExposeObservableMeasurements` | `Bing.EventBus.Tests` net6/net8 各 6/6；`artifacts/ordered-meter/scan-failure-net6/ordered-meter-scan-failure-net6.trx`、`artifacts/ordered-meter/scan-failure-net8/ordered-meter-scan-failure-net8.trx` |
| `BingOrderedEventMetrics`：发布固定低基数 `bing.ordered.scan.failures`，没有年龄时省略年龄测量 | `OrderedEventMetricsTest.Metrics_WhenPendingAgeIsMissing_ShouldNotPublishAgeMeasurement`、`Metrics_ShouldExposeObservableMeasurements` | 同上；年龄缺失不再以负数哨兵冒充数据 |
| `OrderedEventConsumer.ConsumerWorker`：主扫描、全局积压统计和最老年龄统计失败均记录扫描失败，并保留失败统计前的旧值 | `OrderedEventConsumerTest.HostedWorker_WhenPendingStreamScanFails_ShouldRecordScanFailure`、`HostedWorker_WhenStatisticsScansFail_ShouldRecordScanFailures` | `Bing.EventBus.Tests` net6/net8 各 10/10；`artifacts/ordered-consumer-scan-failure-retain/net6/ordered-consumer-scan-failure-retain-net6.trx`、`artifacts/ordered-consumer-scan-failure-retain/net8/ordered-consumer-scan-failure-retain-net8.trx` |

本批只覆盖进程内指标和 Worker 异常路径，不等同于 exporter、告警、诊断授权、归档及长期内存探针验收。实际 SDK 为 8.0.425，锁定 8.0.424 仍未验证。

### FreeSQL 异步查询回退（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `FreeSqlAsyncQueryableProvider`：`CanExecute` 保持 `false`；异步元素、数量、聚合、列表和数组方法执行取消检查后同步 LINQ 并包装 `Task` | `FreeSqlAsyncQueryableProviderTest.CanExecute_ShouldRemainFalse`、`ElementAndCountQueries_ShouldMatchLinqSemantics`、`AggregateAndMaterializationQueries_ShouldReturnExpectedValues`、`QueryWithCancelledToken_ShouldThrowCancellation` | `Bing.FreeSQL.Tests` net6/net8 各 4/4；`framework/tests/Bing.FreeSQL.Tests/TestResults/freesql-async-provider-net6-final4.trx`、`framework/tests/Bing.FreeSQL.Tests/TestResults/freesql-async-provider-net8-final2.trx` |

该回退只保证 API 和异常/取消语义，不等同于 FreeSQL 数据库 Provider 的真正异步 I/O。

完整回归补充：`Bing.FreeSQL.Tests` net6.0/net8.0 均为 74 通过、24 跳过、0 失败，证据为 `artifacts/freesql-full/root-net6/freesql-full-root-net6.trx` 和 `artifacts/freesql-full/root-net8/freesql-full-root-net8.trx`；跳过项依赖 MySQL/多进程环境。

### Redis 限流独立进程额度（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `RedisTokenBucketRateLimiter`：两个独立进程竞争同一策略和分桶键时共享容量，不因进程数翻倍 | `RedisTokenBucketRateLimiterTest.TryAcquireAsync_TwoIndependentProcesses_ShouldShareCapacity` | `Bing.Caching.CSRedis.Tests.Integration` net6/net8 各 1/1；`artifacts/rate-limit-independent-process/final-net6/redis-rate-independent-process-final-net6.trx`、`artifacts/rate-limit-independent-process/final-net8/redis-rate-independent-process-final-net8.trx` |

该测试依赖本机 Redis，仅证明正常跨进程原子竞争；故障恢复、主从切换及真实 HTTP 认证代理链仍需部署验收。

同一环境的 `Bing.Caching.CSRedis.Tests.Integration` 全量回归 net6/net8 均为 72/72 通过，证据为 `artifacts/rate-limit-independent-process/full-net6/csredis-full-independent-net6.trx` 和 `artifacts/rate-limit-independent-process/full-net8/csredis-full-independent-net8.trx`。

### Redis 锁执行入口持有者契约（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `CSRedisDistributedLock.ExecuteWithLock`、`ExecuteWithLockAsync` 及泛型重载：按调用方提供的持有者值获取并校验释放 | `RedisDistributedLockTest.ExecuteWithLock_ShouldHonorProvidedOwner`，并结合并发获取、错误持有者、过期租约和回调/释放异常测试 | `Bing.Caching.CSRedis.Tests.Integration` net6/net8 各 10/10；`artifacts/lock-owner-contract/net6`、`artifacts/lock-owner-contract/net8` |

### 锁便捷扩展唯一持有者（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `LockExtensions.ExecuteWithLock*`：自动释放调用每次使用唯一持有者；`LockTak/LockRelease` 保持固定值兼容语义 | `LockExtensionsTest.ExecuteWithLock_ShouldUseUniqueOwner`、`LockExtensionsTest.LockTakAndRelease_ShouldKeepLegacyOwner` | `Bing.Core.Tests` net6/net8 各 2/2；`artifacts/lock-owner-contract/core-net6`、`artifacts/lock-owner-contract/core-net8` |

Core 全量回归补充：`Bing.Core.Tests` net6.0、net8.0 均为 629/629 通过，证据为 `artifacts/lock-owner-contract/core-full-net6`、`artifacts/lock-owner-contract/core-full-net8`。

### Admin EFCore 资源排序查询（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `Bing.Admin.Data.Stores.Implements.Systems.ResourcePoStore.GetMaxSortIdAsync`：按应用和父模块过滤并返回最大排序号，空结果返回 `null` | `ResourcePoStoreTest.GetMaxSortIdAsync_ShouldFilterApplicationAndParent`、`ResourcePoStoreTest.GetMaxSortIdAsync_ShouldReturnNullWhenNoMatch` | `Bing.Admin.EFCore.Tests` net6.0 2/2；`artifacts/admin-resource-store/net6-final/admin-resource-store-net6.trx` |

该映射只证明 SQLite 作用域中的 EFCore 查询行为；生产 MySQL、库存并发和 CAP 事务验收仍按 FIX-006/007/013 的未完成项执行。

### Redis 限流原子提交后的取消语义（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `RedisTokenBucketRateLimiter.TryAcquireAsync`：脚本开始前取消不访问 Redis；脚本完成后的调用方取消不把已提交额度伪装成未执行 | `RedisTokenBucketRateLimiterTest.TryAcquireAsync_WhenCancelledBeforeRedisCall_ShouldNotConsumePermit`、`TryAcquireAsync_ConcurrentCalls_ShouldAllowOnlyCapacity`、`TryAcquireAsync_TwoIndependentProcesses_ShouldShareCapacity` | `Bing.Caching.CSRedis.Tests.Integration` net6/net8 全量各 72/72；本批限流职责集各 12/12；`artifacts/rate-limit-cancel/net6-final/redis-rate-cancel-net6-final.trx`、`artifacts/rate-limit-cancel/net8-final/redis-rate-cancel-net8-final.trx` |

实现不通过定时取消伪造“脚本完成后”竞态测试，因为 Redis 操作完成与取消的先后需要部署级故障注入才能确定；该边界要求调用方按事件 ID 或请求幂等键处理已提交结果。Redis 断连、主从切换和真实 HTTP 认证代理链仍未验收。

### 模块与插件长期资源探针（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| 模块初始化、失败、取消和插件换代后的资源释放、弱引用回收及 GC/分配观测 | `ModuleResourceProbeTest.ModuleCycles_ShouldReleaseOwnedResourcesAndReportGc`、`ModuleResourceProbeTest.PluginReloadCycles_ShouldCollectRetiredContextsAndReportGc`、`ModuleResourceProbeTest.ModuleInitializationCancellationDuringCallback_ShouldReleaseOwnedResources` | `Bing.Core.Tests` `net6.0`、`net8.0` 各 5/5；`artifacts/module-resource-probe/net6/module-resource-probe-net6.trx`、`artifacts/module-resource-probe/net8/module-resource-probe-net8.trx` |

探针没有把工作集变化转换成“无泄漏”结论，也不验证默认加载上下文程序集卸载；生产 exporter、告警权限、归档和长期容量预算仍需单独交付。

### CAP 元数据边界校验（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `ClusterEventHeaders.Write`：限制本地事件 ID、租户、业务流、实体键、契约版本和跟踪 ID，并验证正序号 | `ClusterEventHeadersTest.Write_WhenMetadataExceedsLimit_ShouldReject` | `Bing.EventBus.Tests` net6/net8 各 15/15；`artifacts/cluster-event-headers/sequence-net6/cluster-event-headers-sequence-net6.trx`、`artifacts/cluster-event-headers/sequence-net8/cluster-event-headers-sequence-net8.trx` |
| `ClusterEventHeaders.Read`：拒绝超限远端消息头（包括序号），并保留缺失可选字段的兼容读取 | `ClusterEventHeadersTest.Read_WhenMetadataExceedsLimit_ShouldReject` 及现有兼容读取测试 | 同上 |

该映射证明消息头大小边界和异常类型已经固定，不等同于有序事件生产、消费恢复、CAP 状态归档或监控导出器验收。实际 SDK 为 8.0.425，锁定的 8.0.424 仍未验证。

### 蓝绿镜像不可变性校验（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `deploy/bluegreen/Test-BlueGreen.ps1`：Live 模式拒绝可变标签、缺少 digest 的镜像和 Kubernetes 占位镜像 | `Test-BlueGreen.ps1 -Platform Compose -Live`、`Test-BlueGreen.ps1 -Platform Kubernetes -Live` 的输入预检 | 本地 PowerShell 预检；静态 Compose/Kubernetes 各 9/9 通过。Docker/Kubernetes 服务不可用，未执行真实就绪、切流或回滚 |

该映射只证明发布输入不会绕过不可变镜像约束，不等同于动态蓝绿部署验收。镜像必须由部署环境提供完整 `@sha256:<64hex>` 标识。

### 审计保留清理授权入口（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `AuditLogRetentionService.DeleteBeforeAsync`：先校验请求和授权，授权成功后才调用低层删除；拒绝、超批量和预取消不产生存储副作用 | `AuditLogRetentionServiceTest.DeleteBeforeAsync_ShouldAuthorizeBeforeStore`、`DeleteBeforeAsync_WhenAuthorizationFails_ShouldNotDelete`、`DeleteBeforeAsync_WhenCancelledBeforeAuthorization_ShouldNotCallDependencies`、`DeleteBeforeAsync_WhenBatchExceedsLimit_ShouldRejectBeforeAuthorization` | `Bing.Auditing.Tests` net6/net8 各 5/5；`artifacts/audit-retention/net6-final/audit-retention-net6-final.trx`、`artifacts/audit-retention/net8-final/audit-retention-net8-final.trx` |
| `AuditingPersistenceExtensions.AddBingAuditLogRetention<TAuthorizer>`：授权服务按 Scoped 幂等注册并尊重预先注册的授权器 | `AuditLogRetentionServiceTest.AddBingAuditLogRetention_ShouldKeepExistingAuthorizer` | 同上 |

该映射只覆盖框架级授权委托和注册边界；身份、租户策略、冷归档、管理端点及生产 MySQL 权限验收仍由宿主交付。

### 审计查询授权入口（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `AuditLogQueryService.QueryAsync`：先校验查询、显式租户和取消状态，再执行授权，授权成功后才访问低层查询存储 | `AuditLogQueryServiceTest.QueryAsync_ShouldAuthorizeBeforeStore`、`QueryAsync_WhenAuthorizationFails_ShouldNotQuery`、`QueryAsync_WhenInvalidOrCancelled_ShouldNotCallDependencies` | `Bing.Auditing.Tests` net6/net8 各 4/4；`artifacts/audit-query-service/net6-final/audit-query-service-net6-final.trx`、`artifacts/audit-query-service/net8-final/audit-query-service-net8-final.trx` |
| `AuditingPersistenceExtensions.AddBingAuditLogQuery<TAuthorizer>`：查询服务按 Scoped 幂等注册并尊重预先注册的授权器 | `AuditLogQueryServiceTest.AddBingAuditLogQuery_ShouldKeepExistingAuthorizer` | 同上 |

该映射只覆盖框架级授权委托和注册边界；宿主身份策略、明细脱敏、管理端点、冷归档和生产 MySQL 权限验收仍需单独完成。

审计持久化最终回归：`Bing.Auditing.Tests` net6.0、net8.0 各 170 通过、0 失败、0 跳过；证据为 `artifacts/audit-persistence-final-v2/net6/auditing-full-final-net6.trx` 和 `artifacts/audit-persistence-final-v2/net8/auditing-full-final-net8.trx`。该全量结果不包含外部 MySQL/CAP、宿主身份策略和冷归档验收。

### CAP 有序入箱适配器（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `CapOrderedEventIngress.ReceiveAsync`：保留 CAP 消息头和原始正文，等待有序收件箱事务提交后返回 | `CapOrderedEventIngressTest.ReceiveAsync_ShouldPersistCapMetadataAndPayload` | `Bing.EventBus.Tests` net6/net8 各 3/3；`artifacts/cap-ordered-ingress/net6-final/cap-ordered-ingress-net6-final.trx`、`artifacts/cap-ordered-ingress/net8-final/cap-ordered-ingress-net8-final.trx` |
| `CapOrderedEventIngress.ReceiveAsync`：空正文在打开入箱事务前拒绝，避免无效消息产生状态 | `CapOrderedEventIngressTest.ReceiveAsync_WhenPayloadIsNull_ShouldRejectInput` | 同上 |
| `AddBingCapOrderedEventIngress` 及 `AddBingOrderedConsumer` 的适配器注册：重复注册不覆盖已有实现 | `CapOrderedEventIngressTest.AddBingCapOrderedEventIngress_ShouldRegisterOnce` | 同上 |

该适配器只完成 CAP 到有序收件箱的显式接线；主题、CAP 组名、生产 `IOrderedEventHandler`、多租户业务授权和 RabbitMQ 网络验收仍需宿主交付。

Admin 两套 CAP 基础设施在 `OrderedEvents:Enabled` 下注册 `CapOrderedEventIngress` 的直接验证：`AdminCapRegistrationTest.AddServices_WhenOrderedEventsEnabled_ShouldRegisterFoundation`，EFCore 与 FreeSQL net6.0 各 14/14，证据为 `artifacts/admin-cap-ingress/efcore/admin-cap-ingress-efcore-net6.trx` 和 `artifacts/admin-cap-ingress/freesql/admin-cap-ingress-freesql-net6.trx`。
### Admin 库存有序生产接线（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `AdminInventoryService`：在同一库存事务中保留旧完成主题并可选发布 `CompletedOrderedTopic` | `AdminInventoryOrderedEventHandlerTest.HandleAsync_WhenEventMatchesStream_ShouldSaveUsingSameTransaction`；生产发布事务仍需门控 MySQL/CAP | `Bing.Admin.FreeSQL.Tests` 共享测试 net6.0 定向集 19/19；EFCore 被本机离线 EFCore/Pomelo 包缺失阻断 |
| `AdminInventoryOrderedEventSubscriber`：固定主题和 CAP 组将正文送入 `CapOrderedEventIngress` | `AdminCapRegistrationTest.AddServices_WhenConsumerRoleAndInventoryAreEnabled_ShouldRegisterInventoryConsumer` | `Bing.Admin.FreeSQL.Tests` net6.0 定向集；EFCore 同名测试待依赖恢复后运行 |
| `AdminInventoryOrderedEventHandler`：校验业务流、契约版本、租户/订单身份，并使用固定逻辑消费者写入投影 | `AdminInventoryOrderedEventHandlerTest.HandleAsync_WhenEventMatchesStream_ShouldSaveUsingSameTransaction`、`HandleAsync_WhenStreamIdentityDoesNotMatch_ShouldRejectBeforeSaving`、`HandleAsync_WhenContractOrStatusIsInvalid_ShouldRejectBeforeSaving` | `Bing.Admin.FreeSQL.Tests` net6.0 定向集；处理器单元证据不替代真实 CAP 消费 |
| `DbInventoryOrderProjectionStore` 与 `InventoryOrderSchema`：投影与有序事务使用同一连接事务，事件 ID 幂等约束 | `InventoryOrderProjectionStoreTest.SaveAsync_ShouldUseCallerTransactionAndPreservePayload`、`InventoryOrderSchemaTest.GetCreateStatements_WhenMySql_ShouldMatchCompleteSql`、`GetCreateStatements_WhenSqlite_ShouldMatchCompleteSql` | `Bing.EventBus.Tests` net6/net8 定向集各 17/17 通过；MySQL DDL 和重复投影仍需门控环境验证 |

该批只证明 Admin 生产源码已经具备显式主题、固定组名、稳定消费者和投影处理器接线；真实 MySQL/CAP/RabbitMQ 的事务提交、重投、跨节点竞争、恢复授权和投影清理仍属于 FIX-006 的未完成验收。

### Admin 库存双主题事务直接验证（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `AdminInventoryService.ExecuteAsync`：库存事务回调中同时发布兼容完成主题和可选有序主题，且序号分配、CAP 发布使用同一 `DbTransaction` | `AdminInventoryOrderedEventPublishingTest.ExecuteAsync_WhenOrderedPublisherIsPresent_ShouldPublishBothTopicsInOneTransaction` | `Bing.Admin.FreeSQL.Tests` net6.0 定向集 20/20；`modules/admin/tests/Bing.Admin.FreeSQL.Tests/TestResults/admin-ordered-publishing-freesql-net6-single.trx` |

该测试固定了应用服务的事务身份和消息元数据，不等同于真实 MySQL/CAP/RabbitMQ 网络提交、重投、跨节点竞争或恢复验收。EFCore 共享测试因本机离线 EFCore/Pomelo 包缺失暂未执行。

### Admin 有序库存正文大小预检（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `AdminInventoryOrderedEventHandler.HandleAsync`：在解析正文前拒绝超过 65536 UTF-8 字节的库存消息，避免超大外部正文先进入 JSON 解析 | `AdminInventoryOrderedEventHandlerTest.HandleAsync_WhenPayloadExceedsLimit_ShouldRejectBeforeParsingOrSaving` | `Bing.Admin.FreeSQL.Tests` net6.0 有序定向集 21/21；`modules/admin/tests/Bing.Admin.FreeSQL.Tests/TestResults/admin-ordered-publishing-freesql-net6-single.trx` |

该边界与投影存储的 65536 字节限制一致，只覆盖 Admin 库存处理器，不代表通用有序事件或 RabbitMQ 代理的消息大小限制已完成部署验收。
### Admin 有序库存投影状态白名单（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `DbInventoryOrderProjectionStore.SaveAsync`：拒绝处理器不会产生的 `Pending` 或未知库存状态，并在写入前结束 | `InventoryOrderProjectionStoreTest.SaveAsync_WhenStatusIsUnsupported_ShouldRejectBeforeWriting` | `Bing.EventBus.Tests` net6/net8 各 3/3；`framework/tests/Bing.EventBus.Tests/TestResults/inventory-projection-status-net6-final.trx`、`inventory-projection-status-net8-final.trx` |

该映射只覆盖投影存储的输入完整性边界；`AdminInventoryOrderedEventHandler` 的业务身份校验和真实 MySQL/CAP/RabbitMQ 消费验收仍需分别验证。

### 有序消费者身份边界（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `OrderedEventConsumerExtensions.AddBingOrderedConsumer`：注册前拒绝首尾带空白的消费者名称 | `OrderedEventConsumerTest.AddBingOrderedConsumer_WhenConsumerHasBoundaryWhitespace_ShouldReject` | `Bing.EventBus.Tests` net6/net8 有序消费者职责集各 27/27；`framework/tests/Bing.EventBus.Tests/TestResults/ordered-consumer-identity-net6.trx`、`ordered-consumer-identity-net8.trx` |
| `CapOrderedEventIngress.ReceiveAsync`：在开启入箱事务前拒绝首尾带空白的消费者名称 | `CapOrderedEventIngressTest.ReceiveAsync_WhenConsumerHasBoundaryWhitespace_ShouldRejectBeforeOpeningTransaction` | 同上 |
| `DbOrderedEventStore.EnqueueAsync`：存储层拒绝首尾带空白的消费者名称并保持数据库无写入 | `DbOrderedEventStoreTest.EnqueueAsync_WhenConsumerHasBoundaryWhitespace_ShouldRejectBeforeWriting` | 同上 |

该映射只覆盖逻辑消费者身份的输入一致性；真实 CAP/RabbitMQ 消费组切换、MySQL 排序规则和跨节点部署仍需环境验收。

### 恢复审计消费者身份边界（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `OrderedEventRecoveryAuditEntry`：公共审计记录构造复用消费者身份校验，拒绝首尾带空白的名称 | `OrderedEventRecoveryServiceTest.RecoveryAuditEntry_WhenConsumerHasBoundaryWhitespace_ShouldReject` | `Bing.EventBus.Tests` net6/net8 各 7/7；`framework/tests/Bing.EventBus.Tests/TestResults/ordered-recovery-identity-net6.trx`、`ordered-recovery-identity-net8.trx` |

该映射只覆盖恢复审计对象的本地输入边界；授权策略、审计持久化和真实 MySQL/CAP/RabbitMQ 环境仍需分别验收。

### Admin 恢复消费者精确授权（2026-10-06）

| 最终生产符号及关键行为 | 直接测试方法 | 测试项目/证据 |
| --- | --- | --- |
| `AdminOrderedEventRecoveryAuthorizer`：消费者白名单按 `Ordinal` 精确大小写匹配，并拒绝边界空白配置 | `AdminOrderedEventRecoveryTest.Authorizer_ShouldRejectConsumerWithDifferentCasing`、`Authorizer_WhenConsumerConfigurationHasBoundaryWhitespace_ShouldReject` | `Bing.Admin.FreeSQL.Tests` net6.0 5/5；`modules/admin/tests/Bing.Admin.FreeSQL.Tests/TestResults/admin-recovery-identity-freesql-net6.trx` |

EFCore Admin 同职责集因离线缺少 EFCore/Pomelo 包未构建；该映射不覆盖外部身份、授权服务和真实 MySQL/CAP/RabbitMQ 环境。

### EventBus 有序身份完整回归（2026-10-06）

`Bing.EventBus.Tests` net6.0、net8.0 各 283/283 通过、0 失败、0 跳过；证据为 `framework/tests/Bing.EventBus.Tests/TestResults/eventbus-recovery-identity-full-net6.trx` 和 `eventbus-recovery-identity-full-net8.trx`。该全量回归不覆盖 EFCore Admin 离线依赖、锁定 SDK 8.0.424 或真实 MySQL/CAP/RabbitMQ 多节点部署。

### 有序协调器组合清理故障（2026-10-06）

| 生产符号与关键行为 | 测试项目与直接方法 | 证据 |
| --- | --- | --- |
| `OrderedEventCoordinator.RunTransactionAsync`：业务异常、失败标记提交失败、回滚失败和释放失败同时发生时，保留原始异常、未知事务结果和两类清理异常 | `OrderedEventAtomicFailureTest.ProcessNextAsync_WhenBusinessRollbackAndDisposeFail_ShouldPreserveOriginalAndCleanupErrors` | `Bing.EventBus.Tests` net6/net8 各 7/7；`ordered-combined-cleanup-net6-final.trx`、`ordered-combined-cleanup-net8-final.trx` |

该映射只覆盖协调器级本地异常聚合和资源调用次数；出箱已写后强杀、CAP 异步发送完成和生产在途资源排空仍需专用环境验收。

### EFCore 审计值格式化容错（2026-10-06）

| 生产符号与关键行为 | 测试项目与直接方法 | 证据 |
| --- | --- | --- |
| `UnitOfWorkBase.CaptureAuditChanges`：值读取或格式化失败时跳过对应审计值，正常属性和业务保存继续 | `EfCoreAuditPropertySetterIntegrationTest.SaveChangesAsync_WhenAuditValueFormattingThrows_ShouldKeepBusinessSaveSuccessful` | `Bing.EntityFrameworkCore.Tests` SQLite 定向测试；本机因离线缺少 EFCore/Pomelo 包和 NuGet 源不可达未执行 |
| `UnitOfWorkBase.GetAuditEntityId`：主键格式化失败时不阻断业务保存并记录有限上下文 | 同上（测试实体使用正常生成主键；值对象属性覆盖格式化异常路径） | 同上 |

该映射只覆盖 EFCore 内存审计快照的 best-effort 边界；数据库/事务/审计存储异常仍应由现有保存与清理测试验证，生产 MySQL、冷归档、授权和长期容量仍未完成。

### 有序消费者全局待处理流指标（2026-10-06）

| 生产符号与关键行为 | 测试项目与直接方法 | 证据 |
| --- | --- | --- |
| `DbOrderedEventStore.GetPendingStreamCountAsync`：按消费者统计仍有未完成事件的不同业务流 | `DbOrderedEventStoreTest.GetPendingEventCountAsync_ShouldCountAllUnprocessedEvents` | `Bing.EventBus.Tests` net6/net8 各 35/35 定向职责集；`ordered-global-stream-net6-final.trx`、`ordered-global-stream-net8-final.trx` |
| `OrderedEventCoordinator.GetPendingStreamCountAsync`：支持可选统计契约并在旧存储中返回空值 | `OrderedEventCoordinatorTest.GetPendingStreamCountAsync_ShouldCountAllPendingStreams` | 同上 |
| `OrderedEventConsumerExtensions.ConsumerWorker`：分页扫描时优先记录全局流数，统计失败保留上一份快照 | `OrderedEventConsumerTest.HostedWorker_WhenPendingStreamsExceedBatch_ShouldRecordGlobalCount`、既有 `HostedWorker_WhenStatisticsScansFail_ShouldRecordScanFailures` | 同上 |

自定义存储未实现新契约时仍使用分页回退；exporter、告警、归档和长期容量测量不在本批范围内。

### 有序消费者停止排空诊断（2026-10-06）

| 生产符号与关键行为 | 测试项目与直接方法 | 证据 |
| --- | --- | --- |
| `OrderedEventConsumerExtensions.ConsumerWorker.StopAsync`：处理器忽略停止令牌时报告未完成排空，保留活动作用域；释放后可再次等待退出；未启动时停止不误报 | `OrderedEventConsumerTest.HostedWorker_WhenHandlerIgnoresCancellation_ShouldReportIncompleteDrainAndWaitLater`、`HostedWorker_WhenStoppedBeforeStart_ShouldNotReportIncompleteDrain` | `Bing.EventBus.Tests` net6/net8 各 2/2；`ordered-consumer-drain-net6-final.trx`、`ordered-consumer-drain-net8-final.trx`、`ordered-consumer-stop-before-start-net6-final.trx`、`ordered-consumer-stop-before-start-net8-final.trx` |

该测试验证消费者自身的停止协议和未确认事件保留，不覆盖 CAP、Hangfire、HTTP 长连接或多进程蓝绿排空。

### EventBus 有序消费者停止回归（2026-10-06）

`Bing.EventBus.Tests` net6.0、net8.0 各 288/288 通过；证据为 `eventbus-consumer-drain-full-net6.trx` 和 `eventbus-consumer-drain-full-net8.trx`。全量回归包含有序消费者停止诊断、未启动停止和扫描失败快照稳定性测试；不覆盖真实 CAP/Hangfire 在途资源和部署切流。

### 本地事件类型键回收（2026-10-07）

| 生产符号与关键行为 | 测试项目与直接方法 | 证据 |
| --- | --- | --- |
| `LocalEventBus`：取消最后一个订阅或清理未订阅类型后删除空处理器列表；发布期间使用稳定快照 | `LocalEventBusConcurrencyTest.PublishAsync_ConcurrentSubscriptionChanges_UsesInitialSnapshot`、`UnsubscribeLastHandler_ShouldRemoveEmptyEventTypeEntry` | `Bing.EventBus.Tests` net6/net8 各 2/2；`local-event-bus-memory-net6.trx`、`local-event-bus-memory-net8.trx` |

EventBus 全量回归 net6/net8 各 289/289；该映射验证进程内字典保留和并发快照，不代表跨节点事件或 CAP 消费排空。

### CAP 有序事务适配器注册冲突（2026-10-07）

| 生产符号与关键行为 | 测试项目与直接方法 | 证据 |
| --- | --- | --- |
| `AddBingCapOrderedEventTransaction<TTransactionFactory>`：同一底层工厂幂等，不同底层工厂在注册阶段拒绝且不产生部分服务描述 | `CapOrderedEventTransactionRegistrationTest.AddBingCapOrderedEventTransaction_WhenCalledTwice_ShouldRegisterOnce`、`AddBingCapOrderedEventTransaction_WhenDifferentFactoryIsRegistered_ShouldRejectWithoutPartialRegistration` | `Bing.EventBus.Tests` net6/net8 各 3/3；`artifacts/cap-ordered-registration/net6/cap-ordered-registration-net6.trx`、`artifacts/cap-ordered-registration/net8/cap-ordered-registration-net8.trx` |

该映射验证服务集合注册一致性，不代表底层 ORM 事务、CAP Flush、RabbitMQ 确认或多节点竞争已经完成环境验收。

### 蓝绿消费者就绪预检（2026-10-07）

| 生产符号与关键行为 | 直接验证 | 证据 |
| --- | --- | --- |
| `deploy/bluegreen/Test-BlueGreen.ps1` 静态模式：校验 Compose/Kubernetes 消费者角色、启用开关、稳定组名及 Web 就绪配置 | `& .\\deploy\\bluegreen\\Test-BlueGreen.ps1 -Platform Compose` | 11/11 静态断言通过；命令输出未落盘，执行记录为 `execution.md` 同名章节 |
| `Test-BlueGreen.ps1 -RequireConsumer` Compose 分支：要求蓝/绿消费者服务至少一个运行 | 受控 `docker compose ps --services --filter status=running` 路径 | 未执行；需要真实 Compose 镜像和容器 |
| `Test-BlueGreen.ps1 -RequireConsumer` Kubernetes 分支：要求蓝/绿消费者就绪副本合计至少为 1 | 受控 `kubectl get deployment ... .status.readyReplicas` 路径 | 未执行；需要真实 Kubernetes 命名空间和镜像 |

该映射只覆盖消费者就绪门槛和静态清单边界，不覆盖在途消息排空、CAP 确认、蓝绿切流、回滚或多节点单次提交。

### CAP 消费组配置边界（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `Bing.Admin.Modules.CapModule`：Consumer 角色拒绝显式空白或非稳定 `BING_CAP_CONSUMER_GROUP`，未配置时保留默认稳定约定 | `AdminCapRegistrationTest.AddServices_WhenConsumerGroupIsBlankOrMismatched_ShouldRejectWithoutExposingValues` | `Bing.Admin.FreeSQL.Tests` net6.0 18/18；`modules/admin/tests/Bing.Admin.FreeSQL.Tests/TestResults/admin-cap-registration-group-freesql-net6-final.trx` |
| `Bing.Admin.FreeSQL.Modules.CapModule`：同上，且错误不泄露 RabbitMQ 密码 | 同上 | 同上 |

`Bing.Admin.EFCore.Tests` 中的同名方法已编译到测试项目，但本次执行被 `Bing.EntityFrameworkCore` / `Bing.EntityFrameworkCore.MySql` 的 `NU1301` 阻断，未形成通过证据。

该校验不改变 CAP 订阅特性中的具体组名，也不证明真实 RabbitMQ 多节点切换或消费排空。

### 有序消费者指标快照提供程序（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `BingOrderedEventMetricsSnapshotProvider`：按 `Ordinal` 稳定排序，复制来源并返回只读快照，不暴露消息正文或高基数标识 | `OrderedEventMetricsTest.SnapshotProvider_ShouldReturnStableReadOnlySnapshots` | `Bing.EventBus.Tests` net6/net8 各 12/12；`ordered-metrics-provider-net6-final-v3.trx`、`ordered-metrics-provider-net8-final-v3-retry.trx` |
| `BingOrderedEventMetricsSnapshotProvider`：重复消费者或来源与快照身份不一致时拒绝，避免诊断数据静默覆盖 | `SnapshotProvider_WhenConsumerNamesDuplicate_ShouldReject`、`SnapshotProvider_WhenSnapshotIdentityDiffers_ShouldReject` | 同上 |
| `AddBingOrderedEventMetricsDiagnostics`：默认注册幂等，尊重调用方自定义提供程序；无消费者时返回空集合 | `AddDiagnostics_ShouldKeepExistingProvider`、`AddDiagnostics_WhenNoConsumerIsRegistered_ShouldReturnEmptySnapshot` | 同上 |

该入口只提供进程内只读诊断契约，不包含 HTTP 认证/授权、告警节流、OpenTelemetry/Prometheus exporter、消息归档或长期容量测量；这些仍需宿主和专用环境验收。

`Bing.EventBus.Tests` 全量回归 net6.0、net8.0 各 296/296 通过；证据为 `eventbus-metrics-provider-full-net6-final.trx` 和 `eventbus-metrics-provider-full-net8-final.trx`。

### 租户范围审计保留（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `AuditLogTenantRetentionService.DeleteBeforeAsync`：统一校验租户身份、先授权后存储，并传播调用方取消 | `AuditLogTenantRetentionServiceTest.DeleteBeforeAsync_ShouldAuthorizeTenantBeforeStore`、`DeleteBeforeAsync_WhenTenantIsMissing_ShouldRejectBeforeDependencies`、`DeleteBeforeAsync_WhenCancelledBeforeAuthorization_ShouldNotCallDependencies`、`DeleteBeforeAsync_WhenAuthorizationFails_ShouldNotCallStore`、`DeleteBeforeAsync_WhenTenantHasUnpairedSurrogate_ShouldRejectBeforeAuthorization` | 修正定向集 net6/net8 各 11/11；`audit-tenant-retention-net6-corrected.trx`、`audit-tenant-retention-net8-corrected.trx` |
| `DbAuditingStore.DeleteBeforeAsync(string, ...)`：参数化租户条件按原值只删除目标租户的过期普通审计，保留其他租户、关键审计和新记录 | `DbAuditingStoreTest.DeleteBeforeAsync_WhenTenantIsSpecified_ShouldNotDeleteOtherTenants`、`DeleteBeforeAsync_WhenTenantHasBoundaryWhitespace_ShouldDeleteExactTenantOnly` | 同上；覆盖首尾空格精确匹配 |
| `AuditLogSchema.GetCreateStatements`：建表语句包含租户清理复合索引 | `AuditLogSchemaTest.GetCreateStatements_WhenSqlite_ShouldMatchCompleteStatement`、`GetCreateStatements_WhenMySql_ShouldMatchCompleteStatement` | 同上；MySQL/SQLite 字符串均完整断言 |
| `DbAuditingStore` 的 MySQL 租户清理：使用完整删除语句和严格 UTF-8 二进制租户参数 | `DbAuditingStoreMySqlCommandTest.DeleteBeforeAsync_WhenMySql_ShouldUseTenantSqlAndBinaryParameter` | net6/net8 各 1/1；`audit-mysql-command-net6.trx`、`audit-mysql-command-net8-final.trx`；不替代真实 MySQL 执行 |
| `AddBingAuditLogTenantRetention<TAuthorizer>`、`AddBingDatabaseAuditing`：租户服务注册幂等，数据库存储同时提供租户保留实现 | `AuditLogTenantRetentionServiceTest.AddBingAuditLogTenantRetention_ShouldKeepExistingAuthorizer`、`AuditingPersistenceExtensionsTest.AddBingDatabaseAuditing_ShouldReplaceDefaultStoreAndShareInstance` | 同上 |

`Bing.Auditing.Tests` 最终完整回归 net6.0、net8.0 各 179/179；证据为 `auditing-tenant-retention-full-net6-final-v3.trx` 和 `auditing-tenant-retention-full-net8-final-v3.trx`。SQLite 行为、MySQL 删除 SQL/参数构造和 Schema 字符串已验证；真实 MySQL 执行、冷归档、HTTP 权限端点和跨节点清理协调仍未验收。

### Prometheus 指标文本导出（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `BingOrderedEventMetricsPrometheusExporter.Export`：按 `Ordinal` 稳定排序快照并输出固定低基数 `consumer` 标签 | `OrderedEventMetricsTest.PrometheusExporter_ShouldWriteStableLowCardinalityText` | `Bing.EventBus.Tests` net6/net8 各 18/18；`ordered-metrics-prometheus-boundary-net6.trx`、`ordered-metrics-prometheus-boundary-net8.trx` |
| 导出标签特殊字符转义、可选年龄指标及 Prometheus 类型声明 | 同上 | 同上 |
| 重复消费者或 null/非法快照在导出前拒绝 | `OrderedEventMetricsTest.PrometheusExporter_WhenProviderReturnsDuplicateNames_ShouldReject`、`PrometheusExporter_WhenProviderReturnsNull_ShouldReject`、`PrometheusExporter_WhenProviderReturnsNullSnapshot_ShouldReject`、`PrometheusExporter_WhenConsumerNameIsInvalid_ShouldReject` | 同上 |

该导出器不负责 HTTP 端点、认证授权、告警、归档或长期容量测量；`Bing.EventBus.Tests` 完整回归 net6/net8 各 302/302，证据为 `eventbus-metrics-exporter-full-net6-final.trx` 和 `eventbus-metrics-exporter-full-net8-final.trx`。实际 SDK 为 8.0.425，锁定 8.0.424、真实多节点监控端点和生产抓取仍未验证。

### 有序消费者指标告警判定（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `BingOrderedEventAlertRule`：校验规则名称、阈值、年龄及冷却参数 | `OrderedEventAlertingTest.ConstructorAndEvaluate_WhenInputIsInvalid_ShouldReject` | `Bing.EventBus.Tests` net6/net8 各 7/7；`ordered-event-alerting-net6-final.trx`、`ordered-event-alerting-net8-final.trx` |
| `BingOrderedEventMetricsAlertEvaluator`：待处理数量和最老年龄达到阈值时告警，恢复后允许再次告警 | `Evaluate_WhenPendingEventsStayAboveThreshold_ShouldThrottleUntilCooldown`、`Evaluate_WhenPendingEventsRecover_ShouldAllowNextAlert`、`Evaluate_WhenOldestAgeIsMissing_ShouldNotAlert` | 同上 |
| `BingOrderedEventMetricsAlertEvaluator`：扫描失败按累计值增量触发，并按消费者/规则独立冷却 | `Evaluate_WhenScanFailuresIncrease_ShouldUseDeltaAndCooldown`、`Evaluate_WhenConsumersAndRulesDiffer_ShouldThrottleIndependently` | 同上 |

该评估器只保存进程内冷却状态并返回告警结果，不包含通知发送、HTTP 认证授权、跨节点去重、长期归档或容量/GC 测量。`Bing.EventBus.Tests` 完整回归 net6/net8 各 309/309，证据为 `eventbus-alerting-full-net6.trx` 和 `eventbus-alerting-full-net8.trx`；实际 SDK 为 8.0.425，锁定 8.0.424 和真实集群告警链路仍未验证。

### Admin 有序指标受保护端点（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `OrderedEventMetricsController`：诊断未注册时返回 404，授权成功时只输出 Prometheus 低基数文本 | `AdminOrderedEventMetricsTest.GetAsync_WhenDiagnosticsAreDisabled_ShouldReturnNotFound`、`GetAsync_WhenAuthorized_ShouldReturnPrometheusText` | `Bing.Admin.FreeSQL.Tests` net6.0 4/4（包含控制器和授权边界）；`artifacts/admin-metrics-freesql-net6-final` |
| `AdminOrderedEventMetricsAuthorizer`：未认证或缺少配置角色时拒绝读取 | `AdminOrderedEventMetricsTest.GetAsync_WhenUserIsNotInConfiguredRole_ShouldReject`、`Authorizer_WhenRoleIsMissing_ShouldRejectConfiguration` | 同上 |
| 两套 Admin `CapModule`：指标端点仅允许 Consumer 角色；Web 角色配置会在注册阶段失败 | `AdminOrderedEventMetricsTest.CapModule_WhenMetricsAreEnabledOnWebRole_ShouldReject`、`CapModule_WhenMetricsAreEnabledOnConsumerRole_ShouldRegisterAdapters` | FreeSQL net6.0 该职责集通过；EFCore 因 `Bing.EntityFrameworkCore` / `Bing.EntityFrameworkCore.MySql` 的 `NU1301` 未形成通过证据 |

端点仍需由宿主的认证中间件和网关 ACL 保护；本地测试不替代真实多节点抓取、告警聚合、部署切流或锁定 SDK 8.0.424 验证。
### Admin 有序指标受保护端点结果更正（2026-10-07）

上一条映射中的 FreeSQL 4/4 为补充注册测试前的中间结果。最终 `AdminOrderedEventMetricsTest` 在 `Bing.Admin.FreeSQL.Tests` net6.0 为 6/6，通过证据为 `artifacts/admin-metrics-freesql-net6-final`；EFCore 对应职责集受 `Bing.EntityFrameworkCore` / `Bing.EntityFrameworkCore.MySql` 的 NU1301 还原阻断，未形成通过证据。

生产构建证据：`Bing.Admin.FreeSQL` net6.0 构建 0 错误；`Bing.Admin` EFCore 构建在 `Bing.EntityFrameworkCore` / `Bing.EntityFrameworkCore.MySql` 还原阶段受 NU1301 阻断。两者均使用 SDK 8.0.425，锁定 SDK 8.0.424 未验证。

### Admin 审计元数据查询入口（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `AdminAuditLogQueryAuthorizer`：独立角色和默认当前租户匹配，跨租户需显式配置 | `AdminAuditLogQueryTest.Authorizer_WhenUserIsNotAuthorized_ShouldReject`、`Authorizer_WhenTenantDiffers_ShouldReject`、`Authorizer_WhenTenantMatches_ShouldAllow`、`Authorizer_WhenCrossTenantIsEnabled_ShouldAllowGlobalQuery` | `Bing.Admin.FreeSQL.Tests` net6.0 7/7；`artifacts/admin-audit-query-freesql-net6-final-v2` |
| `AuditLogController`：查询未启用返回 404，启用后调用高层查询服务 | `AdminAuditLogQueryTest.QueryAsync_WhenQueryIsDisabled_ShouldReturnNotFound`、`QueryAsync_WhenQueryIsEnabled_ShouldCallService` | 同上 |

EFCore Admin 对应职责集未形成通过证据，原因是 `Bing.EntityFrameworkCore` / `Bing.EntityFrameworkCore.MySql` 还原时的 NU1301；真实 MySQL、网关 ACL、归档和跨节点授权仍需环境验收。

### Admin 审计查询结果更正（2026-10-07）

上一条映射中的 FreeSQL 7/7 为补充安全边界测试前的中间结果。最终 `AdminAuditLogQueryTest` 在 `Bing.Admin.FreeSQL.Tests` net6.0 为 12/12，通过证据为 `artifacts/admin-audit-query-freesql-net6-final-v4`。

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `AdminAuditLogQueryAuthorizer`：拒绝空租户全局查询，跨租户要求独立角色 | `Authorizer_WhenTenantIsEmpty_ShouldRejectGlobalQuery`、`Authorizer_WhenCrossTenantRoleIsMissing_ShouldRejectConfiguration`、`Authorizer_WhenCrossTenantIsEnabled_ShouldAllowOtherTenant` | `Bing.Admin.FreeSQL.Tests` net6.0 12/12；`artifacts/admin-audit-query-freesql-net6-final-v4` |
| `AppModule`：查询默认关闭，数据库未启用时拒绝查询注册 | `AppModule_WhenQueryIsDisabled_ShouldNotRegisterQueryService`、`AppModule_WhenQueryIsEnabledWithoutDatabase_ShouldReject` | 同上 |

EFCore 对应职责集仍受 `Bing.EntityFrameworkCore` / `Bing.EntityFrameworkCore.MySql` 的 NU1301 还原阻断；真实 MySQL 查询、认证网关 ACL、跨租户审批、归档和长期容量仍需环境验收。

### 审计查询高层校验（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `AuditLogQueryValidator`：在授权和存储前拒绝空白租户、超限分页和逆序 UTC 时间 | `AuditLogQueryServiceTest.QueryAsync_WhenInvalidOrCancelled_ShouldNotCallDependencies`、`QueryAsync_WhenQueryBoundsAreInvalid_ShouldNotCallDependencies` | `Bing.Auditing.Tests` net6/net8 各 180/180；`artifacts/audit-query-validation-net6`、`artifacts/audit-query-validation-net8` |
| `AuditLogQueryService` 与 `DbAuditingStore` 共享查询边界；空字符串仍表示非租户审计范围 | `AuditLogQueryServiceTest.QueryAsync_ShouldAuthorizeBeforeStore`、`DbAuditingStoreTest.QueryAsync_ShouldFilterByTenantAndReturnStableMetadata`、`QueryAsync_WhenArgumentsAreInvalid_ShouldRejectBeforeOpeningConnection` | 同上 |
| `AuditLogController`：空白租户返回 400，不调用查询服务 | `AdminAuditLogQueryTest.QueryAsync_WhenTenantIsWhitespace_ShouldReturnBadRequest` | `Bing.Admin.FreeSQL.Tests` net6.0 13/13；`artifacts/admin-audit-query-freesql-net6-final-v5` |

该校验只约束查询输入，不替代宿主认证、租户授权、数据库权限、审计归档和真实 MySQL/EFCore 查询验收。

### 审计查询注册边界结果更正（2026-10-07）

补充 `AppModule_WhenCrossTenantRoleIsMissing_ShouldReject` 后，`Bing.Admin.FreeSQL.Tests` net6.0 的 `AdminAuditLogQueryTest` 最终为 14/14，通过证据为 `artifacts/admin-audit-query-freesql-net6-final-v6`。该测试固定跨租户配置缺少独立角色时在模块注册阶段失败，避免配置错误推迟到请求期才暴露。

审计高层校验补充超长文本条件后，`Bing.Auditing.Tests` net6/net8 重新执行仍各 180/180 通过，最终证据为 `artifacts/audit-query-validation-net6-final` 和 `artifacts/audit-query-validation-net8-final`。

### Admin CAP Dashboard 默认拒绝与角色授权（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `Bing.Admin.Modules.CapModule`：Web 角色显式启用 CAP Dashboard 时绑定授权策略，默认关闭及非 Web 角色不注册 | `AdminCapRegistrationTest.AddServices_ShouldUseConfiguredRabbitAndDisableDashboardByDefault`、`AddServices_WhenDashboardIsExplicitlyEnabled_ShouldRegisterConfiguredTransport`、`AddServices_WhenDashboardRoleIsMissing_ShouldDenyAuthenticatedUser`、`AddServices_WhenDashboardIsEnabledForNonWebRole_ShouldNotRegisterDashboard` | `Bing.Admin.EFCore.Tests` net6.0 22/22；`artifacts/admin-cap-dashboard-auth-efcore-net6-v2/admin-cap-dashboard-auth-efcore-net6-v2.trx` |
| `Bing.Admin.FreeSQL.Modules.CapModule`：FreeSQL CAP 7.1.4 使用相同 Dashboard 授权配置 | 同名 `AdminCapRegistrationTest` 四个 Dashboard 注册边界用例 | `Bing.Admin.FreeSQL.Tests` net6.0 22/22；`artifacts/admin-cap-dashboard-auth-freesql-net6-v2/admin-cap-dashboard-auth-freesql-net6-v2.trx` |
| `AdminRoleAuthorization.HasRole`：要求认证身份并按 `role_code`/标准角色声明做逗号拆分后的不区分大小写完整匹配 | `AdminCapRegistrationTest.AddServices_WhenDashboardRoleIsConfigured_ShouldRequireMatchingRole`；与 `AdminHangfireRegistrationTest` 的匿名、缺失角色和匹配角色用例 | 同上；Hangfire 证据为 `artifacts/admin-hangfire-role-auth-net6/admin-hangfire-role-auth-net6.trx` |

该映射只证明服务注册和授权策略判定，不替代真实认证中间件、反向代理 ACL、CAP/RabbitMQ 多节点消费或蓝绿在线切换验证。实际 SDK 为 8.0.425，仓库锁定的 8.0.424 未验证。

### Exceptionless 凭据配置化（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `Bing.Admin.Modules.LogModule`、`Bing.Admin.FreeSQL.Modules.LogModule`：空 API Key 不注册远程客户端，有效配置注册客户端并传递 API Key/ServerUrl | `AdminLogConfigurationTest.AddServices_WithoutExceptionlessApiKey_ShouldKeepRemoteSinkDisabled`、`AddServices_WithExceptionlessApiKey_ShouldRegisterConfiguredRemoteClient` | EFCore Admin net6.0 2/2：`artifacts/admin-log-config-efcore/admin-log-config-efcore.trx`；FreeSQL Admin net6.0 2/2：`artifacts/admin-log-config-freesql-v2/admin-log-config-freesql-v2.trx` |
| `Bing.Samples.Hangfire.Modules.LogModule`：读取同一配置节并在缺少 Key 时保留本地日志 | Sample 生产构建 | `dotnet build samples/Bing.Samples.Hangfire/Bing.Samples.Hangfire.csproj --no-restore`，SDK 8.0.425，0 错误 |

该映射证明配置分支和编译兼容性，不证明真实 Exceptionless 远程投递、密钥轮换、HTTPS、外部服务可用性或其他数据库/Redis/MQ/JWT 凭据已经处理。

### Admin 示例凭据清空（2026-10-07）

| 生产配置与关键行为 | 直接验证 | 证据 |
| --- | --- | --- |
| `Bing.Admin/appsettings.json`、`Bing.Admin.FreeSQL/appsettings.json`：数据库、Redis、JWT 配置只保留空键，不携带共享凭据 | PowerShell `ConvertFrom-Json` UTF-8 解析；`rg` 扫描旧 MySQL 密码、JWT Secret 与 Exceptionless 内网端点 | 两个 JSON 均输出 `JSON OK`；扫描无匹配 |
| 两套 `CapModule.ReadRabbitConfiguration`：RabbitMQ 从 `CAP:RabbitMQ` 配置读取，缺少配置时 fail-fast | `AdminCapRegistrationTest` 的 RabbitMQ 配置注册与缺失配置边界 | EFCore/FreeSQL CAP 定向职责集各 22/22；对应 TRX 见上节 |

该验证只证明仓库当前文件不携带本轮清除的值，不替代历史凭据轮换、密钥服务审计和真实基础设施连通性验收。

### 审计保留后台调度（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `AuditLogRetentionScheduleOptions`：调度默认关闭，启用时校验间隔、保留时长和批量上限 | `AuditLogRetentionHostedServiceTest.Scheduler_WhenDisabled_ShouldNotRun`、`Scheduler_WhenIntervalIsInvalid_ShouldRejectRegistration` | `Bing.Auditing.Tests` net8/net6 各 4/4；`artifacts/audit-retention-scheduler/net8-v3/audit-retention-scheduler-net8-v3.trx`、`artifacts/audit-retention-scheduler/net6-v2/audit-retention-scheduler-net6-v2.trx` |
| `AuditLogRetentionHostedService`：启动立即执行单批，调用高层授权服务，停止等待当前任务完成 | `AuditLogRetentionHostedServiceTest.Scheduler_WhenEnabled_ShouldRunAuthorizedBatch`、`Scheduler_WhenCurrentBatchIgnoresCancellation_ShouldWaitForCompletion` | 同上 |
| `AddBingAuditLogRetentionScheduler<TAuthorizer>`：复用授权注册入口，不绕过 `IAuditLogRetentionService` | `AuditLogRetentionServiceTest.AddBingAuditLogRetention_ShouldKeepExistingAuthorizer` 与调度器注册测试 | `Bing.Auditing.Tests` 完整 net8/net6 各 184/184；`artifacts/audit-retention-scheduler/full-net8-v2/auditing-full-with-scheduler-net8-v2.trx`、`full-net6-v2/auditing-full-with-scheduler-net6-v2.trx` |

该调度器仍不提供跨节点选主、冷归档或关键审计删除；宿主必须提供受信后台授权器并为多实例部署配置外部协调策略。

### 审计保留协调租约（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `AuditLogRetentionScheduleOptions`：协调默认关闭，拒绝无限等待，支持有限租期配置 | `AuditLogRetentionHostedServiceTest.Scheduler_WhenCoordinationLeaseUnavailable_ShouldSkipBatch`、`Scheduler_WhenCoordinationLeaseAcquired_ShouldReleaseLeaseOnce` | `Bing.Auditing.Tests` net8/net6 定向各 7/7；`artifacts/audit-retention-scheduler/coord-net8/coord-net8.trx`、`coord-net6/coord-net6.trx` |
| `AuditLogRetentionHostedService`：租约不可用不调用存储，成功路径异步释放租约，未注册提供程序不执行清理 | 同上及 `Scheduler_WhenCoordinationProviderMissing_ShouldSkipBatch` | `Bing.Auditing.Tests` net8/net6 各 7/7 |
| 审计保留调度与现有授权/作用域回归 | `AuditLogRetentionHostedServiceTest` 全部用例及 `Bing.Auditing.Tests` 全量 | net8/net6 各 187/187；`artifacts/audit-retention-scheduler/full-coord-net8/full-coord-net8.trx`、`full-coord-net6/full-coord-net6.trx` |

协调租约只降低重复调度，不提供 fencing 或存储幂等；Redis 故障切换、真实多节点清理、冷归档和关键审计留存仍需专用环境与运维策略验收。

### 协调租约启动校验结果更正（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `AuditLogRetentionHostedService.StartAsync`：启用协调却缺少租约提供程序时启动失败，不进入后台静默重试 | `AuditLogRetentionHostedServiceTest.Scheduler_WhenCoordinationProviderMissing_ShouldRejectStartup` | `Bing.Auditing.Tests` net8/net6 各 7/7；`artifacts/audit-retention-scheduler/coord-start-net8/coord-start-net8.trx`、`coord-start-net6/coord-start-net6.trx` |
| 调度器修正后的完整审计回归 | `Bing.Auditing.Tests` 全量 | net8/net6 各 187/187；`artifacts/audit-retention-scheduler/full-start-net8/full-start-net8.trx`、`full-start-net6/full-start-net6.trx` |

启动失败语义与“已注册租约但暂时未获取到则跳过本轮”分开验证；真实 Redis 故障切换和多节点清理仍未在本地证据中宣称通过。

### FreeSQL/EFCore Docker MySQL-CAP-RabbitMQ 有序事务

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| FreeSQL CAP 有序事务：提交、回滚重试、重复投递去重、乱序消息补齐 | `Bing.Admin.FreeSQL.Tests.OrderedEventCapIntegrationTest` 三个测试方法 | `artifacts/docker-cap-validation/freesql-ordered-full-migrated-net6/freesql-ordered-full-migrated-net6.trx`，3/3 |
| FreeSQL 回滚后从序号 1 重试，数据库事务与 CAP 出箱同批提交 | `PublishAsync_WhenRolledBack_ShouldRetryFromFirstSequence` | `artifacts/docker-cap-validation/freesql-ordered-retry-migrated-net6/freesql-ordered-retry-migrated-net6.trx`，1/1 |
| EFCore CAP 有序事务：提交、回滚重试、重复投递去重、乱序消息补齐 | `Bing.Admin.EFCore.Tests.OrderedEventCapIntegrationTest` 三个测试方法 | `artifacts/docker-cap-validation/efcore-ordered-net6/efcore-ordered-net6.trx`，3/3 |

验证使用隔离 Docker MySQL 8.4 和 RabbitMQ 4.3；旧 `bing_event_inbox` 表缺少 `CreatedAtUtc` 时，先执行显式 schema 迁移后再运行。该证据不覆盖真实多节点竞争、CAP 断连恢复、Redis 故障切换、蓝绿部署或 SDK 8.0.424。

### Redis 限流与分布式锁真实集成

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `RedisTokenBucketRateLimiter`：跨独立进程共享容量、并发令牌申请、取消和键回收 | `RedisTokenBucketRateLimiterTest.TryAcquireAsync_TwoIndependentProcesses_ShouldShareCapacity` 及该类其余 11 个场景 | net8/net6 各 12/12；`artifacts/redis-rate-limit-validation/redis-tokenbucket-net8/redis-tokenbucket-net8.trx`、`redis-tokenbucket-net6/redis-tokenbucket-net6.trx` |
| `CSRedisDistributedLock`：原子获取、长租期、错误所有者保护、重复释放及异常释放 | `RedisDistributedLockTest` 10 个场景 | net8/net6 各 10/10；`artifacts/redis-lock-validation/redis-lock-net8/redis-lock-net8.trx`、`redis-lock-net6/redis-lock-net6.trx` |

测试使用隔离 Redis Docker 实例。结果不替代 Redis 主从故障切换、网络断连恢复、真实 HTTP 认证代理链路或限流显式降级验收。

上项测试方法重命名后重新执行，最终定向证据为 `artifacts/audit-retention-scheduler/coord-start-final-net8/coord-start-final-net8.trx` 和 `artifacts/audit-retention-scheduler/coord-start-final-net6/coord-start-final-net6.trx`，仍各 7/7；全量证据为 `full-start-net8`、`full-start-net6`，仍各 187/187。

### 有序消费者运行时排空契约

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `IOrderedEventConsumerRuntime`：按稳定逻辑消费者暴露运行状态、在途数量和排空控制 | `OrderedEventConsumerTest.ConsumerRuntime_WhenBeginDrain_ShouldWaitForActiveHandlerWithoutForcingRelease` | `Bing.EventBus.Tests` net6/net8 各 1/1；`artifacts/ordered-consumer-runtime7/consumer-runtime-net6.trx`、`consumer-runtime-net8.trx` |
| `ConsumerWorker<THandler>.StopAsync`：宿主停止与显式 `BeginDrain` 共用停止扫描路径，取消或超时不强制释放活动作用域 | `OrderedEventConsumerTest.HostedWorker_WhenHandlerIgnoresCancellation_ShouldReportIncompleteDrainAndWaitLater`、`HostedWorker_WhenStoppedBeforeStart_ShouldNotReportIncompleteDrain` | `Bing.EventBus.Tests` net6/net8 各 16/16 的完整 `OrderedEventConsumerTest` 回归；`artifacts/ordered-consumer-runtime7/ordered-consumer-full-net6.trx`、`ordered-consumer-full-net8.trx` |

该契约只覆盖 Bing 有序消费者；CAP 原生回调、Hangfire、SSE/WebSocket、其他后台任务和 Compose/Kubernetes 动态切流仍需独立验收。测试使用 SDK 8.0.425，仓库锁定的 8.0.424 未验证。

### Admin 库存跨作用域与跨进程竞争

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `AdminInventoryService`、`DbInventoryOrderStore`：同一整单请求在两个作用域及两个独立进程中只提交一次，重复结果复用首次结果 | `AdminMySqlCapIntegrationTest.InventoryOrder_WhenScopesCompeteForSameOrder_ShouldCommitExactlyOnce`、`InventoryOrder_WhenProcessesCompeteForSameOrder_ShouldCommitExactlyOnce` | FreeSQL net6.0 4/4 定向集：`artifacts/inventory-competition-freesql-net6-migrated/inventory-competition-freesql-net6-migrated.trx`；EFCore net6.0 4/4 定向集：`artifacts/inventory-competition-efcore-net6-migrated/inventory-competition-efcore-net6-migrated.trx` |
| `IInventoryStore` 的条件扣减与整单保存点：不同整单竞争时库存不为负且不留下部分扣减 | `AdminMySqlCapIntegrationTest.InventoryOrder_WhenDifferentOrdersCompete_ShouldKeepNonNegativeAtomicStock`、`InventoryOrder_WhenProcessesCompeteForDifferentOrders_ShouldKeepStockAtomic` | 同上；测试库先执行 `deploy/migrations/auditing/001-critical-flag.mysql.sql` 与 `004-tenant-retention-index.mysql.sql`，生产不自动迁移 |

该证据覆盖隔离 MySQL/RabbitMQ 的真实同作用域和跨进程竞争，不覆盖数据库主从切换、CAP 连接断开、提交确认丢失后的 Unknown 恢复和蓝绿动态部署。实际 SDK 为 8.0.425，锁定的 8.0.424 未验证。

### Admin 库存死锁、确认丢失与出箱恢复

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `AdminInventoryOrderExecutor`、两套 ORM 工作单元：死锁回滚后等待清理并使用新作用域重试，持续死锁在四次后保留最后异常 | `AdminMySqlCapIntegrationTest.InventoryOrder_WhenInnoDbDeadlocks_ShouldAwaitCleanupAndRetryFreshScope`、`InventoryOrder_WhenInnoDbDeadlocksPersist_ShouldStopAfterFourCleanAttempts` | FreeSQL/EFCore net6.0 各 5/5 定向集；`artifacts/inventory-fault-recovery-freesql-net6/inventory-fault-recovery-freesql-net6.trx`、`artifacts/inventory-fault-recovery-efcore-net6/inventory-fault-recovery-efcore-net6.trx` |
| `InventoryCommitAckLossProxy`、`AdminInventoryOrderExecutor.ExecuteWithRecoveryAsync`：提交确认丢失保留 Unknown，原请求查询恢复首次结果并聚合清理错误 | `InventoryOrder_WhenCommitAcknowledgementIsLost_ShouldRecoverOriginalCommittedResult`、`InventoryOrder_WhenCommitAcknowledgementIsLost_ShouldPreserveRecoveryAndCleanupFailures` | 同上 |
| CAP 出箱恢复：原出箱记录由后台扫描投递，库存和整单结果不重复写入 | `InventoryOrder_WhenCommitAcknowledgementIsLost_ShouldRecoverOutboxThroughBackgroundScan` | 同上 |

该映射证明隔离 MySQL/RabbitMQ 中的真实异常路径，不替代主从/网络故障注入、RabbitMQ 重启和蓝绿动态部署验收。实际 SDK 为 8.0.425，锁定的 8.0.424 未验证。

### CAP 原生消费回调排空

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `IBingCapConsumerRuntime` / `BingSubscribeInvoker`：排空后拒绝新回调，在途回调结束后才归零 | `BingSubscribeInvokerTest.ConsumerRuntime_WhenDraining_ShouldWaitForInFlightAndRejectNewInvocation` | `Bing.EventBus.Tests` net6/net8 定向类各 12/12 |
| `EnableBingCapConsumerCancellationRetry`：运行时与 `ISubscribeInvoker` 共享实例、重复注册不替换调用器，Scoped 调用器不被根容器捕获，宿主不解析 scoped 运行时 | `BingSubscribeInvokerTest.EnableCancellationRetry_ShouldExposeInvokerRuntime`、`BingCapConsumerCancellationRegistrationTest.Enable_WhenAdaptedInvokerIsScoped_ShouldRegisterScopedRuntime`、`Enable_WhenScopedInvokerIsUsedByCapHost_ShouldBuildWithValidateScopes` | `Bing.EventBus.Tests.Cap` net6/net8 完整定向集各 64/64；`artifacts/cap-consumer-drain-net6/bing-eventbus-cap-net6.trx`、`artifacts/cap-consumer-drain-net8/bing-eventbus-cap-net8.trx`；其中注册与宿主生命周期职责集各 29/29 |

该契约只覆盖 `BingSubscribeInvoker` 的 CAP 回调，不证明 broker 拉取、CAP 原生处理器、Hangfire 或其他后台任务已经排空；等待取消不会释放活动作用域。实际 SDK 为 8.0.425，锁定的 8.0.424 未验证。

### Admin Hangfire 应用层任务排空（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `IBingHangfireRuntime` / `BingHangfireRuntime`：开始排空后拒绝新任务创建和执行，等待在途计数归零，不强制释放活动任务；等待取消时保留活动任务并提供诊断数据 | `AdminHangfireRegistrationTest.HangfireDrainFilter_WhenCreatingDuringDrain_ShouldCancelCreation`、`HangfireDrainFilter_WhenDraining_ShouldKeepInFlightAndRejectNewJobs`、`HangfireDrainHostedService_WhenStopping_ShouldWaitForInFlightJob`、`HangfireDrainHostedService_WhenStopWaitIsCanceled_ShouldKeepInFlightJob`、`HangfireDrainHostedService_WhenApplicationStopping_ShouldBeginDrain` | `Bing.Admin.EFCore.Tests` net6.0 定向类 13/13；`modules/admin/tests/Bing.Admin.EFCore.Tests/TestResults/admin-hangfire-drain-net6.trx` |
| `HangfireModule`：仅 Task 角色注册运行时、过滤器和宿主排空服务，Web/Consumer 不注册；宿主停止等待在途任务 | `AdminHangfireRegistrationTest.AddServices_WhenTaskRole_ShouldRegisterHostedServer`、`AddServices_WhenWebRole_ShouldNotRegisterHostedServer`、`AddServices_WhenConsumerRole_ShouldNotRegisterHostedServer`、`HangfireDrainFilter_WhenCreatingDuringDrain_ShouldCancelCreation`、`HangfireDrainHostedService_WhenStopping_ShouldWaitForInFlightJob`、`HangfireDrainHostedService_WhenStopWaitIsCanceled_ShouldKeepInFlightJob`、`HangfireDrainHostedService_WhenApplicationStopping_ShouldBeginDrain` | 同上 13/13 |

该映射只证明 Hangfire 过滤器层的准入和在途计数，不证明 Hangfire 原生服务器已停止拉取、存储任务已完成迁移、多个 Task 节点已协调排空或动态蓝绿回滚。实际 SDK 为 8.0.425，锁定的 8.0.424 未验证。

### 蓝绿 Task 部署模板（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `deploy/bluegreen/compose.yaml`：Web、Consumer、Task 使用独立 profile，Task 使用 `BING_HOST_ROLE=task` 且关闭 CAP 消费 | `Test-BlueGreen.ps1 -Platform Compose` 的 `Compose task slots`、`Compose task role` 静态断言 | Compose 静态检查 15/15；`docker compose ... config --quiet` 返回 0；Live Task 服务未验证 |
| `deploy/bluegreen/kubernetes.yaml`：Task 蓝绿 Deployment 独立、绿版默认停放、Task 不接收 CAP 消费 | `Test-BlueGreen.ps1 -Platform Compose` 读取 Kubernetes 清单的 `Kubernetes task role`、`Kubernetes green task parked` 断言 | 同上；Kubernetes dry-run、Task 就绪副本、排空和回滚未验证 |
| `Test-BlueGreen.ps1 -RequireTask`：Live 模式按平台检查至少一个运行中/就绪的 Task 实例 | Compose `docker compose ps` 分支、Kubernetes `readyReplicas` 分支 | 代码路径已实现，当前无可用真实镜像/集群，未执行 |

该映射只证明部署输入和可选就绪门槛，不证明 Hangfire 原生服务器停止、任务存储迁移、调度单活、跨节点排空或蓝绿动态回滚。

Task 槽位现已同时接入 EFCore 和 FreeSQL Admin 的 Hangfire 模块；下方测试覆盖 FreeSQL 注册边界。该证据仍不替代真实任务调度器和多节点动态验收。

### FreeSQL Admin Hangfire Task 注册（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `Bing.Admin.FreeSQL.HangfireModule`：Task 角色注册 `BingHangfireRuntime`、`IBingHangfireRuntime` 和托管排空服务；Web/Consumer 角色不启动 Hangfire Server | `AdminHangfireRegistrationTest.AddServices_WhenTaskRole_ShouldRegisterHangfireRuntime`、`AddServices_WhenWebRole_ShouldNotRegisterHostedServer`、`AddServices_WhenConsumerRole_ShouldNotRegisterHostedServer` | `Bing.Admin.FreeSQL.Tests` net6.0 定向类 6/6；`modules/admin/tests/Bing.Admin.FreeSQL.Tests/TestResults/admin-freesql-hangfire-drain-net6-v3.trx` |
| `Bing.Admin.FreeSQL.Startup`：Consumer 跳过 Hangfire 模块，Task 加入 Hangfire 模块；排空过滤器拒绝新执行并等待在途任务 | `AdminHangfireRegistrationTest.Startup_WhenConsumerRole_ShouldNotRegisterHangfireModule`、`Startup_WhenTaskRole_ShouldRegisterHangfireModule`、`DrainFilter_WhenDraining_ShouldRejectNewJobsAndWaitForInFlight` | 同上 6/6 |

该测试只覆盖 FreeSQL 宿主的注册边界和应用层排空接线，不替代真实 Hangfire Server 拉取停止、任务存储迁移、多节点排空或动态蓝绿部署。Task 槽位现在可用于两套 Admin 宿主，但动态验收仍需真实镜像和外部基础设施。

### 审计日志显式归档（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `DbAuditingStore.ArchiveBeforeAsync`：只归档过期普通审计，关键/新记录保留，归档与删除在同一事务内完成 | `DbAuditingStoreTest.ArchiveBeforeAsync_ShouldMoveOnlyExpiredOrdinaryAudits`、`ArchiveBeforeAsync_WhenDeleteFails_ShouldRollbackArchiveInsert` | `Bing.Auditing.Tests` net6.0/net8.0 定向归档集各 16/16；完整回归各 196/196；TRX 为 `audit-archive-target-net6.trx`、`audit-archive-target-net8.trx`、`audit-archive-full-net6.trx`、`audit-archive-full-net8.trx` |
| 归档历史身份复用被拒绝且主表保留，预取消不打开连接，单批受有界批量限制 | `DbAuditingStoreTest.ArchiveBeforeAsync_WhenArchiveAlreadyContainsIdentity_ShouldRejectIdentityReuse`、`ArchiveBeforeAsync_WhenBatchIsLimited_ShouldArchiveAtMostBatchSize`、`DeleteBeforeAsync_WhenArgumentsAreInvalid_ShouldRejectBeforeOpeningConnection` | 同上 |
| `AuditLogArchiveService`：先授权再归档，拒绝和取消不触发低层存储；注册入口尊重已有授权器 | `AuditLogArchiveServiceTest.ArchiveBeforeAsync_ShouldAuthorizeBeforeStore`、`ArchiveBeforeAsync_WhenAuthorizationFails_ShouldNotArchive`、`ArchiveBeforeAsync_WhenCancelledBeforeAuthorization_ShouldNotCallDependencies`、`AddBingAuditLogArchive_ShouldKeepExistingAuthorizer` | `Bing.Auditing.Tests` net6.0/net8.0；完整回归各 196/196 |
| `AuditLogSchema`、`AddBingDatabaseAuditing`：MySQL/SQLite 归档表完整 DDL、索引和低层契约注册 | `AuditLogSchemaTest.GetCreateStatements_WhenSqlite_ShouldMatchCompleteStatement`、`GetCreateStatements_WhenMySql_ShouldMatchCompleteStatement`、`AuditingPersistenceExtensionsTest.AddBingDatabaseAuditing_ShouldReplaceDefaultStoreAndShareInstance`、`DbAuditingStoreMySqlCommandTest.ArchiveBeforeAsync_WhenMySql_ShouldUseInsertThenDeleteSql` | `Bing.Auditing.Tests` net6.0/net8.0 |

上述证据不替代真实 MySQL 归档事务、归档历史二次清理、冷存储备份、跨节点协调和 SDK 8.0.424 验证；Docker API 当前无权限，实际验证使用 SDK 8.0.425。

### 审计归档历史显式清理（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `DbAuditingStore` 的 `IAuditLogArchiveRetentionStore.DeleteBeforeAsync`：只删除过期普通归档，保留新记录和关键标记，使用有界批次 | `DbAuditingStoreTest.DeleteArchivedBeforeAsync_ShouldDeleteOnlyExpiredOrdinaryArchives`、`DeleteArchivedBeforeAsync_WhenBatchIsLimited_ShouldDeleteAtMostBatchSize` | `Bing.Auditing.Tests` net6.0/net8.0 归档清理定向集各 24/24；TRX 为 `audit-archive-retention-target-net6.trx`、`audit-archive-retention-target-net8.trx` |
| 归档历史清理的事务回滚、取消和参数预检 | `DbAuditingStoreTest.DeleteArchivedBeforeAsync_WhenDeleteFails_ShouldKeepHistory`、`DeleteBeforeAsync_WhenArgumentsAreInvalid_ShouldRejectBeforeOpeningConnection` | 同上 |
| `AuditLogArchiveRetentionService`：独立授权器先于低层删除，拒绝/取消无存储副作用，重复注册不替换调用方授权器 | `AuditLogArchiveRetentionServiceTest.DeleteBeforeAsync_ShouldAuthorizeBeforeStore`、`DeleteBeforeAsync_WhenAuthorizationFails_ShouldNotDelete`、`DeleteBeforeAsync_WhenCancelledBeforeAuthorization_ShouldNotCallDependencies`、`AddBingAuditLogArchiveRetention_ShouldKeepExistingAuthorizer` | `Bing.Auditing.Tests` 完整 net6.0/net8.0 各 204/204；TRX 为 `audit-archive-retention-full-net6.trx`、`audit-archive-retention-full-net8.trx` |
| MySQL 归档历史 SQL 及低层契约注册 | `DbAuditingStoreMySqlCommandTest.DeleteArchivedBeforeAsync_WhenMySql_ShouldUseArchiveRetentionSql`、`AuditingPersistenceExtensionsTest.AddBingDatabaseAuditing_ShouldReplaceDefaultStoreAndShareInstance` | MySQL SQL 录制夹具通过；真实 MySQL 事务仍待专用环境 |

清理证据不替代监管留存、冷存储备份、跨节点协调、真实 MySQL 验证或 SDK 8.0.424 验证。

### 审计归档历史可选调度（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `AuditLogArchiveRetentionScheduleOptions`：默认关闭，校验间隔、保留时长、批量和协调租约边界 | `AuditLogArchiveRetentionHostedServiceTest.Scheduler_WhenDisabled_ShouldNotRun`、`Scheduler_WhenCoordinationProviderMissing_ShouldRejectStartup` | net6.0/net8.0 各 7/7 定向集；SDK 8.0.425，锁定 8.0.424 未验证 |
| `AuditLogArchiveRetentionHostedService`：立即执行有界批次，租约不可用时跳过，停止等待不响应取消的清理调用 | `AuditLogArchiveRetentionHostedServiceTest.Scheduler_WhenEnabled_ShouldRunAuthorizedBatch`、`Scheduler_WhenCoordinationLeaseUnavailable_ShouldSkipBatch`、`Scheduler_WhenCurrentBatchIgnoresCancellation_ShouldWaitForCompletion` | net6.0/net8.0 各 7/7 定向集；完整 `Bing.Auditing.Tests` 各 209/209 |
| `AddBingAuditLogArchiveRetentionScheduler<TAuthorizer>`：注册独立归档授权器、服务和托管调度器 | `AuditLogArchiveRetentionHostedServiceTest` 全类 | `audit-archive-retention-scheduler-net6-final.trx`、`audit-archive-retention-scheduler-net8-final.trx`；不替代真实 MySQL、Redis 多节点和备份/重放窗口验收 |

### 审计调度作用域租约修正（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `AuditLogRetentionHostedService`：协调租约提供程序在每个批处理作用域内解析，单例托管服务不捕获 scoped 服务 | `AuditLogRetentionHostedServiceTest.Scheduler_WhenCoordinationProviderIsScoped_ShouldBuildWithValidateScopes` | `Bing.Auditing.Tests` net6.0/net8.0；普通与归档调度器合并定向集各 16/16，`framework/tests/Bing.Auditing.Tests/TestResults/audit-retention-scoped-net6.trx`、`audit-retention-scoped-net8.trx` |
| `AuditLogArchiveRetentionHostedService`：协调租约和归档清理服务共享本批次作用域，停止时等待实际清理完成 | `AuditLogArchiveRetentionHostedServiceTest.Scheduler_WhenCoordinationProviderIsScoped_ShouldBuildWithValidateScopes` | 同上 |
| 两类调度器的注册、授权、租约跳过、取消和释放回归 | `AuditLogRetentionHostedServiceTest`、`AuditLogArchiveRetentionHostedServiceTest` 全类 | `Bing.Auditing.Tests` 全量 net6.0/net8.0 各 213/213；`framework/tests/Bing.Auditing.Tests/TestResults/audit-retention-scoped-full-net6.trx`、`audit-retention-scoped-full-net8.trx` |

该修正证明的是进程内 `ValidateScopes` 生命周期安全和批次级资源释放；Redis 多节点 fencing、真实 MySQL 归档事务、冷存储/重放窗口和锁定 SDK 8.0.424 仍需独立环境验证。

### 审计调度启动预检（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `AuditLogRetentionHostedService.StartAsync`：启用调度但缺少 `IAuditLogRetentionService` 时立即失败，不创建后台循环 | `AuditLogRetentionHostedServiceTest.Scheduler_WhenRetentionServiceMissing_ShouldRejectStartup` | `Bing.Auditing.Tests` net6.0/net8.0；调度器定向集各 18/18，`framework/tests/Bing.Auditing.Tests/TestResults/audit-scheduler-startup-validation-net6.trx`、`audit-scheduler-startup-validation-net8.trx` |
| `AuditLogArchiveRetentionHostedService.StartAsync`：启用调度但缺少 `IAuditLogArchiveRetentionService` 时立即失败 | `AuditLogArchiveRetentionHostedServiceTest.Scheduler_WhenArchiveRetentionServiceMissing_ShouldRejectStartup` | 同上 |
| 预检后归档、保留、查询、租户清理和资源释放回归 | `Bing.Auditing.Tests` 全量 | net6.0/net8.0 各 215/215，`framework/tests/Bing.Auditing.Tests/TestResults/audit-scheduler-startup-full-net6.trx`、`audit-scheduler-startup-full-net8.trx` |

该映射只证明依赖缺失的启动期诊断，不证明外部数据库、Redis 多节点租约、冷存储备份、动态蓝绿部署或锁定 SDK 8.0.424 已验收。

### 有序事件留存启动预检（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `OrderedEventRetentionHostedService.StartAsync`：启用调度但缺少 `OrderedEventCoordinator` 时立即失败，不创建后台清理循环 | `OrderedEventRetentionHostedServiceTest.Scheduler_WhenCoordinatorMissing_ShouldRejectStartup` | `Bing.EventBus.Tests` net6.0/net8.0；定向集各 7/7，`framework/tests/Bing.EventBus.Tests/TestResults/ordered-retention-startup-validation-net6.trx`、`ordered-retention-startup-validation-net8.trx` |
| 有序留存调度正常清理、租约跳过、取消等待和依赖预检回归 | `OrderedEventRetentionHostedServiceTest` 全类及 EventBus 全量 | net6.0/net8.0 各 332/332，`framework/tests/Bing.EventBus.Tests/TestResults/ordered-retention-startup-full-net6.trx`、`ordered-retention-startup-full-net8.trx` |

该映射只证明调度器启动期依赖诊断和进程内资源边界；真实 MySQL、Redis 多节点、CAP 重放和动态蓝绿验收仍需专用环境。

### FreeSQL 存储自动登记领域事件（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `StoreBase<TEntity,TKey>` 的新增、更新、删除及异步写入路径：自动登记实现 `IHasDomainEvents` 的实体 | `UnitOfWorkCancellationTest.StoreChanges_WithDomainEventEntity_ShouldDispatchWithoutExplicitRegistration`、`StoreChanges_WithDomainEventEntityCollection_ShouldDispatchWithoutExplicitRegistration` | `Bing.FreeSQL.Tests` net6.0/net8.0 各 2/2；`framework/tests/Bing.FreeSQL.Tests/TestResults/freesql-store-domain-events-net6-v2.trx`、`freesql-store-domain-events-net8-v2.trx` |
| 单实体和批量删除路径保留原聚合事件，事务完成后清空事件源并删除记录 | 同上 | 同上 |
| 有序事件恢复仍要求授权，不因测试夹具而放宽默认拒绝 | `FreeSqlOrderedEventTransactionFactoryTest.Coordinator_WhenBusinessFails_ShouldRollbackSavepointAndBlockStream` | `framework/tests/Bing.FreeSQL.Tests/TestResults/freesql-ordered-recovery-authorized-net6.trx`；全量 net6/net8 各 75 通过、24 跳过 |
| FreeSQL 既有生命周期、库存/有序事件和取消路径回归 | `Bing.FreeSQL.Tests` 全量 | `framework/tests/Bing.FreeSQL.Tests/TestResults/freesql-domain-event-full-net6-v3.trx`、`freesql-domain-event-full-net8-v3.trx`；76 通过、24 外部 MySQL 测试因环境门控跳过 |

该映射不替代真实 MySQL/CAP/RabbitMQ 多节点、进程中断恢复、跨节点顺序、Redis 故障切换、动态蓝绿部署或 SDK 8.0.424 验证。

### CAP 消费收据显式留存清理（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `IEventConsumptionRetentionStore` / `EventConsumptionStore.DeleteBeforeAsync`：按稳定消费者、UTC 截止时间和固定批次清理过期收据，其他消费者和新收据不受影响 | `EventConsumptionStoreTest.DeleteBeforeAsync_ShouldDeleteOnlyExpiredConsumerReceiptsAndRespectBatch` | `Bing.EventBus.Tests` net6.0/net8.0 定向集各 3/3；`framework/tests/Bing.EventBus.Tests/TestResults/event-consumption-retention-net6.trx`、`event-consumption-retention-net8.trx` |
| 收据清理由调用方事务拥有，回滚保留记录；取消、非 UTC 截止时间和非法批量在数据库命令前拒绝 | `EventConsumptionStoreTest.DeleteBeforeAsync_WhenTransactionRollsBack_ShouldKeepReceipts`、`DeleteBeforeAsync_WhenCanceledOrInvalid_ShouldNotDeleteReceipts` | 同上 |

该映射只证明 SQLite 的事务、过滤和取消边界；真实 MySQL 语句、跨节点协调、授权、重放窗口和长周期容量仍需专用环境与部署策略验证。实际 SDK 为 8.0.425，锁定的 8.0.424 未验证。

MySQL 语法的环境门控用例为 `EventConsumptionMySqlIntegrationTest.DeleteBeforeAsync_ShouldDeleteOnlyExpiredConsumerReceipts`；net6.0/net8.0 各 0 通过、1 跳过，TRX 为 `framework/tests/Bing.FreeSQL.Tests/TestResults/event-consumption-mysql-retention-net6.trx` 和 `event-consumption-mysql-retention-net8.trx`。当前没有把跳过结果写成 MySQL 验收通过。

SQLite 收据时间格式固定后的回归为 `EventConsumptionStoreTest.DeleteBeforeAsync_ShouldDeleteOnlyExpiredConsumerReceiptsAndRespectBatch`，net6.0/net8.0 各 3/3；EventBus 全量回归各 335/335，TRX 为 `framework/tests/Bing.EventBus.Tests/TestResults/event-consumption-retention-full-net6-v2.trx` 和 `event-consumption-retention-full-net8-v2.trx`。该证据覆盖驱动无关的 SQLite 时间比较，不替代真实 MySQL 事务验证。

### CAP 收据高层授权入口（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `EventConsumptionRetentionService`：参数预检、取消检查、授权成功后才调用低层收据清理，并沿用调用方事务 | `EventConsumptionRetentionServiceTest.DeleteBeforeAsync_ShouldAuthorizeBeforeStore`、`DeleteBeforeAsync_WhenCanceledOrInvalid_ShouldNotCallDependencies` | `Bing.EventBus.Tests` net6.0/net8.0 各 4/4；`framework/tests/Bing.EventBus.Tests/TestResults/event-consumption-retention-service-net6-v2.trx`、`event-consumption-retention-service-net8-v2.trx` |
| 授权拒绝无数据库副作用；重复注册不覆盖已有授权器 | `EventConsumptionRetentionServiceTest.DeleteBeforeAsync_WhenAuthorizationFails_ShouldNotCallStore`、`AddBingEventConsumptionRetention_ShouldKeepExistingAuthorizer` | 同上 |

该映射证明的是进程内授权和 DI 注册顺序，不替代宿主提供的真实授权逻辑、MySQL 事务、跨节点清理协调或业务重放窗口验收。

高层服务接入后的 `Bing.EventBus.Tests` 全量回归在 net6.0、net8.0 各 339/339 通过，TRX 为 `framework/tests/Bing.EventBus.Tests/TestResults/event-consumption-retention-service-full-net6.trx` 和 `event-consumption-retention-service-full-net8.trx`。

### CAP 收据表结构与迁移契约（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `EventConsumptionSchema.GetCreateStatements`：MySQL/SQLite 收据表、二进制身份列和留存索引使用完整固定 SQL | `EventConsumptionSchemaTest.GetCreateStatements_WhenMySql_ShouldUseBinaryKeysAndRetentionIndex`、`GetCreateStatements_WhenSqlite_ShouldCreateTableAndRetentionIndex`、`GetCreateStatements_WhenDatabaseIsInvalid_ShouldThrow` | `Bing.EventBus.Tests` net6.0/net8.0 各 17/17 消费定向集；`event-consumption-schema-net6.trx`、`event-consumption-schema-net8.trx` |
| `EventConsumptionStore` 与 MySQL 集成夹具复用 schema 契约，避免测试表结构漂移 | `EventConsumptionStoreTest` 全类、`EventConsumptionMySqlIntegrationTest` 全类 | SQLite 定向集通过；MySQL net6.0 环境门控 0 通过/5 跳过，未将跳过写成外部验收通过 |

迁移文件为 `deploy/migrations/events/001-event-consumption.mysql.sql` 和 `001-event-consumption.sqlite.sql`；它们只提供受控部署输入，不自动执行迁移，也不决定业务重放窗口。

schema 接入后的 `Bing.EventBus.Tests` 全量回归为 net6.0/net8.0 各 342/342，TRX 为 `event-consumption-schema-full-net6.trx` 和 `event-consumption-schema-full-net8.trx`。FreeSQL MySQL 集成类在两个框架均 0 通过/5 跳过，仅证明源码和门控路径可编译，不证明真实 MySQL 迁移或事务通过。

路径修正后重新验证：`EventConsumptionRetentionServiceTest` 在 net6.0/net8.0 各 4/4，TRX 为 `event-consumption-retention-service-net6-v3.trx`、`event-consumption-retention-service-net8-v3.trx`；EventBus 全量各 339/339，TRX 为 `event-consumption-retention-service-full-net6-v2.trx`、`event-consumption-retention-service-full-net8-v2.trx`。源码已从正确的 `Consumption/EventConsumptionRetentionExtensions.cs` 编译。

### 真实 IP 可信代理边界（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `RealIpMiddleware`：仅在连接对端命中 `RealIpOptions.TrustedProxies` 时解析转发头，空名单或不匹配时保留连接地址 | `RealIpMiddlewareTest.InvokeAsync_WhenRemoteProxyIsUntrusted_ShouldKeepConnectionAddress`、`InvokeAsync_WhenRemoteProxyIsTrusted_ShouldUseRightmostUntrustedAddress` | `Bing.AspNetCore.Mvc.Tests` net6.0 5/5，`framework/tests/Bing.AspNetCore.Mvc.Tests/TestResults/real-ip-trust-net6-final.trx` |
| 转发链跳过可信代理、无效地址继续解析、无效代理配置快速失败及 IPv6 地址比较 | `RealIpMiddlewareTest.InvokeAsync_WhenForwardedChainHasNoClientAddress_ShouldKeepConnectionAddress`、`Constructor_WhenTrustedProxyIsInvalid_ShouldThrow`、`InvokeAsync_WhenUnrelatedIpv6AddressesAreConfigured_ShouldKeepConnectionAddress` | 同上 |

该映射只证明进程内请求头信任边界；真实反向代理、认证网关、代理地址轮换和 ASP.NET Core `ForwardedHeaders` 多跳部署仍需专用环境验证。旧无名单 `UseRealIp` 调用会安全地忽略转发头，迁移时必须显式配置可信代理或改用 `KnownProxies`/`KnownNetworks`。

### CAP Trace 日志正文脱敏（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `MessageEventBus`：Trace 日志只输出事件名、回调名、头数量和阶段，不序列化消息正文或完整请求头 | `MessageEventBusLoggingTest.PublishAsync_WhenTraceEnabled_ShouldNotLogEventPayload` | `Bing.EventBus.Tests` net6.0/net8.0 各 1/1，`framework/tests/Bing.EventBus.Tests/TestResults/cap-logging-redaction-net6.trx`、`cap-logging-redaction-net8.trx` |

该映射只覆盖 Bing CAP 发布入口；CAP 自身、RabbitMQ、应用业务日志和外部日志 Sink 仍需独立审查访问控制、轮换、历史日志清理和旧敏感内容。

脱敏改动后的 `Bing.EventBus.Tests` 完整回归在 net6.0、net8.0 各 343/343 通过，TRX 为 `framework/tests/Bing.EventBus.Tests/TestResults/eventbus-cap-redaction-full-net6.trx` 和 `eventbus-cap-redaction-full-net8.trx`。

### 消费身份首尾空白边界

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `EventConsumptionStore.ValidateKey`：写入和留存清理拒绝消费者名称、事件 ID 的首尾空白，避免应用身份与 MySQL 文本主键的尾部空格规则不一致 | `EventConsumptionStoreTest.TryRecordAsync_InvalidKeyOrCancellation_ShouldNotWriteReceipt`、`DeleteBeforeAsync_WhenCanceledOrInvalid_ShouldNotDeleteReceipts` | `Bing.EventBus.Tests` net6.0/net8.0 各 11/11 定向集；`framework/tests/Bing.EventBus.Tests/TestResults/event-consumption-identity-net6-final.trx`、`event-consumption-identity-net8-final.trx` |
| Unicode 身份按原始值精确比较，不对组合形式隐式规范化 | `EventConsumptionStoreTest.TryRecordAsync_UnicodeIdentities_ShouldRemainDistinct` | 同上 |
| 消费收据、CAP 适配、顺序事件和留存路径未回归 | `Bing.EventBus.Tests` 全量 | net6.0/net8.0 各 344/344；`framework/tests/Bing.EventBus.Tests/TestResults/eventbus-consumer-identity-full-net6.trx`、`eventbus-consumer-identity-full-net8.trx` |

该证据只覆盖应用入口和 SQLite 精确比较；既有 MySQL 存量空白数据清理、`utf8mb4_bin` PAD SPACE 行为、跨节点旧版本混跑和迁移锁等待仍需专用环境验证。实际 SDK 为 8.0.425，锁定的 8.0.424 未验证。

### 可信代理日志回显边界

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `RealIpMiddleware`：Trace 不记录完整转发头，解析失败时不回显客户端提供的非法值 | `RealIpMiddlewareTest.InvokeAsync_WhenForwardedHeaderContainsInvalidValue_ShouldNotLogHeaderValue` | `Bing.AspNetCore.Mvc.Tests` net6.0/net8.0 各 6/6；`framework/tests/Bing.AspNetCore.Mvc.Tests/TestResults/real-ip-logging-net6-final2.trx`、`real-ip-logging-net8-final2.trx` |
| 可信代理、IPv6 比较、转发链及配置异常仍保持原行为 | `RealIpMiddlewareTest` 全类 | 同上 |
| 请求管道回归 | `Bing.AspNetCore.Mvc.Tests` 全量 | net6.0/net8.0 各 146 通过、1 项既有上传测试跳过；`framework/tests/Bing.AspNetCore.Mvc.Tests/TestResults/aspnetcore-mvc-logging-full-net6.trx`、`aspnetcore-mvc-logging-full-net8.trx` |

该证据只覆盖 Bing 中间件日志；外部网关、ASP.NET Core ForwardedHeaders 和第三方日志 Sink 的访问控制、历史日志轮换仍需部署审查。实际 SDK 为 8.0.425，锁定的 8.0.424 未验证。

### Admin MySQL/CAP 真实门控复验（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| EFCore Admin 库存、事务、CAP 出箱、死锁、提交确认丢失、取消和有序消费路径 | `Bing.Admin.EFCore.Tests.AdminMySqlCapIntegrationTest`、`Bing.Admin.EFCore.Tests.OrderedEventCapIntegrationTest` | 隔离 Docker MySQL 8.4/RabbitMQ 4.3；34/34 通过、0 失败、0 跳过；`artifacts/admin-mysqlcap-results/consumer-identity-final/net6.0-20261007T123203391Z/admin-efcore-mysqlcap.trx` |
| FreeSQL Admin 同一组事务、CAP、库存和有序消费路径 | `Bing.Admin.FreeSQL.Tests.AdminMySqlCapIntegrationTest`、`Bing.Admin.FreeSQL.Tests.OrderedEventCapIntegrationTest` | 同一隔离环境；33/33 通过、0 失败、0 跳过；`artifacts/admin-mysqlcap-results/consumer-identity-final/net6.0-20261007T123203391Z/admin-freesql-mysqlcap.trx` |

诊断文件 `admin-mysqlcap-diagnostic.json` 确认 `MySqlReachable=true`、`RabbitReachable=true`、脚本退出码为 0。该证据只覆盖脚本列出的 net6.0 外部场景；两套工程和门控脚本未声明 net8.0，SDK 8.0.424 仍未验证，Redis 故障切换、长时间分区及动态蓝绿验收不由该测试替代。

### 审计归档批次一致性（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `DbAuditingStore.ArchiveBeforeCoreAsync`：删除仅匹配本次归档批次标记和审计身份，避免第二次 `LIMIT` 选到不同源行 | `DbAuditingStoreMySqlCommandTest.ArchiveBeforeAsync_WhenMySql_ShouldUseInsertThenDeleteSql`、`DbAuditingStoreTest.ArchiveBeforeAsync_ShouldMoveOnlyExpiredOrdinaryAudits`、`ArchiveBeforeAsync_WhenSourceChangesAfterCopy_ShouldRollbackBatch` | 审计定向集 net6.0/net8.0 各 37/37；SQLite 实际归档及源记录资格变化回滚通过；完整 SQL 断言在 `framework/tests/Bing.Auditing.Tests/DbAuditingStoreMySqlCommandTest.cs` |
| 归档和删除影响行数不一致时回滚并保留原始诊断 | `DbAuditingStoreMySqlCommandTest.ArchiveBeforeAsync_WhenBatchCountsDiffer_ShouldRollback`、`DbAuditingStoreTest.ArchiveBeforeAsync_WhenDeleteFails_ShouldRollbackArchiveInsert` | 审计定向集 net6.0/net8.0 各 37/37；异常清理和事务回滚有断言 |
| 归档批量、重复身份、关键审计和失败回滚行为保持 | `DbAuditingStoreTest.ArchiveBeforeAsync_WhenBatchIsLimited_ShouldArchiveAtMostBatchSize`、`ArchiveBeforeAsync_WhenArchiveAlreadyContainsIdentity_ShouldRejectIdentityReuse`、`ArchiveBeforeAsync_WhenDeleteFails_ShouldRollbackArchiveInsert` | `Bing.Auditing.Tests` 全量 net6.0/net8.0 各 217/217；实际 SDK 8.0.425，8.0.424 未验证 |
| MySQL 归档插入、按 `ArchivedAtUtc` 标记删除和批次边界 | Docker MySQL 8.4 临时表 SQL probe | `bing_cluster_test` 隔离库：`inserted=2`、`deleted=2`、源表剩余 1 条、归档表 2 条；临时表已清理 |

### 本地事件递归发布深度保护（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `LocalEventBusOptions.MaxPublishDepth`：默认提供有限递归上限，配置为非正数时拒绝发布 | `LocalEventBusOptionsTest.Constructor_MaxPublishDepth_ShouldHaveSafeDefault`、`LocalEventBusRecursionTest.PublishAsync_WhenMaxDepthIsInvalid_ShouldFailBeforeHandlers` | `Bing.EventBus.Tests` net6.0/net8.0 定向集各 19/19 |
| `LocalEventBus.PublishAsync(LocalEventMessage)`：递归超过上限时失败，成功或异常后恢复异步发布上下文，后续发布可继续 | `LocalEventBusRecursionTest.PublishAsync_WhenDepthExceeded_ShouldFailAndRestoreContext` | `Bing.EventBus.Tests` 全量 net6.0/net8.0 各 347/347；`framework/tests/Bing.EventBus.Tests/TestResults/eventbus-net6-recursion.trx`、`eventbus-net8-recursion.trx` |

该映射只覆盖进程内本地事件链；CAP/RabbitMQ 消息环、重试和跨节点幂等仍需分布式环境验证。实际 SDK 为 8.0.425，锁定的 8.0.424 未验证。

### 审计留存与查询真实 MySQL 门控（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `DbAuditingStore.DeleteBeforeAsync`：普通审计按截止时间和批量上限清理，关键审计及未到期记录保留 | `DbAuditingStoreMySqlIntegrationTest.DeleteBeforeAsync_WhenMySql_ShouldKeepCriticalAndRespectBatch` | Docker MySQL 8.4，net6.0/net8.0 各 1/1；`db-auditing-mysql-retention-query-net6.trx`、`db-auditing-mysql-retention-query-net8.trx` |
| `DbAuditingStore.QueryAsync`：按租户、应用和分页返回安全元数据投影，正确解码 MySQL 二进制租户标识 | `DbAuditingStoreMySqlIntegrationTest.QueryAsync_WhenMySql_ShouldReturnTenantScopedMetadata` | 同上 |
| 归档、留存、查询真实连接路径共同回归 | `DbAuditingStoreMySqlIntegrationTest` 全类 | 两个框架各 3/3；无门控审计全量各 217 通过、3 跳过 |

该映射只证明单实例真实 SQL 和安全投影；多实例清理协调、备份恢复、长期容量及生产授权仍需部署级验收。实际 SDK 为 8.0.425，锁定的 8.0.424 未验证。

### DbAuditingStore 真实 MySQL 集成门控（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `DbAuditingStore.ArchiveBeforeAsync`：真实 MySQL 连接内按批次归档过期普通审计，保留未过期源记录 | `DbAuditingStoreMySqlIntegrationTest.ArchiveBeforeAsync_WhenMySql_ShouldMoveExactBatch` | Docker MySQL 8.4，net6.0/net8.0 各 1/1；`framework/tests/Bing.Auditing.Tests/TestResults/db-auditing-mysql-net6.trx`、`db-auditing-mysql-net8.trx` |
| 集成测试数据隔离、schema 初始化和自身清理 | `DbAuditingStoreMySqlIntegrationTest` 的随机租户、`finally` 清理及 `IntegrationDatabaseSafetyValidator` | 使用 `bing_cluster_test`；未重置共享数据库，未操作生产库 |

集成门控证明的是单实例真实 SQL 和事务路径；多实例并发、锁等待、恢复及长期归档容量仍需专门故障注入和运维验证。

该映射不替代真实 MySQL 并发归档、锁等待、索引计划和跨节点调度验收；归档表备份、恢复和长期容量治理仍为部署侧 TODO。

### LockExtensions 异步上下文所有权隔离（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `LockExtensions.LockTak` / `LockTakAsync`：每次成功获取使用唯一持有者，并在异步上下文切换时隔离派生任务的租约记录 | `LockExtensionsTest.LockTakAndRelease_WhenOwnerGenerationChanges_ShouldNotReleaseSuccessor` | `Bing.Core.Tests` net6.0、net8.0 各 21/21 锁定向测试通过；父上下文预热后并发 `Task.Run` 场景未发生后继锁误释放 |
| `LockExtensions.LockRelease` / `LockReleaseAsync`：释放使用获取时捕获的持有者，空租约容器不继续引用锁实例 | `LockExtensionsTest.LockTakAndRelease_WhenOwnerGenerationChanges_ShouldNotReleaseSuccessor`、`TryAcquireLeaseAsync_WhenWaitingIsCancelled_ShouldReclaimWaiter` | 同上；本地锁跨线程释放、ABA 重复释放和等待取消均通过 |

该映射覆盖进程内便捷锁的异步上下文隔离，不替代 Redis 主从切换、租期到期、网络分区或跨进程分布式锁验收。实际 SDK 为 8.0.425，锁定的 8.0.424 未验证。

### CAP dispatcher 入队完成等待（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `CapOrderedEventTransactionFactory.OwnedCapTransaction.FlushNativeAsync`：物理提交后等待 CAP 发布、执行和调度 dispatcher 返回的 `ValueTask`，不提前完成提交任务 | `CapOrderedEventTransactionFactoryTest.CommitAsync_WhenNativeDispatchIsIncomplete_ShouldWaitForDispatchCompletion` | `Bing.EventBus.Tests` net6.0/net8.0 各 20/20；完整回归各 348/348；`framework/tests/Bing.EventBus.Tests/TestResults/eventbus-cap-dispatch-full-net6.trx`、`eventbus-cap-dispatch-full-net8.trx` |
| 入队同步/异步失败保持原异常并保留 `Committed` 结果，dispatcher 的宿主 `Start`/`Dispose` 不被事务跟踪器接管 | `CapOrderedEventTransactionFactoryTest.CommitAsync_WhenNativeFlushThrowsSynchronously_ShouldKeepCommittedOutcome`、`DisposeAsync_WhenRollbackAndDisposeFail_ShouldAggregateOriginalErrorsOnce` | 同一工厂定向集；职责替身不代表 RabbitMQ broker 确认、重投或网络故障恢复 |

该映射证明适配器等待本次调用已经发起的入队任务，不证明消息已被 broker 确认、消费成功或蓝绿排空完成。实际 SDK 为 8.0.425，锁定的 8.0.424 未验证。

### CAP dispatcher 同步完成与异步故障路径（2026-10-07）

| 生产符号 | 直接测试与证据 |
| --- | --- |
| `CapOrderedEventTransactionFactory.TrackingDispatcher.Track` 同步完成 ValueTask 路径 | `Bing.EventBus.Tests`: `CommitAsync_WhenNativeDispatchIsIncomplete_ShouldWaitForDispatchCompletion`（未完成 ValueTask 仍被等待）；CAP 工厂定向 net6/net8 各 21/21 编译执行覆盖 |
| `CapOrderedEventTransactionFactory.TransactionScope.CommitAsync` 异步入队异常、已提交结果与资源释放 | `Bing.EventBus.Tests`: `CommitAsync_WhenNativeDispatchFailsAsynchronously_ShouldKeepCommittedOutcome`；CAP 工厂定向 net6/net8 各 21/21 |
| CAP 适配器与事件总线回归 | `Bing.EventBus.Tests` 全量 `eventbus-cap-dispatch-v2-net6.trx`、`eventbus-cap-dispatch-v2-net8.trx`，各 349/349 |

### CAP dispatcher 多故障聚合（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `CapOrderedEventTransactionFactory.TrackingDispatcher.WaitForCompletionAsync`：多个异步入队失败时保留全部原始异常，单个故障不改变异常身份，纯取消保持取消语义 | `CapOrderedEventTransactionFactoryTest.CommitAsync_WhenMultipleNativeDispatchesFail_ShouldAggregateAllErrors` | CAP 工厂定向测试 net6.0/net8.0 各 22/22；`framework/tests/Bing.EventBus.Tests/TestResults/eventbus-cap-dispatch-multi-net6.trx`、`eventbus-cap-dispatch-multi-net8.trx` |
| 物理事务已提交时仍报告 `Committed`，不重复回滚或释放资源 | 同上，以及 `CommitAsync_WhenNativeDispatchFailsAsynchronously_ShouldKeepCommittedOutcome` | `Bing.EventBus.Tests` 全量 `eventbus-cap-dispatch-multi-full-net6.trx`、`eventbus-cap-dispatch-multi-full-net8.trx`，各 350/350 |

该证据覆盖 CAP dispatcher 本次调用返回的多异常诊断；不替代 RabbitMQ broker 确认、网络故障恢复、CAP 后台重试、跨节点排空或 SDK 8.0.424 验证。实际测试使用 SDK 8.0.425。

### CAP 宿主排空等待预算（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `BingCapConsumerOptions.DrainTimeout`：默认无限等待，启用方可配置有限宿主排空预算 | `CapHostedLifecycleTest.StopAsync_WhenConsumerRuntimeIsRegistered_ShouldDrainBeforeCompletion`、`StopAsync_WhenConfiguredDrainTimeoutExpires_ShouldReportIncomplete` | CAP 宿主定向测试 net6.0/net8.0 各 20/20；默认调用仍传入 `Timeout.InfiniteTimeSpan`，有限配置传入 30ms |
| `EnableBingCapConsumerCancellationRetry(Action<BingCapConsumerOptions>)`：非法排空时间在宿主解析阶段拒绝 | `BingCapConsumerCancellationRegistrationTest.Enable_WhenDrainTimeoutIsInvalid_ShouldRejectHostResolution` | 注册边界测试 net6.0/net8.0 各 11/11；`cap-registration-timeout-net6.trx`、`cap-registration-timeout-net8.trx` |
| `BingCapConsumerHostDrain.WaitForDrainAsync` 与 `CapHostedLifecycleService.ShutdownCoreAsync`：有限预算返回未完成诊断，不强制释放在途资源，重复停止复用同一关闭结果 | `CapHostedLifecycleTest.StopAsync_WhenConfiguredDrainTimeoutExpires_ShouldReportIncomplete` | `failure.Data["Bing.CapConsumerDrainIncomplete"]`、排空调用参数及重复停止均断言；完整 `Bing.EventBus.Tests` net6.0/net8.0 各 351/351，TRX 为 `cap-host-drain-timeout-net6.trx`、`cap-host-drain-timeout-net8.trx`、`eventbus-cap-drain-full-net6-v2.trx`、`eventbus-cap-drain-full-net8-v2.trx` |

该映射只证明 Bing `BingSubscribeInvoker` 的宿主排空预算和资源保留语义；不覆盖 CAP 原生调用器、RabbitMQ 确认、Hangfire/定时任务、跨节点动态切流或 SDK 8.0.424。实际测试使用 SDK 8.0.425。

### Compose 蓝绿后台角色编排入口（2026-10-07）

| 生产符号与关键行为 | 直接验证 | 证据 |
| --- | --- | --- |
| `Invoke-ComposeBlueGreenCutover.ps1`：目标 Web 预检、可选 Consumer/Task 启动、HTTP 切换、旧角色有限排空停止 | PowerShell `Invoke-ComposeBlueGreenCutover.ps1 -Target green -SwitchConsumer -SwitchTask -DryRun` | 输出顺序为 Web 就绪、Consumer、Task、Nginx 切换和成功结果；不访问 Docker 或网络 |
| `Invoke-ComposeBlueGreenCutover.ps1`：失败时恢复旧 Web 路由和后台角色 | `Test-BlueGreen.ps1` 的 `Compose cutover orchestration` 静态断言 | 验证脚本包含 `DrainTimeoutSeconds`、`Restore-WebTraffic` 和 Consumer/Task 开关；PowerShell AST 解析无错误 |
| Compose 模板与编排入口的静态契约 | `Test-BlueGreen.ps1 -Platform Compose` | 17 项静态检查通过；真实镜像、RabbitMQ、长请求和动态回滚仍未验证 |

该映射只证明脚本语法、DryRun 顺序和静态回滚契约，不代表 Compose 真实容器已切流或后台任务已安全排空。动态验收仍需固定镜像摘要、Docker 版本、请求/消息标识和运行日志证据。

### Kubernetes 蓝绿后台角色编排入口（2026-10-07）

| 生产符号与关键行为 | 直接验证 | 证据 |
| --- | --- | --- |
| `Invoke-KubernetesBlueGreenCutover.ps1`：读取当前 Service 颜色、目标 Web 预热、可选 Consumer/Task 扩容、Service 切换和旧角色缩容排空 | PowerShell `Invoke-KubernetesBlueGreenCutover.ps1 -Target green -SwitchConsumer -SwitchTask -DrainTimeoutSeconds 30 -DryRun` | DryRun 在无 Kubernetes 环境完成目标 Web、旧角色排空和成功路径解析；不访问集群 |
| `Invoke-KubernetesBlueGreenCutover.ps1`：异常时恢复旧 selector 和角色副本 | `Test-BlueGreen.ps1` 的 `Kubernetes cutover orchestration` 静态断言 | 验证脚本包含 `DrainTimeoutSeconds`、`Restore-WebService` 和 Consumer/Task 开关；PowerShell AST 解析无错误 |
| 两套蓝绿脚本与清单静态契约 | `Test-BlueGreen.ps1 -Platform Compose`、`Test-BlueGreen.ps1 -Platform Kubernetes`、`docker compose -f deploy/bluegreen/compose.yaml config --quiet` | Compose/Kubernetes 各 17 项静态检查通过，Compose config 退出码为 0（本机 Docker 配置访问警告）；真实 Kubernetes、Pod 排空、CAP/Hangfire 恢复和回滚仍未验证 |

该映射不把 DryRun 或模板静态检查当作集群验收。动态验证仍需记录 Kubernetes 版本、镜像 digest、Service selector 变化、Pod 终止时间、消息 ID、任务 ID 和恢复日志。

### 有序消费者页面内槽位补位（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `OrderedEventConsumerExtensions.ConsumerWorker.ProcessStreamsAsync`：并发页面内任一流完成后立即补充后续流，保持 `MaxConcurrency` 上限 | `OrderedEventConsumerTest.HostedWorker_WhenOneStreamIsSlow_ShouldRefillAvailableSlot`、`HostedWorker_WhenMaxConcurrencyIsTwo_ShouldBoundDifferentStreams` | 有序消费者定向测试 net6.0/net8.0 各 17/17；慢流保持未释放时第三流已进入处理，最大并发仍为 2 |
| 页面游标按源扫描顺序推进，不使用异步完成顺序 | `OrderedEventConsumerTest.HostedWorker_WhenOneStreamIsSlow_ShouldRefillAvailableSlot`、`HostedWorker_WhenBlockedStreamsFillFirstPage_ShouldProcessOthersInSeparateScopes` | `Bing.EventBus.Tests` 全量 net6.0/net8.0 各 353/353；0 失败、0 跳过 |

该映射覆盖单页慢流隔离、分页安全和有界跨页回绕；永久不响应取消的资源排空、RabbitMQ broker 确认与蓝绿多节点动态验收仍未完成。实际 SDK 为 8.0.425，锁定的 8.0.424 未验证。

### 有序消费者跨页公平回绕（2026-10-08）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `OrderedEventConsumerExtensions.AddBingOrderedConsumer`：旧五参数入口保持默认 16 页预算，新重载允许设置 `maxPagesPerScan` | `OrderedEventConsumerTest.HostedWorker_WhenPagesRemainFull_ShouldWrapCursorForFairness` | 定向测试 net6.0/net8.0 各 1/1；无效页预算沿用注册边界校验 |
| `ConsumerWorker.ExecuteAsync`：连续满页达到预算后回到首个流键，避免较小的新流等待到全部后续页耗尽 | `OrderedEventConsumerTest.HostedWorker_WhenPagesRemainFull_ShouldWrapCursorForFairness` | 首流保持缺口、后续页持续有积压时，新流在最多 2 页后获得处理；`HighCompletedAtNewStream` 断言不超过 2 |

该能力只提供有界扫描公平，不改变同一业务流的数据库顺序、幂等或失败重试语义，也不强制终止慢处理器。真实 CAP/RabbitMQ broker 确认、跨节点排空和生产负载下的预算调优仍需专用环境验收。实际 SDK 为 8.0.425，锁定 SDK 8.0.424 未验证。

### 有序事件逻辑消费者全局容量闸门（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `OrderedEventSchema.GetCreateStatements`：容量表与序号、游标、收件箱、归档表一起生成，保持 MySQL/SQLite SQL 顺序契约 | `OrderedEventSchemaTest.GetCreateStatements_WhenMySql_ShouldIncludePendingAgeIndex`、`GetCreateStatements_WhenSqlite_ShouldAddPendingAgeIndex` | MySQL 5 条、SQLite 7 条完整 SQL 断言；EventBus 最终全量 net6.0/net8.0 各 357/357 |
| `DbOrderedEventStore` 构造函数：保留旧三参数签名，容量专用重载可用，非正值拒绝 | `DbOrderedEventStoreTest.Constructor_WhenConsumerCapacityIsInvalid_ShouldReject`、`Constructor_ShouldKeepLegacySignatureAndSupportCapacityOverload` | `ArgumentOutOfRangeException` 和旧/新构造调用直接断言，容量定向集 net6.0/net8.0 各 21/21 |
| `DbOrderedEventStore.EnqueueAsync`：跨业务流共享容量，容量满时拒绝新事件，重复事件先做幂等判断 | `DbOrderedEventStoreTest.EnqueueAsync_WhenConsumerCapacityIsFull_ShouldRejectAcrossStreamsAndReleaseAfterProcessing` | 两条不同业务流占满容量，第三条被拒绝；成功处理一条后第三条可入箱；收件箱计数断言为 2 |
| `DbOrderedEventStore.EnqueueAsync` / `LockConsumerCapacityAsync`：启用容量后重算旧收件箱，避免迁移前积压绕过闸门 | `DbOrderedEventStoreTest.EnqueueAsync_WhenCapacityIsEnabledAfterExistingMessages_ShouldReconcilePendingCount` | 先用兼容存储写入旧积压，再用容量存储上限 1 拒绝新消息，原记录保留 |
| `DbOrderedEventStore.ProcessNextAsync`：状态、游标和容量刷新在同一事务内完成，失败事件继续占用容量 | 上述跨流容量测试及既有 `ProcessNextAsync` 成功/失败测试 | SQLite 真实事务路径通过；真实 MySQL 并发和锁等待仍需门控环境验证 |
| `003-consumer-capacity.mysql.sql` / `003-consumer-capacity.sqlite.sql`：受控创建容量表并回填未完成记录 | 迁移脚本静态审查、`OrderedEventSchemaTest` | 应用启动不自动迁移；MySQL/SQLite 动态迁移和大表回填性能仍未验证 |

该映射只证明数据库存储的可选全局积压闸门，不代表跨数据库、Redis/CAP broker 配额或生产容量压测已经完成；跨页面公平回绕见下一节。实际 SDK 为 8.0.425，锁定的 8.0.424 未验证。

`003-consumer-capacity.mysql.sql` 另在隔离 Docker MySQL 8.4 的 `bing_cluster_test` 数据库直接执行成功，`SHOW CREATE TABLE` 与回填查询确认容量表结构和 0 行初始回填；该命令不替代大表回填性能、备份恢复或生产迁移演练。

### 有序事件容量真实 MySQL 验证（2026-10-07）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `DbOrderedEventStore` MySQL `LockConsumerCapacityAsync` / `RefreshConsumerCapacityAsync`：容量行锁与收件箱聚合在真实事务内工作 | `OrderedEventCapacityMySqlTest.EnqueueAsync_WhenConsumerCapacityIsFull_ShouldRejectAndReleaseAfterProcessing` | Docker MySQL 8.4，专用 `bing_cluster_test`，net6.0/net8.0 各 1/1；TRX 为 `ordered-capacity-mysql-docker-net6.trx`、`ordered-capacity-mysql-docker-net8.trx` |
| 容量满拒绝、成功处理释放、第三条跨业务流再次入箱 | 同上 | 两条流先占满上限 2，第三条被拒绝；第一条消费提交后第三条成功；最终数据库 `PendingCount=2` |
| 测试库隔离和失败清理 | 同上，`IntegrationDatabaseSafetyValidator`、`finally` 清理 | 只操作 `bing_cluster_test`，清理收件箱、归档、游标、序号和容量行；没有生产库或容器生命周期操作 |

该映射证明 MySQL 单实例事务语义，不代表多实例容量压测、主从切换、跨库/Redis 配额或 broker 级流控已经完成。实际 SDK 为 8.0.425，锁定的 8.0.424 未验证。

### Admin 宿主消费者容量配置（2026-10-08）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `Bing.Admin.Modules.CapModule.AddOrderedEventFoundation`：读取 `OrderedEvents:MaxPendingPerConsumer` 并传递给 `DbOrderedEventStore` | `Bing.Admin.EFCore.Tests.AdminCapRegistrationTest.AddServices_WhenOrderedEventCapacityIsConfigured_ShouldApplyCapacity` | Admin EFCore net6.0 定向注册测试 26/26；`AdminCapRegistrationTest` 通过服务实例私有字段断言容量为 3 |
| `Bing.Admin.FreeSQL.Modules.CapModule.AddOrderedEventFoundation`：读取同一配置并保持 FreeSQL 注册路径 | `Bing.Admin.FreeSQL.Tests.AdminCapRegistrationTest.AddServices_WhenOrderedEventCapacityIsConfigured_ShouldApplyCapacity` | Admin FreeSQL net6.0 定向注册测试 26/26；同一容量值断言 |
| 两个 Admin 宿主拒绝非法容量值，不创建有序事件基础设施 | 两个测试类的 `AddServices_WhenOrderedEventCapacityIsInvalid_ShouldReject` | 0、负数、非数字各被注册阶段拒绝；TRX 为 `admin-cap-registration-efcore-net6.trx`、`admin-cap-registration-freesql-net6.trx` |

该映射只覆盖宿主配置到数据库存储的接线和输入边界。Admin 测试工程当前仅声明 net6.0；真实 CAP/RabbitMQ 多实例、容量大表迁移和跨节点配额仍需独立环境验收。实际 SDK 为 8.0.425，锁定 SDK 8.0.424 未验证。

### SQLite 容量迁移实执行（2026-10-08）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `003-consumer-capacity.sqlite.sql`：创建容量表并回填已有未完成收件记录 | `OrderedEventSchemaTest.ConsumerCapacityMigration_WhenExistingInbox_ShouldBackfillPendingCount` | 内存 SQLite 先建立历史表并插入状态 0、1、2，再执行仓库脚本；迁移后 `PendingCount=2` |
| 迁移脚本按 UTF-8 读取且不依赖应用启动自动建表 | 同上 | EventBus 定向测试 net6.0/net8.0 各 3/3；`ordered-schema-migration-net6.trx`、`ordered-schema-migration-net8.trx` |

该映射只覆盖 SQLite 本地脚本的功能语义，不替代 MySQL 大表迁移、备份恢复、停写窗口和生产回滚演练。实际 SDK 为 8.0.425，锁定 SDK 8.0.424 未验证。

新增迁移测试后，`Bing.EventBus.Tests` 全量回归 net6.0/net8.0 各为 358/358，0 失败、0 跳过；这两个数量包含本节 SQLite 迁移测试，不覆盖锁定 SDK 8.0.424 或真实多节点部署。

### 集群指南审计状态收口（2026-10-08）

| 文档事实 | 依据 | 说明 |
| --- | --- | --- |
| 普通审计数据库落库、留存、归档和安全查询已提供实现 | `DbAuditingStore`、`AuditLogArchiveRetentionHostedService`、`DbAuditingStoreMySqlIntegrationTest` | 文档修正复用既有 net6.0/net8.0 MySQL 门控与审计全量回归证据，不新增代码测试 |
| 生产审计保留窗口、备份恢复、跨节点清理协调和长期容量仍需部署验证 | 既有审计门控执行记录及部署指南边界 | 归类为 `BLOCKED_EXTERNAL`，不作为本地实现缺陷 |

### 有序消费者失败流退避（2026-10-08）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `ConsumerWorker.ProcessStreamAsync`：失败状态在退避窗口内不重复执行业务处理，显式恢复后重新处理 | `OrderedEventConsumerTest.HostedWorker_WhenStreamFailed_ShouldBackoffUntilExplicitRetry` | net6.0/net8.0 定向测试各 2/2；状态探测、人工 `RetryAsync` 和真实业务提交均断言 |
| `ConsumerWorker.ProcessStreamAsync`：处理器构造/依赖解析持续失败也进入按流退避 | `OrderedEventConsumerTest.HostedWorker_WhenHandlerConstructionKeepsFailing_ShouldBackoff` | 250ms 内尝试次数受限且业务回调未执行；net6.0/net8.0 定向测试各 2/2 |
| `ConsumerWorker`：失败探测缓存固定上限，满载时使用短暂全局窗口 | `OrderedEventConsumerTest.ConsumerWorker_WhenManyFailuresArriveConcurrently_ShouldBoundProbeCache` | 并发登记 5000 个失败键后缓存不超过 4096 条；`Bing.EventBus.Tests` net6.0/net8.0 各 364/364，未引入静态引用 |

该映射只覆盖单进程退避、人工恢复和资源有界性；跨节点共享退避、重启后的即时唤醒、CAP/RabbitMQ 重投和生产告警仍需部署级验证。实际 SDK 为 8.0.425，锁定的 8.0.424 未验证。

### 消费收据多字节身份边界（2026-10-08）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `EventConsumptionStore.ValidateKey`：首尾空白拒绝，消费者和事件标识按 Unicode 字符数执行上限 | `EventConsumptionStoreTest.TryRecordAsync_NonBmpAndUtf8Boundary_ShouldKeepOrdinalIdentity`、`TryRecordAsync_InvalidKeyOrCancellation_ShouldNotWriteReceipt` | net6.0/net8.0 定向测试各 12/12；非 BMP、200/128 多字节边界和超限拒绝均断言 |
| `EventConsumptionStore.TryRecordAsync`：不同非 BMP 身份不发生收据合并 | `EventConsumptionStoreTest.TryRecordAsync_NonBmpAndUtf8Boundary_ShouldKeepOrdinalIdentity` | SQLite `COLLATE BINARY` 真实事务插入，两个非 BMP 消费者均成功登记 |

该映射覆盖 API 和 SQLite 身份边界；MySQL `utf8mb4_bin`、旧表字符集迁移和多实例唯一键竞争仍属于专用数据库门控，不由本地 SQLite 测试替代。

### CAP 事务状态同步边界（2026-10-08）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `CapOrderedEventTransactionFactory.TransactionScope`：提交完成后同步发布 `Outcome` 与结束状态，禁止旧 ambient 再次激活 | `CapOrderedEventTransactionFactoryTest.ActivateAmbient_WhenCommitCompleted_ShouldRejectReactivation` | 隔离测试工程 net6.0 定向运行 23/23，0 失败、0 跳过；生产 `Bing.Events` 源码编译 0 警告、0 错误，结果文件为临时工程 `TestResults/cap-state-sync-isolated.trx`（临时工程已清理） |
| `TransactionScope.Outcome`：并发读取与结束检查使用同一 `_gate` 同步边界 | 同上 | 源码审查确认 `_outcome`、`_finished` 通过 `SetOutcome` 原子地更新，未将 CAP broker 确认或跨节点恢复误算为本地证据 |

该映射只覆盖进程内状态可见性和重复激活保护，不能替代 RabbitMQ broker 确认、提交断连、跨节点排空和真实 MySQL/CAP 验收。实际 SDK 为 8.0.425，锁定 SDK 8.0.424 未验证。

### 有序消费者失败探测缓存满载保护（2026-10-08）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `ConsumerWorker.ProcessStreamAsync`：失败探测缓存达到上限时，先查询持久化状态，避免未知流因缓存溢出反复执行业务 | `OrderedEventConsumerTest.HostedWorker_WhenFailureProbeCacheIsFull_ShouldProbeStateBeforeRetryingBusiness` | 隔离测试工程 net6.0 1/1 通过；4096 条失败探测记录已填满，额外流的业务处理只调用一次 |
| `ConsumerWorker.IsFailedStreamOverflowProbeDue` / `ScheduleNextOverflowProbeIfNeeded`：缓存满载时使用短窗口限制状态探测和失败登记 | 同上及同文件回归 | 有序消费者测试 net6.0 24/24 通过；真实 SQLite 状态存储和 Hosted Worker 路径通过；生产 `Bing.Events` 源码编译 0 警告、0 错误 |

该映射只证明单进程缓存满载保护，不覆盖跨节点退避、进程重启恢复、RabbitMQ 重投或生产负载调优；实际 SDK 为 8.0.425，锁定 SDK 8.0.424 未验证。

### 有序消费者缺口告警节流（2026-10-08）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `ConsumerWorker.ShouldLogGap`：同一业务流在 30 秒内只允许一次缺口告警，恢复或非缺口结果会清除状态 | `OrderedEventConsumerTest.ConsumerWorker_WhenGapRepeats_ShouldThrottleAndBoundWarnings` | 首次告警、重复抑制和清除后重新告警均直接断言 |
| `ConsumerWorker` 缺口告警缓存：最多保留 4096 条，满载后使用全局节流窗口 | 同上 | 隔离有序消费者测试 net6.0 25/25 通过；5000 个不同缺口键后缓存仍不超过 4096 条；生产 `Bing.Events` 源码编译 0 警告、0 错误 |

该映射只覆盖单进程日志诊断的有界性，不代表跨节点日志聚合、缺口告警导出或序号恢复已经完成；实际 SDK 为 8.0.425，锁定 SDK 8.0.424 未验证。

### CAP 消息头总大小边界（2026-10-08）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `ClusterEventHeaders.Read`：识别事件标识后统计全部头名称和值的 UTF-8 字节数，超过 16 KiB 拒绝解析 | `ClusterEventHeadersTest.Read_WhenHeaderCollectionExceedsByteLimit_ShouldReject` | 隔离头部契约测试 net8.0 16/16 通过；700 个未知扩展头超过上限并返回 `FormatException` |
| `ClusterEventHeaders.Write`：写入已知元数据后统计预置未知头，超过 16 KiB 拒绝发布 | `ClusterEventHeadersTest.Write_WhenHeaderCollectionExceedsByteLimit_ShouldReject` | 最小 net6.0 隔离运行器通过写入拒绝、读取拒绝和有效往返；标准 xUnit 定向回归待在共享工程可还原后执行 |
| `ClusterEventHeaders.Read`：有效头及旧消息无 Bing 事件标识的行为保持兼容 | `ClusterEventHeadersTest.PublishAsync_OrderedEvent_ShouldWriteMetadataOnce`、`Read_LegacyAndInvalidOrderedHeaders_ShouldHandleExplicitly` | 同一隔离测试程序集通过；独立头部运行检查输出 `Header checks passed.` |

该映射只覆盖进程内远端头解析边界，不代表 broker、网关或签名层的总头大小策略已经统一；契约版本兼容、来源认证和敏感字段脱敏仍待后续设计与部署验证。

### 有序事件归档保留调度

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `OrderedEventRetentionScheduleOptions.ArchiveRetentionPeriod` 与 `ArchiveConsumers`：两项同时配置才启用归档清理，归档专用配置不要求收件箱保留期限 | `OrderedEventRetentionHostedServiceTest.Scheduler_WhenArchiveRetentionIsConfigured_ShouldDeleteArchivedRecords`、`Scheduler_WhenArchiveOnly_ShouldIgnoreProcessedRetentionPeriod`、`Scheduler_WhenArchiveConfigurationIsIncomplete_ShouldRejectConfiguration`、`Scheduler_WhenArchiveConsumersAreMissing_ShouldRejectConfiguration` | net6.0 隔离精准测试 4/4 通过，覆盖有效配置、归档专用配置和两种不完整配置；SDK 8.0.425 |
| `OrderedEventRetentionHostedService`：同一轮按归档期限和消费者调用 `DeleteArchivedBeforeAsync`，不删除待处理或失败记录 | `OrderedEventRetentionHostedServiceTest.Scheduler_WhenArchiveRetentionIsConfigured_ShouldDeleteArchivedRecords` | 最小 net6.0 隔离工程实际 SQLite 运行通过，输出 `Archive retention check passed.`；SDK 8.0.425 |

该映射只证明显式配置的单进程调度和 SQLite 行为，不替代生产 MySQL 大表、归档备份、重放窗口、跨节点租约或容量治理验收。

### Admin MySQL/RabbitMQ 单实例集群门控（2026-10-08）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| Admin EFCore CAP/库存链路：重复请求、跨作用域竞争、双 SKU 原子扣减、关键审计回滚、进程中断、提交确认丢失、消费取消和有序乱序 | `AdminMySqlCapIntegrationTest`、`OrderedEventCapIntegrationTest` 脚本声明的 21 个必需方法 | 专用 Docker MySQL 8.4 + RabbitMQ 3.13，EFCore net6.0 `34/34`，0 失败、0 跳过；TRX 为 `artifacts/admin-mysqlcap-results/rabbit-20261008/net6.0-20261008T072313184Z/admin-efcore-mysqlcap.trx` |
| Admin FreeSQL 同一 CAP/库存链路和故障矩阵 | 同上 FreeSQL 测试程序集的 21 个必需方法 | 同一专用环境，FreeSQL net6.0 `33/33`，0 失败、0 跳过；TRX 为 `artifacts/admin-mysqlcap-results/rabbit-20261008/net6.0-20261008T072313184Z/admin-freesql-mysqlcap.trx` |
| 恢复授权、恢复审计和审计查询不跨租户泄露 | `AdminOrderedEventRecoveryTest`、`AdminAuditLogQueryTest` | EFCore/FreeSQL 定向单元测试各 `19/19`；TRX 位于 `artifacts/admin-mysqlcap-results/rabbit-20261008/recovery-unit/` |

该映射证明专用单实例真实数据库和消息代理链路，不证明生产网络分区、broker 确认丢失的真实传输形状、多实例竞争、RabbitMQ 集群故障、Redis 故障切换或跨节点蓝绿验收；实际 SDK 为 8.0.425，锁定 SDK 8.0.424 未验证。

### Redis 限流与分布式锁单实例门控（2026-10-08）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| `RedisTokenBucketRateLimiter`：独立服务提供程序、独立进程和同键并发共享同一策略额度；闲置键回收、配置冲突、取消和同步/异步入口保持一致 | `RedisTokenBucketRateLimiterTest.TryAcquireAsync_MultipleInstances_ShouldShareQuota`、`TryAcquireAsync_IndependentProviders_ShouldShareConcurrentQuota`、`TryAcquireAsync_TwoIndependentProcesses_ShouldShareCapacity`、同类回归 | 现有 Redis 5.0.12 容器，net6.0/net8.0 各 22/22，0 失败、0 跳过；TRX 位于 `framework/tests/Bing.Caching.CSRedis.Tests.Integration/TestResults/redis-local-net6/redis-lock-rate-limit.trx` 与 `redis-local-net8/redis-lock-rate-limit.trx` |
| `CSRedisDistributedLock`：租期、错误所有者释放、过期释放、同步/异步异常和释放失败保留原始诊断 | `RedisDistributedLockTest` 全部定向用例 | 同上；真实 Redis 脚本路径通过，实际 SDK 8.0.425 |

该映射只证明单实例 Redis 的原子脚本和跨进程共享行为，不替代主从故障切换、脑裂、fencing token、时钟漂移或生产多节点容量验收；锁不承担库存最终正确性。

### Admin 宿主 HTTP/Redis 限流链路（2026-10-08）

| 生产符号与关键行为 | 直接测试方法 | 证据 |
| --- | --- | --- |
| 两套 `AppModule.AddServices` 注册及 `UseBingRateLimiting` 管道：真实 Redis 首次请求通过、同一客户端随后按策略拒绝并返回 `Retry-After` | `AdminRateLimitingHttpPipelineTest.AppModule_WhenRedisIsAvailable_ShouldLimitThroughHttpPipeline` | EFCore/FreeSQL net6.0 各 `3/3` 定向测试通过；实际 Redis 5.0.12；TRX 位于各测试项目 `TestResults/rate-http-net6-v2/admin-rate-http-v2.trx` |
| Admin 宿主在真实 Redis 连接失败或限流器抛出基础设施异常时默认失败闭合 | `AdminRateLimitingHttpPipelineTest.AppModule_WhenRedisIsUnavailable_ShouldReturnUnavailable`、`AppModule_WhenLimiterFails_ShouldReturnUnavailable` | 同上；无监听端口和替身两条路径均断言 HTTP `503`，与框架 TestServer 的 FailOpen/FailClosed 直接测试组合 |

该映射覆盖宿主注册与请求管道组合，不证明真实 Redis 连接中断、多实例故障切换、进程重启恢复或生产降级策略；这些仍需专用故障环境。
