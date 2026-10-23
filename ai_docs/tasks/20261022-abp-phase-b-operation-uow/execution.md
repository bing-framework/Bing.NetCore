<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: 20261022-abp-phase-b-operation-uow
AI_EXECUTION_FINISHED_AT: 2026-10-23T12:00:13+08:00
AI_EXECUTION_STARTED_AT: 2026-10-22T19:54:29.207Z

AI_EXECUTION_STARTED_AT: 2026-10-22T17:03:54.974Z

AI_EXECUTION_STARTED_AT: 2026-10-22T14:01:12.513Z
AI_EXECUTION_UPDATED_AT: 2026-10-23T12:00:13+08:00

# 实施执行报告

## 当前候选

- 当前候选：`phase-b-review-fix-round-3-fix005-2026-10-23-v1`，只包含 Round 3 FIX-005 限定修复；关联清单 `artifacts/review-fix-round-3/candidate-fix005-v1.json`，前序为 `phase-b-review-fix-round-2-2026-10-23-v1`（262 项清单 `artifacts/review-fix-round-2/candidate-review-fix-round-2-v1.json`）。更早候选仅用于历史结果。B00 起点见 `artifacts/b00-worktree-startpoint/snapshot-manifest.json`，起点 HEAD 为 `5065c38ced05a95088e17d89d96c9a1610e37159`，SDK `10.0.400`。
- 实施范围：B00、B01.1、B01.2、B02.1、B02.2、B02.3、B02.4、B03、B05、B06；B04 FreeSQL 留给独立计划，C/D 未进入本次实施。
- 首轮自评曾将 10 个计划编号标作“完成”；该完成断言被 Review 判定不成立，不作当前状态统计。当前按 finding 和门禁证据报告。
- `review.md` 由独立 Reviewer 维护，本执行器只修复并记录证据，不改 review 状态或父路线图。Round 1 的 L4/Release 证据不属于当前候选；Round 2 时默认 L4/Release 为 `NOT_REFRESHED_AFTER_FIX`，当前候选已在 B06 执行新一轮 Release build/L4，结果见下。
- Round 1 的 finding 结果、L2 表及门禁表均为历史候选快照；本报告后续的 Round 2 结果覆盖当前候选。Round 2 修复 FIX-003/005/007/008，EXT-001 保持 CLOSED；FIX-001/002/004/006 没有新证据，不重开。

## Review Fix Round 3 进展（限定 FIX-005，当前候选）

- Review 状态：Review 4 `PASS`（本次实现/受影响测试）；Fix Scope：仅 Round 3 `FIX-005`（HIGH / MUST_FIX）。原 8 项现为 8 CLOSED / 0 OPEN_ACTIONABLE，无新增 finding；未修改 `review.md`、`plan.md`、父路线图或旧 artifacts。
- 本轮变更 5 个文件：`framework/src/Bing.EntityFrameworkCore/Core/UnitOfWorkBase.cs`、`framework/src/Bing.EntityFrameworkCore/Core/ResolvedEfCoreUnitOfWorkRegistry.cs`、`framework/src/Bing.Uow/Bing/Uow/UnitOfWorkScopeManager.cs`、`framework/tests/Bing.EntityFrameworkCore.Tests/Integration/EfCoreOperationSqliteTest.cs`、`framework/tests/Bing.EntityFrameworkCore.Tests/Core/EfCoreSqliteUnitOfWorkTestSupport.cs`。真实起点与 SHA-256 在 `artifacts/review-fix-round-3/startpoint/`、`startpoint-sha256.txt`；Review 3 证据副本在 `startpoint/review-round-3/`，旧 `artifacts/review-round-3/` 未覆盖。
- 根因：Context 只保存一个 `_protectedOperationRootId`。A 有 pending/claim 时，B 启动记录 B，C 启动覆盖为 C；退出 C 只恢复 frame，不恢复仍活动的 B，因此 B 的 `Complete` 或显式 `SaveChanges`/`Commit` 可提交 A 的状态。
- 修复机制：`UnitOfWorkBase` 按 root id 保存 `OperationRootProtection` 字典，每次更深 requiresNew 进入都保留祖先约束；`EnsureOperationRootMayUseContext` 按当前 root id 查找。`ResolvedEfCoreUnitOfWorkRegistry` 在 root 正常退出和启动失败时释放该 root 的记录；`UnitOfWorkScopeManager` 在发布 frame 前调用 observer，失败时先清理再抛出，`Current` 保持原 root。
- 直接回归（`RequiresNewNested_*`，共 16 项真实 SQLite 场景）：A pending→B→C→退出 C 后，B 的 `Complete`、显式 `SaveChanges`、显式 `Commit` 同步/异步各拒绝；entity、transaction action、domain event、query-first claim 四类祖先状态均拒绝，action/event 调用 0；`Begin` 观察器/DetectChanges 异常后 `Current` 仍为 B，B 后续写入仍在 I/O 前拒绝；B 自有 inner-only 状态经 C 退出后仍合法提交；单层矩阵、独立 executor 新 scope 子提交由既有测试保留。
- 红绿证据：`artifacts/review-fix-round-3/l1-red/` 首次 15 Failed / 1 Passed（14 项目标三层反例失败，inner-only 成功对照通过，observer 用例因测试实体仍走 backing field 未触发 DetectChanges 而失败）；修正测试实体的 `PropertyAccessMode.Property` 后，`l1-red-observer4/` 在未改生产代码时 1/0/0 通过；生产修复后最终精准集 `l1-green/` 16/0/0、退出 0。旧红证据保留，不改写。
- 当前 L2：EF Core net6 235/0/0；Uow net6/net8 各 70/0/0；SQLite integration net6/net8 各 233/0/1（唯一 NotExecuted 为既有 provider contract 环境门控，不计通过）。命令、stdout/stderr、exit 和 TRX 在 `artifacts/review-fix-round-3/l2/`，逐 `UnitTestResult` 汇总在 `artifacts/review-fix-round-3/trx-summary.json` 与 `unit-test-results.json`，命令/退出码/结果分栏在 `validation-summary.md`；TRX Summary 的 `notExecuted=0` 不用于替代逐结果。
- Round 3 精准/受影响 L2 之后，B06 已执行当前候选的最终 Release build 与默认完整 L4（见下节）；本轮未运行外部 MySQL/CAP、probe、性能/GC。历史 Round 1 Release FAIL / GC `NOT_DETERMINED`、EXT-002、APR-001、LIM-001、DEF-001/002 分类不变。无公共 API、API baseline、SDK、TFM、包版本或性能预算变化。
- Round 3 结束时下一步为主线程转原 Codex GPT-6.1-Sol/high；Review 4 已完成 PASS（8/8 CLOSED），当前后续为 B06 最终检查点及主线程收口（见下节），本执行器不修改 `review.md`。

## B06 最终检查点（Release build + 默认完整 L4，已执行）

- 前置：Sol/high Review 4 为 `PASS`（本次实现及受影响测试），FIX-005 CLOSED，8/8 CLOSED，`OPEN_ACTIONABLE=0`。本检查点不启动新的 Fix/Review，不修改生产/测试或 `review.md`。
- 候选冻结：`phase-b-review-fix-round-3-fix005-2026-10-23-v1`；Round 2 的 262 路径加 Round 3 的 5 文件增量。开始前与结束后均核对 262/262 SHA-256/length 匹配，mismatches=0；证据 `artifacts/final-candidate-after-fix005/candidate-freeze.json`、`post-checkpoint-source-check.json`。
- 构建（独立子进程，外部/probe gate 关闭，无 runsettings）：`dotnet build Bing.All.slnx -c Release --no-restore --nologo -m:1`；exit 0，199 warnings，0 errors，57.6 秒。
- 默认完整 L4（同一候选，`--no-build`，无 filter/全局-f/隐式 runsettings）：exit 1；103 TRX、6 个零结果 TRX；逐 `UnitTestResult` 为 13,996 total / 12,881 Passed / 1 Failed / 1,114 NotExecuted。net6：53 TRX（3 空）、6,716/0/593；net8：50 TRX（3 空）、6,165/1/521。
- 唯一失败：`Bing.Tests.Modularity.ModuleLifecycleTest.RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive`（net8，`Bing.Core.Tests`；Shouldly `references.All(r => !r.IsAlive)` False）。与历史同方法 GC 弱引用失败同类（Round 1 为 net6、Review 前 L4-v2 为 net8；本轮 net8），因果仍 `NOT_DETERMINED`；不隔离追绿、不扩修、不自动生成新 finding。
- 文档：产品文档未变；Review 4 已允许复用 13 项 checker 与编译例，本轮不再重跑。仅更新任务报告并检查 UTF-8/当前 diff。
- 结论分栏：Implementation PASS（8/8 CLOSED）；受影响 Test PASS（Review 4）；Release build PASS；默认完整 L4 FAIL（已知 GC）；Release 总体不宣称 PASS；EXT-002 仍 `BLOCKED_EXTERNAL`，APR-001 仍 `BLOCKED_APPROVAL`，历史 Round 1 FAIL/GC `NOT_DETERMINED` 保留。
- 证据：`artifacts/final-candidate-after-fix005/`（`candidate-freeze.json`、`change-impact.md`、`release-build/`、`L4/`、`checkpoint-summary.json`、`report-encoding-check.json`、`docs-reuse.md`）。未 git add/commit/push，未发送外部通知。

## Review Fix Round 2 进展（历史候选）

- Fix Scope：`recommended`。Round 2 的直接红证据保留于 `artifacts/review-round-2/ef-boundaries/Program.cs`、`stdout.txt`、`command.json` 与 `artifacts/review-fix-round-2/l1/ef-boundaries-red-v2/`；自动代理组合链各次 L1 的失败/诊断输出也保留于同目录 `l1/`。
- Change Impact Analysis：`StoreBase` 同步 Store 查询、`UnitOfWorkBase`/`ResolvedEfCoreUnitOfWorkRegistry` requiresNew Context 归属、`UnitOfWorkScopeManager` root-start admission 观察器、EF SQLite 直接测试、automatic AspectCore 组合测试。受影响项目为 Bing.EntityFrameworkCore、Bing.Uow、Bing.Ddd.Application.AspectCore、Bing.Events 及 EF/AOP/EventBus/Uow/SQLite tests；TFM 沿用 EF net6 与其他项目原 net6/net8。无公共签名、依赖、SDK、baseline、Provider 策略或 probe 预算变化；RiskLevel=HIGH。
- FIX-003（MUST_FIX）：新增 `SynchronousStoreCount_WhenOperationTokenIsCancelled_ShouldRejectBeforeSqliteIo`，对 owner caller、deadline、participant caller 各验证真实 `Count`、`FindAll`、`FindById` 在 SQLite I/O 前以有效 OCE 拒绝；root rollback-only、命令数增量0、提交0、最终行集空。`SynchronousStoreReads_WhenOperationIsActiveOrAbsent_ShouldRunNormally` 保留成功/无操作对照。真实 `Bing.EntityFrameworkCore.Tests` net6 L1 为 27/0/0、L2 为 219/0/0；最终共享 DLL 下的 SQLite integration net6/net8 各 233/0/1。原红现象为 `cancelled-sync-store`: SQL1、无异常、rollback-only=false。
- FIX-005（MUST_FIX）：保存前由 root-start observer 记录外层已占用 Context 的 pending/claim 归属，显式 sync/async `SaveChanges` 与 `Commit`、自动 handler 和 query-first claim 均在首次迁移/写入前拒绝 entity/action/event/claimed 外层状态；只有 inner 创建的新 Context 状态仍可提交。正式回归为 `RequiresNewSameScope_ExplicitSaveBeforeHandler_ShouldRejectOuterPendingEntity`（4 种 sync/async 与 Save/Commit 组合）、`RequiresNewSameScope_ExplicitSaveBeforeHandler_ShouldRejectOuterAuxiliaryState`（action/event/claimed × 4 组合）、`RequiresNewSameScope_QueryFirstOuterClaim_ShouldRejectInnerCommitBeforeSqliteIo`、`RequiresNewSameScope_InnerOnlyContextChanges_ShouldCommitAfterOuterRollback` 与 `ExecutorNewScope_ShouldCommitItsContextWhenOuterRootRollsBack`。EF net6 L1 27/0/0、完整 L2 219/0/0；SQLite net6/net8 各 233/0/1。红证据 v2 显示 10 项失败；显式保存外层行在 outer rollback 后仍存在、query-first claim 漏判、inner-only 状态被误拒。
- FIX-007（SHOULD_FIX）：真实 `EnableBingAutomaticUnitOfWork` proxy host 加外层成功观察器。`CapTraceAndAutomaticBoundary_ShouldPreserveTargetFaultedCancellationAndOriginalException` 检查四种目标 Faulted OCE 与代理 Task/ValueTask 直接终态、原异常/令牌、target1、取消 commit/回调0；Task/Task<T> 预取消与 TargetEntered 后取消、同步原 OCE、source-backed VT/VT<T> 预取消/信号取消和成功对照覆盖相同组合链，适用时 `GetResultCount=1`。L1 net6 18/0/0；当前 AOP 完整 L2 net6/net8 各 178/0/0，EventBus L2 各 409/0/1（唯一 NotExecuted 是有界 probe 门控）。曾尝试的 CapTrace 直通返回导致 EventBus 取消回归，失败证据保留在 `eventbus-net6/`、`eventbus-net8/`；最终恢复 Round 1 的 TCS `TrySetException` 实现后，AOP/EventBus 两 TFM 重跑通过。
- FIX-008（SHOULD_FIX）：14 份原始 L2 command/definition/TRX 已逐项核对，Round 1 Sample v2 每 TFM 为 26/0/18、SQLite 每 TFM 为 233/0/1；EF v1 193/3/0 与 v2 196/0/0 分开记录。TRX counters 的 `notExecuted=0` 不符合这些样例的逐条 `UnitTestResult`，并由 stdout `[SKIP]` 与测试 gate 确认：Sample 的 `RUN_CAP_MYSQL_TESTS`、SQLite 的受控 provider contract 环境变量未配置。审计摘要 `artifacts/review-fix-round-2/l2-raw-reconciliation.md`，Reviewer 原始机械证据保持不变。
- Round 2 当前完整 L2：Uow net6/net8 各 70/0/0；AOP 各 178/0/0；EF net6 219/0/0；SQLite integration net6/net8 各 233/0/1；EventBus net6/net8 各 409/0/1。命令、stdout/stderr、exit 与 TRX 均在 `artifacts/review-fix-round-2/l2/`。所有当前 L2 均以完整无 filter 命令从共享源码重建；跳过结果不计通过。
- EXT-001 保持 CLOSED 并复用未变化范围的独立 consumer 证据。正确归因是 7 个新 executor operation，加第 8 个启用 operations 的旧手工 partial-commit root；该 root 进入新 saving/completion，但不是新 executor 路由。
- 当前候选 Release solution build、完整 L4：`NOT_REFRESHED_AFTER_FIX`；Round 1 的 103 TRX / 12,828 Passed / 1 Failed / 1,114 NotExecuted 与 Core GC `NOT_DETERMINED` 失败仅作为历史候选结果保留。未因报告计数修正运行 docs checker；Round 2 未更改产品文档。MySQL/Rabbit、probe、GC、B04 FreeSQL、C/D 按用户范围未重跑。
- Round 2 的下一步曾是交原 Sol/high 独立 Reviewer 执行 Round 3；该轮已完成并留下 FIX-005 反例，当前状态以上方 Round 3 限定修复为准。

## Review Fix Round 1 进展（历史候选）

- 进度更新时间：2026-10-23；候选 manifest 已生成。未覆盖 B00 起点、未修改独立 Reviewer 的 `review.md` 或父路线图状态。
- FIX-001–006：实现及精准红绿回归已完成；FIX-007 新模式代理、EF、异常清理、executor、双 SQLite 存储和 Sample 实际宿主直接矩阵已补齐；FIX-008 文档/可编译示例及 checker 结果已记录。
- Round 1 原始完整 L2：Uow 70/0/0、AOP 171/0/0、Dapper Core 181/0/0；EF v1 为 193/3/0、v2 为 196/0/0；Sample v1 每 TFM 26/0/17、v2 每 TFM 26/0/18；SQLite 每 TFM 233/0/1。Sample/SQLite 的 NotExecuted 依据逐条结果和 stdout skip 记录，不按 TRX counters 的 0 计。逐项命令及原始结果见 `artifacts/review-round-1/l2/` 和 `artifacts/review-round-2/evidence-mechanical.json`。
- EF 完整 L2 首次结果为 193 Passed / 3 Failed / 0 NotExecuted，三个 `MarkRollbackOnly(null)` 旧兼容回归因 null reason 触发 NRE；该结果保留在 `ef-net6-current-v1`。修正空原因处理后，同一完整集重跑 `ef-net6-current-v2` 为 196/0/0，退出码 0；不是忽略失败或仅重跑失败方法。
- SQLite L3 runner 首次因 ResultsDirectory 必须位于根 `artifacts/` 的工具约束在测试启动前退出 1；按 runner 路径限制调整输出目录后 `sqlite-contract-runner-net6-current-v2` 为 1 Passed、0 Failed、0 NotExecuted，退出码 0。两个 command/result 均留档；实测结果写在根 `artifacts/test-results/phase-b-review-fix-round1-sqlite-net6/`，task wrapper 证据在本 task `artifacts/review-fix-round-1/l3/`。
- Round 1 记录当时等待 Sol/high 独立复审；Round 2 Reviewer 随后关闭 EXT-001。新 operation 分进程 producer/consumer 证据仍见 v6 TRX/journal；EXT-002、性能审批、历史 GC 因果、FreeSQL/B04 与 C/D 仍按原边界保留。

## 任务状态

| Task | 状态 | 交付与边界 |
| --- | --- | --- |
| B00 | Completed | 保留原 dirty working tree 起点、失败回归与清单；没有以 HEAD 覆盖源码。 |
| B01.1 | Completed | 新操作/保存契约、owner/participant 生命周期、注册别名及超时选项；旧 scope 模式保持兼容。 |
| B01.2 | Completed | 仅 opt-in 的自动边界，真实代理保留终端分派、ValueTask 单次消费和原始取消异常。 |
| B02.1 | Completed | EF registry 只跟踪已解析实例；mapped operation 使用纯保存入口，拒绝漏映射 dirty Context。 |
| B02.2 | Completed | 保存完成后快照资源；无脏状态不额外 flush/通知，支持非事务保存并保持部分提交边界。 |
| B02.3 | Completed | timeout/caller token 贯穿真实异步读取，reader 释放前保留 linked CTS；完整消费后的迟到取消仍允许完成提交。 |
| B02.4 | Completed | executor 每次新建 DI scope，先 Begin 后解析 scoped 业务服务；构造失败也按 root 再 scope 的顺序清理。 |
| B03 | Completed | Sample 加显式关闭的 operation 路由、真实 BackgroundService 与 Hangfire operation 入口，旧 HTTP 默认行为保留。 |
| B05 | Completed | Uow/支持矩阵/Sample/迁移文档说明 opt-in、所有权与 EF SQLite 真实 API；13 项 docs checker 通过。 |
| B06 | PARTIAL | 最终检查点已执行：Release build exit 0（199 warnings/0 errors）；默认完整 L4 exit 1、12,881/1/1,114，唯一失败为既有 Core GC 弱引用（因果 NOT_DETERMINED）；产品文档未变、复用已接受 checker；probe 阈值未获批。 |

## Review 前候选 Change Impact Analysis（历史）

ChangedFiles：`framework/src/Bing.Uow`、`Bing.Ddd.Application.AspectCore`、`Bing.EntityFrameworkCore`、`Bing.Dapper.Core`、CAP 相关 `Bing.Events`、对应测试项目、`samples/Bing.Samples.SqlUnitOfWork*`、Uow/支持矩阵/迁移文档及本 task artifacts。B00 快照保留完整相关起点。

ChangedProjects：Uow、AspectCore、EF Core、Dapper Core、Events、AOP/Uow/EF/Dapper/SQLite/SQL Server tests、Sample 与 Sample.Tests。Runtime/TFM：保留已有 netstandard2.0、net6.0、net8.0 目标；没有新增依赖、SDK、TFM 或预算。Providers：SQLite 必跑并完成；MySQL + RabbitCAP 真实场景完成；其他专属 Provider 路径未改变。PublicContracts：只增加计划冻结的 companion operation API，旧接口不增加必实现成员；详情见 `api-delta.md`。BuildPackaging：最终 Release solution build 成功；API baseline 维持 B00 原值。BenchmarkHarness：只运行计划内两项 probe；未改预算。

## Review 前候选验证结果（历史，candidate-v2）

| 层级 | 命令/项目和 TFM | 结果 | 原始证据 |
| --- | --- | --- | --- |
| B00 | 起点捕获与 net6 失败回归 | 保存 dirty 状态、相关源码快照和失败 TRX | `artifacts/b00-worktree-startpoint/` |
| L1 | 真实代理取消组合、SQL Server 多结果迟到取消，net6/net8 | 迟到取消修复后两 TFM 均通过；旧 `ExecuteMultipleAsync_WhenCancelledAfterAllResultsConsumed_ShouldCommitAndReleaseResources` 首次最终候选曾各失败一次 | `artifacts/l1/sqlserver-late-cancel-net60-v1/`、`net80-v1/`；初次完整结果见 `artifacts/final-candidate/L4/` |
| L2 | Uow net6/net8 | 各 47/47 passed | `artifacts/l2/uow-net60-v1/`、`uow-net80-v1/` |
| L2 | AspectCore net6/net8 | 各 155/155 passed；包含真实代理 Task/ValueTask/终端与取消组合 | `artifacts/l2/aop-net60-v1/`、`aop-net80-v1/` |
| L2 | Dapper Core net6/net8，修复后 | 各 180/180 passed | `artifacts/l2/dapper-net60-v3/`、`dapper-net80-v3/` |
| L2 | EF Core net6 | 171/171 passed | `artifacts/l2/ef-net60-v2/` |
| SQLite integration | net6/net8 | 各 232 passed、1 NotExecuted、0 failed；另有独立受控 SQLite contract probe 1/1 passed | `artifacts/l2/sqlite-net60-v3/`、`sqlite-net80-v3/`、`artifacts/l3/sqlite-contract-probe-v1/` |
| L2 | SQL Server routing/execution contracts net6/net8 | 各 327/327 passed | `artifacts/l2/sqlserver-net60-v3/`、`sqlserver-net80-v3/` |
| L2 | Sample.Tests net6/net8 | 各 23 passed、17 NotExecuted、0 failed；HTTP、BackgroundService、Hangfire 的两种存储模式和精确 SQLite 行集断言已执行 | `artifacts/sample-l2-net60-v3/`、`sample-l2-net80-v3/` |
| L3 | CAP/MySQL/Rabbit真实 integration class，net6 | 22/22 passed，覆盖 EF/Dapper 访问顺序、业务/Outbox 行及发布后失败回滚 | `artifacts/l3/sample-cap-mysql-net6-v1/` |
| L3 | MySQL Provider runner net6/net8 | 每 TFM TRX 为 53 passed、6 optional NotExecuted、0 failed；runner 最终退出码 1，因 dirty source 的受保护 release-evidence validator 拒绝证据，不归类为 runner PASS | `artifacts/provider-test-results/phase-b-operation-uow/mysql-net6.0-*`、`mysql-net8.0-*`；wrapper 记录见 `artifacts/l3/mysql-provider-net60-v1/`、`mysql-provider-net80-v1/` |
| Build | `dotnet build H:/Bing_Framework/Bing.NetCore/Bing.All.slnx -c Release --no-restore --nologo -m:1` | 退出码 0，39.81 秒；159 warnings、0 errors | `artifacts/final-candidate/Release-build-v2/` |
| L4 | `dotnet test H:/Bing_Framework/Bing.NetCore/Bing.All.slnx -c Release --no-build --no-restore --logger trx --results-directory .../L4-v2/trx`，外部 gate/probe 关闭的独立子进程 | 103 TRX；12,715 passed、1 failed、1,112 NotExecuted。net6: 6,601/0/592；net8: 6,114/1/520。6 个空 TRX 不计入方法数。唯一失败是历史 `Bing.Tests.Modularity.ModuleLifecycleTest.RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive` 弱引用 GC 断言；阶段 A 已记因果 `NOT_DETERMINED`，本次不扩大修复、不重跑冲销。 | `artifacts/final-candidate/L4-v2/` |
| Docs | `python -X utf8 eng/ci/check-docs.py --quiet` | 退出码 0；13 项检查全部通过 | `artifacts/final-candidate/docs-check-final/` |

修复链：初次完整 L4 的 net6/net8 各由 `SqlServerRoutingAndExecutionTest.ExecuteMultipleAsync_WhenCancelledAfterAllResultsConsumed_ShouldCommitAndReleaseResources` 触发 `CommitCount=0`；问题是完全消费多结果后、DisposeAsync 前的迟到取消被完成回调当作执行取消。修复后完成回调不再重新检查操作 token，实际 reader I/O 仍接收 linked token，reader 释放后才清理 token source。两个单项 L1、完整 Dapper Core、SQL Server、SQLite L2 和最终候选都覆盖修正；最终 L4 中该两项通过。

## 外部环境和 probe

- MySQL 身份已核实为本任务隔离测试库 `bing_phase_b_test`；CAP 使用已有隔离 MySQL/Rabbit 容器，未启动/停止/重置他人容器，凭据未写入 evidence。身份核验见 `artifacts/l3/mysql-identity-v2/identity.json`。
- Provider runner 没有传 `-RunSettingsPath`，也没有把普通 `integration.local.runsettings` 当成受保护 Provider settings。L4 子进程不传 runsettings；`RUN_*` 与 probe gate 显式关闭，连接/broker 名称及 `RUN_METADATA_PATH` 清除，父进程和本地凭据文件未改。
- Automatic operation probe：只运行一次 net8。diagnostic 3,000 次、21.3454 ms、7,543,928 bytes、4,000 scopes opened/disposed/root commits；formal 15,000 次、45.3946 ms、37,641,608 bytes、20,000 scopes opened/disposed/root commits；最大并发 1，连接 0。结果是 current-only observation，无 before，`THRESHOLD_STATUS=BLOCKED_APPROVAL`，不作提速或性能门禁结论。证据 `artifacts/probe/automatic-operation-probe-net8.json` 与同目录 TRX/stdout/stderr/exit。
- SQL Uow probe JSON 为 `EXPECTED_SUCCESS`，60 秒限额内 1,893.682 ms，16 项测量、1,554 connections created/opened/closed/disposed，314 transactions begun/committed、0 rollback，峰值打开连接 2；`THRESHOLD_STATUS=BLOCKED_APPROVAL`。包装 PowerShell 用 `$pid` 命名局部变量触发自动变量冲突，外层返回 1 且未采集 child exit/stdout/stderr。原 JSON 保留未覆盖，捕获限制记录于 `artifacts/probe/uow-probe-capture-note.md`；按有界 probe 规则不补跑。

## Review 前候选状态分组（历史，candidate-v2）

- 历史自评：曾错误标称 B 主线 10 个计划项全部完成；Review 确认该自评不成立，当前不沿用。
- Open Actionable：0。迟到取消回归已由明确根因、局部修正和双 TFM 验收关闭。
- Blocked Approval：性能阈值未获批准，两个 probe 只报告观测数据。
- Blocked External：无；已执行计划要求的 SQLite、MySQL 和 RabbitCAP 场景。MySQL runner 的 release-evidence wrapper 退出 1 单独保留。
- Not Applicable：本主线不适配 FreeSQL；HTTP 方法/名称推断事务、跨库分布式事务、C/D 能力未引入。
- Accepted Limitations：多资源为有序本地事务；后续资源失败不能撤销已提交资源；新自动保存只在 opt-in；已捕获 participant 失败仍将 root 标为 rollback-only。
- Verified Boundaries：opt-in false 保持旧 Sample HTTP；旧 `AddUnitOfWorkScopes` 不自动保存 tracked-only；无脏数据不增加保存通知；SaveChanges(false)、迟到跨 Context pending、非事务、reader 生命周期、真实取消及部分提交均有对应直接验证。
- Deferred：最终 L4 唯一历史 Core GC 弱引用失败因果仍 `NOT_DETERMINED`；B04 FreeSQL 独立计划；最终 reviewer 未运行。

## Review 前候选收口（历史）

Review 前候选曾执行 task-finish 并交由独立 Reviewer；该记录属于 candidate-v2，不代表当前 Review Fix Round 1 状态。

## Review 修复记录

### Round 3（限定 FIX-005）

- Review 状态：NEEDS_FIX（只读 `review.md`）
- Fix Scope：仅 FIX-005（HIGH / MUST_FIX）
- 执行状态：实现完成，精准 L1 与受影响 Uow/EF/SQLite L2 PASS；等待独立 Reviewer
- 修改文件：
  - `framework/src/Bing.EntityFrameworkCore/Core/UnitOfWorkBase.cs`
  - `framework/src/Bing.EntityFrameworkCore/Core/ResolvedEfCoreUnitOfWorkRegistry.cs`
  - `framework/src/Bing.Uow/Bing/Uow/UnitOfWorkScopeManager.cs`
  - `framework/tests/Bing.EntityFrameworkCore.Tests/Integration/EfCoreOperationSqliteTest.cs`
  - `framework/tests/Bing.EntityFrameworkCore.Tests/Core/EfCoreSqliteUnitOfWorkTestSupport.cs`
- 根因：单槽 `_protectedOperationRootId` 被更深 requiresNew root 覆盖；退出最内层仅恢复 frame，仍活动中间 root 失去祖先 pending/claim 保护。
- 修复：每个 root 独立保护记录；进入更深 root 时追加而非覆盖；root 正常退出/启动失败时释放；observer 在 frame 发布前失败时保持 `Current` 和既有保护并清理失败 root 的残留记录。
- 直接回归：`RequiresNewNested_WhenDeepestRootExits_ShouldKeepMiddleRootProtected`（Complete/SaveChanges/Commit × sync/async）、`RequiresNewNested_WhenDeepestRootExits_ShouldRejectOuterAuxiliaryState`（Entity/TransactionAction/DomainEvent/MappedResourceClaim × sync/async）、`RequiresNewNested_InnerOnlyMiddleRoot_ShouldCommitAfterDeepestExitAndOuterRollback`、`RequiresNewNested_WhenBeginObserverThrows_ShouldKeepCurrentRootProtection`。
- 验证：精准 `artifacts/review-fix-round-3/l1-green/` 16/0/0，exit 0；EF L2 net6 235/0/0；Uow L2 net6/net8 各 70/0/0；SQLite L2 net6/net8 各 233/0/1，exit 0；详见 `artifacts/review-fix-round-3/trx-summary.json`。
- 下一步：交主线程转 Sol/high 独立 Review；未 commit/push，不修改 Review 结论。

### B06 最终检查点

- Review 4：PASS（实现/受影响测试），8/8 CLOSED，OPEN_ACTIONABLE=0；`review.md` 只读。
- 冻结候选：262 路径 + 本轮 5 文件；冻结前后哈希均 262/262 匹配。
- Release build：exit 0，199 warnings，0 errors。
- 默认完整 L4：exit 1；103 TRX（6 空），12,881/1/1,114；唯一失败为既有 `ModuleLifecycleTest.RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive`（net8），GC 因果 `NOT_DETERMINED`。
- 文档：产品文档未变，复用 Review 4 已接受的 checker/编译例。
- 状态：Implementation/Test PASS（Review 4）；Release local gate FAIL（已知 GC）；EXT-002/BLOCKED_EXTERNAL、APR-001/BLOCKED_APPROVAL 独立保留；B06 仍为 PARTIAL，不写 10/10 全 PASS。
- 证据：`artifacts/final-candidate-after-fix005/`。未修改生产/测试/`review.md`，未 commit/push/通知。

### Round 1

- Review 状态：NEEDS_FIX
- Fix Scope：recommended
- Review 文件：`ai_docs/tasks/20261022-abp-phase-b-operation-uow/review.md`（只读）
- 当前候选起点：`phase-b-working-tree-2026-10-23-v2`；259/259 文件 SHA-256/长度与 `artifacts/final-candidate/candidate-v2.json` 一致。
- 起点副本：`artifacts/review-fix-round-1/startpoint/`，含 259 个候选文件及本 task 的 execution/traceability/api-delta/final-report 原件；历史红证据保留在 `artifacts/review-round-1/`。
- Round 1 初始进度：FIX-001–008 均 OPEN；实现尚未开始。原始状态机、AOP 共存、participant token、EF 显式 flush 与 requiresNew 反例见 `artifacts/review-round-1/stdout-valid.txt`、`artifacts/review-round-1/ef-repro/stdout.txt` 及同目录源文件/命令/退出码。
- 原始证据口径：夹具命令退出 0 表示观察完成；其中的不变量违反是本轮修复红证据，不计作产品通过。初始 EF 观察为 owner 已取消后真实 SQL 仍执行且 command token 不可取消；同 scope requiresNew 保存后 outer rollback 外仍有 1 行。
- 实施顺序：先 FIX-002 状态机，再 FIX-001 旧标记 participant、FIX-003/004 取消贯穿、FIX-005/006 执行器隔离与 Trace、FIX-007 新模式直接验收，最后 FIX-008 文档和报告修正。共享根因合并实施时仍逐项记录结果。
- 外部/递延边界：EXT-001 仅在复查既有隔离 MySQL/Rabbit 环境所有权与健康后尝试新独立 consumer 证据；EXT-002、APR-001、LIM-001、DEF-001、DEF-002 维持 Review 1 分类，不触发清理、GC 修复或 probe 重跑。
- 本轮 Change Impact Analysis：修改 `framework/src/Bing.Uow/Bing/Uow/UnitOfWorkScope.cs` 与 `framework/tests/Bing.Uow.Tests/OperationScopeLifecycleTest.cs`；影响 Bing.Uow / Bing.Uow.Tests 的保存状态与生命周期测试；公共接口、Provider、TFM、打包图、依赖和 probe 均未改变；RiskLevel=HIGH（共享提交状态机）。本步先运行生命周期 L1，完整 Uow L2 留到同一共享生产范围修改结束后。

#### FIX-002

- 严重程度：HIGH；处理要求：MUST_FIX；执行状态：实现完成，精准 L1 与受影响 Uow L2 net6/net8 PASS。
- 红证据：保留 `artifacts/review-round-1/repro/Program.cs` 与 `stdout-valid.txt`；原候选中 handler `SetRollbackOnly` 后 commit1/rollback0，handler 重入 Rollback 后同一资源 rollback1 且仍 commit1。
- 根因：Saving 允许 handler 执行 root Rollback/Dispose/Complete/Begin participant；其返回后完成路径没有复核 root state 和 rollback-only，直接进入 Completing 并物理提交。
- 修复：Saving 执行流只保留保存所需操作；禁止的重入和跨流访问会拒绝并标记 rollback-only。保存结束在同一锁内验证仍为 Saving、未 rollback-only、无活动参与者/租约/资源工厂后才快照并进入 Completing；否则统一以非取消清理回滚全部资源。
- 直接回归：`OperationScopeLifecycleTest.SaveHandlerRollbackOnly_ShouldRollbackEveryResourceWithoutCallbacks`；`CapturedWriteFailureMarkedRollbackOnly_ShouldNotCommit`；`ReentrantRootOperationDuringSave_ShouldBeRejectedAndRollback`（rollback/complete/dispose/participant，同步/异步）；`SavingHandlerPending_ShouldRejectOtherExecutionFlowAdmission`；正常新增资源成功对照 `Complete_ShouldSaveBeforeCommittingResources`。
- 验证：精准 net6/net8 各 19 Passed / 0 Failed / 0 NotExecuted，退出 0；原始证据 `artifacts/review-fix-round-1/l1/uow-fix002-net6-v2/`、`uow-fix002-net8/`。受影响完整 `Bing.Uow.Tests` net6/net8 各 70/0/0，退出 0，`artifacts/review-fix-round-1/l2/uow-net6-current-v1/`、`uow-net8-current-v1/`。首次夹具参数编译错误仍保留于 `uow-fix002-net6/`。
- 当前结果：`SaveHandlerRollbackOnly_ShouldRollbackEveryResourceWithoutCallbacks`、`CapturedWriteFailureMarkedRollbackOnly_ShouldNotCommit`、`ReentrantRootOperationDuringSave_ShouldBeRejectedAndRollback`、`SavingHandlerPending_ShouldRejectOtherExecutionFlowAdmission` 均在当前完整 L2 通过；handler 毒化、捕获写入错误后毒化、保存期间重入和跨流准入均不再到达物理 commit；资源回滚/释放一次、成功回调 0。正常新增资源成功对照也通过。

#### FIX-001

- 严重程度：HIGH；处理要求：MUST_FIX；执行状态：实现完成，精准 L1 与 AOP 完整 L2 net6/net8 PASS。
- 红证据：原 review host `artifacts/review-round-1/repro/Program.cs` / `stdout-valid.txt` 显示 `operations-plus-legacy-marker` 为 legacy commit1、捕获失败后 owner commit1/rollback0。新增真实代理回归在 net6 首次运行 4 Failed / 2 Passed、退出 1，证据 `artifacts/review-fix-round-1/l1/aop-fix001-net6-red/`；四项失败分别覆盖两注册顺序的 active-root 成功与捕获失败，root 外两项通过。
- 根因：旧 interceptor 只按 automatic registration marker 分派；未启用自动代理时，`AddBingOperations` 中的活动 execution root 不参与旧标记分支判断，旧 `CommitAsync` 仍被调用。
- 修复：旧 interceptor 在 executor 能力已注册且当前 manager root 实现活动 `IUnitOfWorkExecutionScope` 时建立 participant；成功只关闭 participant 并向 root 登记去重后的 `ICommitAfter`，异常通过 participant rollback 毒化 root。automatic marker 保持委托给自动 owner/participant interceptor；无 active operation 时保留旧 Commit/回调路径。`IUnitOfWorkScopeManager.Current` 继续返回 root。
- 直接回归：`Bing.Aop.AspectCore.OperationRootLegacyMarkerParticipantTest.ExistingOperationRoot_ShouldTreatMarkedCallsAsParticipants`（同步+异步、两注册顺序/重复注册、root commit 后一次通知）；`CaughtMarkedCallFailure_ShouldPoisonExistingOperationRoot`；`MarkedCallOutsideOperationRoot_ShouldKeepLegacyCommit`。均通过实际 AspectCore Host 与接口代理。
- 验证：真实代理精准集 net6/net8 各 61/0/0，退出 0；原始 TRX 在 `artifacts/review-fix-round-1/l1/aop-fix001-combined-net6/`、`aop-fix001-combined-net8/`。受影响完整 AOP L2 两 TFM 各 171/0/0，退出 0，`artifacts/review-fix-round-1/l2/aop-net6-current-v1/`、`aop-net8-current-v1/`。
- 当前结果：`ExistingOperationRoot_ShouldTreatMarkedCallsAsParticipants`、`CaughtMarkedCallFailure_ShouldPoisonExistingOperationRoot`、`MarkedCallOutsideOperationRoot_ShouldKeepLegacyCommit` 在真实代理及完整 L2 通过；两种注册顺序下 root 内不提前 legacy commit，owner 决定保存/提交/回滚并对同服务 `ICommitAfter` 去重；root 外旧行为保留。

#### FIX-003 / FIX-004

- 严重程度与处理要求：FIX-003 HIGH / MUST_FIX；FIX-004 HIGH / MUST_FIX。执行状态：当前实现完成；Uow、Dapper Core、EF Core/SQLite 精准 L1 与受影响完整 L2 PASS。
- 红证据：原 review EF repro `artifacts/review-round-1/ef-repro/Program.cs` / `stdout.txt` 中 root 已取消后 `SaveChangesAsync()` 仍写入 1 行，SQL command token 不可取消；原 review AOP repro `artifacts/review-round-1/stdout-valid.txt` 中 participant 已取消但 `IoTokenCancelled=false`。正式回归红结果：`artifacts/review-fix-round-1/l1/uow-fix003-token-red-v2-net6/`（3 Failed / 0 Passed，退出 1：同步 Complete 忽略取消、非取消但可取消的 CompleteAsync 参数覆盖 participant 取消、嵌套 participant 不继承父 token）；`artifacts/review-fix-round-1/l1/ef-fix003-red-net6/`（1 Failed / 0 Passed，退出 1：传入 CancellationToken.None 后 EF 未抛取消，失败行集不符合未触碰数据库的前置门槛）。
- 根因：`IUnitOfWorkScopeManager.Current` 为兼容性始终返回 root；Dapper/EF 从该属性读取 ambient token 时看不到当前 participant。嵌套 participant 也只链接 root token。participant `CompleteAsync` 优先选取另传但尚未取消的可取消令牌，同步 `Complete()` 不检查自身取消。
- 修复：manager 内部按当前 AsyncLocal frame 返回有效执行 token，同时 `Current` 继续返回 root；嵌套 participant 链接父 frame 的有效 token；同步/异步 participant 完成均优先拒绝自身已取消 token并标记 root rollback-only。Dapper 与 EF 通过 Uow 内部 helper 获取当前 token，EF SaveChanges 与所有 Store 异步查询/写入在实际 await 生命周期内持有合成取消 lease；SaveChanges/Store 对活动 operation token 注册 rollback-only 回调。未改变公开 API，也未改显式独立 SQL 连接判定。
- 直接回归方法：`OperationScopeLifecycleTest.ParticipantCompleteAsync_WithNonCancelledArgument_ShouldPreserveParticipantCancellation`、`ParticipantComplete_WithCancelledParticipant_ShouldRejectSynchronousCompletion`、`NestedParticipant_ShouldInheritParentParticipantCancellation`；`OperationCancellationTest.MultipleQuery_WhenParticipantIsCancelled_ShouldRejectBeforeConnectionAccess`；`EfCoreOperationSqliteTest.SaveChangesAsync_WithCancelledOperationAndDefaultArgument_ShouldRejectBeforeSqliteIo`、`StoreRead_WhenParticipantCancelsAfterReaderOpened_ShouldCancelAndDisposeReader`。EF reader 回归在实际 SQLite command 已返回 reader 后经测试信号取消，验证回调中的 I/O token 可取消且收到取消、查询抛 OCE、reader 已关闭、root rollback-only；root identity assertion 继续使用 manager.Current。
- 精准验证：Uow net6/net8 各 3/0/0，Dapper token net6 2/0/0，EF reader SQLite net6 5/0/0，EF Store/save net6 10/0/0，退出码均为 0；原始目录见 `artifacts/review-fix-round-1/l1/uow-fix003-token-green-net6/`、`uow-fix003-token-green-net8/`、`dapper-fix004-token-net6-v2/`、`ef-fix004-reader-net6-v3/`、`ef-fix003-token-green-net6/`。当前受影响完整 L2：Uow 70 each TFM、Dapper Core 181 each TFM、EF Core net6 196、SQLite integration 233 each TFM，均 0 failed/0 not executed、退出 0，详见 L2 表。
- 当前结果：`ParticipantComplete_WithCancelledParticipant_ShouldRejectSynchronousCompletion`、`ParticipantCompleteAsync_WithNonCancelledArgument_ShouldPreserveParticipantCancellation`、`NestedParticipant_ShouldInheritParentParticipantCancellation`、`StoreRead_WhenParticipantCancelsAfterReaderOpened_ShouldCancelAndDisposeReader`、`SaveChangesAsync_WithCancelledOperationAndDefaultArgument_ShouldRejectBeforeSqliteIo` 和 `SqliteUnitOfWorkContractTest.AsyncStream_WhenParticipantCancelsDuringReaderRead_ShouldCancelAndReleaseReader` 通过；真实 reader/I/O 取消、reader 释放后清理 CTS、root rollback-only 均有证据。`Current` 仍为 root；无 operation/显式独立 SQL 的兼容回归在完整 L2 通过。

#### FIX-005

- 严重程度：HIGH；处理要求：MUST_FIX；执行状态：实现完成，EF Core/SQLite net6 精准 L1、EF/SQLite 完整 L2 PASS。
- 红证据：`artifacts/review-round-1/ef-repro/Program.cs` / `stdout.txt` 的 `requires-new-pending-context` 记录 outer tracked entity 后直接开始同 scope `Begin(requiresNew:true)`，inner `SaveAfterCount=1` 且 outer rollback 后独立连接仍读到 1 行。
- 根因：EF registry 记录已解析实例但不记录操作 root 归属；`EnsureMappedContextCanJoin` 只拒绝已有 SQL resource claim 的 Context，尚未 flush 的 tracked entity/action/event 因 `_mappedSqlScopeId` 为空可被 inner root 自动保存。
- 修复：`EfCoreOperationSaveChangesHandler` 在遍历并保存任何 Context 之前，检查当前 manager frame 是否为同 DI scope 的 active `requiresNew` 子 root；若已解析 Context 存在 pending entity、transaction action、未分发 domain event，或 Context 已由其他活动 root claim，则标记 inner rollback-only 并在 I/O 前拒绝。无 pending 的 clean context 仍可进入保存流程；新的 executor scope 没有活动祖先，按独立 Context 正常完成。
- 直接回归：`EfCoreOperationSqliteTest.RequiresNewSameScope_WithPendingEntity_ShouldRejectBeforeSqliteIo`、`RequiresNewSameScope_WithPendingTransactionAction_ShouldRejectBeforeActionOrIo`、`RequiresNewSameScope_WithPendingDomainEvent_ShouldRejectBeforeDispatchOrIo`、`RequiresNewSameScope_WithContextClaimedByOuter_ShouldRejectBeforeInnerSave`；成功对照 `ExecutorNewScope_ShouldCommitItsContextWhenOuterRootRollsBack` 验证真实 executor 子 Context 不同且 inner commit 在 outer rollback 后行集精确为 1。
- 验证：精准 EF Core net6.0/SQLite 5/0/0、退出 0，`artifacts/review-fix-round-1/l1/ef-fix005-net6/`；Round 1 EF L2 196/0/0，SQLite L2 net6/net8 各 233/0/1，退出 0，见 `artifacts/review-fix-round-1/l2/` 对应 TRX。
- 当前结果：`RequiresNewSameScope_WithPendingEntity_ShouldRejectBeforeSqliteIo`、`RequiresNewSameScope_WithPendingTransactionAction_ShouldRejectBeforeActionOrIo`、`RequiresNewSameScope_WithPendingDomainEvent_ShouldRejectBeforeDispatchOrIo`、`RequiresNewSameScope_WithContextClaimedByOuter_ShouldRejectBeforeInnerSave` 与 `ExecutorNewScope_ShouldCommitItsContextWhenOuterRootRollsBack` 通过；四类外层状态均在 I/O 前拒绝，新 scope executor 的 context/query/root 身份不同且子提交保留。

#### FIX-006

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：实现完成，executor 精准 L1、Uow 完整 L2 net6/net8 PASS。
- 红证据：review `artifacts/review-round-1/stdout-valid.txt` 的 `executor-trace` 中业务 `SameObject=true`，原地修改 TraceId 后父对象值变成 `mutated-by-child`。
- 根因：`BingOperationExecutor.ExecuteCoreAsync` 只隔离 DI scope，没有建立 `TraceIdContext` AsyncLocal 作用域；Sample 入口快照无法保护直接调用公共 executor 的用户。
- 修复：options 校验和预取消检查通过后，executor 复制当前 TraceIdContext 的 `TraceId/RootId/ParentId/ChildId` 四个字符串到新对象；整个 scope 创建、manager Begin、业务构造/执行、root cleanup 和 DI cleanup 都置于 `try/finally`，finally 恢复原父对象引用。无父 Trace 时继续保持 null。
- 直接回归：`BingOperationExecutorTest.Execute_ShouldCopyTraceBeforeConstructionAndRestoreAfterNestedAndParallelWork` 覆盖构造时观察、四字段副本、嵌套和屏障并行对象身份/恢复；`Execute_ShouldRestoreParentTraceAfterResolutionBusinessCancellationAndCleanupFailures` 覆盖解析、业务异常、信号取消和 DI 清理异常。
- 验证：精准 Bing.Uow.Tests net6.0 6/0/0，退出 0，`artifacts/review-fix-round-1/l1/uow-fix006-net6-v2/`；完整 Uow L2 net6/net8 各 70/0/0，退出 0。
- 当前结果：`Execute_ShouldCopyTraceBeforeConstructionAndRestoreAfterNestedAndParallelWork` 与 `Execute_ShouldRestoreParentTraceAfterResolutionBusinessCancellationAndCleanupFailures` 在当前 L2 通过。executor 在业务构造/解析前复制四个 Trace 字段，嵌套/并行及失败、取消、清理后恢复父引用。

#### FIX-007

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：新模式直接验收已补齐；精准 L1、受影响完整 L2、SQLite 与真实 host L3 PASS，等待独立复审。
- 红证据：review 的 FIX-007 表及 C03/C05–C14 记录指出旧 `EfCoreSqliteUnitOfWorkContractTest` 默认未注册 `AddBingOperations`，旧通知/flush、executor probe 与旧代理测试不能证明新分支；其余缺口包括新自动真实代理返回形态、EF legacy Commit/late state、两个 SQLite 模式取消和身份等。原始缺口说明保留于只读 `review.md`。
- 新自动代理直接回归：真实 AspectCore source-backed `ValueTask` 预取消、信号取消和成功对照；faulted OCE/token 真实组合链；同步类代理、显式接口、嵌套 owner/participant、重复 `ICommitAfter` 去重。net6/net8 精准矩阵各 16/0/0，完整 AOP L2 各 171/0/0，退出 0；首次编译失败与当前结果分别保留于 `artifacts/review-fix-round-1/l1/aop-fix007-auto-matrix-net6-v1/`、`aop-fix007-auto-matrix-net6-v2/`、`aop-fix007-auto-matrix-net8-v1/` 及 `artifacts/review-fix-round-1/l2/aop-net6-current-v1/`、`aop-net8-current-v1/`。
- 生命周期异常/释放直接回归：`OperationScopeLifecycleTest.CommitFailure_ShouldKeepPartialOutcomeAndNotifyOnlyCommittedResource`、`NotificationFailure_ShouldNotRetryAfterOperationResourcesCommitted`、`SaveCancellation_ShouldPreservePrimaryAndAttemptNonCancelableCleanupOnce` 覆盖 opt-in 保存取消、第二资源提交部分结果、主异常/清理异常、一次释放与已提交资源回调。net6 生命周期精准集 25 Passed / 0 Failed / 0 NotExecuted、退出 0；原始 TRX/命令/stdout/stderr 在 `artifacts/review-fix-round-1/l1/uow-fix007-operation-failure-net6-v1/`。
- EF 新模式直接矩阵：mapped operation 不调用 legacy `TransactionCommit`、unused mapping 不构造、unmapped dirty 与显式旧写先拒绝、tracked-only、两种手工 flush/事件一次分发/无额外空 flush、late cross-context pending/重变更、非事务 action/event I/O 前拒绝、通知失败与部分提交行集、写入失败回滚、EF/Dapper 双访问次序均由 `EfCoreOperationSqliteTest` 覆盖。Round 1 精准集 v3 30/0/0，EF L2 net6 196/0/0，SQLite integration L2 net6/net8 各 233/0/1；首次 v1/v2 失败原始结果保留，v3 与其命令/输出/TRX 在 `artifacts/review-fix-round-1/l1/ef-fix007-operation-matrix-net6-v3/`。
- Sample 新模式：真实 HTTP、BackgroundService 与 Hangfire Server 具有服务/manager/EF Context/Query/root 身份、Trace/tenant 标量和两个 storage mode 的请求/停机取消、下一项隔离、精确零残留断言。`OperationEntryPointTests` net6/net8 各 14/0/0；Sample L2 v2 net6/net8 各 26/0/18，包含 18 个 `RUN_CAP_MYSQL_TESTS` 门控项未执行；证据 `artifacts/review-fix-round-1/l1/sample-operation-entrypoints-net60-v2/`、`net80-v2/` 与 `l2/sample-net60-current-v2/`、`sample-net80-current-v2/`。
- 新 operation CAP L3：`CapMySqlIntegrationTests.OperationWebHost_WithIndependentConsumer_ShouldPersistExactDualSourceDeliveryAcrossEntryPoints` 由真实 producer Web host 和独立 consumer 子进程执行；包含双库 orders/reporting、EF-first、HTTP/worker/Hangfire、失败/取消/下一项及部分提交，逐项断言 Outbox、business event、inbox、effect、attempt 和来源/root/event 标识。net6.0 1/1/0、退出 0；TRX `artifacts/review-fix-round-1/l3/cap-independent-process-net6-v6/trx/cap-independent-process-net6-v6.trx`，operation journal `artifacts/review-fix-round-1/l3/cap-independent-bdcec1dede7745a2a5cd1ea4e9776d18/producer-operations.jsonl`。
- 当前结果：新增矩阵和当前 L2/L3 通过；Review 表中的新模式直接证据已有当前方法/TFM/TRX。失败的早期夹具结果继续保留为红/修正过程证据，不计当前通过。是否满足 finding 的独立验收由 Sol/high 决定。

#### FIX-008

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：README/支持矩阵/迁移指南/编译示例/本轮报告已更新；当前 docs checker 与相关测试 PASS，等待独立复审。
- 红证据：review FIX-008 指出 B00 的 Bing.Uow README/支持矩阵未更新，迁移指南缺自动代理 opt-in，旧报告夸大支持与完成度。该原始对照保留于 `review.md` 和 `artifacts/review-fix-round-1/startpoint/`，未修改历史材料。
- 修复内容：`framework/src/Bing.Uow/README.md`、`docs/architecture/abp-infrastructure-support-matrix.md`、`docs/sql/migration-guide.md`、Sample README 已加入新旧 opt-in、owner/participant、executor 新 DI scope/结果物化、EF 纯保存与限制、CAP 证据边界；真实 EF SQLite `UseSqlite` 仍链接 Sample Startup，不伪造 Bing API。原报告“10/10 完成”已改为逐 finding / gate 状态。
- Round 1 候选验证：实际 `UseSqlite` 对应物为 Sample Startup；`UnitOfWorkDocumentationExamplesTest` 的 opt-in executor 主例 net6/net8 已由 Uow L2 运行；Round 1 Release solution build 退出 0。Python 3.12.1 `-X utf8` 下 `eng/ci/check-docs.py --quiet` 13 项通过、退出 0；首次 PATH shim 启动错误 9009 保留在 `artifacts/review-fix-round-1/final-candidate/docs/`，成功证据在 `docs/direct-python/`。operation、自动边界、EF saving 与 executor XML/API 文档已核对。`final-report.md` 已更正首轮“10/10”及历史 CAP 22/22 的证据边界。

## Round 1 历史候选门禁

本节只记录 `phase-b-review-fix-round-1-2026-10-23-v1`。历史 `candidate-v2` 结果保留在上方历史表，不升级为本候选证据。

| 层级 | 当前方法/项目与 TFM | 结果 | 原始证据 |
| --- | --- | --- | --- |
| L1 | FIX-001–008 的新增精确回归，Uow/AOP/EF/Dapper/Sample net6/net8（EF 与 DB provider 仅既有 net6） | 各项 0 Failed、0 NotExecuted、exit 0；早期失败迭代另存并不计通过 | `artifacts/review-fix-round-1/l1/` |
| L2 | Uow 70、AOP 171、Dapper Core 181、SQLite integration 233，net6/net8；EF v2 196 net6；Sample v2 26 net6/net8 | Uow/AOP/Dapper/EF 为 0/0/0；SQLite 为 233/0/1（ProviderContract gate）；Sample 为 26/0/18（CAP/MySQL gate） | `artifacts/review-fix-round-1/l2/`、`artifacts/review-round-2/evidence-mechanical.json` |
| L3 | SQLite contract runner net6；独立 CAP consumer 方法 `OperationWebHost_WithIndependentConsumer_ShouldPersistExactDualSourceDeliveryAcrossEntryPoints` net6 | SQLite 1/0/0；CAP 1/0/0；两个命令 exit 0 | `artifacts/review-fix-round-1/l3/` |
| Release build | `dotnet build H:/Bing_Framework/Bing.NetCore/Bing.All.slnx -c Release --no-restore --nologo -m:1` | 退出 0；0 errors、170 warnings | `artifacts/review-fix-round-1/final-candidate/Release-build/` |
| 默认 L4 | solution 全量 net6/net8；无过滤，外部/性能门控关闭的独立子进程；无 runsettings，xUnit 默认并行 | 103 TRX、12,828 Passed / 1 Failed / 1,114 NotExecuted；net6 6,669/1/593，net8 6,159/0/521；6 个空 TRX 单列；退出 1。唯一失败是 `Bing.Tests.Modularity.ModuleLifecycleTest.RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive`，本轮落在 net6，因果 NOT_DETERMINED，不隔离重跑 | `artifacts/review-fix-round-1/final-candidate/L4/command.json`、`summary.json`、原始 TRX/stdout/stderr |
| Docs | Python 3.12.1 `-X utf8 H:/Bing_Framework/Bing.NetCore/eng/ci/check-docs.py --quiet` | 13 checks 通过，退出 0；PATH shim 首次启动退出 9009，随后绝对路径重跑成功 | `artifacts/review-fix-round-1/final-candidate/docs/`、`docs/direct-python/` |

Round 1 Release status 为 FAIL（该候选默认 L4 有一项历史 GC 弱引用失败）。不以局部通过拼总体 PASS。Round 1 执行任务已通过 `task-finish.mjs` 收口，finalStatus=COMPLETED、notificationStatus=skipped；随后由主线程安排独立 Reviewer。Round 2 状态见本文件顶部，不修改 `review.md` 或父路线图状态。

#### EXT-001 独立 CAP 消费证据

- Review 初始分类为 `BLOCKED_EXTERNAL`，因为旧 22/22 场景没有等待独立 consumer。复核任务隔离 MySQL/Rabbit 身份和健康后，运行了新 operation 的 producer/consumer 分进程测试；未启动、停止或重置其他容器。
- `CapMySqlIntegrationTests.OperationWebHost_WithIndependentConsumer_ShouldPersistExactDualSourceDeliveryAcrossEntryPoints` 使用真实 producer Web host 与 standalone consumer 子进程。net6.0 TRX 为 1 Passed / 0 Failed / 0 NotExecuted，命令退出 0；结果在 `artifacts/review-fix-round-1/l3/cap-independent-process-net6-v6/`。
- 验证 orders/reporting 双 MySQL source、EF-first、HTTP/worker/Hangfire 成功、失败/取消后业务行与两侧 Outbox/business event/inbox/effect/attempt 均为零、后续操作成功，以及部分提交对照。成功 consumer row 按 event/source/operation/trace/root/consumer/effect/attempt 断言；由真实 CAP broker 消费，没有直接插入消费记录。
- TRX 记录 producer PID 60680、consumer PID 100048；journal `artifacts/review-fix-round-1/l3/cap-independent-bdcec1dede7745a2a5cd1ea4e9776d18/producer-operations.jsonl` 共 8 项最终状态：7 项走新 executor，另 1 项为 operations-enabled 的旧手工 partial-commit root；状态为 5 Completed、1 Failed、1 Cancelled、1 部分提交场景 Failed。测试完成后两个子进程均已退出。
- Round 1 执行证据已提供；Round 2 Reviewer 随后关闭 EXT-001，本节只保留 Round 1 CAP 原始证据。
