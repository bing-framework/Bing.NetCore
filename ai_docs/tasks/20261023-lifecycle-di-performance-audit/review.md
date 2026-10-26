<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: 20261023-lifecycle-di-performance-audit
AI_REVIEWED_AT: 2026-10-23T23:48:44+08:00

# 独立 Review 3：Fix Round 2 验收

**PASS。FIX-006 已闭环，FIX-001—005 保持 CLOSED；6/6 已关闭，OPEN_ACTIONABLE=0。没有新的可执行缺陷，停止 Review/Fix 循环。**

本轮对照 Round 2 工作区起点、真实源码/测试增量、execution.md、traceability.md、六次原始运行和主线程 Round 1 核验记录独立验收，不以执行者自评代替结论。已读取 AGENTS.md、GOAL_RULES.md、review-code、plan.md 和 fix-plan.md。生产、测试、execution、traceability 均未修改；仅维护当前 review.md 与 artifacts/review-3/。

原 Review 2 在 [review.round2.snapshot.md](artifacts/fix-round-2/worktree-start/review.round2.snapshot.md) 保留，写报告前确认与原当前报告逐字节一致，未覆盖历史。Round 1 原审查快照也保持不变。用户修复授权及 fix-plan 覆盖原只读审查阶段限制，不存在待确认的实施授权问题。

```text
IMPLEMENTATION_STATUS: PASS
TEST_STATUS: PASS_AFFECTED_SCOPE_WITH_ONE_EXPLICIT_SKIP
RESOURCE_EVIDENCE_STATUS: PASS_ORIGINAL_FIXES_REUSED_UNCHANGED_SCOPE
PERFORMANCE_EVIDENCE_STATUS: OBSERVED_ROUND_1_ONLY_NO_BUDGET_CLAIM
EXTERNAL_GATE_STATUS: BLOCKED_EXTERNAL
LOCAL_ACCEPTANCE_STATUS: PASS
RELEASE_STATUS: NOT_ASSESSED_FOR_EXTERNAL_ENVIRONMENTS
OPEN_ACTIONABLE: 0
CLOSED_FINDINGS: 6
GOAL_STATUS: STOPPED_BLOCKED
```

STOPPED_BLOCKED 仅表示外部环境/发布证据仍未验证；本地修复验收已完成，Review 为 PASS，不允许因此继续自动修复。

## 本轮增量与候选身份

以 `artifacts/fix-round-2/worktree-start/` 的真实工作区快照为 before，而非 HEAD。Round 2 的12个生产/测试候选文件与当前内容全部匹配；与 Round 1 身份比较，只有以下两项发生变化，其余10项未变：

- [EventBusBase.cs:208](../../../framework/src/Bing.EventBus/Bing/EventBus/EventBusBase.cs#L208)：记录当前处理器主异常；旧同步工厂清理失败时立即抛出；双失败时抛包含原始两异常的 AggregateException。
- [EventHandlerScopeLifecycleTest.cs:286](../../../framework/tests/Bing.EventBus.Tests/EventHandlerScopeLifecycleTest.cs#L286)：新增两个真实 LocalEventBus 公共分发回归和业务失败处理器，未删除或放宽原断言。

本轮增量未修改公开接口、获取/释放包装器、IoC scope 创建、Uow/Core、依赖、SDK、TFM 或 API baseline。两个起点快照也匹配 Round 1 的文件身份。独立比对结果见 [evidence-check.json](artifacts/review-3/evidence-check.json)；实际差异见 [生产差异](artifacts/review-3/EventBusBase.cs.diff)、[测试差异](artifacts/review-3/EventHandlerScopeLifecycleTest.cs.diff)。

## Finding 验收

| Finding | 当前状态 | 结论依据 |
| --- | --- | --- |
| FIX-001 | CLOSED | Uow 未发布 root 的失败取消资源清理、外层 Current 和正常释放保持 Review 2 已验收实现；相关文件身份未变，无新直接反证。 |
| FIX-002 | CLOSED | 事件 scope 交接前解析/转换失败清理保持；IoC 工厂和包装器未变，现有对应回归在本轮13项及完整套件继续通过。 |
| FIX-003 | CLOSED | 异步获取/释放及旧同步成功路径保持；仅同步、仅异步、双接口、业务与清理异常回归继续通过。本轮没有同步阻塞异步清理或新增契约。 |
| FIX-004 | CLOSED | NotifyAsync 异步 scope 和解析主异常保留实现未变；复用 Core 证据，不重新打开。 |
| FIX-005 | CLOSED | 已附加/已释放 runtime 构建前拒绝及首次失败清理实现未变；复用 Core/模块证据，旧 GC 关闭结论保持关闭。 |
| FIX-006 | CLOSED | 旧同步清理失败立即中止后续分发；同一处理器业务与清理双失败同时保留。两 TFM 的正式失败与成功回归、当前源码控制流相互印证。 |

### FIX-006 关闭证据

[EventBusBase.cs:253](../../../framework/src/Bing.EventBus/Bing/EventBus/EventBusBase.cs#L253) 的分支仅在 `asyncFactory == null` 时立即退出。没有当前处理器主异常时使用 `throw;` 传播原清理异常；已有主异常时通过 `new AggregateException(handlerError, cleanupError)` 保存两者。handlerError 保存反射包装的 InnerException 或 await 产生的原异常，而非用清理异常替换它。异常越过分发 foreach，因此后续订阅器不会运行。

异步工厂分支继续将清理异常加入原列表，由原上层策略汇总；异步获取、await DisposeAsync、同步释放回退、正常成功、业务失败且清理成功的分发行为均未改。未引入第二次释放。此前其他处理器的异常收集和旧工厂创建失败行为没有在本轮扩展契约。

两个新增测试均通过真实 `LocalEventBus.PublishAsync`，不是直接调用 private helper：

| 测试 | 实际断言 |
| --- | --- |
| `PublishAsync_WhenLegacyCleanupFails_ShouldAbortDispatchAndPreserveCleanupError` | 原 InvalidOperationException 消息保留、第一处理器执行1次、后续执行0次、清理1次 |
| `PublishAsync_WhenLegacyHandlerAndCleanupFail_ShouldPreserveBothAndAbortDispatch` | AggregateException 内按业务/清理顺序保留两个异常消息、第一执行1次、后续执行0次、清理1次 |

两 TFM 修复前都恰好在 `second.Handled` 应为0、实际为1的断言失败；修复后这两个测试都通过。双失败测试覆盖 await 返回的失败 Task；同步反射抛错由同一 handlerError/InnerException 分支处理，未宣称所有异常排列都新增了独立测试。源码及现有证据足以关闭既定验收项，无须再运行探针。

## Round 2 原始运行核验

直接解析各运行目录的 command.json、result.json、TRX 单项结果，并读取 stdout/stderr；原始二进制输出也存在。luna_worker 只做机械交叉核对，不参与最终实现判断。独立汇总见 [evidence-check.json](artifacts/review-3/evidence-check.json)，没有重新执行这些测试。

| 运行目录（artifacts/fix-round-2/runs/） | 单项结果：通过/失败/未执行 | exit | 超时 | 预设上限 / 实际秒数 |
| --- | --- | --- | --- | --- |
| [eventbus-net8-fail-round2](artifacts/fix-round-2/runs/eventbus-net8-fail-round2/) | 11 / 2 / 0 | 1 | false | 900 / 7.563 |
| [eventbus-net6-fail-round2](artifacts/fix-round-2/runs/eventbus-net6-fail-round2/) | 11 / 2 / 0 | 1 | false | 900 / 7.000 |
| [eventbus-net8.0-pass-round2](artifacts/fix-round-2/runs/eventbus-net8.0-pass-round2/) | 13 / 0 / 0 | 0 | false | 900 / 4.047 |
| [eventbus-net6.0-pass-round2](artifacts/fix-round-2/runs/eventbus-net6.0-pass-round2/) | 13 / 0 / 0 | 0 | false | 900 / 4.000 |
| [eventbus-full-net8.0-round2](artifacts/fix-round-2/runs/eventbus-full-net8.0-round2/) | 422 / 0 / 1 | 0 | false | 1800 / 35.610 |
| [eventbus-full-net6.0-round2](artifacts/fix-round-2/runs/eventbus-full-net6.0-round2/) | 422 / 0 / 1 | 0 | false | 1800 / 35.422 |

command.json 的 argv 明确是 EventBus.Tests、Release、对应 net6.0/net8.0；精准运行筛选 EventHandlerScopeLifecycleTest，完整运行无筛选。result.json 保存实际进程退出码、结束时刻与耗时，均在预设时限内。它们与 TRX 和 stdout 的结果一致，不是从会话声明推断 exit 0。

完整套件唯一未执行项为 `Bing.EventBus.Tests.Cap.CapTraceProbeTest.TraceProbe_ShouldObserveBoundedSynchronousScenarios`，门控为 RUN_CAP_TRACE_PROBE=1，不计作通过。TRX 的 ResultSummary/Counters 写 notExecuted=0，但其 UnitTestResult 明确为 NotExecuted，且 total423-executed422=1、stdout显示跳过1项；本轮按真实单项节点统计，与 summary.json/报告一致。traceability 顶部 Round 1 标签仍属历史元信息，正文已有 Round 2 映射，不影响证据定位和验收，不单列可执行缺陷。

## Round 1 整合及复用边界

已纳入 [main-thread-fix-round-1-verification.json](artifacts/main-thread-fix-round-1-verification.json)。这是 **Round 1 候选** 的机械核验，不能当成 Round 2 全方案 fresh 结果：

| 范围 | TRX数 | 通过 | 失败 | 未执行 |
| --- | --- | --- | --- | --- |
| net6.0 | 53 | 6745 | 0 | 593 |
| net8.0 | 50 | 6195 | 0 | 521 |
| 合计 | 103 | 12940 | 0 | 1114 |

其中6份空 TRX 不计作测试通过；主线程记录没有重复 run ID / execution ID，Round 1 的12个候选文件比对均匹配。原始 Round 1 build/test/probe 没有独立进程退出文件，exit 0 仅为执行会话、commands.md、execution.md 的声明；TRX 单项节点独立支持上述测试结果。没有补造 exit 证据，也没有将 Round 2 捕获到的退出码倒推给 Round 1。

Uow/Core/SQLite/GC 相关源范围本轮未变，复用 Review 2 已验收及 Round 1 原始证据：Core各653、Uow各74、AOP各178、SQLite Uow/Factory各79、EF SQLite net6为131通过。EventBus 使用本轮各422通过的局部完整套件，不把两轮总数相加，也不声称当前候选重新跑了12940项。

性能描述已与真实代码边界对齐：Round 1 probe 是同 scope 内直接解析 DI Transient，不是 SQL Query 工厂 benchmark；60秒检查是迭代间协作检查，没有 watchdog。既有3000次、142.2801ms、scope/service计数平衡、Timer增量与Current泄漏观测为0，仅支持那次 current-only 运行。本轮未测分发异常分支的性能，没有预算、before/after或全业务吞吐达标结论。没有因缺预算而新建代码 finding。

外部 MySQL/RabbitMQ/CAP 仍未执行，本地 SQLite 不替代其证据。历史 GC 结论不重新打开；长寿命 DI scope 保留可释放 Transient、纯同步入口不承诺释放仅异步依赖等既有边界保持记录，不触发额外修复。

## 收口

本轮 CIA：生产变化仅 EventBusBase 的失败分发控制流，对应测试增加两个回归；无公共契约/Provider/TFM/构建图/性能 harness 变化。现有 L1/L2 证据足够且未发现新疑点，因此未启动测试、probe、全量、GC、压力或外部数据库。已完成源码/快照比对、候选身份、原始证据、UTF-8/本地链接/机器头检查。

```text
Task: 20261023-lifecycle-di-performance-audit
Round: Review 3 (after Fix Round 2)
Implementation: 6/6 (100%)
Release Gates: 4/4 Round 2 local validation runs passed; external gates not evaluated
Changed Scope: review.md and artifacts/review-3 only
Validation Run:
  L0: source/snapshot diff, scoped diff check, identity and report checks
  L1: reused two-TFM 13/13 targeted results; no new run
  L2: reused two-TFM EventBus 422 passed / 1 skipped; no new run
  L3: reused unchanged AOP/SQLite evidence
  L4: incorporated Round 1 main-thread verification; no rerun
  L5: no run; historical bounded observation only
Reused Evidence: Round 1 unchanged Uow/Core/SQLite/GC, Round 2 explicit six runs
Completed: FIX-001, FIX-002, FIX-003, FIX-004, FIX-005, FIX-006
Open Actionable: none (0)
Blocked Approval: no approval needed for local acceptance; performance budget not established
Blocked External: external DB/CAP and all-environment release evidence
Not Applicable: repeated full/GC/stress/baseline runs for this local fix
Accepted Limitations: synchronous cleanup cannot promise async-only dependency support
Verified Boundaries: legacy cleanup abort and double-failure retention in net6/net8
Deferred: historical module-graph scalability suggestions outside this task
No-Progress Check: CHANGED (FIX-006 implementation and red/green evidence)
Next Action: STOP
Goal Status: STOPPED_BLOCKED (external release evidence only; local review complete)
```
