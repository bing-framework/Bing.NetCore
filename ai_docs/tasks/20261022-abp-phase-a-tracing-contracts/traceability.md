# 阶段 A 符号到验证映射

以下映射只覆盖本阶段最终修改的生产符号与文档示例；G02–G11 其他能力按父路线图历史证据引用，不因本阶段矩阵而宣称已完成。原始命令、退出码和 TRX 在 `artifacts/phase-a-20261022T110137Z/`。

## `Bing.Events.Cap.CapTraceAttribute`

生产入口 `Invoke(AspectContext, AspectDelegate)` 将调用交给 `InvokeCoreAsync`；测试代理在接口方法和接口参数上真实声明 `[CapTrace]` / `[FromCap]`，并检查目标调用计数、代理元数据及 Trace 上下文。实现专属属性路径不是本阶段新增能力。完整直接回归方法：

```text
Bing.EventBus.Tests.Cap.CapTraceAttributeTest.Invoke_WithoutFromCap_ShouldCallTargetOnce
Bing.EventBus.Tests.Cap.CapTraceAttributeTest.Invoke_WithNullFromCap_ShouldCallTargetOnce
Bing.EventBus.Tests.Cap.CapTraceAttributeTest.Invoke_WithNonHeaderFromCap_ShouldCallTargetOnce
Bing.EventBus.Tests.Cap.CapTraceAttributeTest.Invoke_WithNonCapHeaderFromCap_ShouldCallNextOnce
Bing.EventBus.Tests.Cap.CapTraceAttributeTest.Invoke_WhenCanceled_ShouldFaultReturnedPipelineTask
Bing.EventBus.Tests.Cap.CapTraceAttributeTest.Invoke_WithFromCapCancellationToken_ShouldCallTargetOnce
Bing.EventBus.Tests.Cap.CapTraceAttributeTest.Invoke_WithTraceId_ShouldUseIndependentContextAndRestoreParent
Bing.EventBus.Tests.Cap.CapTraceAttributeTest.Invoke_WithoutNonEmptyTraceId_ShouldKeepParent
Bing.EventBus.Tests.Cap.CapTraceAttributeTest.Invoke_WithWhitespaceOrSameTraceId_ShouldUseIndependentContext
Bing.EventBus.Tests.Cap.CapTraceAttributeTest.Invoke_WithAllReturnKinds_ShouldPreserveResultsAndCallOnce
Bing.EventBus.Tests.Cap.CapTraceAttributeTest.Invoke_WithAsyncTarget_ShouldKeepTraceUntilCompletion
Bing.EventBus.Tests.Cap.CapTraceAttributeTest.Invoke_WhenTargetThrows_ShouldRestoreParentAndPreserveException
Bing.EventBus.Tests.Cap.CapTraceAttributeTest.Invoke_WhenTargetCancels_ShouldRestoreParentAndPreserveToken
Bing.EventBus.Tests.Cap.CapTraceAttributeTest.Invoke_WhenSuspendedTargetCancels_ShouldRestoreParentAndPreserveToken
Bing.EventBus.Tests.Cap.CapTraceAttributeTest.Invoke_WhenNested_ShouldRestoreEachParent
Bing.EventBus.Tests.Cap.CapTraceAttributeTest.Invoke_WhenNestedTargetThrows_ShouldRestoreOuterAndRootContexts
Bing.EventBus.Tests.Cap.CapTraceAttributeTest.Invoke_WhenOverlapped_ShouldIsolateContexts
```

原阶段 L1 的 `l1-eventbus-net6-results/L1-eventbus-net6.trx` 与 net8 对应 TRX 各 36/36 Passed；本轮直接管道断言的 `CapTraceAttributeTest` net6/net8 各 24/24 Passed，完整 EventBus L2 net6/net8 各 409 Passed、0 Failed、1 NotExecuted。完整 TRX 位于 `artifacts/review-fix-round-1/L1-L2/eventbus-full-net{6,8}/`；NotExecuted 是默认门控 probe。

真实 AspectCore 代理与 Uow 组合由 `Bing.Aop.AspectCore.Tests.CapTraceUnitOfWorkCancellationTest` 覆盖。`Proxy_SynchronousCancellation_ShouldPreserveExceptionAndSkipSuccessPath` 验证同步抛出的原异常；`Proxy_PreCanceledAsyncReturn_ShouldPreserveExceptionAndSkipSuccessPath` 与 `Proxy_SuspendedAsyncReturn_ShouldPreserveExceptionAndSkipSuccessPath` 各覆盖 Task、Task<T>、ValueTask、ValueTask<T>。这些方法断言同一取消异常/token、目标调用一次、代理内新 Trace 上下文及父引用/字段恢复、外层成功路径为 0、真实 `IUnitOfWorkManager.CommitAsync` 与 `ICommitAfter` 均为 0；ValueTask 源的 `GetResult` 次数为 1。`Proxy_Success_ShouldRunOuterPathAndCommitWorkUnit` 证明测试主机中的外层观察器和 `EnableBingUnitOfWork` 确实参与：外层进入/成功、Commit、CommitAfter 各为 1。net6/net8 聚焦各 10/10 Passed，完整 AOP L2 各 148/148 Passed；证据在 `artifacts/review-fix-round-1/L1-L2/aop-focused-net8/` 与 `aop-full-net{6,8}/`，net6 聚焦最终证据在 `artifacts/review-fix-round-1/L1/aop-net6-focused-after-observer-fix/`。

真实红证据：`before/L1/eventbus-direct-red/` 记录原始 Attribute 直接调用遗漏 `next` 的次数断言；阶段 A 的有效代理预取消失败见 `L1-after/eventbus-net6-matrix/`。本轮首个组合运行曾有 3 个预取消/sync 用例失败，但原始外层测试观察器用 async/await 重包 Faulted Task，将 OCE 状态转成 Canceled；`aop-net6-focused-attempt-2/` 保存该证据，`aop-net6-diagnostic/` 记录 target=1、outerEntered=1、outerSucceeded=0、commits=0 及 sync 的 NRE。将观察器改为原样返回 `nextTask` 并用独立成功 continuation 观察后，`aop-net6-focused-after-observer-fix/` 中 10/10 通过；首轮三项分类为夹具干扰，不作为生产回归。先前一份接口没有声明特性的代理红夹具也无效，已排除；null/non-Header 代理行为以当前真实代理绿和原始直接 `Invoke` 证据描述，不推断所有 CAP 代理一定吞掉目标。本轮没有生产行为失败证据，未修改生产代码。

## `Bing.Admin.Modules.CapModule.CapSubscribeInvoker`

`GetInstance(IServiceProvider, ConsumerContext)` 从消息头初始化独立上下文，`InitTraceIdContext(CapHeader)` 不改写父对象、不在解析后立刻恢复。下面测试通过 CAP 8.0.1 原生 `ISubscribeInvoker.InvokeAsync` 调用边界执行真实同步/异步目标，并观察调用者恢复：

```text
Bing.Admin.EFCore.Tests.AdminCapTraceInvocationTest.InvokeAsync_WithTrace_ShouldKeepTargetTraceAndParent
Bing.Admin.EFCore.Tests.AdminCapTraceInvocationTest.InvokeAsync_WithoutNonEmptyTraceHeader_ShouldKeepParent
Bing.Admin.EFCore.Tests.AdminCapTraceInvocationTest.InvokeAsync_WithoutTraceHeaderAndParent_ShouldKeepNull
Bing.Admin.EFCore.Tests.AdminCapTraceInvocationTest.InvokeAsync_WithNonEmptyTraceHeader_ShouldInstallIndependentContext
Bing.Admin.EFCore.Tests.AdminCapTraceInvocationTest.InvokeAsync_WhileTargetIsSuspended_ShouldKeepTraceUntilCompletion
Bing.Admin.EFCore.Tests.AdminCapTraceInvocationTest.InvokeAsync_WhenTargetThrows_ShouldPreserveExceptionAndRestoreParent
Bing.Admin.EFCore.Tests.AdminCapTraceInvocationTest.InvokeAsync_WhenTargetCancels_ShouldPreserveCancellationAndRestoreParent
Bing.Admin.EFCore.Tests.AdminCapTraceInvocationTest.InvokeAsync_WhenCallsAreSequential_ShouldRestoreParentBetweenMessages
Bing.Admin.EFCore.Tests.AdminCapTraceInvocationTest.InvokeAsync_WhenCallsOverlap_ShouldIsolateAndRestoreEachContext
```

实际调用器父对象污染红证据在 `before/L1/admin-net6-red-confirm2/`；L1 最终组为 38 Passed、0 Failed、1 NotExecuted，其中 Admin Trace 类 12 Passed，NotExecuted 是默认跳过的 probe。L2 Admin net6 为 152 Passed、0 Failed、37 NotExecuted；L4 Admin Trace 类为 12 Passed、0 Failed、1 NotExecuted（受门控 probe）。这些路径只覆盖 Admin 已注册调用器，不代表 CAP/FreeSQL 所有消费者自动追踪。

## Uow README 编译契约

README 旧示例对应 `IUnitOfWorkManager.CommitAsync(CancellationToken)`；新示例对应 `IUnitOfWorkScopeManager.Begin()` 与 scope 的 `CompleteAsync(CancellationToken)`。完整可编译方法：

```text
Bing.Uow.Tests.UnitOfWorkDocumentationExamplesTest.LegacyCommitExample_ShouldCommitRegisteredWorkOnce
Bing.Uow.Tests.UnitOfWorkDocumentationExamplesTest.ScopeExample_ShouldCompleteAndRestore
Bing.Uow.Tests.UnitOfWorkDocumentationExamplesTest.ScopeExample_WhenFailed_ShouldNotComplete
```

net6/net8 各 3/3 Passed，L4 两个 TFM 合计 6/6 Passed。README 的多数据源主例现映射到已编译 Sample：`samples/Bing.Samples.SqlUnitOfWork/Application/SqlUnitOfWorkApplicationService.cs` 中 `SqlUnitOfWorkApplicationService.ExecuteAsync` 的 `MultiDatabaseCommit` / `CapMultiSourceCommit` 分支；装配入口是 `samples/Bing.Samples.SqlUnitOfWork/Startup.cs` 的 `ConfigureServices`，注册两数据源及 `AddUnitOfWorkScopes()`。本轮 `dotnet build samples/Bing.Samples.SqlUnitOfWork/Bing.Samples.SqlUnitOfWork.csproj -c Release --verbosity minimal` 退出 0，3 个 net6 生命周期警告、0 错误；证据 `artifacts/review-fix-round-1/L1-L2/sample-sqluow-build-net6-valid/`。Uow 文档三个示例方法仍在 net6/net8 各 3/3 Passed（原 L1 结果复用）。本轮真实代理组合回归使用实际 `EnableBingUnitOfWork` 和 `IUnitOfWorkManager`，成功对照验证 Commit/CommitAfter 可执行，取消断言 Commit=0、CommitAfter=0；Uow 生产代码未修改。

## 有界 Probe 与候选结果

```text
Bing.EventBus.Tests.Cap.CapTraceProbeTest.TraceProbe_ShouldObserveBoundedSynchronousScenarios
Bing.Admin.EFCore.Tests.AdminCapTraceInvocationTest.TraceProbe_ShouldObserveBoundedSynchronousConsumerScenarios
```

默认运行时上述两个 Fact 的 TRX outcome 是 `NotExecuted`；显式设置 `RUN_CAP_TRACE_PROBE=1` 后，Attribute net6、net8 与真实 Admin CAP 调用器 net6 各为 1/1 Passed。入口/环境/原始逐批值位于 `probe/enabled-net6-results/probe-enabled-net6.trx`、`probe/enabled-net8-results/probe-enabled-net8.trx`、`probe/admin-enabled-net6-results/admin-probe-enabled-net6.trx`。各场景 100 次 warmup、5 批 × 1,000 次、最大并发 1、无数据库。没有可比 before、没有批准的回归预算；测量仅为 `OBSERVED`，不宣称提升或门槛通过。

L4 Release 构建退出 0，测试退出 1。103 个 TRX 共 12,625 Passed / 2 Failed / 1,108 NotExecuted；失败测试在 Bing.Core.Tests net6 与 net8 各出现一次：`Bing.Tests.Modularity.ModuleLifecycleTest.RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive`。原始逐项目/TFM 计数和两条失败信息见 `L4/test-counts.md`、`L4/test-results-summary.json`；候选输入/程序集散列见 `L4/candidate-manifest.json`。失败关联性待 Sol 独立审查。

主线程对失败方法所做的单方法隔离诊断 net6/net8 各 1/1 Passed，证据为 `artifacts/core-gc-diagnostic/core-gc-net6.trx`、`core-gc-net8.trx` 与对应日志。该结果不能覆盖或替代 L4 全量失败，失败因果仍未确定。

## Review Fix Round 1

### FIX-001：取消管道与真实代理组合

| 行为 / 最终符号 | 测试方法 | 证据 |
| --- | --- | --- |
| `CapTraceAttribute.Invoke` 返回的管道 Task 保持 Faulted，恢复父上下文并传播原 OCE | `Bing.EventBus.Tests.Cap.CapTraceAttributeTest.Invoke_WhenCanceled_ShouldFaultReturnedPipelineTask` | `artifacts/review-fix-round-1/L1-L2/eventbus-captrace-net{6,8}/` |
| 真实代理同步取消不进入成功路径或提交 Uow | `CapTraceUnitOfWorkCancellationTest.Proxy_SynchronousCancellation_ShouldPreserveExceptionAndSkipSuccessPath` | `artifacts/review-fix-round-1/L1/aop-net6-focused-after-observer-fix/`、`L1-L2/aop-focused-net8/` |
| Task / Task<T> / ValueTask / ValueTask<T> 预取消及挂起后取消均保持异常、Token、单次消费和父 Trace 恢复；外层成功、Commit、CommitAfter 均为 0 | `Proxy_PreCanceledAsyncReturn_ShouldPreserveExceptionAndSkipSuccessPath`、`Proxy_SuspendedAsyncReturn_ShouldPreserveExceptionAndSkipSuccessPath` | 同上；各理论覆盖四种返回类型，聚焦每个 TFM 10/10 Passed |
| 真实 CapTrace 与 Uow 成功组合确实执行外层成功及提交 | `Proxy_Success_ShouldRunOuterPathAndCommitWorkUnit` | 同上；外层进入/成功各 1，Commit=1、CommitAfter=1 |

首次组合红 TRX 位于 `artifacts/review-fix-round-1/L1/aop-net6-focused-attempt-2/`；原因是测试观察器以 async/await 重包 next Task，改变了 OCE 对应 Task 的状态。修正后的观察器原样返回 next Task 并独立记录完成信号。该红分类为夹具干扰，不代表生产缺陷；最终有效矩阵为绿，未据此修改生产代码。AOP.Tests 的测试专用 Bing.Events ProjectReference 位于 `framework/tests/Bing.Aop.AspectCore.Tests/Bing.Aop.AspectCore.Tests.csproj`，不改变生产依赖图或版本。

### FIX-002：Uow README 多数据源示例

| 文档入口 | 对应真实实现 | 验证 |
| --- | --- | --- |
| `framework/src/Bing.Uow/README.md` 多数据源例 | `SqlUnitOfWorkApplicationService.ExecuteAsync` 的 `MultiDatabaseCommit`、`CapMultiSourceCommit` 分支，以及 `Startup.ConfigureServices` 的双数据源注册和 `AddUnitOfWorkScopes()` | `dotnet build samples/Bing.Samples.SqlUnitOfWork/Bing.Samples.SqlUnitOfWork.csproj -c Release --verbosity minimal` 退出码 0、0 错误、3 warnings；原始输出见 `artifacts/review-fix-round-1/L1-L2/sample-sqluow-build-net6-valid/` |

README 指向 Sample 实际源码；本轮不再引用不可编译的重构示例。AOP.Tests 完整 net6/net8 各 148/148 Passed，EventBus.Tests 完整 net6/net8 各 409 Passed / 0 Failed / 1 NotExecuted；NotExecuted 为默认门控 probe，不计作通过。阶段 A 原有 L4 仍为 12,625 Passed / 2 Failed / 1,108 NotExecuted，失败关联性 `NOT_DETERMINED`；本轮未重跑 L4 或 GC/probe。仓库级 docs checker 的 `UseSqlite` 悬空引用另列 deferred，未改其源文件。
