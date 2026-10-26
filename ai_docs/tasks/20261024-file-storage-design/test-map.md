# 生产符号 → 测试方法

下表使用真实生产符号及实际测试方法。Storage 测试在 net6.0/net8.0；业务 Theory 的 false/true 分别使用真实 EF Core/FreeSql SQLite，MinIO 用专用隔离端点。Admin 控制器测试为 net6.0。

| 生产符号 / 行为 | 测试项目 | 对应测试方法 |
|---|---|---|
| StorageScope / FileAddress / FileStorageNames 范围、对象键 | Bing.FileStorage.Tests | RejectsUnsafeObjectKeys；ScopeIsolation_AndLongUnicodeTenant |
| IFileStorageFactory / AddBingFileStorage 命名实例 | Bing.FileStorage.Tests | Factory_KeepsOldStore_AndRejectsUnconfiguredContainer |
| FileSystemStorage.SaveAsync / ReadAsync / GetPropertiesAsync / DeleteAsync | Bing.FileStorage.Tests | Save_Read_Metadata_StreamOwnership_AndDelete |
| FileStorageStreams.CopyExactAsync / BoundedReadStream | Bing.FileStorage.Tests | InvalidLength_DoesNotPublishPartialOrOverwrite；Save_Read_Metadata_StreamOwnership_AndDelete |
| FileSystem 原子 CreateOnly、取消与容量 | Bing.FileStorage.Tests | CreateOnly_IsAtomicUnderConcurrency；CancelledWrite_PreservesExistingContent；CapacityFailure_IsEarly_AndDoesNotPublish |
| LocalPaths.CheckLinks | Bing.FileStorage.Tests | LinkedRoot_IsRejected |
| FileSystemStorage.ReadRangeAsync | Bing.FileStorage.Tests | Range_UsesContentOffset_AndBounds |
| FileSystemStorage multipart 持久清单、冻结、摘要、中止 | Bing.FileStorage.Tests | Multipart_Restart_RepeatComplete_AndLatePart；Multipart_Checksum_ETag_Abort_AndHandleIsolation；EmptyMultipart_CanCompleteAndRetry；InFlightPart_CannotReplaceFrozenCompletion |
| FileSystemStorageMaintenance.CleanupTemporaryFiles | Bing.FileStorage.Tests | OfflineCleanup_Previews_AndPreservesObjectsAndRecentTemporaryFiles |
| S3FileStorage Save/Read/Range/Delete/分片分页及签名；Minio 注册 | Bing.FileStorage.S3.Tests | Minio_BasicMultipartRangeAndSignedRequests_RoundTrip |
| S3FileStorage.CompleteMultipartAsync / SignPartAsync | Bing.FileStorage.S3.Tests | Complete_ValidatesETag_IsIdempotent_AndRejectsLateParts；PresignedPart_Expires_AndCannotChangeChecksum |
| FileManagementService.CreateUploadAsync / GetSessionAsync / CompleteUploadAsync | Bing.Biz.FileManagement.Tests | Upload_Restart_RepeatComplete_AndDelete；Resume_UsesBackendParts_AndRejectsDifferentContent |
| FileUploadSession.BackendMetadataJson / FileModelExtensions.Upload / 空文件 | Bing.Biz.FileManagement.Tests | PersistedSession_EmptyOrDirectUpload_CompletesWithoutRetry（真实 MinIO、两 ORM、0/37 字节） |
| FileManagementService.CreatePartRequestAsync | Bing.Biz.FileManagement.Tests | PartRequest_RejectsNullChecksum；Resume_UsesBackendParts_AndRejectsDifferentContent |
| FileManagementService.AuthorizeReadAsync / ScopedAsync / OwnedSessionAsync | Bing.Biz.FileManagement.Tests | CrossTenant_AndSessionOwner_AreIsolated；PublicFiles_RequirePublish_AndSnapshotVisibility |
| IFileAccessAuthorizer / BindAsync / UnbindAsync / DeleteAsync | Bing.Biz.FileManagement.Tests | Binding_DefaultDeny_ProtectsDelete_AndBoundRead |
| FileOperationProcessor 初始化/完成/到期/删除及 DB 重试 | Bing.Biz.FileManagement.Tests | ExpiredSession_CleansParts_ButReadyUnboundSurvives；CompletionResponseLost_ReconcilesByLocationAndSessionMetadata；Completion_DatabaseFailureAfterObjectCommit_RemainsRecoverable |
| FileMigrationService.QueueMigrationAsync / ExecuteMigrationAsync / DeleteReplicaAsync | Bing.Biz.FileManagement.Tests | Migration_VerifiesContent_ChangesLocation_AndRetainsDeletionState；Migration_LosingCommit_DoesNotDeleteWinnersContent；Migration_DatabaseFailureAfterCopy_RetriesWithoutChangingSource |
| FileMigrationService.ImportAsync | Bing.Biz.FileManagement.Tests | LegacyImport_Previews_InvalidAddress_AndStableGuid；真实 CLI 双 ORM 导入及 MinIO verify |
| EfFileManagementStore.TryCreateAsync / TryUpdateAsync | Bing.Biz.FileManagement.Tests | VersionConflict_RollsBackPartsAndOperations；DuplicateRequest_DoesNotDuplicateChildren |
| EfFileManagementStore.TryClaimAsync / RenewLeaseAsync / FinishOperationAsync | Bing.Biz.FileManagement.Tests | Lease_OneWinner_ExpiredTakeover_AndOldTokenCannotFinish |
| FreeSqlFileManagementStore create/find/update/parts/bindings | Bing.Biz.FileManagement.FreeSql.Tests | CreateAndFind_AreAtomicAndRequestKeyIsIdempotent；Update_CasReplacesBindingsUpsertsPartsAndRollsBackStaleChildren |
| FreeSqlFileManagementStore 列表与任务租约 | Bing.Biz.FileManagement.FreeSql.Tests | Lists_FilterScopeOwnerExpiryAndRetryability；LeaseClaimRenewFinish_UsesVersionAndCurrentToken |
| FreeSql import / 注册生命周期 / 事务 | Bing.Biz.FileManagement.FreeSql.Tests | ImportUpsert_DoesNotRewriteImportedRecord；Registration_BorrowsClientAndDoesNotSynchronizeSchema；StoreTransaction_CommitsBeforeUnrelatedUnitOfWorkRollsBack |
| 两 ORM GetStatisticsAsync | Bing.Biz.FileManagement.Tests | Upload_Restart_RepeatComplete_AndDelete（断言活跃会话与清理积压） |
| FileManagementControllerBase / StoredFileResult / 签名 token / 异常映射 | Bing.Biz.FileManagement.Tests | RealHttp_Upload_Download_Range_Signing_AndAuthorization |
| 两 Admin.Apis.Commons.FileController + SQLite | Bing.Admin.EFCore.Tests / Bing.Admin.FreeSQL.Tests | RealAdminController_Upload_Bind_Download_Range_TenantIsolation_AndDelete |
| 两 Admin 控制器 + MySQL 8.4 + CAS/抢占 | 同上 | RealAdminController_MySql_Upload_Bind_Download_Range_TenantIsolation_Cas_Claim_AndDelete |
| DefaultFileUploadService.SaveWithMd5Async | Bing.FileStorage.AspNetCore.Tests | SaveWithMd5_HashesWrittenBytes（abc 和空内容） |
| 浏览器 uploader.js | 独立 Chrome / Playwright | 暂停、刷新、服务重启、签名刷新、网络中断恢复；见 browser-evidence.md |
| 分片缓冲与大文件流 | Bing.FileStorage.CapacityProbe | Local/MinIO 各 1 GiB 和 100 GiB；见 capacity-evidence.md |

数据库故障测试只在真实 SQLite 提交边界注入故障，内容仍经过真实 FileSystem；没有用 Mock 代替真实上传链路。不会把测试中的故障注入代理注册到产品。

未进行电源断电级磁盘耐久性测试；磁盘不足验证为真实磁盘剩余容量的预检拒绝，不人为填满系统磁盘。其他云厂商与多节点 Local 不在本次支持范围。

## Review 修复 Round 1

| FIX / 生产符号与行为 | 测试项目 | 测试方法（两 ORM、net6/net8） |
|---|---|---|
| FIX-001：FileOperationProcessor.RunBatchAsync；两仓储 TryUpdateAsync/FinishOperationAsync 租约条件 | Bing.Biz.FileManagement.Tests | LostLease_CannotCommitReadyOrFailNewOwnersFile（完成、永久异常、重试耗尽）；ExpiredLeaseWithoutTakeover_CannotCommitBusinessOrFailure |
| FIX-003：两仓储 FinishOperationAsync 原子失败与 Abort 排队 | 同上 | TerminalFailure_CommitFailureOrLostResponse_RemainsAtomic；TerminalFailure_InsideTransactionErrorOrRootConflict_RollsBackWholeCommit（真实 SQLite trigger，Initialize/Complete） |
| FIX-005：FileOperationProcessor 完成对账及 Local 幂等收尾 | 同上 | LocalCommittedObject_ReconcilesManifestAndRemovesParts（completing / completed 清单） |
| FIX-002：FileMigrationService.ExecuteMigrationAsync；FileTransferCoordinator；原子发布 | 同上 | Migration_LateWriter_CannotOverwriteWinnerOrResurrectDeleted；MinioMigration_LateWriter_CannotOverwriteWinnerOrResurrectDeleted（真实固定镜像） |
| FIX-004：AdminFilePermissionChecker；生产 AddJwt/SecurityService.SignInAsync；两个实际 FileController | Bing.Admin.EFCore.Tests / Bing.Admin.FreeSQL.Tests | AdminLoginToken_EnforcesFilePermissionsAndPublicRoute；DisabledFilePermissionModule_RejectsPermission |
| FIX-004：IAdminFilePermissionResourceInitializer；AddBingAdminFileStoragePermissions | 同上 | PermissionResourceInitializer_IsExplicitAndIdempotent |
| 新仓储租约/复制发布映射，MySQL 8.4 | 同上 | RealAdminController_MySql_Upload_Bind_Download_Range_TenantIsolation_Cas_Claim_AndDelete（增加当前/旧 lease、根 CAS 回滚、发布/退休互斥断言） |
| FIX-006：FileMigrationService.ImportAsync；两仓储 TryCreateAsync 带 transfer | 同上 | Import_FaultsAndRestart_KeepOnePublishedObject（提交前、响应丢失、重启、摘要错误） |
| FIX-002/006：两仓储 FileTransferAttempt CRUD/CAS；清理和监控 | 同上 | Transfer_RetirementAndPublicationAreExclusive_RootConflictRollsBack；UnknownWriter_RetiredTargetIsRecheckedAfterLateWrite |

历史 review-probe 是修复前源码/观察证据，保留不变；修复后的可执行回归位于上述正式测试类。故障测试只在调度、提交或 SQLite trigger 边界注入，内容与元数据仍执行真实 IO。

## Review 修复 Round 2

| FIX / 生产符号与行为 | 测试项目 | 测试方法 |
|---|---|---|
| FIX-007：FileOperationProcessor.CompleteEmptyAsync；FileTransferCoordinator；两 ORM 条件发布、旧执行者写入回收 | Bing.Biz.FileManagement.Tests（net6/net8，两 ORM） | EmptyCompletion_LateWriter_ProtectsReadyAndDeleted；MinioEmptyCompletion_LateWriter_ProtectsReadyAndDeleted |
| FIX-007：空文件写入成功后的数据库提交前失败、提交响应丢失、清理失败后重启 | 同上 | EmptyCompletion_DatabaseFaults_RecoverAfterRestart；MinioEmptyCompletion_DatabaseFaults_RecoverAfterRestart |
| FIX-001/007：失去任务租约后拒绝发布、回收独立空对象且保留接管者状态 | 同上 | LostLease_CannotCommitReadyOrFailNewOwnersFile |
| FIX-008：SecurityService.AddRoleClaimsAsync；两套 RoleRepository.GetRolesAsync 实际角色查询及新 JWT 文件授权 | Bing.Admin.EFCore.Tests / Bing.Admin.FreeSQL.Tests（net6） | AdminLoginToken_EnforcesFilePermissionsAndPublicRoute（角色与关联真实 SQLite，启用/禁用混合角色、四项文件权限、拒绝优先） |

MinIO 新增用例运行固定摘要镜像并执行真实 Put/Get/Delete；两 ORM 使用实际 SQLite 文件。仅在 IO 调度及数据库调用返回边界注入故障，不替换内容存储或仓储提交实现。

## 分片查询与任务调度优化（2026-10-25）

| 生产符号与关键行为 | 测试项目 | 直接测试方法 |
|---|---|---|
| IFileManagementStore.FindPartAsync；EfFileManagementStore / FreeSqlFileManagementStore 联合主键查询、快照、取消、6400 片规模 | Bing.Biz.FileManagement.Tests（net6/net8，两 ORM） | PartLookupStoreTests.FindPartAsync_UsesCompositeKeyAndMaterializesOnlyTheMatch |
| FileManagementService.CreatePartRequestAsync / UploadPartAsync 单条查询、重复摘要/冲突、鉴权、输入流、CAS 重试 | 同上 | PartLookupServiceTests.PartLookup_RegistrationAndUpload_UseOnePartAndPreserveContracts；PartLookup_CasRetry_RequeriesWithoutFullList |
| 两 ORM ListDueOperationsAsync 的 FileOperationQueue 重载，数据库分类后排序/限量、到期/租约、非法/零数量/取消；旧重载兼容 | 同上 | OperationQueueStoreTests.ListDueOperationsAsync_FiltersQueuesBeforeOrderingAndLimit |
| FileOperationScheduler.RunAsync；FileOperationProcessor.RunNextAsync / RunMaintenanceAsync / RenewAsync；真实 Web HostedService | 同上 | OperationSchedulingTests.LongTask_DoesNotBlockNewShortTasksOrMaintenance_AndRenewsLease |
| 固定执行槽、每项独立作用域、未领取任务等待、重复启动拒绝、实际并行上限 | 同上 | OperationSchedulingTests.BothQueues_RespectConfiguredBounds_WithoutPreclaimingWaitingWork |
| 调度停止取消、作用域释放、Running 租约恢复、独立目标发布及旧目标回收凭据 | 同上 | OperationSchedulingTests.Cancellation_ReleasesScopes_AndExpiredLeaseCompletesAfterRestart |
| 多调度器竞争、数据库查询故障后的有限频率轮询、单个 token 胜者 | 同上 | OperationSchedulingTests.CompetingSchedulers_AndQueryFailure_KeepOneLeaseWinner |
| FileManagementOptions / AddBingFileManagement 并发与轮询配置校验 | 同上 | OperationSchedulingTests.Configuration_RejectsUnboundedOrBusyPolling |
| 新 HostedService + 实际 MinIO 分片签名上传、完成、删除 | 同上，固定 MinIO 镜像 | OperationSchedulingTests.Minio_HostedScheduler_CompletesSignedUploadAndDeletion |

本轮重新运行原 ReviewRecoveryTests、EmptyCompletionRecoveryTests、TransferRecoveryTests 等受影响完整业务项目，验证提取后的单任务执行核心没有改变租约/失败/恢复语义；两 Admin 文件 HTTP 与独立 FreeSQL 仓储测试验证调用方。6400 片测试仅生成元数据，通过实际 SQL 与物化结果比较查询规模，不据此宣称端到端吞吐提升。

## FIX-009：可见文件分页

| 生产符号与行为 | 测试项目 | 直接测试方法 |
|---|---|---|
| EF/FreeSQL ListReadyFilesAsync：SQL 先过滤 Ready/范围/创建者，稳定分页、快照、参数/取消；ListFilesAsync 全状态兼容 | Bing.Biz.FileManagement.Tests（net6/net8，两 ORM SQLite） | FileListStoreTests.ListReadyFilesAsync_FiltersBeforePagingAndPreservesStoreContracts |
| FileManagementService.ListAsync：50 条 Aborted/Deleted/Failed 不隐藏真实已上传文件 | 同上 | FileListPaginationTests.List_TerminalRecordsDoNotHideUploadedFile |
| ListAsync：权限过滤后计算 skip/take，跨多批拒绝继续查找，不漏项/重复，保留参数归一化 | 同上 | List_VisibleOffsetCrossesDeniedBatchesWithoutGapsOrDuplicates |
| ListAsync：普通创建者范围、显式 manage 权限、租户隔离、匿名拒绝 | 同上 | List_PreservesCreatorTenantAndExplicitManageBoundaries |
| ListAsync：扫描中取消传播；不能吞掉业务授权基础设施异常 | 同上 | List_PropagatesCancellationAndUnexpectedAuthorizationFailures |
| FileManagementControllerBase.List：JSON 数组、默认首页、可见偏移、403/401 与跨租户 | 同上 | HttpList_KeepsArrayContractAndUsesVisibleOffsets |
| 两 Admin 实际 FileController 继承链：真实上传后被 50 条终态记录遮挡的首页/末页 | Bing.Admin.EFCore.Tests / Bing.Admin.FreeSQL.Tests（net6，SQLite） | AdminFileControllerHttpTests.RealAdminController_Upload_Bind_Download_Range_TenantIsolation_AndDelete（AssertVisiblePaginationAsync） |

本轮证据见 [fix-pagination](evidence/fix-pagination/README.md)。本修复不改变 Provider IO、任务调度、HTTP 路由/返回形状、数据结构和依赖版本；示例相同无参数列表请求由 HTTP 回归覆盖，不重建前端分页 UI。

## 文件管理增强能力

| 生产符号 / 行为 | 测试项目 | 对应测试方法 |
|---|---|---|
| `IFileManagementStore.QueryFilesAsync` / `QuerySessionsAsync`、业务绑定查询及 SQL 过滤后的键集分页 | Bing.Biz.FileManagement.Tests（net6/net8，两 ORM SQLite） | `EnhancementQueryTests.BusinessQueries_ExposeOnlyAuthorizedBindingsAndDiscoverOtherOwners`；`Search_UsesKeysetAndFiltersWithoutDuplicateOrUnauthorizedRows`；`SessionDiscovery_IsScopedAndSurvivesRestartWithoutBackendReads` |
| `FileOperationAdministration.RecoverAsync` / `FileMigrationBatchService`：唯一后继、审计、批次发现、暂停、冲突和原子恢复 | Bing.Biz.FileManagement.Tests（net6/net8，两 ORM SQLite） | `EnhancementWorkflowTests.FailedDelete_RecoversOnceWithAuditAndPreservedHistory`；`Recovery_RejectsFailedUploadAndNeverRevivesDeletedRoot`；`BatchMigration_PauseRestartAndIdempotentDiscovery_PreserveActualContents`；`BatchQueue_RejectsConcurrentDeletionWithoutResurrection`；`BatchRecovery_AtomicallyFollowsSuccessorEvenWhenCallerStopsBeforeUpdatingItem` |
| 上传额度预留、共享 IO 租约、Local 分片槽、调度准入与 Outbox | Bing.Biz.FileManagement.Tests（net6/net8，两 ORM SQLite） | `EnhancementResourceTests.Admission_ReservesAcrossRestartAndReleasesOnlyAfterCleanup`；`Admission_ConcurrentCreatesNeverOverbookAndScopesRemainIndependent`；`ResourceSlots_AreSharedBoundedAndTokenFenced`；`Outbox_IsAtomicWithStateAndReplaysSameEventAfterRestart`；`LocalPartSlot_RejectsBeforeReadingAndAllowsRetryAfterRelease`；`Scheduler_SkipsStoreWithoutCapacityAndDoesNotSpendItsAttempts` |
| `FileContentInspector`：策略快照、文件头、ClamAV INSTREAM 和隔离失败关闭 | Bing.Biz.FileManagement.Tests（net6/net8；真实 clamd 用例受专用环境门控） | `EnhancementContentTests.ContentPolicy_FreezesAtCreationAndQuarantinesUntilActualHeaderPasses`；`HeaderRejection_FailsClosedAndCleansActualObject`；`ClamProtocol_StreamsExactBytesAndRequiresExplicitCleanResponse`；`ScannerUnavailable_KeepsFileQuarantinedAndDoesNotEmitReady`；`RealClamAv_CleanAndEicar_ArePublishedAndRejectedRespectively` |
| 增强 HTTP 查询游标、管理路由、额度响应与健康检查 | Bing.Biz.FileManagement.Tests（net6/net8，两 ORM SQLite） | `EnhancementHttpTests.SearchCursor_IsBoundToUserTenantAndFilters_AndSessionsAreDiscoverable`；`AdministrativeRoutes_RequirePermissionScopeAndVersion_AndHidePayload`；`CapacityReturns429_AndHealthDetectsMissingProbeAndStoppedWorker` |
| EF Core / FreeSql SQLite 基础 schema 到增强 schema 的回填与 Store 读写 | Bing.Biz.FileManagement.Tests（net6/net8） | `EnhancementSchemaUpgradeTests.EfCoreSqliteUpgrade_BackfillsLegacyDataAndSupportsNewReservations`；`FreeSqlSqliteUpgrade_BackfillsLegacyDataAndSupportsNewReservations` |
| EF Core / FreeSql Admin 随机 MySQL 8 数据库增强迁移、审计/批次表及新上传 | Bing.Admin.EFCore.Tests / Bing.Admin.FreeSQL.Tests（net6，MySQL 环境门控） | `AdminFileEnhancementUpgradeTests.MySql8EnhancementUpgrade_BackfillsLegacyRowsAndSupportsNewOperations` |

上述表仅记录生产符号与直接测试方法的对应关系，不表达本轮 MySQL 集成测试的运行结果。该 Admin 测试源文件须由两个 Admin 测试项目链接，且只有设置隔离 MySQL 测试连接串后才会执行。

## FIX-010：暂停迁移批次的进度对账

| 生产符号 / 行为 | 测试项目 | 直接测试方法 |
|---|---|---|
| `IFileManagementStore.ListBatchItemsToReconcileAsync`、两 ORM 实现：SQL 先筛选 Queued + Running/Paused + 关联终态操作，再稳定限量；不受 1005 条执行中记录遮挡；快照、取消与参数检查 | Bing.Biz.FileManagement.Tests（net6/net8，两 ORM SQLite） | `BatchReconciliationStoreTests.ReconcileQuery_FiltersBeforeLimitAndReturnsOnlyMatchingTerminalOperationsInStableOrder` |
| `FileMigrationBatchService.AdvanceAsync`：暂停期间已入队任务成功/永久失败及重启后对账；不发现或安排新条目；真实目标内容正确 | 同上 | `BatchMigrationPauseTests.PausedBatch_ReconcilesTerminalOperationAfterRestart_WithoutDiscoveryOrQueueing` |
| `CompleteIfFinishedAsync`：完成检查与暂停/恢复版本竞争，不覆盖并发管理状态 | 同上 | `BatchMigrationPauseTests.Reconciliation_PreservesConcurrentPauseAndResume` |
| 对账与安排使用独立候选配额，空 Paused 批次不阻止 Running 批次 | 同上 | `BatchMigrationPauseTests.EmptyPausedBatches_DoNotBlockRunningBatchScheduling` |
| HTTP 查询暂停批次与条目显示真实结果，跨租户仍拒绝 | 同上 | `EnhancementHttpTests.PausedMigrationBatch_ReportsCompletedItemsThroughHttp` |
| 两 Admin 实际控制器链、两 ORM SQLite/MySQL：任务 Pending 不进入对账候选、Succeeded 进入候选；暂停进度更新后恢复完成 | Bing.Admin.EFCore.Tests / Bing.Admin.FreeSQL.Tests（net6） | `AdminFileControllerHttpTests.RealAdminController_Upload_Bind_Download_Range_TenantIsolation_AndDelete`、`RealAdminController_MySql_Upload_Bind_Download_Range_TenantIsolation_Cas_Claim_AndDelete` 中的 `AssertEnhancementTransactionsAsync` |
| CLI `batch-status` 独立进程读取暂停结果，与服务读取同一持久状态 | 独立 net8 探针调用 net6 CLI，两 ORM SQLite | [Program.cs](evidence/fix-paused-batches/Program.cs) |

验证范围与实际结果见 [FIX-010 证据](evidence/fix-paused-batches/README.md)。本轮不改变内容 Provider、数据库 schema 或 HTTP 返回结构。

## Admin 业务与宿主接入（2026-10-25）

用户资料授权、认证租户、审计消费、运维接口与部署命令的当前映射见 [host-integration/test-map.md](evidence/host-integration/test-map.md)。本轮不改变已有 Provider 或核心上传状态符号。
