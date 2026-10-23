<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: 20261021-sql-multidb-unit-of-work
AI_EXECUTION_FINISHED_AT: 2026-10-21T09:45:07Z
AI_EXECUTION_STARTED_AT: 2026-10-21T09:03:20.156Z

# 实施执行报告

## 候选与状态

- 计划：PLAN_READY_REVISED，725 行，按完整计划实施；起始 HEAD 为 5065c38ced05a95088e17d89d96c9a1610e37159。
- 候选边界和初始脏文件清单：artifacts/candidate-manifest.md、artifacts/initial-worktree-status.txt。保留了 F008 与 merged-code-audit 已有修改；没有 reset、staging、commit、push、PR 或 PublicAPI baseline 改动。
- REVIEW_FIX_STATUS: PASS；Sol/high Review Round 3 独立验收通过，2 轮修复后 9 项 Finding 全部 CLOSED，OPEN_ACTIONABLE=0。权威结论见 [review.md](review.md)。
- IMPLEMENTATION_STATUS: PASS；计划 T01–T08 验收 8/8，T07 的测试配置差异经独立审查接受为 DEVIATED_OK。
- TEST_STATUS: PASS；Round 2 Dapper Core net6/net8 各 179/179（Debug）；SQLite Integration net6/net8 各 229 passed、1 个既有 Provider gate skip（Debug）；EF Core net6 最新 165/165（Release）。Sol 另行重新构建并通过 Release 精准 SQLite net8 35/35、EF net6 6/6，以及独立边界诊断 6/6。原始证据分别见 `artifacts/review-fix-round-2/`、`artifacts/review-round-3/`；未将 Debug 完整套件表述为 Release。
- PERFORMANCE_EVIDENCE_STATUS: Round 2 复用 FIX-008 已确认直接 host/child exit 0 的 16/16 有界 probe，未重跑；阈值仍 BLOCKED_APPROVAL。
- RESOURCE_EVIDENCE_STATUS: Round 2 复用的 probe 观测到 1,554 connections opened/closed/disposed、314 physical transactions begin/commit/dispose、结束 open=0、peak=2。
- EXTERNAL_GATE_STATUS: BLOCKED_EXTERNAL；未运行需要 MySQL/PostgreSQL/SQL Server/Oracle/Doris 服务的 integration 项目。
- RELEASE_STATUS: BLOCKED；本地实施和独立验收 PASS，外部服务与正式性能预算尚未完成。
- GOAL_STATUS: STOPPED_BLOCKED；与独立报告一致。OPEN_ACTIONABLE=0，停止自动修复；外部验证和预算状态不触发新的 Fix。交付汇总见 [final-report.md](final-report.md)。

## REVIEW_FIX 第 2 轮（历史实施记录）

本轮按 `.agents/skills/fix-review/SKILL.md` 的 `fixScope=recommended` 只处理 Round 2 的 FIX-004 与 FIX-009；原 FIX-001/002/003/005/006/007/008 七项保持 CLOSED，不重新开启。起始和结束 `review.md` SHA-256 均应为 `C47F0B40CFDAE2C5E618571A680ED41505EE10450B9FB150CA6B71EA5E69CA65`；该文件未编辑。Round 2 CIA 与冻结工作区分别见 `artifacts/review-fix-round-2/CIA.md`、`starting-state.md`。保留 F008、merged-code-audit 和其他起始脏文件；本轮没有 API、包版本或 PublicAPI baseline 变更，没有暂存/提交/推送/PR 或外部通知。

| Finding | 生产修复与直接回归 | Round 2 原始证据 |
| --- | --- | --- |
| FIX-004 | `SqlQueryBase.AttachUnitOfWorkResourceForExecution` 将异常标记放在活动 root 已绑定后的真实资源关联边界；保留并重抛原异常，不执行错误 context 的连接。纯描述/参数验证、显式 Independent/External/Owned 及既有资源租约冲突不通过该 catch 毒化 root。mapped Query-first 双 SQLite 文件测试现在覆盖同步与异步：先在 good 源写入，catch 物理库身份拒绝，断言 rollback-only、Complete 拒绝、EF 错误连接关闭，最后两个文件的外部行集均为 0。 | 原同步反例：`artifacts/review-round-2/sol-repro-001/`。修复前红色 EF 测试：`artifacts/review-fix-round-2/red-ef-net6-002/`。同步/异步精准 2/2：`artifacts/review-fix-round-2/fix004-sync-async-ef-net6-003/trx/fix004-sync-async-ef-net6-003.trx`；对应生产符号、方法与当前完整 EF TRX 见 `traceability.md`。 |
| FIX-009 | 参与式默认 Executor batch 持有同一个 Query/root 资源租约，所有分块命令使用该 executor 的冻结源与 root 事务；不创建或提交内部 `ISqlTransactionScope`，root 是唯一物理提交者。事务 root 即使 batch option 的 `UseTransaction=false` 也继续 enlist；要求事务的 batch 在非事务 root 中执行前拒绝并毒化 root。无 root 与显式 Owned/SQL transaction scope 保留原自有批次事务。直接回归覆盖 Insert/Update/Delete × sync/async × option × root Complete/Rollback、同源 Query、物理事务计数，以及命令失败、中途/预取消、受影响行数验证失败 catch 后 rollback-only；mapped EF 同样覆盖 sync/async × Complete/Rollback，同冻结映射 context/resource 验证提交/回滚和连接/事务释放计数。 | SQLite 精准 net6 31/31、net8 31/31：`artifacts/review-fix-round-2/fix009-sqlite-net6-001/fix009-sqlite-net6-001.trx`、`artifacts/review-fix-round-2/fix009-sqlite-net8-002/fix009-sqlite-net8-002.trx`。mapped EF sync/async 精准 4/4：`artifacts/review-fix-round-2/fix009-mapped-ef-sync-async-net6-005/trx/fix009-mapped-ef-sync-async-net6-005.trx`；EF 完整复验 165/165：`full-ef-net6-003/trx/full-ef-net6-003.trx`。 |

### Round 2 受影响验证

- Dapper Core Debug：net6/net8 完整项目各 179/179，分别见 `artifacts/review-fix-round-2/full-core-net6-001/`、`full-core-net8-001/`。
- SQLite Integration Debug：net6/net8 各 229 passed、1 个既有 Provider integration gate skip、0 failed，分别见 `full-sqlite-net6-001/`、`full-sqlite-net8-001/`。新增/涉及 batch contract 的精准套件 net6/net8 各 31/31（Debug），见上表。真实本地 SQLite 文件执行；skip 单独列出，不计为通过。
- EF Core Tests Release：最新 net6 完整项目 165/165，TRX 为 `artifacts/review-fix-round-2/full-ef-net6-003/trx/full-ef-net6-003.trx`；mapped admission 同步/异步直接用例 2/2 见上轮新增的 `fix004-sync-async-ef-net6-003/`，mapped batch 同步/异步 × Complete/Rollback 4/4 见上表。较早 162/163/163 结果及初次 xUnit 数据错误日志均保留，不作为当前完整结果。
- `red-ef-net6-002/`、`red-sqlite-net8-003/`、`red-sqlite-net8-004/` 等早期失败/测试 harness 调整日志均保留。映射 EF sync/async 扩展的初次测试数据声明因 xUnit1009 编译失败，原始日志保留于 `fix009-mapped-ef-sync-async-net6-004/`，修正后精准测试在 `...-005/` 与完整 EF `full-ef-net6-003/` 通过。只有独立反例与干净修复后绿测用于本轮结论；测试 harness 错误不计为生产缺陷或 PASS 证据。
- FIX-008 复用 `artifacts/review-round-2/probe-confirmation-001/` 的一次直接确认：16/16 cells，probe elapsed 1,200.7183 ms，60 秒 watchdog `TimedOut=false`，child/host direct exit 均为 0。E2E 三个场景分别 warmup 10、独立 3×100；计数来自实际 connection/transaction 调用观察；不重跑 probe、不重建 baseline。详细指标及历史 Round 1 的 superseded 结果见 `artifacts/performance-summary.md`。
- Review Round 2 的剩余状态：执行者修复范围 FIX-004/FIX-009 均已完成，未留执行侧 OPEN_ACTIONABLE；独立 `review.md` 仍保留 2 项 OPEN_ACTIONABLE，等待原 Sol/high 线程复验。外部数据库互操作仍 BLOCKED_EXTERNAL，性能阈值仍 BLOCKED_APPROVAL；两者均不触发下一 Fix。
- 相关直接追溯见 `traceability.md` 的 REVIEW_FIX Round 2 表；candidate 与新鲜测试索引见 `artifacts/candidate-manifest.md`、`artifacts/review-fix-round-2/index.md`。

## REVIEW_FIX 第 1 轮（历史实施记录）

本轮按 `.agents/skills/fix-review/SKILL.md` 的 `fixScope=recommended` 处理 review.md FIX-001–008。`review.md` 保持不变，Round 1 起始与结束 SHA-256 均为 `988AA7C860D80E4D603A369505689C1843940D2016557541B6E11173CF6A6D2C`。修改前 CIA 与冻结工作区分别见 `artifacts/review-fix-round-1/CIA.md`、`starting-state.md`；所有新结果均写入新目录，既有审查证据未覆盖。

| Finding | 生产修复与直接回归 | 本轮证据 |
| --- | --- | --- |
| FIX-001 | standalone 已执行的默认 Query/Executor 不能加入后续 Current；显式 Independent/External/Owned/显式事务仍沿原路径。更正 Query 反例为拒绝；验证先前独立写入仍保持已提交、后续 root 写入不能逃逸回滚。 | `SqliteUnitOfWorkContractTest.PreviouslyStandaloneQuery_ShouldRejectJoiningLaterRoot`、`PreviouslyStandaloneExecutor_ShouldRejectJoiningLaterRoot`，两参数值覆盖 sync/async；SQLite net6/net8，198 passed 各自 exit 0。 |
| FIX-002 | 执行入口遇到预取消 token 时，在不打开连接的情况下将参与 root 标记 rollback-only；纯参数验证错误不毒化。映射 EF 两个异步 SaveChanges overload 同样处理。 | SQLite `PreCancelledAsyncExecution_ShouldMarkRootRollbackOnlyWithoutCreatingConnection` 覆盖 scalar/list/executor/paging/stream 首次 MoveNext/Multiple，`InvalidMultipleCommand_ShouldNotMarkRootRollbackOnlyEvenWhenTokenIsCancelled`；EF `PreCancelledMappedSaveChangesAsync_ShouldMarkRootRollbackOnlyWithoutOpeningConnection` 两个 overload。SQLite net6/net8、EF net6 Release。 |
| FIX-003 | Current 已终态时，映射 EF 首次 SaveChanges/旧 Commit 拒绝且不走 legacy 自动提交；完成且无变更的 late Commit 仍返回 0。 | `FirstSaveAfterCurrentRootIsTerminal_ShouldRejectInsteadOfUsingLegacyCommit` 覆盖 Completed/RolledBack/Faulted × 四 SaveChanges overload/旧 Commit；`LateCommitAfterMappedRootCompletedWithoutChanges_ShouldReturnZero` 覆盖 sync/async。EF net6 Release。 |
| FIX-004 | Dapper 与 mapped EF / SaveChanges-first 统一验证冻结有效数据源：物理库、只读、事务支持、冻结 replica 主库重定向、非事务 Transaction 读策略；无效源在 Open/Begin 前拒绝且不偷偷换源。 | Dapper `EnsureReady_WhenFrozenSourceCannotJoinTransaction_ShouldFailBeforeOpen`、`EnsureReady_NonTransactionalScopeWithTransactionReadStrategy_ShouldFailBeforeOpen`；EF `SaveChangesFirst_WithMismatchedPhysicalDatabase_ShouldRejectBeforeOpeningEitherConnection`、`SaveChangesFirst_WithInvalidFrozenSource_ShouldRejectBeforeOpeningConnection`、`SaveChangesFirst_NonTransactionalRootWithTransactionReadStrategy_ShouldRejectBeforeOpeningConnection`、`QueryFirst_WithMismatchedPhysicalDatabase_ShouldRejectBeforeOpeningContextConnection`。SQLite真实双文件/EF net6 Release。 |
| FIX-005 | root 自建 Dapper/EF 连接使用可信内部借用路径，不要求 opaque custom resolver 的身份可比较；Query 专属资源仍分别由 root 提交、回滚、释放。真正外部连接继续严格校验。 | `OpaqueResolverQueries_ShouldUseSeparateRootResourcesAndBothRollback` 与 `OpaqueExternalConnection_ShouldStillFailClosed` 使用两个真实 SQLite 文件；SQLite net6/net8 Release。 |
| FIX-006 | `AddEfCoreSqlUnitOfWork<TContext>` 幂等调用 `AddUnitOfWorkScopes`，双向拒绝同 context 映射多个源，并保留预注册自定义 manager。 | `AddEfCoreSqlUnitOfWork_ShouldBeIdempotentAndRejectConflictingContext`、`AddEfCoreSqlUnitOfWork_ShouldPreserveCustomScopeManagerRegistration`；EF net6 Release。 |
| FIX-007 | 新增 Provider 职责矩阵和 EF 真 consumer 生命周期回归；覆盖官方 Provider、Doris/custom 分支的资源路由、native async 能力回退、冻结 replica、独立多资源清理；EF 覆盖部分提交回调、通知失败不重提、reader 与 SaveChanges/Complete 互斥、实际连接/事务生命周期计数。 | `SqlUnitOfWorkProviderContractTest` net6/net8 各 30/30；EF `MappedEfResource_ShouldObserveOnePhysicalTransactionAndConnectionLifecycle`、`EfResourcePartialCommit_ShouldRunOnlyCommittedResourceNotification`、`EfPostCommitNotificationFailure_ShouldRemainCommittedAndLateCommitMustNotRetryIt`、`EfReader_ShouldExcludeSaveChangesAndRootCompletionUntilReaderIsDisposed`，EF 完整 net6 159/159。远端 Provider 未运行，不声称服务器互操作。 |
| FIX-008 | 有界 probe 为每个 E2E 场景预热 10、独立测 3×100；受控 connection/transaction wrapper 观察实际物理调用；分配口径为进程级 GC；外层 60 秒 watchdog 可杀进程树并保存命令/退出结果。 | 唯一正式 Release net8 run：`artifacts/review-fix-round-1/probe-fix-008-report.md` 与 `probe-net8-release-001/`。16/16 cells，1,733.9612 ms；1,554 connections、314 physical transaction begin/commit/dispose、0 rollback、0 open at end。watchdog wrapper 原始 exit 1；应用 exit 0 只能按 stdout/JSON/源码恢复，未伪称直接捕获；smoke 单独证明子进程 exit 7 捕获与 timeout tree kill。阈值仍 `BLOCKED_APPROVAL`。 |

### 本轮 Release 验证

- Dapper Core：net6/net8 完整项目各 179/179；SQLite Integration：net6/net8 各 198 passed、1 个既有 Provider gate skip、0 failed；EF Core：net6 159/159。命令、TRX、combined stdout、退出码见 `artifacts/review-fix-round-1/release-{dapper-core,sqlite,ef}-*`。
- Provider 新职责矩阵：net6/net8 各 30/30，见 `release-provider-matrix-net6-001/`、`release-provider-matrix-net8-001/`。这是 controlled connection contract，不是异构数据库服务端验证。
- FreeSQL：net6/net8 各 78 passed、26 个 MySQL gate skips；AspectCore：net6/net8 各 104/104。Admin EFCore consumer net6 140 passed/36 个 MySQL-CAP 或 Redis gate skips；Admin FreeSQL consumer net6 150 passed/35 个 MySQL-CAP gate skips。各 consumer TRX/命令/退出码在对应 `release-*-001/`。
- 受影响生产与 consumer Release builds：Dapper Core、EF Core、FreeSQL、Hangfire 四项全部 exit 0；Hangfire 有既有 net6 EOL 警告。证据在 `release-affected-builds-001/`。
- 双 SQLite 文件和 EF bridge 使用本地真实文件。外部数据库、CAP、Redis 服务 gate 未启用，无生产服务访问。此处不重跑未受影响的全仓矩阵，也不重建性能 baseline。

## T01–T08

| 任务 | 状态 | 结果与证据 |
| --- | --- | --- |
| T01 冻结候选与回归入口 | COMPLETE | 冻结起点 HEAD/脏文件；保留 F008；新契约在实现前因缺少 companion API 类型编译失败，见 artifacts/T01-contract-before-implementation。 |
| T02 Bing.Uow companion API | COMPLETE | 新增 AsyncLocal root/nested/requiresNew、资源登记、非阻塞租约、提交/回滚/通知状态机、DI 注册及接口文档；net6/net8 直接与完整 Uow 测试通过。 |
| T03 资源键与 owner | COMPLETE | 冻结配置、Provider、tenant、resolver 与 route 身份键；未知身份 Query 专属隔离但仍由 root 管理；同步/异步准备、能力拒绝和所有权测试通过。 |
| T04 Query/Executor/执行路径 | COMPLETE | 首次执行绑定、同步/异步查询与写入、分页、reader、stream、Multiple、失败 rollback-only、显式单库事务排除及无 Uow 旧行为均有 SQLite contract。 |
| T05 双库与 Provider | COMPLETE | 两个真实 SQLite 文件 net6/net8 提交/回滚；Provider unit 项目 net6/net8 通过；受控 MySQL Provider key 到 SQLite resolver 的本地资源路由测试通过。它不是 MySQL 服务端互操作证据。 |
| T06 EF/FreeSQL/消费者 | COMPLETE | 显式 EF 映射支持 app-first/store-first；四个 SaveChanges 重载与旧 Commit flush、物理提交/回滚及通知时序通过；Round 2 补齐 mapped Query-first admission sync/async rollback-only 与 mapped batch sync/async 生命周期；FreeSQL SQL/native 边界和后台 scope consumer 通过。 |
| T07 性能与最终验证 | COMPLETE | Round 1 唯一正式 probe 曾记录 1.734 秒；Round 2 独立验收要求的直接 host/child exit 证据复用 `probe-confirmation-001`：16/16 cells、1.2007183 秒、每 E2E 场景 10 warmups + 3×100、60 秒 watchdog 下 child/host exit 0。没有重跑 probe 或重建 baseline；性能预算仍未批准。 |
| T08 文档与交付 | COMPLETE | 已写 migration.md、traceability.md、public-api-increments.md、candidate-manifest.md、最终矩阵及局部用法/集成文档。 |

## Review 0 实施轮历史验证

以下结果保留为起始实施轮证据。Round 1 修改了 Dapper/EF 执行与资源准入路径、增加了 Provider/EF consumer 职责测试，因此旧 27-cell matrix、13 个 Release builds、25-case EF bridge 和单轮性能 probe 均不再作为本轮生产路径的最终验收结果；本轮 fresh Release 证据见上表。

- 最终项目/TFM 矩阵：27/27 退出码 0，逐格证据及替代关系见 artifacts/final-candidate-matrix.md。原 final-matrix-001 保留了 FreeSQL 两个早期失败结果；后续完整 net6/net8 成功结果取代它们。增加后台消费者和 DI 生命周期测试后，Uow 与 Dapper Core 两个 TFM 的最终单元格使用后续完整成功 TRX。
- Dapper Core 生命周期补测：新增方法精准 net6/net8 各 1/1；完整项目 net6/net8 各 175/175。命令、TRX、日志和退出码见 artifacts/T08-di-lifecycle-001。
- SQLite Uow：SQLite Integration 完整项目 net6/net8 exit 0；实际用两个不同本地数据库文件验证同源共享、多源分别提交/回滚及所有权。EF bridge contract 在 net6 为 25/25，见 artifacts/T06-efcore-contracts-net6-002/trx/ef-uow-contracts-net6.trx。
- FreeSQL 完整 net6/net8 各 78 passed、26 个既有 MySQL gate skips、0 failed；最终证据见 artifacts/T06-freesql-net6-net8-007。native FreeSQL 与 Dapper SQL 用不同临时 SQLite 文件，因此不声称它们共享物理事务。
- Release builds：13/13 成功，见 artifacts/final-builds-001/index.md；Benchmark harness build 见 artifacts/T07-benchmark-build-003。
- diff/文档：git diff --check exit 0，见 artifacts/final-diff-check；8 份最终 UTF-8 文档无尾随空格；迁移文档相对链接检查 0 处失败。已批准 shipped/unshipped PublicAPI baseline 和版本属性文件无 diff；git diff --cached --quiet exit 0。
- 外部 Provider：只读检查确认对应 integration runsettings 会启用真实服务/重置 gate；环境变量未启用。没有连接生产或外部数据库。外部服务互操作仍需专用受控测试环境。
- 性能：probe 预热与测量工作量、同步并发 1、时间/分配/连接/事务计数见 artifacts/T07-uow-probe-001/probe.json 及 artifacts/performance-summary.md。探针无超时、无异常、无遗留打开连接。正式预算未批准，因此不把它判为阈值 PASS。

## CIA 与偏差

- 每一阶段与最终矩阵/构建都保存 CIA；失败重试原始日志继续保留，后续成功使用新目录，没有覆盖历史证据。
- T05 初次受控 Provider 测试先后遇到测试身份适配和 Provider native async capability mismatch；修复后 T05-mixed-provider-net8-003 通过。该测试使用真实本地 SQLite 文件执行，不代表远端 MySQL。
- FreeSQL consumer 迭代修复了测试依赖/命名空间、身份解析、资源清理、SQLite 文件锁、FreeSQL 自有 Commit 与事务动作注册，相关 CIA 和失败日志保留；最终两个 TFM 完整项目通过。
- 最终追溯发现默认 Query/Executor Transient 生命周期缺少直接断言，因此 T08 增加一个仅检查实际 DI ServiceDescriptor 的回归，并运行精准及完整 net6/net8 测试。
- 同一个 root 内的多数据源资源依登记顺序逐个做本地提交，不具备跨库原子性。未知配置保持 root 所有权但不与其他 Query 合并。EF requiresNew 不能借用外层 scoped context。FreeSQL native CRUD、CAP-owned actions、未映射 EF Shared 和 Independent 不自动桥接。这些均是计划确认的边界，不属于 OPEN_ACTIONABLE。
- 不启动 Hangfire server；后台 operation/worker iteration/job 使用 fresh DI scope 的隔离契约由本地测试验证。没有运行外部 Provider integration，因为门控会访问真实服务。
- 状态脚本结束使用 task-finish.mjs --no-notify。脚本支持该本地禁通知选项，执行结果写入 .agents/runtime/current-task.json；未发送飞书或其他外部通知。

## 第 1 轮历史 Findings 与循环状态

| ID | 分类 | Finding / 根因 | 处理 |
| --- | --- | --- | --- |
| F-EXT-01 | BLOCKED_EXTERNAL | 未连接外部 Provider 服务；本机对应 runsettings 会打开真实服务和数据库重置门控，任务明确禁止使用生产数据库。 | 外部互操作需专用受控测试服务和 gate；未伪装为本地 SQLite 路由结果。 |
| F-PERF-01 | BLOCKED_APPROVAL | 性能 probe 的正式预算/阈值尚无授权。 | 保存完整有界证据，不自动设阈值，不因该状态开始 Fix。 |
| F-LIMIT-01 | VERIFIED_BOUNDARY | 多数据源采用多个独立本地事务，后续资源失败不能撤销已提交资源。 | migration.md 明确顺序与部分提交语义。 |
| F-LIMIT-02 | VERIFIED_BOUNDARY | FreeSQL native CRUD 不被 Dapper business root enlist；两者 consumer 测试用不同 SQLite 文件。 | 保留 FreeSQL 自有 Uow 提交责任，不声称同连接原子性。 |
| F-LIMIT-03 | VERIFIED_BOUNDARY | 受控 Provider key/resolver 测试仅验证路由和资源隔离。 | 单独标记，不冒充真实异构数据库服务执行。 |

- Review round 0 是最初实施轮历史结论；父会话随后运行独立审查，原始审查结论记录在未修改的 review.md。
- Review/Fix round 1 implementation: FIX-001–008 changes and direct tests completed; implementation-side OPEN_ACTIONABLE=0. Independent reviewer has not yet reconfirmed finding states, so review.md remains NEEDS_FIX/8 until the parent re-review.
- BLOCKED_APPROVAL: F-PERF-01；BLOCKED_EXTERNAL: F-EXT-01；VERIFIED_BOUNDARY: F-LIMIT-01..03。以上状态不触发自动修复。
- No-Progress Check: 没有相同 Finding/根因/验收/候选范围的无进展自动循环；已确认的 approval/external/边界状态均不触发 Fix。
- Next Action: hand Round 1 candidate and evidence to the parent for the same Sol/high independent re-review; no review is authored in this execution.

## 第 1 轮历史 Goal 轮次摘要

- Task/Round: 20261021-sql-multidb-unit-of-work / REVIEW_FIX Round 1 implementation
- Implementation: 8/8
- Release Gates: Dapper Core net6/net8 179/179; SQLite Integration net6/net8 198 pass + 1 existing Provider gate skip each; EF net6 159/159; new Provider matrix net6/net8 30/30; FreeSQL net6/net8 78 pass + 26 MySQL skips each; AspectCore net6/net8 104/104; Admin EFCore net6 140 pass + 36 gated skips; Admin FreeSQL net6 150 pass + 35 gated skips; affected Release builds 4/4; external service gate BLOCKED_EXTERNAL; performance budget BLOCKED_APPROVAL
- Changed Scope: Dapper Query/Executor affinity and pre-cancel paths; Dapper/EF source admission; mapped EF lifecycle and registration; Provider and EF consumer regressions; bounded probe; task reports/evidence
- Impact: L0 companion Uow API; L1 Dapper resource lifecycle/provider; L2 EF/FreeSQL/AspectCore consumers; L3 受影响 package/sample builds; L4 tests/docs/evidence; L5 no schema migration and no production DB access
- Reused Evidence: frozen starting state and pre-fix repros; unaffected baseline evidence only for unchanged modules. All changed Dapper/EF/provider paths use fresh Round 1 Release evidence.
- Completed: T01–T08
- Implementation Open Actionable: 0; independent review.md remains NEEDS_FIX until parent re-review.
- Blocked Approval: F-PERF-01
- Blocked External: F-EXT-01
- Not Applicable: 历史等价共享 Uow API before baseline、生产数据库 migration
- Accepted Limitations: 无跨库原子性；FreeSQL native/CAP/未映射 Shared/Independent 所有权不自动转移
- Verified Boundaries: 首次执行绑定、借用连接所有权、旧无 Uow/显式单库路径、EF 双顺序 flush/rollback、后台 scoped roots
- Deferred: 外部 Provider 服务互操作与任何需单独批准的正式性能预算
- No-Progress Check: 满足；没有 Open Actionable
- Next Action: parent Sol/high independent re-review of the Round 1 candidate.
- Goal Status: REVIEW_PENDING (implementation complete; independent confirmation pending)
