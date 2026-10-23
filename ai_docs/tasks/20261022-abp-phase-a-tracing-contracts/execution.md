<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: 20261022-abp-phase-a-tracing-contracts
AI_EXECUTION_FINISHED_AT: 2026-10-22T12:59:14Z

# 阶段 A 执行报告

实施范围 A01–A05 已完成，A06 独立 Review 留给主线程安排的 Sol Reviewer；本文件不代写 `review.md`。阶段实现内 `OPEN_ACTIONABLE=0`，但当前最终候选全量测试状态为 `FAIL`，失败关联性 `NOT_DETERMINED`，等待独立审查。

```text
CURRENT_FINAL_FULL_SUITE_STATUS=FAIL
RELEASE_STATUS=FAIL
FAILURE_CAUSALITY=NOT_DETERMINED
PERFORMANCE_EVIDENCE_STATUS=OBSERVED
THRESHOLD_STATUS=BLOCKED_APPROVAL (未设性能预算)
PHASE_A_IMPLEMENTATION_OPEN_ACTIONABLE=0
```

## 实施结果

| 项 | 结果 |
| --- | --- |
| A01 红回归 | 保留真实 `CapTraceAttribute.Invoke` 直接调用 next 计数红证据、原生 CAP `ISubscribeInvoker.InvokeAsync` 父 Trace 对象污染红证据，以及有效代理预取消红证据。早期接口未标记特性的代理夹具未实际触发拦截，已排除，不用作根因证据。 |
| A02 Attribute | `CapTraceAttribute` 缺少/空参数及 null、非 Header 时仍执行 `next` 一次；非空 Trace（含空白）使用新上下文并在 `finally` 恢复精确父引用。取消使 Attribute 返回的管道 Task 本身携带原取消异常，避免外层 Uow 将取消识别为成功。 |
| A03 Admin | `CapSubscribeInvoker` 只为非空 Trace 安装新上下文；通过真实 CAP `ISubscribeInvoker.InvokeAsync` 消费生命周期观察异步结束后的调用方上下文恢复。没有在 `GetInstance` 立即恢复。 |
| A04 文档 | Uow README 分开描述旧 `CommitAsync` 与新 `Begin` / `CompleteAsync` scope，关键代码由测试项目编译；新增 G01–G12 支持矩阵及导航。Uow 类型表已修正为“管理并提交已注册工作单元”，移除不可维护的源文件数。 |
| A05 验证 | 直接回归、受影响 L2、有限 Trace probe 与一次 Release/net6/net8 候选全量均保存了命令、退出码、stdout/stderr 和 TRX。 |

## 影响与验证

先对实际代理与真实 CAP 调用器做 L1 红绿；之后覆盖 EventBus/Admin/Uow/AOP 受影响测试 L2。Admin 改动只新建 Trace 上下文，不更改 CAP 消费、重试、过滤器、事务或数据库协议；已做本机真实 CAP 调用器直接测试，未运行 Rabbit/MySQL 外部 broker 集成。Uow 生产代码和已验收 `BingUnitOfWorkExtensions` 未改；SQL、Provider 与数据库访问路径均未改，因此没有扩建 SQL/数据库测试。

| 验证 | 原始结果 |
| --- | --- |
| EventBus L1 正确性 | `CapTraceAttributeTest` + `BingSubscribeInvokerTest`：net6.0 36/36、net8.0 36/36；TRX 位于 `artifacts/phase-a-20261022T110137Z/L1-after/l1-eventbus-net{6,8}-results/`。 |
| Admin L1 正确性 | `AdminCapTraceInvocationTest` + `AdminCapRegistrationTest`：38 Passed、0 Failed、1 NotExecuted（默认门控的 probe）；TRX：`L1-final/admin-net6-results/L1-admin-net6-final.trx`。 |
| Uow 文档示例 | net6.0、net8.0 各 3/3；原始 TRX：`L1-after/l1-uow-doc-net{6,8}-results/`。README 的提交示例与 scope 示例来自编译通过的测试源码。 |
| 既有 AOP/Uow 兼容 | `UnitOfWorkAdapterTest` net6.0、net8.0 各 49/49；原始 TRX：`L1-after/l1-aop-uow-net{6,8}-results/`。 |
| 受影响 L2 | EventBus net6/net8 各 409 Passed；Admin net6 为 152 Passed、37 NotExecuted、0 Failed；Uow net6/net8 各 35 Passed。TRX 分别在 `L2/eventbus-net{6,8}-results/`、`L2/admin-net6-results/`、`L2/uow-net{6,8}-results/`。NotExecuted 按 TRX 结果节点计数，未当成通过。 |
| Trace probe | 默认环境下 Attribute 与 Admin probe 均为 TRX `NotExecuted`；显式 `RUN_CAP_TRACE_PROBE=1` 后 Attribute net6/net8 与 Admin net6 各 1/1。原始测量位于 `probe/`。每场景 100 warmup、5 批 × 1000 次、并发 1、无数据库；完整运行时/逐批 ticks/当前线程分配见 TRX `StdOut`。没有 before 可比 harness，`BEFORE_STATUS=NOT_APPLICABLE`；这些是有界观察，不代表性能提升或预算通过。 |
| 格式/API | 受影响文件 `git diff --check` 无输出；本阶段未改 SDK、TFM、依赖版本或 API baseline。 |

README 在 L4 后仅做了上表两项文案校正；只复查 UTF-8 内容与该文件 `git diff --check`，没有因此改动生产/测试代码或重跑 L4。`candidate-manifest.json` 记录了该后续文档修正及当前哈希；L4 的二进制和 TRX 仍对应代码候选。

## 候选与遗留

L4 使用任务内隔离 runner，外部 `RUN_*` 门控关闭，保留本地 SQLite 默认路径；构建 `dotnet build Bing.All.slnx -c Release --no-restore --nologo -m:1` 退出码 0。随后一次 `dotnet test Bing.All.slnx -c Release --no-build --no-restore` 退出码 1。103 份 TRX 共 13,735 个结果：12,625 Passed、2 Failed、1,108 NotExecuted；按原始 `UnitTestResult` 统计的项目/TFM 明细在 `artifacts/phase-a-20261022T110137Z/L4/test-counts.md` 和 `test-results-summary.json`。

两项失败都是 `Bing.Tests.Modularity.ModuleLifecycleTest.RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive`，分别在 Bing.Core.Tests net6.0 与 net8.0；断言 `references.All(r => !r.IsAlive)` 为 false（源码断言位于 `framework/tests/Bing.Core.Tests/Modularity/ModuleLifecycleTest.cs:375`）。开始快照表明 `BingModuleManager.cs` 当时已 dirty，且本阶段没有修改它；这只能说明文件起始状态，不能单独证明失败成因。当前全量失败留待 Sol 独立审查调用关系和原始输出，不擅自归类为无关或外部阻塞，也不重复运行全量。

主线程随后对该方法做了一次隔离诊断，没有改源码：net6.0 与 net8.0 各 1/1 Passed，日志和 TRX 在 `artifacts/core-gc-diagnostic/`。隔离通过不替代 L4 的两项全量失败，也不足以确定其原因；`CURRENT_FINAL_FULL_SUITE_STATUS=FAIL` 与 `FAILURE_CAUSALITY=NOT_DETERMINED` 保持不变。

父任务最近基线独立为 12,561 Passed / 0 Failed / 1,105 NotExecuted（103 TRX）；本阶段最终候选为上述 12,625 / 2 / 1,108。两者是不同时间、不同工作区候选，不能拼接成通过结果。

`python eng/ci/check-docs.py --quiet` 的有效运行退出码为 1：仅报告 `docs/sql/migration-guide.md` 悬空符号 `UseSqlite`，原始输出为 `artifacts/phase-a-20261022T110137Z/docs-check-runtime-run.stdout.txt`。该文件在任务开始快照中已 dirty，本阶段未改；此事实不判定回归因果。故仓库级文档 checker 为 FAIL，不能报告 PASS。README 示例编译、阶段矩阵/导航人工核对分别有自己的直接证据；UseSqlite 留作后续 G12/阶段 B 文档工作。

候选身份、退出码、TRX 清单与相关 SHA256 收于 `artifacts/phase-a-20261022T110137Z/L4/`；真实 before 源码、工作区快照和红回归均保存在同一 run 下。现停止实施等待主线程安排 A06 独立 Review，不自动进入阶段 B。

## Review 修复记录

### Round 1

- Review 状态：NEEDS_FIX；Fix Scope：recommended；处理 FIX-001、FIX-002 两项 SHOULD_FIX。`review.md` 保留给独立 Reviewer，本报告不替其关闭 Finding。

#### FIX-001

- 执行状态：COMPLETED。
- 修改：以真实 AspectCore 接口代理覆盖同步取消、Task/Task<T>/ValueTask/ValueTask<T> 预取消与挂起后取消；验证调用一次、传播同一 OCE/token、恢复父 Trace 引用及字段、真实外层成功数为 0、真实 Uow Commit 与 CommitAfter 均为 0。成功对照验证真实外层拦截器进入并成功、Commit=1、CommitAfter=1。直接 `CapTraceAttribute.Invoke` 测试断言返回管道 Task 的 `IsFaulted`。
- 首次组合红结果由测试观察器的 async/await 重包改变 Task 状态导致；已改为原样返回 `nextTask` 后，聚焦代理组合 net6/net8 均 10/10 通过。该红不构成生产行为失败证据，本轮未改生产代码。
- 测试专用项目引用：AOP.Tests 增加对仓库现有 Bing.Events 项目的 ProjectReference，以复用真实 `CapTraceAttribute`；没有新增 NuGet 或生产依赖。

#### FIX-002

- 执行状态：COMPLETED。
- 修改：Uow README 多数据源示例改为链接实际 `SqlUnitOfWorkApplicationService.ExecuteAsync` 的 `MultiDatabaseCommit` / `CapMultiSourceCommit` 分支，并指向 `Startup.ConfigureServices` 的双数据源与 `AddUnitOfWorkScopes()` 装配。移除缺少编译对应物的内联示例，保留本地事务与非原子提交限制说明。
- 验证：对应 Sample Release/net6 build 退出码 0，0 错误、3 个框架生命周期警告。文档链接与源码入口人工核对通过。

### Round 1 汇总

- SHOULD_FIX：2；已完成：2；PARTIAL：0；BLOCKED：0。
- 相关验证：AOP.Tests net6/net8 全量各 148/148；EventBus.Tests net6/net8 全量各 409 Passed / 0 Failed / 1 NotExecuted；CapTrace 直接测试 net6/net8 各 24/24。原始命令、退出码及 TRX 位于 `artifacts/review-fix-round-1/`。
- 本轮未运行全仓 L4、GC 诊断或 probe。阶段 A 候选既有 L4 仍为 12,625 Passed / 2 Failed / 1,108 NotExecuted，`CURRENT_FINAL_FULL_SUITE_STATUS=FAIL`、失败关联性 `NOT_DETERMINED`；隔离 GC 通过不覆盖该结果。仓库 docs checker 的 `UseSqlite` 悬空引用仍为 FAIL，列为后续文档工作。本轮未改 `migration-guide.md`，不据此推断失败因果。
- 未暂存、提交或推送。下一步：交独立 Reviewer 复审 FIX-001、FIX-002，并独立判断阶段 A L4 的两项失败。
