<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: 20261021-sql-uow-sample
AI_REVIEWED_AT: 2026-10-21T11:38:50+00:00

# T07 第2轮独立验收

结论：**PASS**。FIX-001 与 FIX-002 均为 **CLOSED**，没有新的 OPEN_ACTIONABLE。已审核真实分层 Query/builder/参数、并发屏障、DI/数据库资源引用与释放证据；新鲜 net6/net8 Release 小型 Sample 套件各 8/8、0 失败、0 跳过。正常宿主未安装测试 hook 的补充探测也通过。按 GOAL_RULES 停止，不进入下一轮 Fix。

```text
IMPLEMENTATION_STATUS: PASS
TEST_STATUS: PASS
PERFORMANCE_EVIDENCE_STATUS: NOT_APPLICABLE
RESOURCE_EVIDENCE_STATUS: PASS (本计划范围的 scope 与正常 Stop/Dispose)
EXTERNAL_GATE_STATUS: NOT_APPLICABLE
RELEASE_STATUS: PASS (本任务已批准的本地验收范围)
GOAL_STATUS: COMPLETED
OPEN_ACTIONABLE: 0
CLOSED: 2
```

## 1. 输入、候选与历史保护

本轮重新核对 AGENTS、GOAL_RULES、review-code 技能、批准计划、execution 当前修复记录和完整内容、traceability、artifacts-manifest、第1轮两个 Finding、当前 8 个变更文件及关联原调用链。只读证据/哈希核对的窄子任务交由 luna_worker；主 Reviewer 负责代码行为、完整验收判断、fresh gates 和报告。

**第一步已逐字节归档旧 review.md** 至 [review-round-1.md](artifacts/review-round-2/review-round-1.md)，在写入本报告之前再次确认与旧 review.md 字节完全相等；大小/SHA256 见 [归档校验](artifacts/review-round-2/review-archive-verification.txt)。未覆盖 `artifacts/review-round-1`、修复轮原始日志/TRX、plan/execution 或其他任务证据。

- HEAD 仍为 `5065c38ced05a95088e17d89d96c9a1610e37159`；staged diff 为空；前置 F008/Uow 大量未提交修改保持保护，不作为 Sample 新增变更。
- 对比第1轮候选清单：6 个既有文件变化（应用服务、orders 仓储、Program、ScenarioController、README、E2E tests），新增内部 probe 与 AssemblyInfo 共 2 个文件；其余原 Sample 文件 hash 一致。没有包/TFM/API baseline 变化。框架未修改的说明与当前任务范围一致；不把前置框架 diff 重新归属本轮。
- 修复轮 `source-files-sha256.txt` 的 8/8 文件与当前源码全 MATCH，见 [fix-source-verification.txt](artifacts/review-round-2/fix-source-verification.txt)。Reviewer 对 Sample/Test 全部 28 个非 bin/obj 源/配置/文档文件保存前后 hash，均一致，见 `source-candidate-before.txt`、`source-candidate-after.txt`。
- 旧源码没有 commit/snapshot 可直接生成逐行 round1→round2 Git diff，因此变化范围依据第1轮逐文件 hash、前轮已读取源码及当前真实代码核对；当前 tracked/staged diff 已保存。不能把原始 untracked Sample 的 HEAD diff 缺失解释为“无变化”。
- 当前正式修复证据为 `review-fix-round-1/net6-release-final-2` 与 `net8-release-final-2`；分别核对 log 的准确命令/退出码与 TRX：各 8 passed/8 executed/0 failed/0 notExecuted。四个理论方法 × 两种配置，非“三个方法”。原 6/6 和更早诊断均为历史，不作为当前候选通过依据。

## 2. Finding 结案

### FIX-001 — CLOSED（原 HIGH / MUST_FIX）

真实调用链是 `ScenarioController.Run` → `SqlUnitOfWorkApplicationService.ExecuteAsync` → `RunLayerReadQueriesAsync` / `CreateLayerReadQueries`，以及 `OrdersRepository.CreateOperationQuery/AddRepositoryFilters`。

- 应用服务用自己构造函数注入的 `_applicationQuery.Query()` 建立查询；仓储用其独立注入的 `_query.Query()` 建立另一查询。并非测试另建两个工厂 Query 绕开分层服务。
- `Application/SqlUnitOfWorkApplicationService.cs:235-247` 先构造应用 OperationId 筛选，再构造仓储 OperationId 筛选，然后交错添加应用 Layer、仓储 Id/Sequence/Value、应用 Sequence/Value。两层 SQL 与参数明确不同，参数名序号相同但值不同，可识别共享 builder/参数污染。
- `RunLayerReadQueriesAsync:166-185` 在执行前捕获两层真实 ToSql/GetParams，并各重复渲染；随后分别通过真实 Query 执行 `ToListAsync<OrderQueryRow>`，记录实际完整结果，再渲染 SQL。probe 对字典与行值做拷贝，没有生成预期 SQL/虚构行。
- `SqlUnitOfWorkHostTests.cs:41-48,330-385` 用独立固定完整 SQL 常量作 ordinal 相等，比较完整参数字典（应用 OperationId/application/1/application；仓储 OperationId/{id}:repository/2/repository），并逐列核对两层各自唯一一行。不是 Contains SQL 或同对象不同 Guid。
- 执行后状态记录中，`CaptureQueryState(..., applicationState.Parameters)` 使用的是**执行前保留的实际参数集合**，不是重新读取已冻结 Query 的参数管理器；因此不把该 after-stage 解释为“执行后重新取参”的额外证据。结案依据是交错构造后真实取参、重复渲染及两套不同参数的实际数据库结果，足以满足原 Finding；没有靠重放预期参数使前置取参断言通过。
- HTTP 理论测试对 commit 与 rollback 均执行上述断言，Complete/Rollback 前应用和仓储各可见同事务两行，最终独立 SQLite 连接比较完整累计行集。原先事务共享与未提交可见的证据继续成立。

验收：两个配置、两个 TFM 全通过；原核心缺口已关闭，无需继续修改框架查询架构。

### FIX-002 — CLOSED（原 MEDIUM / SHOULD_FIX）

`HttpHost_ShouldKeepConcurrentReadOnlyScopesIndependent:138-220` 使用真实 Kestrel 请求与数据库读取，非仅报告字段：

1. 先真实提交种子两行，用独立连接确认两 DB 完整集合；同时发出两个目标为该 operationId 的只读 HTTP 请求。
2. 每个请求 Begin 非事务 root 后，先实际执行应用/仓储查询，再进入 probe 屏障。只有两者均到达该位置，测试才继续。两个请求未释放时均持有 Open 连接、真实行结果和活动 scope。
3. `SampleScopeIsolationProbe.Attach:121-153` 保存实际 root、当前 manager、两层注入 Query 和 scoped DbContext 引用，并检查 `ReferenceEquals(scope, manager.Current)`；没有用新建 Guid 替代这些对象。活动数在 Attach 增加、root await-dispose 后的观察租约退出时减少；最大值由活动数计算，没有赋固定成功值。
4. Dapper 测试工厂实际 `new SqliteConnection(connectionString)`，记录并把**同一个对象返回给框架 resolver**；不是旁路观察连接。EF 记录当前 scoped DbContext 的 `Database.GetDbConnection()`；真实 orders 映射仍由原 `AddEfCoreSqlUnitOfWork` 资源桥接执行，无假 Provider。连接在屏障时 Open，结束时 Closed。
5. `AssertDistinctScopeResources:424-442` 比较实际 root、manager、两层 Query、context、orders connection 的引用；`AssertScopeResourcesDisposed:460-468` 还实际调用 Query.Query / DbContext.Entry，要求 ObjectDisposedException，不能仅靠 marker 布尔通过。
6. 测试只释放其中一个 operation，等待其响应及 root/DI scope 销毁；确认另一个未销毁且连接仍 Open，结果记录仍只含 before-gate。之后才释放第二个，让它在原 scope 内**重新建立两个 Query 并实际执行**，核对种子完整行，再确认其释放。每个完成阶段都独立比较两 DB 准确行集保持不变。

并发口径：TRX 中 `requestedConcurrency=2; maxActualParallelism=2` 表示实际重叠的活动只读 scope/HTTP 请求，**没有宣称两条 SQL 命令在同一时刻执行**。fresh 两 TFM、每 TFM 两配置共四个并发用例均观测到 2。

Worker/Job 补证：两个原理论方法 `:243-258` / `:289-304` 用真实 HTTP 投递两次顺序操作，通过 operationId 关联实际资源记录；等待 root 与 DI scope 退出，再断言 Query/context 真不可用、connection Closed、两次资源引用不同；before/after 的查询结果为空与操作 ID 筛选一致，并独立确认两 DB 完整行集未变化。保持单消费者/单 Hangfire worker，未增加并发写或假称后台真并行。

生命周期 marker 在测试专用工厂中先解析，再解析 Query/context；DI 逆序释放保证它最后通知，且同时实现 IDisposable/IAsyncDisposable。root 的 disposed 标记来自 `await using scope` 之后的观察租约退出。上述真实对象调用/连接状态断言使标记不是唯一判据。

## 3. 测试 hook 与正常 Sample 回归

- `SampleScopeIsolationProbe`、`SampleScopeLifetime` 均 internal，仅友元测试可访问；默认 Startup 没有注册 probe/marker 或替换连接工厂。
- 应用服务 public 构造函数转到相同内部构造函数，但 probe/lifetime 为 null；业务执行仍是同一个实现。新增宿主 configureServices 回调仅由测试传入。正常宿主不会建立观测字典/屏障、保留测试对象或改变 root/事务完成算法。
- 为真实 Sample 跨层场景增加的 fluent 查询及行校验属于展示查询状态独立的业务路径；没有新增框架 API、第二套 Uow 或模拟数据库。测试替换工厂只包装真实 SQLite 连接构造，应用/仓储依赖仍从原 DI 容器解析。
- Reviewer 补跑**不传 configureServices、不装 probe**的正常宿主：两种模式各 3 个真实 Hangfire 作业，cross-layer commit/rollback 为 Completed/Succeeded，caught SQL 为 Failed/Failed；保留异常 rethrow，Stop 后 Server 从 1 降至 0，Dispose 后目录不存在。见 [正常宿主探测源码](artifacts/review-round-2/normal-host-probe/Program.cs) 与 [原始日志](artifacts/review-round-2/normal-host-probe.log)。该 net8 功能探测不单独比较 DB；准确行集由同候选完整 E2E 套件验证，不能把状态检查替代数据库证据。
- fresh 全套继续覆盖双库 commit/rollback、caught failure rollback-only、100 factory Query、EF SaveChanges/Complete 边界、Worker/Job 失败、Stop worker/端口/目录清理，未发现新增路径破坏前轮通过行为。

## 4. CIA、命令与新鲜证据

测试前 [cia.txt](artifacts/review-round-2/cia.txt) 已记录：本轮仅 Sample/Tests 的查询、HTTP 参数、测试宿主回调和内部观测，SQLite/DI/EF 生命周期，风险 HIGH；框架契约/热路径、Provider 分支、包/TFM 未新增改变。选择完整小型样例 L1/L3，未跑全仓、外部数据库/服务、Benchmark 或容量 probe。

工作目录均为 `H:/Bing_Framework/Bing.NetCore`，Windows/PowerShell、SDK 8.0.424、VSTest 17.11.1。net8 指测试宿主；Sample 仍按计划编译为 net6.0。没有启用外部数据库服务。

| 实际命令 | 结果/退出码 | 原始证据 |
| --- | --- | --- |
| `dotnet test samples/Bing.Samples.SqlUnitOfWork.Tests/Bing.Samples.SqlUnitOfWork.Tests.csproj -c Release -f net6.0 --logger "trx;LogFileName=review-net6.trx" --results-directory ai_docs/tasks/20261021-sql-uow-sample/artifacts/review-round-2/net6` | 8/8 passed，0 failed/notExecuted；0 | [log](artifacts/review-round-2/net6-release.log)、[TRX](artifacts/review-round-2/net6/review-net6.trx) |
| `dotnet test samples/Bing.Samples.SqlUnitOfWork.Tests/Bing.Samples.SqlUnitOfWork.Tests.csproj -c Release -f net8.0 --logger "trx;LogFileName=review-net8.trx" --results-directory ai_docs/tasks/20261021-sql-uow-sample/artifacts/review-round-2/net8` | 8/8 passed，0 failed/notExecuted；0 | [log](artifacts/review-round-2/net8-release.log)、[TRX](artifacts/review-round-2/net8/review-net8.trx) |
| `dotnet run --project ai_docs/tasks/20261021-sql-uow-sample/artifacts/review-round-2/normal-host-probe/ReviewerProbe.csproj -c Release` | 2 正常 hosts，6 jobs，状态一致/Server 退出/目录删除；0 | [log](artifacts/review-round-2/normal-host-probe.log) |
| `git diff --check` | 0；保留原有换行提示 | [log](artifacts/review-round-2/diff-check.log) |

各次 dotnet test 含正常 restore/build，Sample Release 构建成功；修复轮单独 Sample Release build 也为退出码 0、0 errors，身份范围与当前 8 文件清单一致。NETSDK1138/NETSDK1206 为既有目标/RID 提示，不据此扩展升级依赖或 TFM。

[executor-trx-verification.txt](artifacts/review-round-2/executor-trx-verification.txt) 记录修复轮 final-2 的实际 8 用例/并发输出核对；[fresh-trx-summary.txt](artifacts/review-round-2/fresh-trx-summary.txt) 从本轮新鲜 TRX 独立解析同样计数/四个实际并发观测。没有只引用 execution 或统计字段作验收。

## 5. T01—T07 当前矩阵与符号映射

| Task | 状态 | 本轮依据 |
| --- | --- | --- |
| T01 冻结/保护起点 | PASS（复用） | 原 baseline/HEAD；旧 Review 字节归档；原 F008/Uow 边界不重开 |
| T02 双库/双配置宿主 | PASS | 默认注册无变化；两配置严格 DI/真实初始化与 fresh 全套，正常无 hook 宿主也启动成功 |
| T03 分层与场景 | PASS | FIX-001 关闭；真实 builder/SQL/参数/行结果；其余事务/EF/工厂场景 fresh 复验 |
| T04 三入口 | PASS | 真实 Kestrel/Worker/Hangfire；scope 真实引用/释放、失败与正常无 hook 作业状态复验 |
| T05 E2E | PASS | FIX-002 关闭；net6/net8 各 8/8；活动请求重叠/单方结束/另一继续，完整 DB 行集和 Stop/Dispose |
| T06 文档/交付 | PASS | 原 solution/index/依赖范围不变；README 说明 targetOperationId；traceability 更新到具体行为 |
| T07 独立验收 | PASS | 完整修复复核、候选绑定、fresh 验证及本报告，无剩余可执行项 |

| 生产符号 | 具体测试/断言证据 |
| --- | --- |
| `CreateLayerReadQueries`、`OrdersRepository.CreateOperationQuery/AddRepositoryFilters`、`RunLayerReadQueriesAsync` | `HttpHost_ShouldVerifyTransactionsFactoryLifetimeAndExactRows` → `AssertCrossLayerBuilderState` / `AssertQueryStates` / `AssertCrossLayerBuilderRows`；commit+rollback 两分支，完整 SQL/字典/精确行 |
| `ExecuteAsync` readOnly、`RunReadOnlyScopeIsolationAsync`、内部 probe Attach/gate、DI marker | `HttpHost_ShouldKeepConcurrentReadOnlyScopesIndependent` → `AssertDistinctScopeResources` / `AssertReadOnlyQuerySnapshots` / `AssertReadOnlyQueryRows` / `WaitForScopeDisposedAsync` / `AssertScopeResourcesDisposed` |
| `ScenarioBackgroundWorker`、`SqlUnitOfWorkJob` | 对应两个后台理论方法 → operation 关联实际 observation、顺序资源身份/释放/准确行集；正常无 hook probe 补实际 Hangfire 状态 |
| `ReportingRepository`、rollback-only、factory lifetime、EF context/bridge | 原 HTTP 理论方法 :74-131；完整双库最终集合、100 次、EF 保存及不保存边界仍 fresh PASS |
| `Program` / `Startup` / 初始化 / `SampleDatabaseFiles` | `RunningSampleHost.StartAsync/DisposeAsync`，worker exit、TCP close、目录缺席；正常宿主 probe 补 Server 注销 |

所有配套方法位于 `samples/Bing.Samples.SqlUnitOfWork.Tests/SqlUnitOfWorkHostTests.cs`，四理论 × 两 storageMode 在两 TFM 执行。完整行集 helper 为 `AssertRowsAsync:525-558`，对两个真实文件新开独立 Pooling=False 连接并比较全部列，未放宽成日志/count/Contains。

## 6. 边界复用与停止

前轮已接受的 EF 显式 POCO 模型/空 SaveChangesBefore 局部适配、两个本地事务非分布式原子性、MemoryStorage 非持久、正常操作结束后的 Stop/Dispose 边界保持有效。未改相关契约，不重新打开；不宣称软删除/审计、异构数据库、强制取消在途长作业或排队 drain 已验证。不为 execution 历史段落的措辞制造修复循环。

```text
Task: 20261021-sql-uow-sample
Round: 2 independent review
Implementation: T01-T06 6/6 accepted; T07 review complete
Release Gates: 5/5 (build; net6; net8; static; full planned coverage)
Changed Scope: review.md + artifacts/review-round-2 only
Validation Run: L0 PASS; L1/L3 small Sample full net6/net8 PASS; normal-host functional smoke PASS
L4: no full repository rerun; unchanged solution/docs evidence reused
L5: NOT_APPLICABLE
Completed: FIX-001 CLOSED; FIX-002 CLOSED; planned acceptance PASS
Open Actionable: none
Blocked Approval: none
Blocked External: none
Not Applicable: external DB; benchmark/capacity; distributed atomicity
Accepted Limitations: prior EF POCO adaptation; per-source transactions; MemoryStorage non-durability
Verified Boundaries: two actual active readonly scopes; independent resource disposal; orderly host Stop/Dispose
Deferred: none
No-Progress Check: CHANGED (relevant source/test changes and new evidence close both findings)
Next Action: STOP
Goal Status: COMPLETED
```

未修改生产/测试/plan/execution/traceability/原 manifest；未暂存、提交、推送、创建 PR、修改版本/API baseline 或发送外部通知。
