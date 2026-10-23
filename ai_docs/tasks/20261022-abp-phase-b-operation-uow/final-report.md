# 阶段 B 实施与 Review 修复结果

## Round 3 当前候选（限定 FIX-005）

候选为 `phase-b-review-fix-round-3-fix005-2026-10-23-v1`，只包含 Review 3 `FIX-005` 的限定修复；关联清单 `artifacts/review-fix-round-3/candidate-fix005-v1.json` 以前序 `phase-b-review-fix-round-2-2026-10-23-v1` 的 262 项清单为基线。本轮真实起点、5 个变更文件 SHA-256 与 Review 3 只读副本位于 `artifacts/review-fix-round-3/startpoint/`。未覆盖旧 artifacts，未修改 `review.md` 或父路线图，未 commit/push。

Review 4 独立复核后，原 8 项 finding 现为 **8 CLOSED / 0 OPEN_ACTIONABLE**，无新增 MUST_FIX/SHOULD_FIX。FIX-005 的修复将单槽 root 保护改为按 root id 的集合：每个仍活动的祖先 root 约束在更深 requiresNew 进入、退出及启动失败后持续有效；root 正常退出或 Begin observer 失败时只释放对应记录，`Current` 在发布 frame 前失败时保持原 root。

| 验收项 | 直接证据 | 当前结果 |
| --- | --- | --- |
| 三层 A pending→B→C→退出 C 后 B 的 Complete/SaveChanges/Commit 均拒绝 | `RequiresNewNested_WhenDeepestRootExits_ShouldKeepMiddleRootProtected`（sync/async × 3 路径） | 16 项精准集 16/0/0；B rollback-only、受保护 SQL 0、commit 0、外层回滚后精确空行集 |
| entity/action/event/claim 不能跨更深 root 迁移，action/event 0 | `RequiresNewNested_WhenDeepestRootExits_ShouldRejectOuterAuxiliaryState`（4 类 × sync/async） | 全部 I/O 前 `InvalidOperationException`；action/event 调用 0 |
| Begin observer/DetectChanges 在发布前失败 | `RequiresNewNested_WhenBeginObserverThrows_ShouldKeepCurrentRootProtection` | `Current` 保持中间 root，既有保护不丢，后续写入在 I/O 前拒绝 |
| 合法 inner-only 与真新 DI scope 保留 | `RequiresNewNested_InnerOnlyMiddleRoot_ShouldCommitAfterDeepestExitAndOuterRollback`、`ExecutorNewScope_ShouldCommitItsContextWhenOuterRootRollsBack` | inner-only 提交 1 行；executor 独立 Context/root 子行在 outer rollback 后保留 |

验证结果：EF Core net6 L2 235/0/0；Uow net6/net8 各 70/0/0；SQLite integration net6/net8 各 233/0/1（唯一 NotExecuted 为既有 provider contract 门控）。命令、stdout/stderr、exit、TRX 与逐 `UnitTestResult` 汇总在 `artifacts/review-fix-round-3/`。红证据 `l1-red/`（14 项目标嵌套失败）与最终绿证据 `l1-green/` 分开保留；首次 observer 测试因实体 backing-field 访问未触发 DetectChanges 的失败及随后 `l1-red-observer4/` 修正观察均保留，不改写旧失败。

Review 4 已独立验收本次实现与受影响测试为 PASS，FIX-005 CLOSED，8/8 CLOSED，OPEN_ACTIONABLE=0；是否关闭 FIX-005 不再由本执行器自行判断。B06 最终检查点已按 plan §11.3 执行，结果见下节：Release build PASS，默认完整 L4 因既有 GC 失败为 FAIL。无公共 API、API baseline、SDK、TFM、包版本或性能预算变化。

## B06 最终检查点结果

- 候选：`phase-b-review-fix-round-3-fix005-2026-10-23-v1`。Round 2 的 262 路径加 Round 3 的 5 文件增量；冻结前后均 262/262 SHA-256/length 匹配，mismatches=0。
- Build：`dotnet build Bing.All.slnx -c Release --no-restore --nologo -m:1`；exit 0，199 warnings，0 errors，57.6 秒。
- L4：`dotnet test Bing.All.slnx -c Release --no-build --no-restore --logger trx --results-directory .../L4/trx`；exit 1，103 TRX、6 个零结果 TRX。
- 逐 `UnitTestResult`：total 13,996；Passed 12,881；Failed 1；NotExecuted 1,114。net6.0：53 TRX（3 空）、6,716/0/593；net8.0：50 TRX（3 空）、6,165/1/521。
- 唯一失败：`Bing.Tests.Modularity.ModuleLifecycleTest.RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive`（net8，`Bing.Core.Tests`；`references.All(r => !r.IsAlive)` 为 False）。同方法历史 GC 弱引用失败（Round 1 为 net6、Review 前 L4-v2 为 net8；本轮 net8），因果仍 `NOT_DETERMINED`，本轮不隔离追绿、不扩修。
- 跳过范围：1,114 项全部计为 NotExecuted，不混入 Passed。按项目/TFM 分组的主要范围为：缓存集成 546（CSRedis 290、FreeRedis 256）、Dapper 外部 Provider 244、Admin 72（仅 net6）、Sample CAP 36、EasyCaching 140、FreeSQL 52；其余为既有配置/probe 门控。完整分组见 `L4/not-executed-groups.json`，逐项见 `L4/not-executed.json`；本检查点未改变这些门控。
- 空 TRX：`Bing.Localization.Resources`、`Bing.TestShare.MySql`、`Bing.TestShare` 各 net6/net8，共 6 个。
- 环境：独立 PowerShell 子进程；外部/probe `RUN_*` 显式 false；移除 `RUN_METADATA_PATH` 及连接/broker 变量（只记录变量名）；无 runsettings、无 filter/全局-f；保留默认本地 SQLite 与 xUnit 并行。SDK `10.0.400`；使用 `Microsoft.NETCore.App 6.0.36` 与 `8.0.30`（TRX 显示 net8 runtime 8.0.30）。
- 文档：产品文档未变；Review 4 的 `DOCS_CONTENT_STATUS/DOCS_CHECK_STATUS` 为 `PASS / REUSED_UNCHANGED_SCOPE`，复用已接受 13 项 checker 与编译例，未重跑。
- 分栏结论：Implementation PASS（8/8 CLOSED）；受影响 Test PASS（Review 4）；Release build PASS；默认完整 L4 FAIL（已知 GC）；整体 Release 不宣称 PASS。EXT-002 `BLOCKED_EXTERNAL`、APR-001 `BLOCKED_APPROVAL`、DEF-001 GC `NOT_DETERMINED` 独立保留。B06 仍为 PARTIAL，不写成 10/10 全 PASS。
- 证据：`artifacts/final-candidate-after-fix005/candidate-freeze.json`、`change-impact.md`、`release-build/summary.json`、`release-build/stdout.txt`、`release-build/stderr.txt`、`L4/result.json`、`L4/summary.json`、`L4/failures.json`、`L4/not-executed*.json`、`L4/trx/`、`checkpoint-summary.json`、`report-encoding-check.json`、`docs-reuse.md`。

## Round 2 候选（历史）


候选为 `phase-b-review-fix-round-2-2026-10-23-v1`。262 个源码、测试、harness、Sample 和支持/迁移文档文件的当前 SHA-256 清单位于 `artifacts/review-fix-round-2/candidate-review-fix-round-2-v1.json`；该清单保留 Round 1 原有 259 项并补入 3 个遗漏文件。基于 B00 保存的 dirty working tree，起点 HEAD 为 `5065c38ced05a95088e17d89d96c9a1610e37159`，SDK 为 `10.0.400`。没有从 HEAD 覆盖工作区，也没有修改 `review.md` 或父路线图状态。

Round 2 按 `recommended` 修复 FIX-003、FIX-005、FIX-007、FIX-008。Sol/high 的 Round 2 `review.md` 原样保留 `NEEDS_FIX`，本报告只交付修复证据，Round 3 由主线程安排原 Reviewer 复审。FIX-001/002/004/006 与 EXT-001 沿用已关闭状态，本轮没有新证据要求重开。

## Round 2 修复结果

| Finding | 当前直接验收 | 当前结果 |
| --- | --- | --- |
| FIX-003 | `EfCoreOperationSqliteTest.SynchronousStoreCount_WhenOperationTokenIsCancelled_ShouldRejectBeforeSqliteIo` 覆盖 owner caller、deadline、participant caller 的真实同步 `Count`、`FindAll`、`FindById`；逐次断言有效 OCE、命令数增量 0、rollback-only、最终 commit 0 和空行集。`SynchronousStoreReads_WhenOperationIsActiveOrAbsent_ShouldRunNormally` 保留成功/无 operation 对照。 | EF 精准 L1 net6 27/0/0；EF 完整 L2 net6 219/0/0；SQLite L2 net6/net8 各 233/0/1。原红证据 `artifacts/review-round-2/ef-boundaries/stdout.txt` 显示 Count 未取消且发出 SQL。 |
| FIX-005 | `RequiresNewSameScope_ExplicitSaveBeforeHandler_ShouldRejectOuterPendingEntity` 覆盖同步/异步 SaveChanges 与 Commit；`RequiresNewSameScope_ExplicitSaveBeforeHandler_ShouldRejectOuterAuxiliaryState` 覆盖 action/event/claimed；另有 query-first claim 及 inner-only Context 成功提交对照，并保留独立 executor 新 scope 子提交对照。使用真实 SQLite 行集和 I/O/提交计数。 | EF 精准 L1 net6 27/0/0；EF 完整 L2 net6 219/0/0；SQLite L2 net6/net8 各 233/0/1。Round 2 红证据 `artifacts/review-fix-round-2/l1/ef-boundaries-red-v2/stdout.txt` 有 10 个失败场景。 |
| FIX-007 | `AutomaticUnitOfWorkAdapterTest.CapTraceAndAutomaticBoundary_ShouldPreserveTargetFaultedCancellationAndOriginalException` 经 `EnableBingAutomaticUnitOfWork` 的真实代理组合链直接检查同步 OCE、Task/Task<T> 预取消和 target-enter 后取消、4 种 faulted OCE、source-backed ValueTask/ValueTask<T> 单次消费取消/成功；观察器不重新 async 包装返回值。检查原异常/token、target/GetResult 调用次数及取消/成功 commit、CommitAfter、外层成功回调次数。 | automatic 链 L1 net6 18/0/0；受影响 AOP L2 net6/net8 各 178/0/0，EventBus L2 各 409/0/1（唯一跳过项为有界 probe）。早期失败迭代保留在同级 L1 目录。 |
| FIX-008 | 核对 `evidence-mechanical.json` 中 14 份 L2 原始 command、TestDefinition、UnitTestResult、stdout 和退出码，报告与 `review.md` 均未改写。历史 EF v1 193/3/0 与 v2 196/0/0 分列；Sample v2 实际 26/0/18，SQLite 实际 233/0/1。Round 2 不改产品文档，未因报告计数修正重复运行 checker。 | 核对表 `artifacts/review-fix-round-2/l2-raw-reconciliation.md`；262 项当前候选清单覆盖所有 Round 2 修改文件及 3 个 Round 1 漏项。Release build 和完整 L4 标记 `NOT_REFRESHED_AFTER_FIX`，不复用 Round 1 结果。 |

Round 2 当前完整 L2 汇总：Bing.Uow.Tests net6/net8 各 70/0/0；Bing.Aop.AspectCore.Tests 各 178/0/0；Bing.EntityFrameworkCore.Tests net6 219/0/0；Bing.Dapper.Sqlite.Tests.Integration 各 233/0/1；Bing.Events.Tests 各 409/0/1。原始命令、stdout/stderr、退出码和 TRX 位于 `artifacts/review-fix-round-2/l2/`。跳过不计通过。

当前候选的 Release solution build 与默认完整 L4 均为 `NOT_REFRESHED_AFTER_FIX`；Round 1 的 Release/L4 结果只保留为历史证据，不宣称当前候选 Release PASS。MySQL/Rabbit consumer 证据与旧候选代码未受本轮影响的部分按已关闭 EXT-001 复用；本轮不重跑外库、probe 或 GC。

## Round 1 Findings（历史候选）

| Finding | 修复与当前直接验收 | 当前结果 |
| --- | --- | --- |
| FIX-001 | 真实代理 `OperationRootLegacyMarkerParticipantTest.ExistingOperationRoot_ShouldTreatMarkedCallsAsParticipants`、`CaughtMarkedCallFailure_ShouldPoisonExistingOperationRoot`、`MarkedCallOutsideOperationRoot_ShouldKeepLegacyCommit`；两个注册顺序、同步/异步、捕获异常、root 外旧行为和通知去重。 | AOP L1 net6/net8 各 61/0/0；AOP L2 各 171/0/0。证据在 `artifacts/review-fix-round-1/l1/aop-fix001-combined-net6/`、`aop-fix001-combined-net8/` 和 `l2/aop-*`。 |
| FIX-002 | `OperationScopeLifecycleTest.SaveHandlerRollbackOnly_ShouldRollbackEveryResourceWithoutCallbacks`、`CapturedWriteFailureMarkedRollbackOnly_ShouldNotCommit`、`ReentrantRootOperationDuringSave_ShouldBeRejectedAndRollback`、`SavingHandlerPending_ShouldRejectOtherExecutionFlowAdmission` 覆盖同步/异步毒化、已捕获写失败、重入、跨流准入、一次回滚/释放、零提交/回调及正常新增资源。 | L1 net6/net8 各 19/0/0；Uow L2 各 70/0/0。`artifacts/review-fix-round-1/l1/uow-fix002-*`、`l2/uow-*`。 |
| FIX-003 / FIX-004 | Round 1 时 EF 显式保存、Store I/O、Dapper reader/stream 使用当前 operation/participant token；具体回归与计数仅代表 Round 1 候选。 | Round 1 历史结果：Uow L1 net6/net8 各 3/0/0；Dapper token L1 net6 2/0/0；EF L2 net6 196/0/0；SQLite L2 net6/net8 各 233/0/1。当前 FIX-003 同步 Store I/O 见上表。 |
| FIX-005 | Round 1 的 requiresNew 外层 Context 测试未覆盖显式保存、query-first claim 等完整边界；当前正式回归见上方 Round 2 表。 | Round 1 历史 EF L2 net6 196/0/0；SQLite L2 net6/net8 各 233/0/1。Round 1 精准 L1 `artifacts/review-fix-round-1/l1/ef-fix005-net6/`。 |
| FIX-006 | `BingOperationExecutorTest.Execute_ShouldCopyTraceBeforeConstructionAndRestoreAfterNestedAndParallelWork`、`Execute_ShouldRestoreParentTraceAfterResolutionBusinessCancellationAndCleanupFailures` 检查解析前四字段副本、嵌套/并行、异常/取消和清理失败后的父引用恢复。 | 精准 L1 net6 6/0/0；Uow L2 net6/net8 各 70/0/0。`artifacts/review-fix-round-1/l1/uow-fix006-net6-v2/`、`l2/uow-*`。 |
| FIX-007 | Round 1 的 automatic 矩阵不包含本轮要求的全部新模式实际代理组合链；Round 1 EF/Sample/CAP 结果不代替当前 finding 直接证据。 | Round 1 历史 AOP 精准 net6/net8 各 16/0/0；EF 精准 net6 30/0/0；Sample entrypoint L1 各 14/0/0。Round 1 Sample v1 L2 各 26/0/17、v2 各 26/0/18；跳过不计通过。 |
| FIX-008 | 更新 Bing.Uow README、支持矩阵、迁移指南、Sample README 与 XML/API 文档；`UseSqlite` 指向实际 EF SQLite API 和 Sample Startup。`UnitOfWorkDocumentationExamplesTest.OperationExecutorExample_ShouldResolveAfterBeginAndReturnMaterializedResult` 是当前可编译 executor 主例。 | Uow L2 net6/net8 各 70/0/0；docs checker 13 项通过、退出 0。`artifacts/review-fix-round-1/l2/uow-*`、`artifacts/review-fix-round-1/final-candidate/docs/direct-python/`。 |

上述 finding 结果是实施证据；是否达到各 finding 的独立验收条件由 Sol/high 复审决定。

## L2 与 L3

| 层级 | 项目/TFM | Passed / Failed / NotExecuted | 证据 |
| --- | --- | --- | --- |
| L2 | Bing.Uow.Tests net6.0/net8.0 | 每 TFM 70 / 0 / 0，退出 0 | `artifacts/review-fix-round-1/l2/uow-net6-current-v1/`、`uow-net8-current-v1/` |
| L2 | Bing.Aop.AspectCore.Tests net6.0/net8.0 | 每 TFM 171 / 0 / 0，退出 0 | `artifacts/review-fix-round-1/l2/aop-net6-current-v1/`、`aop-net8-current-v1/` |
| L2 | Bing.Dapper.Core.Tests net6.0/net8.0 | 每 TFM 181 / 0 / 0，退出 0 | `artifacts/review-fix-round-1/l2/dapper-core-net6-current-v1/`、`dapper-core-net8-current-v1/` |
| L2 | Bing.EntityFrameworkCore.Tests net6.0 | 196 / 0 / 0，退出 0 | `artifacts/review-fix-round-1/l2/ef-net6-current-v2/` |
| L2 / SQLite | Bing.Dapper.Sqlite.Tests.Integration net6.0/net8.0 | 每 TFM 233 / 0 / 1，退出 0 | `artifacts/review-fix-round-1/l2/sqlite-net6-current-v1/`、`sqlite-net8-current-v1/` |
| L2 | Bing.Samples.SqlUnitOfWork.Tests net6.0/net8.0 | v1 每 TFM 26 / 0 / 17；v2 每 TFM 26 / 0 / 18，退出均为 0 | `artifacts/review-fix-round-1/l2/sample-net60-current-v2/`、`sample-net80-current-v2/` |
| L3 | 真实 SQLite contract runner net6.0 | 1 / 0 / 0，退出 0 | `artifacts/review-fix-round-1/l3/sqlite-contract-runner-net6-current-v2/`；TRX 位于根 `artifacts/test-results/phase-b-review-fix-round1-sqlite-net6/` |
| L3 | Sample CAP/MySQL/Rabbit 独立消费方法 net6.0 | 1 / 0 / 0，退出 0 | `artifacts/review-fix-round-1/l3/cap-independent-process-net6-v6/` |

### EXT-001 独立消费

`CapMySqlIntegrationTests.OperationWebHost_WithIndependentConsumer_ShouldPersistExactDualSourceDeliveryAcrossEntryPoints` 启动真实 Sample producer Web host 和 standalone consumer 两个子进程。测试跑过 MySQL/Rabbit 任务隔离实例；测试结束时 producer/consumer 子进程均已退出。v6 TRX 记录 producer PID 60680、consumer PID 100048 及全部 operation ID；独立 journal `artifacts/review-fix-round-1/l3/cap-independent-bdcec1dede7745a2a5cd1ea4e9776d18/producer-operations.jsonl` 记录 8 项操作的最终状态：7 项走新 executor，另 1 项为 operations-enabled 的旧手工 partial-commit root（进入新 saving/completion），共 5 Completed、1 Failed、1 Cancelled、1 部分提交场景 Failed。没有归档 child stdout/stderr/exit，未据此补写证据。

该方法验证双 MySQL source 的 EF-first、HTTP、真实 worker 与 Hangfire Server 成功投递，producer 失败/HTTP 取消后业务行、两侧 Outbox、business event、consumer inbox/effect/attempt 均为零，且后续成功操作可继续；部分提交对照精确验证 orders 已提交并被 consumer 消费，而 reporting 行、Outbox、business event 与 consumer delivery 为零。成功消息按 event/source/operation/trace/root/consumer/effect/attempt 断言单条精确结果；consumer delivery 等待从真实 CAP broker 消费，不直接插入 inbox/effect。TRX 为 1 Passed / 0 Failed / 0 NotExecuted；Round 2 Reviewer 已关闭 EXT-001，执行器本轮复用未受影响证据，不重复运行外库。

## Round 1 历史候选门禁

| 门禁 | 当前结果 | 原始证据 |
| --- | --- | --- |
| Release solution build | `dotnet build H:/Bing_Framework/Bing.NetCore/Bing.All.slnx -c Release --no-restore --nologo -m:1` 退出 0；0 errors、170 warnings。仅为 Round 1 候选结果。 | `artifacts/review-fix-round-1/final-candidate/Release-build/` |
| 默认完整 L4 | 103 TRX，12,828 Passed / 1 Failed / 1,114 NotExecuted；退出 1。net6：53 TRX、6,669/1/593；net8：50 TRX、6,159/0/521。6 份空 TRX 单列，NotExecuted 不计通过。仅为 Round 1 候选结果。 | `artifacts/review-fix-round-1/final-candidate/L4/`，`summary.json` 以 TRX `UnitTestResult` outcome 统计。 |
| L4 唯一失败 | `Bing.Tests.Modularity.ModuleLifecycleTest.RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive`，本候选发生于 net6。阶段 A 和 Review 前候选 net8 已有同一 GC 弱引用断言失败，因果仍 `NOT_DETERMINED`；按计划不扩修 GC、不单测重跑冲销。 | 当前失败 TRX `artifacts/review-fix-round-1/final-candidate/L4/trx/jianx_DESKTOP-8CUU71G_2026-10-23_03_19_30_net6.0[2].trx`；Review 前结果在 `artifacts/final-candidate/L4-v2/`。 |
| 文档检查 | `C:/Users/jianx/.pyenv/pyenv-win/versions/3.12.1/python.exe -X utf8 H:/Bing_Framework/Bing.NetCore/eng/ci/check-docs.py --quiet` 退出 0，13 项全部通过。PATH shim 的首次启动尝试退出 9009；改用已安装 Python 3.12.1 绝对路径执行成功，两次结果均留档。 | `artifacts/review-fix-round-1/final-candidate/docs/`、`docs/direct-python/`。 |

Round 1 candidate Release status 为 FAIL，因为该候选默认完整 L4 有一个因果未定的 GC 失败。Round 2 候选没有刷新 Release/L4，状态为 `NOT_REFRESHED_AFTER_FIX`；不得把 L1/L2/L3 的通过数拼成当前候选完整 L4 PASS。旧候选的 L4 12,715/1/1,112（失败在 net8）和 Round 1 L4 分开保留。

## 范围与剩余事项

- Round 2 已处理本轮四个 OPEN_ACTIONABLE finding，L1 与受影响完整 L2 已运行；独立关闭状态由 `review.md` 的原 Sol/high Reviewer 决定，本报告不代替复审。
- EXT-001 已由 Round 2 Reviewer 关闭；本报告保留 7 个新 executor operation 与 1 个旧手工 partial-commit root 的准确归因。
- EXT-002 受保护 MySQL 发布证据仍受 dirty-source validator 阻塞；不清理或覆盖用户工作区，不自行制造 protected release artifact。
- 两个有界 probe 的阈值未获批准；SQL Uow probe 的历史捕获限制保留、不重跑。Core GC 因果待独立证据；FreeSQL B04 与 C/D 不在本次范围。
- 没有新增/升级依赖，没有变更 SDK、TFM、API baseline 或性能预算。Sample 的 `UseSqlite` 是外部 EF SQLite API；自动保存仅在 opt-in；无跨数据库原子提交保证，执行器返回值须在新 scope 内完成物化。
- Review 前曾把 10 个计划编号写作全部完成，也曾用历史 CAP 22/22 表述能力；两项均已纠正，原始输出和 Review artifact 未改写。
