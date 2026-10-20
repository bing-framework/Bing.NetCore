# 任务状态核对与清理结果

核对日期：2026-10-15；源码与文档基点：`da0eccc0`。用户确认采用“合并后删除”方案；本轮追加合并 006/V4 的 7 份完成快照，保留 22 个未完成或待确认任务及其验收边界。

## 判断口径与覆盖范围

整理前核对对象包括 24 个独立任务，以及 `completed/` 三份总览中保留的 95 个历史任务 ID，共 119 个任务。整理后 97 个 ID 归入四份总览，22 个任务保留原目录；`CLEANUP.md` 分别记录上一轮 380 份及本轮追加 7 份合并删除，不重复执行历史清单。

- **已完成（局部范围）**：对应计划的实现/验证工作已有源码、职责测试或实验结果与执行记录支撑；不表示全部发布、性能和外部门禁通过。实验得到“没有收益”也可完成实验任务，不能改写为性能提升。
- **部分完成**：有已落地能力，但仍有有效验收缺口。实现通过而发布阻塞时，分别记录，不重新开发已完成能力。
- **待确认**：已有实现或文档，但验收记录、外部证据或原目标变更未闭环；不据此认定功能不存在。
- 旧 TODO 结合后续任务和当前源码核对。测试文件/方法存在是可复核入口；下文通过数量属于原任务记录，本轮未重跑数据库、Redis、Cluster、跨进程或性能矩阵。

22 个保留任务的目标、背景、验收标准及 Review 原文保留；仅查询重构计划的一处 V4 Review 引用迁移到新总览。006/V4 的原任务 ID、结论、职责证据与限制已合并，未完成/待确认任务不进入删除集合。

## 大功能与可复用共性能力

| 大功能 | 可复用能力与合并原则 | 当前依据与边界 |
| --- | --- | --- |
| 缓存公共契约与资源隔离 | 配置与物理键、UTF-16 输入校验、List/Stream 顺序及 null-hit、取消、批量/扫描/删除预算；不同 Provider 共用契约与职责测试 | [缓存总览](completed/caching.md)中的公共契约及批量优化；接口见 [ICache](../../framework/src/Bing.Caching/Bing/Caching/ICache.cs)。保持 NullCache 空结果、SCAN 非快照、响应到达后才校验预算等边界 |
| 分布式缓存与拓扑容错 | 同键合并、租约撤销/失租拒写、Cluster 分槽、分片有界调度、首错取消、命名空间哨兵、故障恢复探针；重复门禁按场景合并 | [缓存总览](completed/caching.md)中的分片和故障门禁；[ProcessProbe](../../framework/tests/Bing.Caching.ProcessProbe/Run-ProcessProbe.ps1)。不把单节点证据替代 Cluster 或独立总线 |
| Hybrid 可靠失效与运维 | 剩余 TTL 回填、代次闸门、重连失效、按序列化字节拆分、outbox 背压/重试/恢复、诊断、死信授权与审计 | 已完成 070–071、093–098；[099 迁移说明](BING-CACHE-TODO-20261007-099/migration.md)仍有跨版本部署缺口，不并入“发布已通过”结论 |
| 缓存性能与证据治理 | 哈希/缓冲复用、List 快路径、命令探针、checkpoint、稳定 workload identity、固定基线及预算判定方法；归并实验结论，避免逐候选建立永久任务入口 | [正式性能报告](BING-CACHE-TODO-20261005-004/performance.md)、[产物规则](ARTIFACTS.md)。保留失败预算、不可比 baseline、超时边界和未知网络/排队指标 |
| SQL 查询、元数据缓存与质量门禁 | 查询上下文/执行器边界、映射缓存版本隔离、失败 Lazy 清除、并发容量/LRU、Provider 契约、分析器与发布证据链 | [元数据缓存总览](completed/sql-metadata-cache.md)与下方独立 SQL 任务；本地治理完成不能替代真实 Provider、受保护 CI 或 FormalHost 验收 |
| 动态 API 与客户端 SDK | 显式服务发现、路由/绑定/描述、原生代理、声明式 SDK 注册、逐请求上下文与多 SDK 隔离；按服务端/原生客户端/第三方 SDK 三层归并 | [API 与 SDK 总览](completed/dynamic-api.md)。不混淆原生元数据代理和 WebApiClientCore，不宣称生成 DTO 或默认重试 |
| 框架文档与知识入口 | 使用说明、架构决策、模块/配置/API 索引与代码对照；统一维护主题文档，任务只保留验收状态 | [文档入口](../../docs/README.md)。原框架文档任务的路径变更和最终验收待确认 |

## 独立缓存与文档任务逐项核对

证据编号指向下一节的当前源码及测试入口；保留任务指向原始记录，006 指向合并后的证据。

| 任务 | 本次判断 | 可验证依据、未完成项与处置 |
| --- | --- | --- |
| [BING-CACHE-HARDENING-20261004-001](BING-CACHE-HARDENING-20261004-001/review.md) | 实现完成；发布待确认 | C1；Review 的 P1–P8 PASS，P9 DEVIATED_OK，性能回归接受/发布决策未闭环。保留整个目录及复现程序 |
| [BING-CACHE-PERF-STREAM-20261004-002](BING-CACHE-PERF-STREAM-20261004-002/review.md) | 部分完成 | C2；流式接口、批量及扫描实现存在；[performance.md](BING-CACHE-PERF-STREAM-20261004-002/performance.md)仍有 P95/分配预算失败。后续功能补证不等于性能通过；保留 |
| [BING-CACHE-TODO-20261004-003](BING-CACHE-TODO-20261004-003/execution.md) | 部分完成 | C2、C3；分批输入、分片及集成能力已实现；[performance.md](BING-CACHE-TODO-20261004-003/performance.md)记录扫描和分配超预算，仍有总体性能验收；保留 |
| [BING-CACHE-TODO-20261005-004](BING-CACHE-TODO-20261005-004/review.md) | 部分完成 | C4；T01–T10 分项实现与采证有记录，14 个性能 cell 已终止，但 7 个是超时边界，正式预算 FAIL，严格三方缺中间源码。保留，不将 PASS_WITH_ISSUES 当作发布完成 |
| [BING-CACHE-TODO-20261005-005](BING-CACHE-TODO-20261005-005/execution.md) | 实现完成；验证待确认 | C5；checkpoint、自适应拆分、回填代次已实现。旧 TODO 的 baseline 缺失描述已变化：共享基线目录及两 Provider 工程当前存在，但恢复后的对应 harness 构建验收未在本轮执行；真实 Hybrid 竞态的候选对应关系仍需复核。保留 |
| [BING-CACHE-TODO-20261006-006](completed/caching.md#BING-CACHE-TODO-20261006-006) | 已完成（单节点计划范围） | C6；计划明确只补单节点/跨进程证据并列明外部边界，执行记录中三 Provider 五类进程场景、Free/CS 各 TFM 4/4、Easy 各 TFM 19/19。Cluster、独立总线和预算不是其完成前提；旧缺口分别被 079、084–085、089–092 后续补证。已合并并删除四份快照，总体预算保留在 002/004 |
| [BING-CACHE-PERF-20261006-007](BING-CACHE-PERF-20261006-007/execution.md) | 部分完成（局部实现完成） | C7；单节点直读分支已实现，原记录批量各 TFM 12 通过/1 跳过、协调 7/7；TODO 仍保留正式性能预算，短基准不能代替 before/after。保留 |
| [BING-CACHE-PERF-20261006-008](BING-CACHE-PERF-20261006-008/execution.md) | 部分完成（局部实现完成） | C8；HashContext 及三 Provider 接入存在，固定 v2 格式有直接测试；正式同构 baseline 预算仍缺，Easy 历史跳过不能计通过。保留 |
| [BING-CACHE-TODO-20261007-099](BING-CACHE-TODO-20261007-099/execution.md) | 实现完成；跨版本验收未完成 | C9；handle API、旧布局迁移及单节点/Cluster 用例存在，历史双 TFM outbox 各 11/11、Cluster 各 1/1；[TODO](BING-CACHE-TODO-20261007-099/TODO.md)明确未执行旧/新二进制蓝绿部署。保留五份文档，尤其 migration.md |
| [bing-framework-docs](bing-framework-docs/plan.md) | 待确认 | 原计划仅一份 plan；当前已存在[使用文档](../../docs/getting-started/使用文档.md)和[设计思路文档](../../docs/architecture/设计思路文档.md)，覆盖安装、DDD、数据访问、横切、决策/数据流/扩展，但路径已调整，缺此任务的最终验收记录。不能继续描述为“尚无文档”，也不直接删除计划 |

### 缓存当前实现与职责测试

| 证据 | 当前实现 | 直接测试或复现入口 |
| --- | --- | --- |
| C1 | [CacheConfiguration](../../framework/src/Bing.Caching/Bing/Caching/CacheConfiguration.cs)、[CacheKeyFormat](../../framework/src/Bing.Caching/Bing/Caching/CacheKeyFormat.cs) | [CacheConfigurationAndKeyFormatTest](../../framework/tests/Bing.Caching.Tests/CacheConfigurationAndKeyFormatTest.cs)：`PhysicalKey_Isolated_AndRoundTripsSpecialCharacters`；完整 P1–P9 见原 Review |
| C2 | [ICache](../../framework/src/Bing.Caching/Bing/Caching/ICache.cs)、[FreeRedis Stream](../../framework/src/Bing.Caching.FreeRedis/Bing/Caching/FreeRedis/FreeRedisCacheManager.Stream.cs) | [BatchContractTest](../../framework/tests/Bing.Caching.FreeRedis.Tests.Integration/BatchContractTest.cs)：`GetAllStream_ReturnsKeyHitAndValue_InInputOrder` |
| C3 | [ShardedRedisCacheManager](../../framework/src/Bing.EasyCaching/Bing/EasyCaching/ShardedRedisCacheManager.cs) | [ShardedScanCancellationUnitTest](../../framework/tests/Bing.EasyCaching.Tests/ShardedScanCancellationUnitTest.cs)：`GetByPrefixStream_CancellationSkipsTrailingIdentityProbe` |
| C4 | [CacheManager.Hybrid](../../framework/src/Bing.EasyCaching/Bing/EasyCaching/CacheManager.Hybrid.cs)、[RedisLeaseCoordinator](../../framework/src/Bing.EasyCaching/Bing/EasyCaching/RedisLeaseCoordinator.cs) | [原生产符号映射](BING-CACHE-TODO-20261005-004/test-mapping.md)逐项列出 TTL、失租、取消、序列化和总线测试；[RedisCoordinatorIntegrationTest](../../framework/tests/Bing.EasyCaching.Tests/RedisCoordinatorIntegrationTest.cs) |
| C5 | [CacheBench Program](BING-CACHE-PERF-STREAM-20261004-002/bench/Program.cs)：`BenchmarkCheckpoint`、原子临时文件替换；[CacheBatchAdaptive](../../framework/src/Bing.Caching/Bing/Caching/CacheBatchAdaptive.cs)；[HybridRefillGeneration](../../framework/src/Bing.EasyCaching/Bing/EasyCaching/HybridRefillGeneration.cs) | [CacheBatchAdaptiveTest](../../framework/tests/Bing.Caching.Tests/CacheBatchAdaptiveTest.cs)：`Read_SplitsPayloadOversizeAndPreservesOrder`；[HybridRefillGenerationTest](../../framework/tests/Bing.EasyCaching.Tests/HybridRefillGenerationTest.cs)：`AdvanceRejectsStaleSynchronousAndAsynchronousRefills` |
| C6 | [FreeRedis 协调](../../framework/src/Bing.Caching.FreeRedis/Bing/Caching/FreeRedis/FreeRedisCacheManager.Coordination.cs)与 C4 协调器 | [Run-ProcessProbe](../../framework/tests/Bing.Caching.ProcessProbe/Run-ProcessProbe.ps1)；[CommandCancellationTest](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/CommandCancellationTest.cs)：`GetAllStream_CancellationDoesNotDispatchSecondMGet`；[合并后的职责映射](completed/caching.md#BING-CACHE-TODO-20261006-006-test-mapping) |
| C7 | [FreeRedis Stream](../../framework/src/Bing.Caching.FreeRedis/Bing/Caching/FreeRedis/FreeRedisCacheManager.Stream.cs)：`ReadInputBatchAsync`、`ReadPrefixBatchAsync` 的 SingleNode 分支 | [BatchContractTest](../../framework/tests/Bing.Caching.FreeRedis.Tests.Integration/BatchContractTest.cs)：`GetAllAsync_PreservesOrderDuplicatesAndMissingDefault_AndEnumeratesOnce` |
| C8 | [CacheKeyFormat](../../framework/src/Bing.Caching/Bing/Caching/CacheKeyFormat.cs)：`HashContext`、`CreateHashContext`、池化 UTF-8 缓冲 | [CacheConfigurationAndKeyFormatTest](../../framework/tests/Bing.Caching.Tests/CacheConfigurationAndKeyFormatTest.cs)：`PhysicalKey_RemainsByteForByteCompatibleWithOriginalV2Format` |
| C9 | [管理接口](../../framework/src/Bing.EasyCaching/Bing/EasyCaching/HybridInvalidationDeadLetterAdmin.cs)、[投递器](../../framework/src/Bing.EasyCaching/Bing/EasyCaching/HybridInvalidationDelivery.cs)：字符串 handle 重载 | [HybridInvalidationDeliveryTest](../../framework/tests/Bing.EasyCaching.Tests/HybridInvalidationDeliveryTest.cs)：`DeadLetterAdmin_HandleAndTokenSurviveJsonRoundTripAcrossDeliveryInstances`；[Redis outbox 测试](../../framework/tests/Bing.EasyCaching.Tests/RedisHybridInvalidationOutboxIntegrationTest.cs)：`Outbox_MigratesLegacyPayloadMemberToHandleWithoutChangingByteBudget` |

005 的旧基线源码并非现在仍缺失；配置位置见 [eng/baselines.props](../../eng/baselines.props)。它与 004 所缺的“中间候选源码”是不同证据，不能互相替代。004 的自适应拆分历史 TODO 已被 005 的实现和直接测试补齐，历史复选框不再单独代表当前未实现。

## 独立 SQL 任务逐项核对

| 任务 | 本次判断 | 可验证实现/测试与未闭环项；处置 |
| --- | --- | --- |
| [BING-SQL-API-HARDENING-20260824-001](BING-SQL-API-HARDENING-20260824-001/execution.md) | 部分完成 | `SqlLambdaQuery` / `ISqlQuery.From<TEntity>`；[SqlQueryApiContractTest](../../framework/tests/Bing.Data.Sql.Tests/SqlQueryApiContractTest.cs) 的 `LambdaQuery_WhenPublicApiInspected_ShouldUseMethodLevelUnaryOrBinaryExpressions`。Runtime Bridge 深拆、完整 Benchmark、外部 Provider 和测试职责拆分仍缺，见执行报告“部分/未完成事项”及 review.md FIX-003；保留 |
| [BING-SQL-QUERY-REFACTOR-20260824-001](BING-SQL-QUERY-REFACTOR-20260824-001/execution.md) | 部分完成 | `SqlLambdaQueryCore.ResolveTwoSources`；[SqlQueryLifecycleTest](../../framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs) 的 `Lambda_WhenTwoSourceAliasesAreExplicit_ShouldRenderCompleteSqlAcrossClauses`。Phase 3–5 的大文件拆分、外部 Provider 和同机 before/after 未闭环；保留 |
| [BING-SQL-RC-HARDENING-20260825-001](BING-SQL-RC-HARDENING-20260825-001/review.md) | 部分完成 | `SqlMultipleQueryResult` 清理路径；[SqlMultipleQueryResultLifecycleTest](../../framework/tests/Bing.Dapper.Core.Tests/SqlMultipleQueryResultLifecycleTest.cs) 的 `Dispose_WhenCleanupFails_ShouldPreservePrimaryAndCleanupExceptionOrder`。Root/Join 正式对照、Provider 及多映射错误/取消矩阵仍缺，Review FIX-001/003；保留 |
| [BING-SQL-RC-HARDENING-20260826-002](BING-SQL-RC-HARDENING-20260826-002/review.md) | 部分完成/外部阻塞 | `SqlQueryOperationAccessor.Mutate`；[SqlQueryLifecycleTest](../../framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs) 的 `RawFluent_WhenWhereIfNotEmptyValueIsBlank_ShouldKeepQueryStateUnchanged`。FormalHost 同构对照、外部数据库、远端 CI 与部分直接合同缺口仍有效；保留 |
| [BING-SQL-RC-HARDENING-20260827-003](BING-SQL-RC-HARDENING-20260827-003/review.md) | 部分完成 | `IntegrationTestGate.GetSkipReason`；[IntegrationTestGateTest](../../framework/tests/Bing.Test.Shared/Bing/Test/Shared/IntegrationTestGateTest.cs) 的 `GetSkipReason_WhenProviderValueIsNotTrue_ShouldKeepProviderDisabled`。受保护 Provider、远端 CI、凭据历史处置和 FormalHost 同 key 对照未闭环；保留 |
| [BING-SQL-RC-HARDENING-20260828-004](BING-SQL-RC-HARDENING-20260828-004/review.md) | 本地 Review PASS；整项部分完成 | `WhereClauseExtensions.WhereIfNotEmpty`；[SqlQueryLifecycleTest](../../framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs) 的 `RawFluent_WhenWhereIfNotEmptyValueExists_ShouldInvalidateCacheOnce`。外部受保护 Provider、远端 CI 与 FormalHost before 仍缺；保留 |
| [BING-SQL-RELEASE-HARDENING-20260831](BING-SQL-RELEASE-HARDENING-20260831/review.md) | 部分完成 | 事务能力与原生异步模式；[SqlFactoryTest](../../framework/tests/Bing.Dapper.Core.Tests/Factories/SqlFactoryTest.cs) 的 `TransactionAsyncOperations_WhenProfileMatchesNativeCommit_ShouldUseNativeMode`。Provider/Procedure、同 key FormalHost、Release Gate 和最终报告口径仍有 Review FIX-002/003；保留 |
| [BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903](BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903/review.md) | 本地治理完成；整项部分完成 | `ProviderReleaseEvidenceValidator`；[直接测试](../../framework/tests/Bing.Test.Shared/ProviderReleaseEvidenceValidatorTest.cs) 的 `Write_WhenCurrentSuccessfulArtifactsMatch_ShouldCreateTrustedReleaseEvidence`。本地 recommended Fix 完成及 Review PASS 不覆盖原批准计划 T07–T09：三个授权 lane 非零真实执行仍缺。**本轮纠正旧索引的整项完成暗示，排除清理候选** |
| [BING-SQL-RC-HARDENING-20260904-001](BING-SQL-RC-HARDENING-20260904-001/review.md) | 部分完成 | 发布验证器与 runner；[ProviderReleaseEvidenceValidatorTest](../../framework/tests/Bing.Test.Shared/ProviderReleaseEvidenceValidatorTest.cs)、[ProviderContractRunnerTest](../../framework/tests/Bing.Test.Shared/ProviderContractRunnerTest.cs)。dirty-source、PostgreSQL OUT/INOUT、FormalHost、正向 CI 仍有 T09–T21/FIX-003/005 缺口；保留 |
| [BING-SQL-RC-HARDENING-20260906-001](BING-SQL-RC-HARDENING-20260906-001/execution.md) | 部分完成/待确认 | `ProviderCapabilityCatalog` 与 RC 验证脚本；[ProviderContractRunnerTest](../../framework/tests/Bing.Test.Shared/ProviderContractRunnerTest.cs) 的 `ReleaseReadiness_WhenEvidenceSessionsDiffer_ShouldRemainFalse`。MySQL 认证、dirty-source、protected 正向工作流及独立 Review 未闭环；本地 136/136 审计不能代替 ReleaseEvidence；保留 |
| [BING-SQL-RC-FINAL-HARDENING-20260908-001](BING-SQL-RC-FINAL-HARDENING-20260908-001/review.md) | 部分完成/发布阻塞 | ProviderEvidence CLI、RC aggregate 验证；[ProviderContractRunnerTest](../../framework/tests/Bing.Test.Shared/ProviderContractRunnerTest.cs) 的 `ReleaseReadiness_WhenAnyGlobalInputIsMissing_ShouldRemainFalse`。四 Provider 双 TFM 受保护正向证据 0/8、同 session aggregate、clean FormalHost 仍缺，见 FINAL-101/201/301；保留 |
| [sql-lambda-query-api-v4](completed/sql-query-api.md#sql-lambda-query-api-v4) | 历史已完成；当前 API 已被替代 | 原 Round 7 Review PASS，Phase 0–10 与 FIX-011/012 闭环；原高元数 API 已被 [V6 计划](sql-query-dev-v6-api-refactor/plan.md)明确删除。当前 [SqlQueryApiContractTest](../../framework/tests/Bing.Data.Sql.Tests/SqlQueryApiContractTest.cs) 验证方法级一/二元表达式。已合并为 API 演进历史，**不能描述为当前仍公开 V4 高元数接口**；引用已迁移，三份原文件合并后删除 |
| [sql-mapping-cache-versioning](sql-mapping-cache-versioning/execution.md) | 实现完成；验收证据待确认 | `VersionedEntityMappingResolver.PublishMappings/CaptureSnapshot`、Builder/Query 快照调用链存在；[直接测试](../../framework/tests/Bing.Data.Sql.Tests/Metadata/VersionedEntityMappingResolverTest.cs) 覆盖旧新快照、失败不发布、并发单调版本。执行报告宣称计划 100% 完成，但 benchmark-notes.md 引用的两份 FormalHost 原始报告在所列位置均不存在，18 场景原始证据待定位/确认；保留三份文件。缺 review.md 仅作记录，不额外发明验收门槛 |
| [sql-query-dev-v6-api-refactor](sql-query-dev-v6-api-refactor/review.md) | 部分完成 | `SqlLambdaQuery` / `SqlQueryPlan`、[SqlQueryLifecycleTest](../../framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs) 和 [SQL 追溯](../sql-metadata-test-traceability.md)。Phase 5/6/10 的外部 Provider、日志 Scope 断言、缓存指纹矩阵和 Clone 正式性能未闭环；保留 |

SQL 当前实现路径：[Lambda 描述](../../framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery.cs)、[查询核心](../../framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQueryCore.cs)、[mutation gateway](../../framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQueryOperationAccessor.cs)、[Dapper 多结果清理](../../framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlMultipleQueryResult.cs)、[映射版本发布](../../framework/src/Bing.Data.Sql/Bing/Data/Sql/Metadata/VersionedEntityMappingResolver.cs)、[Builder 快照捕获](../../framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Core/SqlBuilderServices.cs)、[Query 快照捕获](../../framework/src/Bing.Dapper.Core/Bing/Data/Sql/SqlQueryBase.cs)。

RS0026 的标准 plan.md 仅转指本地 `artifacts/plans/BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903-plan.md`；本轮读取该文件的 T07–T09、Phase D/F 及最终验收清单，确认真实 lane 是有效验收项，不能因后续 recommended Fix 完成而消失。该批准计划也属于仍需保留的证据。

## 已合并任务逐项复核（95 项）

以下每个 ID 保留一次状态入口；详细范围、历史测试结果和限制沿用对应总览。缓存 90 项的 149 个引用路径均可定位，144 个所列测试方法标识可在目标源码中检索；其余入口是基准/进程脚本、注册源码或状态索引。它们证明局部实现/采证可追溯，不构成本轮重新运行通过的结论。

### 公共契约、输入与资源边界

| 任务 | 本次状态 | 可验证入口 |
| --- | --- | --- |
| [BING-CACHE-TODO-20261006-009](completed/caching.md#BING-CACHE-TODO-20261006-009) | 已完成（局部） | [AsyncOperations_WithPreCanceledToken_ShouldReturnCanceledTasksAndSkipRetrievers](../../framework/tests/Bing.Caching.Tests/NullCacheAndOptionsTest.cs) |
| [BING-CACHE-TODO-20261006-011](completed/caching.md#BING-CACHE-TODO-20261006-011) | 已完成（局部） | [InputLimits_ApplyToBatchKeysAndLeaseKeys](../../framework/tests/Bing.Caching.Tests/CacheConfigurationAndKeyFormatTest.cs) |
| [BING-CACHE-TODO-20261006-012](completed/caching.md#BING-CACHE-TODO-20261006-012) | 已完成（局部） | [GetAllStream_CacheKeys_CancellationAfterFirstBatch_DoesNotDispatchNextBatch](../../framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs) |
| [BING-CACHE-TODO-20261006-013](completed/caching.md#BING-CACHE-TODO-20261006-013) | 已完成（局部） | [GetByPrefixStream_CancellationAfterFirstBatch_DoesNotDispatchNextBatch](../../framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs) |
| [BING-CACHE-TODO-20261006-014](completed/caching.md#BING-CACHE-TODO-20261006-014) | 已完成（局部） | [GetByPrefixStream_SpecialKeysStayInNamespaceAndPreserveLogicalText](../../framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs) |
| [BING-CACHE-TODO-20261006-015](completed/caching.md#BING-CACHE-TODO-20261006-015) | 已完成（局部） | [GetByPrefixStream_PropagatesSecondBatchFailureAfterFirstBatch](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/BatchContractTest.cs) |
| [BING-CACHE-TODO-20261006-016](completed/caching.md#BING-CACHE-TODO-20261006-016) | 已完成（局部） | [Provider_SatisfiesSharedContract](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/SharedCacheContractTest.cs) |
| [BING-CACHE-TODO-20261006-026](completed/caching.md#BING-CACHE-TODO-20261006-026) | 已完成（局部） | [GetAllStream_PropagatesSecondBatchFailureAfterFirstBatch](../../framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs) |
| [BING-CACHE-TODO-20261006-036](completed/caching.md#BING-CACHE-TODO-20261006-036) | 已完成（局部） | [GetAllAndGetByPrefix_100000Items_ListAndStreamRemainConsistent](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/BatchContractTest.cs) |
| [BING-CACHE-TODO-20261007-047](completed/caching.md#BING-CACHE-TODO-20261007-047) | 已完成（局部） | [Provider_SatisfiesSharedContract](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/SharedCacheContractTest.cs) |
| [BING-CACHE-TODO-20261007-052](completed/caching.md#BING-CACHE-TODO-20261007-052) | 已完成（局部） | [InputLimits_CountUtf16CodeUnits_AndAllowTheExactBoundary](../../framework/tests/Bing.Caching.Tests/CacheConfigurationAndKeyFormatTest.cs) |
| [BING-CACHE-TODO-20261007-053](completed/caching.md#BING-CACHE-TODO-20261007-053) | 已完成（局部） | [PatternMatcher_ExtractsOnlySafeLiteralPrefix](../../framework/tests/Bing.Caching.Tests/CachePatternTest.cs) |
| [BING-CACHE-TODO-20261007-054](completed/caching.md#BING-CACHE-TODO-20261007-054) | 已完成（局部） | [Configuration_AllowsWellFormedSurrogatePairs_AndKeepsPhysicalKeyIsolation](../../framework/tests/Bing.Caching.Tests/CacheConfigurationAndKeyFormatTest.cs) |
| [BING-CACHE-TODO-20261007-057](completed/caching.md#BING-CACHE-TODO-20261007-057) | 已完成（局部） | [HybridScan_HandlesSpecialKeysWithoutTouchingAnotherNamespace](../../framework/tests/Bing.EasyCaching.Tests/RedisCoordinatorIntegrationTest.cs) |
| [BING-CACHE-TODO-20261007-058](completed/caching.md#BING-CACHE-TODO-20261007-058) | 已完成（局部） | [BatchPayloadSplitting_PreservesListAndStreamSemantics](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/CoordinationBoundaryTest.cs) |
| [BING-CACHE-TODO-20261007-060](completed/caching.md#BING-CACHE-TODO-20261007-060) | 已完成（局部） | [GetAllStream_10000ItemsMatchesListIncludingDuplicatesMissingOrderAndHits](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/BatchContractTest.cs) |
| [BING-CACHE-TODO-20261007-061](completed/caching.md#BING-CACHE-TODO-20261007-061) | 已完成（局部） | [ClientShardedTopology_FailsBeforeProviderRegistration](../../framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs) |
| [BING-CACHE-TODO-20261007-063](completed/caching.md#BING-CACHE-TODO-20261007-063) | 已完成（局部） | [GetByPrefixStream_PreservesNullHitAndReportsMissingValue](../../framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs) |
| [BING-CACHE-TODO-20261007-067](completed/caching.md#BING-CACHE-TODO-20261007-067) | 已完成（局部） | [GetByPrefixStream_FallbackReportsNullHitAndMissingValue](../../framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs) |
| [BING-CACHE-TODO-20261007-069](completed/caching.md#BING-CACHE-TODO-20261007-069) | 已完成（局部） | [ResourceOptions_RejectNegativeScanPageLimit](../../framework/tests/Bing.Caching.Tests/CacheResourceBoundaryTest.cs) |
| [BING-CACHE-TODO-20261007-070](completed/caching.md#BING-CACHE-TODO-20261007-070) | 已完成（局部） | [PublishAsync_CancellationStopsSubsequentMessages](../../framework/tests/Bing.EasyCaching.Tests/HybridInvalidationMessageBatcherTest.cs) |
| [BING-CACHE-TODO-20261007-071](completed/caching.md#BING-CACHE-TODO-20261007-071) | 已完成（局部） | [Publish_DefaultBudgetKeepsOneMessageAndInputOrder](../../framework/tests/Bing.EasyCaching.Tests/HybridInvalidationMessageBatcherTest.cs) |
| [BING-CACHE-TODO-20261007-087](completed/caching.md#BING-CACHE-TODO-20261007-087) | 已完成（局部） | [GetByPrefixStream_EnforcesScanKeyBudget](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/CacheSpecialKeyTest.cs) |

### 命令观测、文档与状态治理

| 任务 | 本次状态 | 可验证入口 |
| --- | --- | --- |
| [BING-CACHE-TODO-20261006-017](completed/caching.md#BING-CACHE-TODO-20261006-017) | 已完成（局部） | [Probe_TracksCallsByKindAndRestoresUnknownCompatibility](../../framework/tests/Bing.Caching.Tests/CacheCommandProbeTest.cs) |
| [BING-CACHE-TODO-20261006-018](completed/caching.md#BING-CACHE-TODO-20261006-018) | 已完成（局部） | [Probe_TracksWriteAndDeleteKindsAndRecoversAfterFailures](../../framework/tests/Bing.Caching.Tests/CacheCommandProbeTest.cs) |
| [BING-CACHE-TODO-20261006-019](completed/caching.md#BING-CACHE-TODO-20261006-019) | 已完成（局部） | [GetAllStream_CancellationDoesNotDispatchSecondMGet](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/CommandCancellationTest.cs) |
| [BING-CACHE-TODO-20261006-045](completed/caching.md#BING-CACHE-TODO-20261006-045) | 已完成（局部） | [GetAllStream_CancellationAfterFirstBatch_DoesNotDispatchNextRead](../../framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs) |
| [BING-CACHE-TODO-20261007-055](completed/caching.md#BING-CACHE-TODO-20261007-055) | 已完成（局部） | [Provider_SatisfiesSharedContract](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/SharedCacheContractTest.cs) |
| [BING-CACHE-TODO-20261007-056](completed/caching.md#BING-CACHE-TODO-20261007-056) | 已完成（局部） | [Provider_SatisfiesSharedContract](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/SharedCacheContractTest.cs) |
| [BING-CACHE-TODO-20261007-059](completed/caching.md#BING-CACHE-TODO-20261007-059) | 已完成（局部） | [当前状态索引](../cache-todo-index.md) |
| [BING-CACHE-TODO-20261007-068](completed/caching.md#BING-CACHE-TODO-20261007-068) | 已完成（局部） | [注册扩展与文档核对](../../framework/src/Bing.EasyCaching/Bing/EasyCaching/CachingServiceCollectionExtensions.cs) |
| [BING-CACHE-TODO-20261007-080](completed/caching.md#BING-CACHE-TODO-20261007-080) | 已完成（局部） | [ClearAsync_StopsBeforeSecondScanPage](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/ScanPageBudgetTest.cs) |
| [BING-CACHE-TODO-20261007-081](completed/caching.md#BING-CACHE-TODO-20261007-081) | 已完成（局部） | [HybridScan_HandlesSpecialKeysWithoutTouchingAnotherNamespace](../../framework/tests/Bing.EasyCaching.Tests/RedisCoordinatorIntegrationTest.cs) |

### 批量读取、删除及内部结构优化

| 任务 | 本次状态 | 可验证入口 |
| --- | --- | --- |
| [BING-CACHE-TODO-20261006-020](completed/caching.md#BING-CACHE-TODO-20261006-020) | 已完成（局部） | [TryGetCount_ReadsCollectionCountWithoutEnumerating](../../framework/tests/Bing.Caching.Tests/CacheCollectionCountTest.cs) |
| [BING-CACHE-PERF-20261006-021](completed/caching.md#BING-CACHE-PERF-20261006-021) | 已完成（局部） | [GetAllAsync_ScalarBatchNullUsesDefaultWithoutLosingHit](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/BatchContractTest.cs) |
| [BING-CACHE-TODO-20261006-022](completed/caching.md#BING-CACHE-TODO-20261006-022) | 已完成（局部） | [BatchWritesAndRemoves_HonorEmptyInputAndCancellationBeforeDispatch](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/BatchContractTest.cs) |
| [BING-CACHE-TODO-20261006-023](completed/caching.md#BING-CACHE-TODO-20261006-023) | 已完成（局部） | [InputLimits_ApplyToBatchKeysAndLeaseKeys](../../framework/tests/Bing.Caching.Tests/CacheConfigurationAndKeyFormatTest.cs) |
| [BING-CACHE-TODO-20261006-024](completed/caching.md#BING-CACHE-TODO-20261006-024) | 已完成（局部） | [PhysicalKey_Isolated_AndRoundTripsSpecialCharacters](../../framework/tests/Bing.Caching.Tests/CacheConfigurationAndKeyFormatTest.cs) |
| [BING-CACHE-TODO-20261006-025](completed/caching.md#BING-CACHE-TODO-20261006-025) | 已完成（局部） | [GetAllStream_PreservesOrderDuplicatesMissingAndNullValues](../../framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs) |
| [BING-CACHE-TODO-20261006-030](completed/caching.md#BING-CACHE-TODO-20261006-030) | 已完成（局部） | [PhysicalKey_Isolated_AndRoundTripsSpecialCharacters](../../framework/tests/Bing.Caching.Tests/CacheConfigurationAndKeyFormatTest.cs) |
| [BING-CACHE-TODO-20261006-031](completed/caching.md#BING-CACHE-TODO-20261006-031) | 已完成（局部） | [GetAllAsync_ListMatchesStream_ForDuplicatesMissingAndNullValues](../../framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs) |
| [BING-CACHE-TODO-20261006-032](completed/caching.md#BING-CACHE-TODO-20261006-032) | 已完成（局部） | [TryMatchLogicalPrefix_UsesCanonicalAsciiFastPathAndValidatesHash](../../framework/tests/Bing.Caching.Tests/CacheConfigurationAndKeyFormatTest.cs) |
| [BING-CACHE-TODO-20261006-033](completed/caching.md#BING-CACHE-TODO-20261006-033) | 已完成（局部） | [GetAllStream_ReturnsKeyHitAndValue_InInputOrder](../../framework/tests/Bing.Caching.FreeRedis.Tests.Integration/BatchContractTest.cs) |
| [BING-CACHE-TODO-20261006-035](completed/caching.md#BING-CACHE-TODO-20261006-035) | 已完成（局部） | [CacheBench：batchRead/scan 方向性采样](BING-CACHE-PERF-STREAM-20261004-002/bench/Program.cs) |
| [BING-CACHE-TODO-20261006-037](completed/caching.md#BING-CACHE-TODO-20261006-037) | 已完成（局部） | [GetAllAsync_ScalarBatchNullUsesDefaultWithoutLosingHit](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/BatchContractTest.cs) |
| [BING-CACHE-TODO-20261006-038](completed/caching.md#BING-CACHE-TODO-20261006-038) | 已完成（局部） | [GetAllAndGetByPrefix_100000Items_ListAndStreamRemainConsistent](../../framework/tests/Bing.Caching.FreeRedis.Tests.Integration/BatchContractTest.cs) |
| [BING-CACHE-TODO-20261006-039](completed/caching.md#BING-CACHE-TODO-20261006-039) | 已完成（局部） | [RedisAndHybridStreams_PreserveFirstBatchAndPropagateSecondBatchDecodeFailure](../../framework/tests/Bing.EasyCaching.Tests/RedisCoordinatorIntegrationTest.cs) |
| [BING-CACHE-TODO-20261006-040](completed/caching.md#BING-CACHE-TODO-20261006-040) | 已完成（局部） | [GetAllAsync_ListMatchesStream_ForDuplicatesMissingAndNullValues](../../framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs) |
| [BING-CACHE-TODO-20261006-041](completed/caching.md#BING-CACHE-TODO-20261006-041) | 已完成（局部） | [GetAllAsync_ListMatchesStream_ForDuplicatesMissingAndNullValues](../../framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs) |
| [BING-CACHE-TODO-20261006-042](completed/caching.md#BING-CACHE-TODO-20261006-042) | 已完成（局部） | [GetByPrefixAsync_CancellationAfterFirstBatch_DoesNotDispatchNextRead](../../framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs) |
| [BING-CACHE-TODO-20261006-044](completed/caching.md#BING-CACHE-TODO-20261006-044) | 已完成（局部） | [ThreeMasters_CrossSlotReadScanAndClear](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/ClusterCacheTest.cs) |
| [BING-CACHE-TODO-20261007-048](completed/caching.md#BING-CACHE-TODO-20261007-048) | 已完成（局部） | [ScanDelete_RemovesDataAndLeaseKeysAcrossBatchedPrefixPatternAndClear](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/ScanDeleteBatchTest.cs) |
| [BING-CACHE-TODO-20261007-049](completed/caching.md#BING-CACHE-TODO-20261007-049) | 已完成（局部） | [RemoveByPrefixAsync_CancellationAfterFirstDelete_DoesNotDispatchNextDelete](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/ScanDeleteBatchTest.cs) |
| [BING-CACHE-TODO-20261007-050](completed/caching.md#BING-CACHE-TODO-20261007-050) | 已完成（局部） | [RemoveAllAsync_UsesBatchSizeAndDeletesDataAndLeaseKeys](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/RemoveAllBatchTest.cs) |
| [BING-CACHE-TODO-20261007-051](completed/caching.md#BING-CACHE-TODO-20261007-051) | 已完成（局部） | [DeleteBatchBudget_HonorsCountAndRejectsSingleOversizedKey](../../framework/tests/Bing.Caching.Tests/CacheResourceBoundaryTest.cs) |
| [BING-CACHE-PERF-20261007-062](completed/caching.md#BING-CACHE-PERF-20261007-062) | 已完成（局部） | [GetHashSlot_MatchesUtf8ReferenceForUnicodeAndInvalidUtf16](../../framework/tests/Bing.Caching.Tests/CacheClusterTopologyTest.cs) |
| [BING-CACHE-PERF-20261007-064](completed/caching.md#BING-CACHE-PERF-20261007-064) | 已完成（局部） | [ClusterWriteBatch_GroupsKeysBySlot](../../framework/tests/Bing.EasyCaching.Tests/RedisLeaseCoordinatorBatchTest.cs) |
| [BING-CACHE-TODO-20261007-065](completed/caching.md#BING-CACHE-TODO-20261007-065) | 已完成（局部） | [ClusterRemoveBatch_PartitionsEachSlotWithinCommandBudget](../../framework/tests/Bing.EasyCaching.Tests/RedisLeaseCoordinatorBatchTest.cs) |
| [BING-CACHE-TODO-20261007-066](completed/caching.md#BING-CACHE-TODO-20261007-066) | 已完成（局部） | [RemoveAsync_CancellationAfterRedisCommit_StillInvalidates](../../framework/tests/Bing.EasyCaching.Tests/RedisLeaseCoordinatorBatchTest.cs) |

### 客户端分片与扫描调度

| 任务 | 本次状态 | 可验证入口 |
| --- | --- | --- |
| [BING-CACHE-TODO-20261006-027](completed/caching.md#BING-CACHE-TODO-20261006-027) | 已完成（局部） | [GetByPrefixStream_CancellationSkipsTrailingIdentityProbe](../../framework/tests/Bing.EasyCaching.Tests/ShardedScanCancellationUnitTest.cs) |
| [BING-CACHE-TODO-20261006-028](completed/caching.md#BING-CACHE-TODO-20261006-028) | 已完成（局部） | [RemoveByPrefix_SyncFailurePreservesPrimaryExceptionAndSkipsTrailingIdentityProbe](../../framework/tests/Bing.EasyCaching.Tests/ShardedScanCancellationUnitTest.cs) |
| [BING-CACHE-TODO-20261006-043](completed/caching.md#BING-CACHE-TODO-20261006-043) | 已完成（局部） | [GetAllAsync_ReturnsValuesFromAllShardsInInputOrder](../../framework/tests/Bing.EasyCaching.Tests/ShardedScanCancellationUnitTest.cs) |
| [BING-CACHE-TODO-20261007-046](completed/caching.md#BING-CACHE-TODO-20261007-046) | 已完成（局部） | [TwoShards_BatchSetAndGetAcrossBothNodes](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/ShardedCacheTest.cs) |
| [BING-CACHE-TODO-20261007-072](completed/caching.md#BING-CACHE-TODO-20261007-072) | 已完成（局部） | [TwoShards_BatchSetAndGetAcrossBothNodes](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/ShardedCacheTest.cs) |
| [BING-CACHE-TODO-20261007-073](completed/caching.md#BING-CACHE-TODO-20261007-073) | 已完成（局部） | [TwoShards_BatchSetAndGetAcrossBothNodes](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/ShardedCacheTest.cs) |
| [BING-CACHE-TODO-20261007-074](completed/caching.md#BING-CACHE-TODO-20261007-074) | 已完成（局部） | [GetByPrefixAsync_ReturnsValuesFromAllShardsAndUsesSharedBudget](../../framework/tests/Bing.EasyCaching.Tests/ShardedScanCancellationUnitTest.cs) |
| [BING-CACHE-TODO-20261007-075](completed/caching.md#BING-CACHE-TODO-20261007-075) | 已完成（局部） | [ClearAsync_DispatchesShardsWithinConfiguredLimit](../../framework/tests/Bing.EasyCaching.Tests/ShardedCleanupConcurrencyUnitTest.cs) |
| [BING-CACHE-TODO-20261007-076](completed/caching.md#BING-CACHE-TODO-20261007-076) | 已完成（局部） | [RemoveByPatternAsync_FailureCancelsOtherIssuedShardPromptly](../../framework/tests/Bing.EasyCaching.Tests/ShardedCleanupConcurrencyUnitTest.cs) |
| [BING-CACHE-TODO-20261007-077](completed/caching.md#BING-CACHE-TODO-20261007-077) | 已完成（局部） | [HybridSetAllAsync_RefillsWritingInstanceLocalLayerAfterCoordinatorCommit](../../framework/tests/Bing.EasyCaching.Tests/CacheManagerBatchCoordinationTest.cs) |
| [BING-CACHE-TODO-20261007-078](completed/caching.md#BING-CACHE-TODO-20261007-078) | 已完成（局部） | [OneShardUnavailable_ScanBackedReadsAndClearThrowInsteadOfReturningPartialSuccess](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/ShardedCacheTest.cs) |
| [BING-CACHE-TODO-20261007-088](completed/caching.md#BING-CACHE-TODO-20261007-088) | 已完成（局部） | [BoundedParallel_CleanupDoesNotExceedConfiguredConcurrency](../../framework/tests/Bing.EasyCaching.Tests/ShardedSyncCleanupConcurrencyUnitTest.cs) |

### 协调、Cluster 与故障门禁

| 任务 | 本次状态 | 可验证入口 |
| --- | --- | --- |
| [BING-CACHE-TODO-20261006-010](completed/caching.md#BING-CACHE-TODO-20261006-010) | 已完成（局部） | [GetAllStream_CancellationDoesNotDispatchSecondMGet](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/CommandCancellationTest.cs) |
| [BING-CACHE-TODO-20261006-029](completed/caching.md#BING-CACHE-TODO-20261006-029) | 已完成（局部） | [GetAllAndGetByPrefix_100000Items_ListAndStreamRemainConsistent](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/BatchContractTest.cs) |
| [BING-CACHE-TODO-20261006-034](completed/caching.md#BING-CACHE-TODO-20261006-034) | 已完成（局部） | [Run-ProcessProbe.ps1](../../framework/tests/Bing.Caching.ProcessProbe/Run-ProcessProbe.ps1) |
| [BING-CACHE-TODO-20261007-079](completed/caching.md#BING-CACHE-TODO-20261007-079) | 已完成（局部） | [AskRedirect_DoesNotReportMigratedKeyAsCacheMiss](../../framework/tests/Bing.Caching.CSRedis.Tests.Integration/ClusterMigrationTest.cs) |
| [BING-CACHE-TODO-20261007-082](completed/caching.md#BING-CACHE-TODO-20261007-082) | 已完成（局部） | [HybridCluster_CrossSlotBatchStreamRemoveAndClear_UpdatesDistributedAndLocalLayers](../../framework/tests/Bing.EasyCaching.Tests/EasyClusterIntegrationTest.cs) |
| [BING-CACHE-TODO-20261007-083](completed/caching.md#BING-CACHE-TODO-20261007-083) | 已完成（局部） | [RedisCluster_SlotOwnershipChangeDuringPrefixStreamIsRejected](../../framework/tests/Bing.EasyCaching.Tests/EasyClusterIntegrationTest.cs) |
| [BING-CACHE-TODO-20261007-084](completed/caching.md#BING-CACHE-TODO-20261007-084) | 已完成（局部） | [HybridBus_PublishDuringPause_ReportsFailureAndRecovers](../../framework/tests/Bing.EasyCaching.Tests/EasyHybridBusReconnectIntegrationTest.cs) |
| [BING-CACHE-TODO-20261007-085](completed/caching.md#BING-CACHE-TODO-20261007-085) | 已完成（局部） | [ShardedHybrid_BusPauseOverlapsBothShardRestarts_RejectsPendingUpdateAndRecovers](../../framework/tests/Bing.EasyCaching.Tests/EasyShardedHybridTwoNodeBusFaultIntegrationTest.cs) |
| [BING-CACHE-TODO-20261007-086](completed/caching.md#BING-CACHE-TODO-20261007-086) | 已完成（局部） | [RedisClusterScan_ResponseLostAfterExecution_PropagatesFailure](../../framework/tests/Bing.EasyCaching.Tests/EasyScanInFlightFailureTest.cs) |
| [BING-CACHE-TODO-20261007-089](completed/caching.md#BING-CACHE-TODO-20261007-089) | 已完成（局部） | [Run-ProcessProbe.ps1：三 Provider 五类场景](../../framework/tests/Bing.Caching.ProcessProbe/Run-ProcessProbe.ps1) |
| [BING-CACHE-TODO-20261007-090](completed/caching.md#BING-CACHE-TODO-20261007-090) | 已完成（局部） | [RedisCluster_Failover_RebuiltManagerReadsAndClearsAllSlots](../../framework/tests/Bing.EasyCaching.Tests/EasyClusterFailureIntegrationTest.cs) |
| [BING-CACHE-TODO-20261007-091](completed/caching.md#BING-CACHE-TODO-20261007-091) | 已完成（局部） | [RedisCluster_MasterRestart_RebuiltManagerRecoversAllSlots](../../framework/tests/Bing.EasyCaching.Tests/EasyClusterRestartIntegrationTest.cs) |
| [BING-CACHE-TODO-20261007-092](completed/caching.md#BING-CACHE-TODO-20261007-092) | 已完成（局部） | [RedisClusterScan_MasterRestartDuringHeldResponse_PropagatesFailure](../../framework/tests/Bing.EasyCaching.Tests/EasyClusterScanRestartIntegrationTest.cs) |

### Hybrid outbox、诊断与死信管理

| 任务 | 本次状态 | 可验证入口 |
| --- | --- | --- |
| [BING-CACHE-TODO-20261007-093](completed/caching.md#BING-CACHE-TODO-20261007-093) | 已完成（局部） | [Outbox_EnforcesBoundAndRecoversProcessingAfterRebuild](../../framework/tests/Bing.EasyCaching.Tests/RedisHybridInvalidationOutboxIntegrationTest.cs) |
| [BING-CACHE-TODO-20261007-094](completed/caching.md#BING-CACHE-TODO-20261007-094) | 已完成（局部） | [Outbox_EnforcesBoundAndRecoversProcessingAfterRebuild](../../framework/tests/Bing.EasyCaching.Tests/RedisHybridInvalidationOutboxIntegrationTest.cs) |
| [BING-CACHE-TODO-20261007-095](completed/caching.md#BING-CACHE-TODO-20261007-095) | 已完成（局部） | [Options_RequirePairedDeadLetterSettings](../../framework/tests/Bing.EasyCaching.Tests/HybridInvalidationDeliveryTest.cs) |
| [BING-CACHE-TODO-20261007-096](completed/caching.md#BING-CACHE-TODO-20261007-096) | 已完成（局部） | [DeadLetterCleanupFailure_PreservesProcessingAndReportsFailure](../../framework/tests/Bing.EasyCaching.Tests/HybridInvalidationDeliveryTest.cs) |
| [BING-CACHE-TODO-20261007-097](completed/caching.md#BING-CACHE-TODO-20261007-097) | 已完成（局部） | [Options_RejectInvalidDeadLetterByteBudget](../../framework/tests/Bing.EasyCaching.Tests/HybridInvalidationDeliveryTest.cs) |
| [BING-CACHE-TODO-20261007-098](completed/caching.md#BING-CACHE-TODO-20261007-098) | 已完成（局部） | [DeadLetterAdmin_ListsReplaysAndAuditsOpaqueEntry](../../framework/tests/Bing.EasyCaching.Tests/HybridInvalidationDeliveryTest.cs) |

035/037 是方向性实验任务，完成含义为实验和结论已形成，结论包括“没有可量化收益”；当前仍保有其 FreeRedis/CSRedis 原始 JSON，位于 `artifacts/ai-tasks/BING-CACHE-TODO-20261006-035/` 和 `artifacts/ai-tasks/BING-CACHE-TODO-20261006-037/`。030 的直接 JSON 组装职责测试位于 [CacheJsonBatchTest](../../framework/tests/Bing.Caching.Tests/CacheJsonBatchTest.cs)，不能只用物理键测试代替该职责。

### 动态 API、SDK 与 SQL 缓存

| 任务 | 本次状态 | 当前实现、职责测试与历史验收 |
| --- | --- | --- |
| [BING-DYNAMIC-API-20260925-001](completed/dynamic-api.md#BING-DYNAMIC-API-20260925-001) | 已完成（计划范围） | MVC 动态注册、描述与原生代理均存在；[端到端测试](../../framework/tests/Bing.DynamicApi.Client.Tests/BingDynamicApiEndToEndTest.cs) 的 `DynamicClient_UsesLiveDefinitionAndCallsMvcActionThroughTestServer`；原 Review PASS、FIX-001–008 CLOSED |
| [BING-WEBAPICLIENT-SDK-20260925-001](completed/dynamic-api.md#BING-WEBAPICLIENT-SDK-20260925-001) | 已完成（计划范围） | [AddBingWebApiClient](../../framework/src/Bing.DynamicApi.WebApiClient/Bing/BingWebApiClientServiceCollectionExtensions.cs)；[SDK 测试](../../framework/tests/Bing.DynamicApi.WebApiClient.Tests/BingWebApiClientEndToEndTest.cs) 的 `DeclaredRoutesAndJsonPayloadsMatchDynamicMvcEndpoints`；原 execution 完成并有 pack 记录 |
| [BING-WEBAPICLIENT-SDK-CHECK-20260925-002](completed/dynamic-api.md#BING-WEBAPICLIENT-SDK-CHECK-20260925-002) | 已完成（计划范围） | 同一 SDK 测试文件的 `RegistrationPreservesHttpHostConfiguredBeforeTheExtension`、`ContextAccessorIsReadForEveryRequest`、`MultipartUploadRawTextAndDownloadKeepTheirExpectedOwnership` 可定位；原双 TFM 各 10/10 |
| [BING-WEBAPICLIENT-SDK-OPT-20260925-003](completed/dynamic-api.md#BING-WEBAPICLIENT-SDK-OPT-20260925-003) | 已完成（计划范围） | [RequestContextHandler](../../framework/src/Bing.DynamicApi.WebApiClient/Bing/BingWebApiClientRequestContextHandler.cs) 逐次清理/读取头；同 SDK 测试的 `ContextHeadersRemainIsolatedForConcurrentRequests`、`MultipleSdkContractsKeepTheirHeaderConfigurationIndependent`；原 Review PASS、各 TFM 13/13 |
| [BING-SQL-RC-CACHE-TEST-HARDENING-20260907-001](completed/sql-metadata-cache.md#BING-SQL-RC-CACHE-TEST-HARDENING-20260907-001) | 已完成（局部范围） | `DefaultEntityMappingResolver` 失败 Lazy 条件移除；[EntityMappingCacheIsolationTest](../../framework/tests/Bing.Data.Sql.Tests/EntityMappingCacheIsolationTest.cs) 的 `Resolve_WhenConcurrentMetadataFailureIsRetried_ShouldAllowAllWaitersToRetry`；[SqlMutationPlanCacheKeyTest](../../framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/SqlMutationPlanCacheKeyTest.cs) 的 `Create_WhenAnyPlanDimensionChanges_ShouldReturnDifferentKey`；原独立 Review 与职责测试摘要保留 |

## 已执行的合并、删除与保留清单

**用户已确认，采用先合并、修正引用，再删除原快照的方式。** 精确处理下列 7 份 Markdown；没有直接丢弃而不留摘要的文件，也没有扩大到运行产物或其他任务。既有本地备份保留，不重复打包。

### 合并后删除：2 个任务、7 份 Markdown

| 任务 | 已归入 | 保留的内容及边界 |
| --- | --- | --- |
| BING-CACHE-TODO-20261006-006 | [缓存总览：跨进程/单节点证据](completed/caching.md#BING-CACHE-TODO-20261006-006) | 原任务 ID、三 Provider 五类进程场景、TFM/历史通过数、生产职责到测试映射；总体性能预算仍指向 002/004，拓扑和总线后续补证指向 079/084/085/089–092 |
| sql-lambda-query-api-v4 | [SQL 查询 API 演进](completed/sql-query-api.md#sql-lambda-query-api-v4) | 原任务 ID、Round 7 PASS、原子 From/Join 与 FIX-011/012、SQLite/Provider 历史边界；明确高元数 API 已由 V6 删除，当前接口以 V6 和当前源码为准 |

精确删除文件（路径相对 `ai_docs/tasks/`）：

- `BING-CACHE-TODO-20261006-006/execution.md`
- `BING-CACHE-TODO-20261006-006/plan.md`
- `BING-CACHE-TODO-20261006-006/test-mapping.md`
- `BING-CACHE-TODO-20261006-006/TODO.md`
- `sql-lambda-query-api-v4/execution.md`
- `sql-lambda-query-api-v4/plan.md`
- `sql-lambda-query-api-v4/review.md`

V4 的历史 Review 引用已由 `BING-SQL-QUERY-REFACTOR-20260824-001/plan.md` 迁到 `completed/sql-query-api.md#sql-lambda-query-api-v4-review`；只修正引用，不改变该未完成计划的目标、背景或验收标准。006 在总索引和缓存状态索引中也保留原 ID 入口。

### 保留：22 个任务、103 份 Markdown

下表列出全部原文件，路径相对 `ai_docs/tasks/`；102 份逐字节不变，一份查询重构计划仅修正上述历史引用。各任务目录内的非 Markdown 复现源码、项目文件、脚本及其依赖同样保留，不进行整目录删除。

| 任务 | 原文件（全部保留） |
| --- | --- |
| [BING-CACHE-HARDENING-20261004-001](BING-CACHE-HARDENING-20261004-001/plan.md) | `execution.md`、`performance.md`、`plan.md`、`review.md`、`bench/README.md` |
| [BING-CACHE-PERF-20261006-007](BING-CACHE-PERF-20261006-007/plan.md) | `execution.md`、`plan.md`、`test-mapping.md`、`TODO.md` |
| [BING-CACHE-PERF-20261006-008](BING-CACHE-PERF-20261006-008/plan.md) | `execution.md`、`plan.md`、`test-mapping.md`、`TODO.md` |
| [BING-CACHE-PERF-STREAM-20261004-002](BING-CACHE-PERF-STREAM-20261004-002/plan.md) | `execution.md`、`performance.md`、`plan.md`、`production-symbol-tests.md`、`review.md`、`bench/README.md` |
| [BING-CACHE-TODO-20261004-003](BING-CACHE-TODO-20261004-003/plan.md) | `execution.md`、`performance.md`、`plan.md`、`review.md` |
| [BING-CACHE-TODO-20261005-004](BING-CACHE-TODO-20261005-004/plan.md) | `execution.md`、`performance-audit.md`、`performance.md`、`plan.md`、`review.md`、`test-mapping.md`、`TODO.md` |
| [BING-CACHE-TODO-20261005-005](BING-CACHE-TODO-20261005-005/plan.md) | `execution.md`、`plan.md`、`review.md`、`test-mapping.md`、`TODO.md` |
| [BING-CACHE-TODO-20261007-099](BING-CACHE-TODO-20261007-099/plan.md) | `execution.md`、`migration.md`、`plan.md`、`test-mapping.md`、`TODO.md` |
| [bing-framework-docs](bing-framework-docs/plan.md) | `plan.md` |
| [BING-SQL-API-HARDENING-20260824-001](BING-SQL-API-HARDENING-20260824-001/plan.md) | `api-migration.md`、`benchmark-report.md`、`execution.md`、`final-summary.md`、`plan.md`、`progress.md`、`review-report.md`、`review.md`、`source-generator-decision.md`、`test-report.md` |
| [BING-SQL-QUERY-REFACTOR-20260824-001](BING-SQL-QUERY-REFACTOR-20260824-001/plan.md) | `execution.md`、`plan.md`、`review.md` |
| [BING-SQL-RC-FINAL-HARDENING-20260908-001](BING-SQL-RC-FINAL-HARDENING-20260908-001/plan.md) | `execution.md`、`plan.md`、`review.md` |
| [BING-SQL-RC-HARDENING-20260825-001](BING-SQL-RC-HARDENING-20260825-001/plan.md) | `api-migration.md`、`benchmark-report.md`、`execution.md`、`final-summary.md`、`plan.md`、`progress.md`、`review-report.md`、`review.md`、`test-report.md` |
| [BING-SQL-RC-HARDENING-20260826-002](BING-SQL-RC-HARDENING-20260826-002/plan.md) | `benchmark-baseline.md`、`decisions.md`、`execution.md`、`plan.md`、`progress.md`、`review.md`、`verification-report.md` |
| [BING-SQL-RC-HARDENING-20260827-003](BING-SQL-RC-HARDENING-20260827-003/plan.md) | `execution.md`、`plan.md`、`review.md` |
| [BING-SQL-RC-HARDENING-20260828-004](BING-SQL-RC-HARDENING-20260828-004/plan.md) | `artifact-index.md`、`benchmark-report.md`、`execution.md`、`integration-test-report.md`、`plan.md`、`progress.md`、`review.md`、`unit-test-report.md`、`verification-report.md` |
| [BING-SQL-RC-HARDENING-20260904-001](BING-SQL-RC-HARDENING-20260904-001/plan.md) | `execution.md`、`plan.md`、`review.md` |
| [BING-SQL-RC-HARDENING-20260906-001](BING-SQL-RC-HARDENING-20260906-001/plan.md) | `execution.md`、`plan.md` |
| [BING-SQL-RELEASE-HARDENING-20260831](BING-SQL-RELEASE-HARDENING-20260831/plan.md) | `execution.md`、`plan.md`、`progress.md`、`review.md` |
| [BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903](BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903/plan.md) | `execution.md`、`plan.md`、`review.md` |
| [sql-mapping-cache-versioning](sql-mapping-cache-versioning/plan.md) | `benchmark-notes.md`、`execution.md`、`plan.md` |
| [sql-query-dev-v6-api-refactor](sql-query-dev-v6-api-refactor/plan.md) | `execution.md`、`plan.md`、`review.md` |

### 总览、索引与本地证据

| 文件/集合 | 处理结果 |
| --- | --- |
| `completed/caching.md` | 保留 90 个历史任务和证据入口，并入 006，共 91 个；按现有大功能章节去重 |
| `completed/dynamic-api.md` | 保留四个已完成任务；服务端、原生客户端、声明式 SDK 分工已经清晰，本轮无新增删除候选 |
| `completed/sql-metadata-cache.md` | 保留唯一已完成缓存职责任务；版本化映射在原始性能证据待确认前不移入“已验收完成”集合 |
| `completed/sql-query-api.md` | 新增 V4 历史结论、可复用原子状态能力及 V6 替代关系，保留任务与 Review 锚点 |
| `README.md` | 汇总 119 个任务，更新 006/V4 入口、四份总览及 22 个保留任务的状态 |
| `CLEANUP.md`、`ARTIFACTS.md` | 前者追加本轮 7 文件处置；后者保留原归档规则，不重复执行历史清单 |
| `STATUS-AUDIT.md` | 本次状态、共性分类、精确处置和验证结果的统一记录 |
| `artifacts/task-doc-audit/tasks-before-cleanup.zip` 与 `artifacts/ai-tasks/` | 保留既有原始快照及测量/测试证据；不重复归档、不删除 |
| `artifacts/plans/BING-SQL-RS0026-PROVIDER-TEST-HARDENING-20260903-plan.md` | 保留实际批准计划；它是标准 plan.md 引用的依据，不是可随意丢弃的运行日志 |
| 任务下的 `bench/`、`cluster/`、`redis-cluster-local.sh` 与 task004 三个运行/汇总/门控脚本 | 保留复现入口；不因 Markdown 合并而删除程序 |

本轮独立任务目录由 24 个减少为 22 个；95 个既有已合并 ID 仍保留，再增加 006 与 V4 的总览追溯入口，共 97 个合并 ID；连同 22 个保留任务，覆盖总数仍为 119。Markdown 总数由本轮清理前的 117 份（含本清单）减至 111 份。

## 本轮核对与清理后验证结果

| 检查 | 本轮实际结果 |
| --- | --- |
| 任务覆盖 | 索引中的 119 个 ID 与本清单一致，119 个唯一状态条目，缺失 0、重复 0、额外 0；97 个总览锚点与 22 个保留任务一一对应 |
| 状态汇总 | 95 个既有局部完成任务 + 1 个单节点范围完成任务（006）+ 1 个历史完成但被替代任务（V4）+ 22 个部分完成/待确认任务 = 119 |
| 原文件保护 | 22 个保留任务的 103 份 Markdown 全部存在；102 份与整理前 SHA-256 相同，查询重构计划仅替换一处历史 Review 引用，其余内容一致 |
| 精确删除集合 | 原 110 份任务 Markdown 仅减少已确认的 006/V4 共 7 份；删除与保留集合无交集，未删除任何未完成任务 |
| 源码/证据定位 | 缓存 90 项的 149 个证据引用可定位；动态 API/SDK 总览 19 个不同源码/测试路径可定位；本清单 22 个额外内联方法标识可在所链接测试源码定位 |
| 文档链接 | 本次更新的六份索引、清单及模块总览共 830 个本地链接/显式锚点可定位；缺失 0 |
| 文档与差异检查 | `eng/ci/check-docs.py --quiet` 的 13 项检查全部通过；`git diff --check` 通过 |
| 本轮文件变更 | 新增本清单与 SQL 演进摘要，修订索引/缓存总览/清单，迁移一处原计划引用并删除 7 份已合并快照；未修改生产源码、测试、运行产物或 Git 暂存区 |

合并先于删除完成，删除范围与已确认清单一致；119 个 ID 仍可追溯，未完成/待确认任务的目标、背景及验收标准没有改写。既有本地原始备份保留，7 份被删除快照亦可由 Git 历史恢复。

本轮没有重跑历史构建、功能/集成测试或性能基准；因此不把历史通过数、当前符号存在或文件完整性检查表述为本轮运行通过，也未改变原任务的性能阈值、外部门禁或发布结论。
