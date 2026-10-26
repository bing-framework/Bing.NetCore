# 最终生产符号 → 测试方法

本任务只验证文件存储影响范围；未修改 Bing.Data.Sql、数据库 Schema 或 ORM 生产实现。下列 SQLite 测试通过真实 EF Core / FreeSql IO 验证。RecordingS3Client 只替代远端 SDK 边界，不构成七牛兼容性证据。

| 生产符号 / 行为 | 测试项目及方法 |
|---|---|
| AddBingQiniuStorage / BingS3StorageOptions.Snapshot；默认代理、HTTPS/区域/密钥/空间校验、不可变映射 | Qiniu.Tests：Registration_DefaultsToProxy_AndSnapshotsBucketMappings、Registration_RejectsInvalidConfiguration；两 Admin：AdminQiniuStorageRegistrationTests.* |
| S3FileStorage.Bucket / ObjectKey；所有对象及分片请求、签名统一映射，逻辑容器及租户隔离 | Qiniu.Tests：Proxy_SaveReadRangeEmptyDelete_UseMappedBucketAndLogicalKeys、Proxy_MultipartPaginates_ValidatesReceipts_RecoversLostCompletion、SharedBucket_PreservesTenantIsolation |
| S3FileStorage.UploadPartAsync / SaveEmptyAsync；真实 SDK SigV4、普通载荷、非 Seek 与空流、长度及所有权 | Qiniu.Tests：RealSdk_ProxyUpload_SendsSignedPayloadWithoutChunkedOrOptionalChecksum(false/true)、Proxy_RejectsWrongLengthChecksumCancellationAndSigning、Proxy_SaveReadRangeEmptyDelete_UseMappedBucketAndLogicalKeys |
| S3FileStorage.EnsureUploadRequest / SignPartAsync；持久化模式不允许静默切换，代理不签发直传 | Qiniu.Tests：SignedMode_RetainsRemoteChecksum_AndRejectsModeSwitch、Proxy_RejectsWrongLengthChecksumCancellationAndSigning |
| S3FileStorage.ListPartsAsync / CompleteMultipartAsync；分页、清单比对、丢响应、NoSuchUpload、重复完成 | Qiniu.Tests：Proxy_MultipartPaginates_ValidatesReceipts_RecoversLostCompletion；S3.Tests：MinioRecoveryTests.Complete_ValidatesETag_IsIdempotent_AndRejectsLateParts |
| FileManagementService.CreatePartRequestAsync / GetSessionAsync；重复登记保留回执、恢复缺回执需重传 | FileManagement.Tests：ProxyUploadTests.Receipt_SurvivesReregistrationRestartAndCompletesWithoutRemoteChecksum、MissingReceiptOrChangedRemotePart_IsNotResumableAndCannotComplete |
| FileManagementService.SavePartReceiptAsync；旧响应、CAS 竞争、数据库失败、过期、冻结/中止后禁止写回 | FileManagement.Tests：ProxyUploadTests.OldUploadResponse_CannotOverwriteNewerReceipt、ReceiptCasConflict_RechecksRemoteVersion、FailedReceiptTransaction_LeavesPartUnconfirmedUntilReupload、ExpiredDuringTransfer_PreventsReceiptCommit、StateChangesDuringTransfer_PreventReceiptCommit |
| FileManagementService.ConfirmPartsAsync；必须匹配可信回执与远端 ETag/长度 | FileManagement.Tests：ProxyUploadTests.MissingReceiptOrChangedRemotePart_IsNotResumableAndCannotComplete、Receipt_SurvivesReregistrationRestartAndCompletesWithoutRemoteChecksum |
| FileOperationProcessor.ExecuteAsync / RequiresCompletionCleanup；对象已提交时 Local 收尾、代理无需枚举消失的 UploadId | FileManagement.Tests：ProxyUploadTests.AlreadyCommittedObject_PublishesWithoutListingConsumedUploadId、ReviewRecoveryTests.LocalCommittedObject_ReconcilesManifestAndRemovesParts |
| FileSystemStorage.Capabilities；Local 重启、完成冻结、取消和清理回归 | FileStorage.Tests：Multipart_Restart_RepeatComplete_AndLatePart、InFlightPart_CannotReplaceFrozenCompletion、CancelledWrite_PreservesExistingContent；FileManagement.Tests：ReviewRecoveryTests.LocalCommittedObject_ReconcilesManifestAndRemovesParts |
| FileManagementControllerBase；上传→关联→鉴权下载→Range→解绑→删除，中文文件名、跨租户 | FileManagement.Tests：FileHttpTests.RealHttp_Upload_Download_Range_Signing_AndAuthorization（两 ORM × Local/代理）；两 Admin：AdminFileControllerHttpTests.RealAdminController_Upload_Bind_Download_Range_TenantIsolation_AndDelete |
| AdminUserInfoFileAuthorizer / 用户资料应用链；本人绑定、读取、越权拒绝、解绑删除 | 两 Admin：AdminUserInfoFileAuthorizationTests.RealFileService_AllowsOwnerBinding_RejectsOtherUser_AndDeletesFile（Local/代理）、其余用户资料授权测试 |
| FileMigrationService / S3FileStorage.SaveAsync；Local→代理复制和摘要读回、CAS、延迟清理、并发删除 | FileManagement.Tests：ProxyUploadTests.LocalToProxyMigration_VerifiesContentAndRetiresOldLocation、OperationRecoveryTests.Migration_DatabaseFailureAfterCopy_RetriesWithoutChangingSource、TransferRecoveryTests.Migration_LateWriter_CannotOverwriteWinnerOrResurrectDeleted / MinioMigration_LateWriter_CannotOverwriteWinnerOrResurrectDeleted |
| S3 SignedParts 原行为；实际校验摘要、签名过期、分页、恢复、空文件 | S3.Tests：MinioLiveTests.Minio_BasicMultipartRangeAndSignedRequests_RoundTrip、MinioRecoveryTests.PresignedPart_Expires_AndCannotChangeChecksum；FileManagement.Tests：MinioUploadTests.*、OperationSchedulingTests.Minio_HostedScheduler_CompletesSignedUploadAndDeletion、EmptyCompletionRecoveryTests.MinioEmptyCompletion_* |
| 包与消费者入口；Abstractions netstandard2.0、Provider net6.0 | evidence/package-consumption 的四包 Release + net6.0 PackageReference consumer；两 Admin、Demo、Tool、CapacityProbe 构建 |

## Review Fix Round 1 增量映射

| FIX / 生产符号 | 直接验证 |
|---|---|
| FIX-001：FileOperationProcessor.ExecuteAsync / TrackUnpublishedUploadAsync / AbortUnpublishedUploadAsync；先查询提交结果，根 CAS 登记独立补偿任务，不误删胜出会话 | FileManagement.Tests：InitializationCompensationTests.DatabaseFailure_ReconcilesCommitBeforeCompensating、CompensationCommitResponseLost_DoesNotDuplicateCleanupTask、Cancellation_UsesIndependentRecoveryToken_AndRetryHasOnlyOneLiveUpload、LosingInitializer_AbortsOnlyItsOwnUpload_AndPreservesWinner（均两 ORM SQLite） |
| FIX-001：FileOperationAdministration.RecoverAsync；失败的 UploadId 补偿可恢复 | FileManagement.Tests：InitializationCompensationTests.FailedAbort_IsDurableAndRecoverableAfterRestart（两 ORM SQLite） |
| FIX-002：FileWriteRequest.RestoreMultipart / S3FileStorage.CreateMultipartAsync、SaveEmptyAsync、EnsureUploadRequest、ValidateMetadataSize；16 项用户额度、内部字段及总字节限制 | Qiniu.Tests：MetadataBudgetTests.CustomBudget_SurvivesSaveAndPersistedMultipartRestore（14/15/16 × 两模式）、NewRequests_RejectReservedMetadata_EvenUsingRestoreFactory、ExcessCustomCountOrBackendBytes_FailsBeforeSdkCall |
| FIX-002：FileModelExtensions.Upload；从原有 BackendMetadataJson 恢复 Provider 字段 | FileManagement.Tests：ProxyUploadTests.Receipt_SurvivesReregistrationRestartAndCompletesWithoutRemoteChecksum；初始化提交丢响应后 FinishAsync 实际上传/完成 |
| FIX-002：Local 与 MinIO 元数据兼容 | FileStorage.Tests：FileWriteMetadataCapacityTests.SaveAsync_PersistsMetadataAtPublicLimit、Multipart_CreateRecoverAndComplete_PreservesSixteenMetadataItems；S3.Tests：MinioMetadataCapacityTests.Minio_SaveAsync_PersistsSixteenMetadataItemsForEmptyAndNonEmptyObjects（实际 MinIO） |
| FIX-003：CapacityProbeDiskSpace.Measure / Program 采样路径及程序集身份 | Qiniu.Tests：CapacityProbeDiskSpaceTests.Qiniu_UsesTempVolume_WhenRootIsLowAndTempHasPartBudget、Qiniu_RejectsLowTempVolume_EvenWhenRootHasPlentyOfSpace、RemoteBackend_BudgetsOnePartPerActualConcurrentUpload、Local_UsesRootVolumeAndKeepsMergeCapacityBudget；CapacityProbe 实际构建 |

以上增量证据位于 `evidence/fix-round-1/`。`FileSystemStorageTests.CapacityFailure_IsEarly_AndDoesNotPublish` 改用卷总容量作为超限输入，避免并行测试释放临时文件使旧 `free+1` 输入失效；不修改生产行为或降低断言。

## Review Fix Round 2 增量映射

| FIX / 生产符号 | 直接验证 |
|---|---|
| FIX-003：CapacityProbeDiskSpace.Measure / ResolveVolumeRoot / QueryAvailableBytes；真实目录解析和失败语义 | Qiniu.Tests：CapacityProbeDiskSpaceTests.DefaultResolution_UsesExistingDirectoryForVolumeAndSpaceQueries、DefaultResolution_FailsForMissingDirectoryInsteadOfFallingBackToPathRoot |
| FIX-003：CapacityDiskSnapshot.ReadAvailableBytes；预检查/监控/最终报告均按实际目录取样 | Qiniu.Tests：DefaultResolution_UsesJunctionTargetVolumeForPrecheckAndSubdirectorySampling；真实 Windows C: → H: 联接及子目录，默认生产解析与独立 OS 查询比较，net6/net8 均通过 |
| FIX-003：Unix 真实路径及挂载点选择 | Qiniu.Tests：DefaultResolution_FollowsSymbolicLinkForTargetAndNestedDirectorySampling；Linux/macOS 门控，本轮 Windows 跳过，不能作为 Unix 已验证证据 |
| FIX-003：原有 Local/远端预算和 Program 接入 | 原 4 项 CapacityProbeDiskSpaceTests 继续通过；CapacityProbe 实际 build，0 警告/错误；Program 预检后两处采样统一调用 ReadAvailableBytes |

本轮精确命令和 TRX 位于 `evidence/fix-round-2/`。这组证据替代 Round 1 关于“真实卷已经验证”的过度结论；Round 1 的预算断言仍有效。未改变七牛 Provider、业务、ORM 或 Admin 源码，相关回归及包消费证据复用。

## 七牛云端门禁（未执行）

QiniuLiveTests.Proxy_RealMultipartRangeSignedDownloadAndIdempotentCompletion、SignedParts_RejectsWrongBytes_ListsChecksum_AndExpires、Proxy_SigV4PayloadTampering_IsRejectedAndUploadCanBeAborted：net6.0/net8.0 均因缺少测试空间配置跳过。代码存在不代表目标云端通过。

真实浏览器 CORS、跨日会话期限、实际应用进程重启后的云端续传，以及 1 GiB/100 GiB 容量均须在目标空间后续验收。容量入口为 CapacityProbe 的 `--backend qiniu --allow-qiniu true --gib 1|100`；本轮没有执行。
