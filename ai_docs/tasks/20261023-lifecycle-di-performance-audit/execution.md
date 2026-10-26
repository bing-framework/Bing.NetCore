<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: 20261023-lifecycle-di-performance-audit
AI_EXECUTION_FINISHED_AT: 2026-10-23T15:38:55Z

# 实施执行报告

任务：20261023-lifecycle-di-performance-audit；模式：REVIEW_FIX；Review Round：2；Fix Scope：recommended。

用户授权“修复一下这些问题”，Round 1 按 `fix-plan.md` 实施 FIX-001—005；Round 2 按独立 Review 2 仅处理唯一新增 OPEN_ACTIONABLE FIX-006。`review.md` 始终保留为 Reviewer 独立证据，不自行把审查状态改为通过。本文件保留 Round 1 完整记录，并在后部追加 Round 2 修复记录。

## 候选与范围

- 基线 HEAD：`3919c1509907d661cfef3679dad79368c8b9cb39`。
- 未修改 SDK 10.0.400、TFM、依赖版本、API baseline 或性能预算。
- 修改前原始文件、`review.md` 与 `fix-plan.md` 快照保存在 `artifacts/fix-round-1/worktree-start/`；未使用 HEAD 覆盖工作区。
- 本轮候选身份、文件 SHA-256 与工作区状态见 `artifacts/fix-round-1/candidate-identity.json`。
- 既有未提交修改（含 `BingModuleManager.cs`、`ModuleLifecycleTest.cs` 等）原样保留，未回退、未覆盖。

## 生产实现

### FIX-001：Uow 启动失败取消资源清理

修改 `framework/src/Bing.Uow/Bing/Uow/UnitOfWorkScopeManager.cs`、`AssemblyInfo.cs`。

- `BeginRoot` 以 try/catch 包围从取消资源创建、保存处理器解析、root 构造、启动观察器到发布 frame 的全过程。
- root 发布前任何失败路径释放尚未交接的 linked/deadline CTS；linked 先于 deadline 释放。
- 创建 linked/deadline 中途失败时也释放已取得的 deadline。
- 主启动异常保持可见；清理异常作为 `Bing.Uow.RootCancellationCleanupError` 附加到主异常 Data。
- 失败不修改外层 `_current`；不调用要求 root 已入栈的 `Dispose` 兜底。
- `InternalsVisibleTo("Bing.Uow.Tests")` 仅用于直接回归测试。

### FIX-002/003：事件处理器 scope 所有权与异步释放

修改 `framework/src/Bing.EventBus/Bing/EventBus/IocEventHandlerFactory.cs`、`EventHandlerDisposeWrapper.cs`、`EventBusBase.cs`，新增 `IAsyncEventHandlerFactory.cs`。

- 新增可选公共契约 `IAsyncEventHandlerFactory.GetHandlerAsync()`；既有 `IEventHandlerFactory`、`IEventHandlerDisposeWrapper` 成员未变。
- `IocEventHandlerFactory` 实现异步契约：
  - `GetHandlerAsync()` 使用 `CreateAsyncScope()`，异步分发可以 await 作用域释放。
  - `GetHandler()` 保留原同步入口；创建、解析或转换失败时释放尚未交接的 scope。
  - 失败清理异常作为 `Bing.EventBus.HandlerScopeCleanupError` 附加，主构造/转换异常保持可见。
- `EventHandlerDisposeWrapper` 增加可选异步释放委托并实现 `IAsyncDisposable`；未提供异步动作时回退同步释放。
- `EventBusBase.TriggerHandlerAsync` 优先异步获取/异步释放；非异步工厂或包装器继续使用原同步入口。
- 异步工厂路径的执行异常与清理异常同时发生时两者都进入 `exceptions`，由现有聚合逻辑保留；不会用清理异常覆盖主异常。
- 旧同步工厂路径在清理失败时立即终止分发（Round 2 FIX-006）；同一处理器已有业务异常时以 `AggregateException` 同时保留两个异常。
- 同步兼容入口不承诺可以正常释放“仅支持异步释放”的依赖；异步分发应使用新契约。

### FIX-004：ExceptionNotifier 异步 scope

修改 `framework/src/Bing.Core/Bing/ExceptionHandling/ExceptionNotifier.cs`。

- `NotifyAsync` 改用 `CreateAsyncScope()` 并 await `DisposeAsync()`。
- 保留订阅器排序、逐订阅器异常隔离和日志语义。
- 解析/枚举失败时等待已创建依赖清理；清理失败作为 `Bing.ExceptionHandling.ScopeCleanupError` 附加，不覆盖解析主异常。
- 正常流程的释放异常继续传播，调用方可观察。

### FIX-005：重复构建提前拒绝

修改 `framework/src/Bing.Core/Bing/Core/Modularity/BingModuleRegistration.cs`。

- `EnsureBuildable` 在底层 `BuildServiceProvider` 之前拒绝已附加（`_provider != null`）或已释放（`_runtimeReleased`）的 runtime。
- 保留首次构建校验失败时释放未初始化模块的语义。
- 不增加 IServiceCollection 并发构建支持，不忽略空模块，不依赖捕获 NullReferenceException。

## 正式回归（先失败，后修复）

失败证据保存在 `artifacts/fix-round-1/failing-regression/`，未覆盖、未删除。

| 回归范围 | TFM | 修复前结果 | 关键失败证据 |
|---|---|---|---|
| EventBus scope/异步释放 | net8.0 | 7 失败 / 2 通过，exit 1 | `eventbus-net8-fail.trx`；仅异步依赖触发 `Use DisposeAsync`，构造失败 scope 释放为 0，清理异常覆盖主异常 |
| ExceptionNotifier | net8.0 | 4 失败，exit 1 | `core-notifier-net8-fail.trx`；异步依赖未释放 |
| BuildBingServiceProvider 重复构建 | net8.0 | 3 失败 / 5 通过，exit 1 | `core-build-net8-fail.trx`；校验失败提前 Dispose 模块并产生关闭 NullReferenceException |
| Uow BeginRoot 失败资源 | net8.0 | 2 失败 / 1 通过，exit 1 | `uow-net8-fail-v2.trx`；caller 注册残留 1，Timer 增量非 0 |

## 修复后精准回归

保存于 `artifacts/fix-round-1/passing-regression/`。

| 范围 | net8.0 | net6.0 |
|---|---|---|
| EventBus 新回归 + 原工厂测试 | 25 通过，exit 0 | 25 通过，exit 0 |
| EventBus 最终新增回归 | 11 通过，exit 0 | 11 通过，exit 0 |
| Core 通知器 + 构建入口 | 12 通过，exit 0 | 12 通过，exit 0 |
| Uow BeginRoot 失败资源 | 4 通过，exit 0 | 4 通过，exit 0 |

`Uow` 计时器断言使用独立测试集合、比较本次操作前后增量，不依赖进程绝对 `Timer.ActiveCount`；未使用重试放宽。独立性能探针子进程另行使用绝对计数观测本次操作增量。

## 受影响套件与集成回归

| 范围 | 结果 | 证据 |
|---|---|---|
| Core.Tests 全套 net8.0 | 653 通过 / 0 失败 | `full-suite/core-full-net8.trx` |
| Core.Tests 全套 net6.0 | 653 通过 / 0 失败 | `full-suite/core-full-net6.trx` |
| EventBus.Tests 全套 net8.0 | 420 通过 / 1 跳 / 0 失败 | `full-suite-final/bing-eventbus-tests-net8.0.trx` |
| EventBus.Tests 全套 net6.0 | 420 通过 / 1 跳 / 0 失败 | `full-suite-final/bing-eventbus-tests-net6.0.trx` |
| Uow.Tests 全套 net8.0 | 74 通过 / 0 失败 | `full-suite-final/bing-uow-tests-net8.0.trx` |
| Uow.Tests 全套 net6.0 | 74 通过 / 0 失败 | `full-suite-final/bing-uow-tests-net6.0.trx` |
| AOP AspectCore net8.0 | 178 通过 / 0 失败 | `aop-sqlite/aop-net8.trx` |
| AOP AspectCore net6.0 | 178 通过 / 0 失败 | `aop-sqlite/aop-net6.trx` |
| SQLite Uow/Factory 契约 net8.0 | 79 通过 / 0 失败 | `aop-sqlite/sqlite-uow-net8.trx` |
| SQLite Uow/Factory 契约 net6.0 | 79 通过 / 0 失败 | `aop-sqlite/sqlite-uow-net6.trx` |
| EF Core SQLite operation/Uow 契约 net6.0 | 131 通过 / 0 失败 | `aop-sqlite/efcore-sqlite-net6.trx` |

EventBus 的 1 个跳过项为既有的 `CapTraceProbeTest` 受控探针；外部数据库/CAP 门控未开启，未把跳过的外部项计为通过。

## 最终候选

- Release solution build：`dotnet build Bing.All.slnx -c Release --no-restore`，执行会话退出码 0，日志显示 0 error。
- 默认本地 solution test：`dotnet test Bing.All.slnx -c Release --no-build --no-restore --logger trx`，执行会话退出码 0。
- 汇总：103 个 TRX，总 14,054；执行 12,940；通过 12,940；失败 0；未执行 1,114。该汇总已由主线程机械解析见 `artifacts/fix-round-1/evidence-check.json`。
- 证据：`artifacts/fix-round-1/final-candidate-2/`（solution build log、全部 TRX、default test log）。
- 说明：Round 1 原始 build/test 日志本身不嵌入 shell 进程退出码；上述 exit 0 是执行会话在 `commands.md`/本报告中的声明，不是从日志重新读出的字段。`evidence-check.json` 已明确记录该差异。
- 首轮最终候选 `final-candidate/` 保留；由于此后只新增测试，未覆盖旧证据。

## 当前性能/资源探针

- CIA 与固定窗口：`artifacts/fix-round-1/perf-probe/cia.md`。
- 固定参数：net8.0 Release；预热 100 次；3 组 × 1000 次；串行；无外部服务。60 秒检查位于每组迭代之间，是协作式截止，不是可中断挂起操作的外部 watchdog。
- 工作负载边界：所谓“同 scope 循环”是在同一真实 Microsoft DI scope 内直接解析 10 次 Transient 服务；它不是 SQL Query 工厂 benchmark，也不代表数据库业务路径。
- 结果：`deadlineExceeded=false`，3 组共 3000 次，总耗时约 142 ms；Scopes 3000/3000；Services 36000/36000；Uow 构造失败 3000；事件构造失败 3000；`TimerDeltaCount=0`；`UowCurrentLeaks=0`。
- 分配与 GC：约 8.72 MB/组；Gen0 0/1/0；Gen1 0；Gen2 0；工作集约 38—47 MB。
- 原始输出：`artifacts/fix-round-1/perf-probe/run-2.log.txt`、`run-2.stdout.json`。首次 `run-1.log.txt` 是探针编译错误（long→int），不是性能运行，未覆盖。
- 速度只作观测，不据此批准或宣称预算达标；未重跑历史 GC stress、长期压力或预算矩阵。

## 符号 → 测试 → 证据映射

| 生产符号 | 直接测试方法 | 原始证据 |
|---|---|---|
| `UnitOfWorkScopeManager.BeginRoot` / `CreateRootCancellation` / `ReleaseUnhandedCancellation` | `UnitOfWorkBeginRootFailureTest.BeginOperation_WhenSaveHandlerConstructionFails_ShouldReleaseCallerRegistration`、`BeginOperation_WhenCallerCannotCancel_ShouldReleaseDeadlineTimer`、`BeginOperation_OnNormalDispose_ShouldReleaseSourcesAndCallerRegistration`、`Begin_WhenRootStartingObserverFails_ShouldPreserveOuterCurrent` | `passing-regression/uow-net8.0-final.trx`、`uow-net6.0-final.trx`、`uow-net8-fail-v2.trx` |
| `IocEventHandlerFactory.GetHandler` / `GetHandlerAsync`、`EventHandlerDisposeWrapper.DisposeAsync` | `EventHandlerScopeLifecycleTest.GetHandler_WhenConstructionFails_ShouldDisposeSyncScopeAndDependency`、`GetHandlerAsync_WhenConstructionFails_ShouldDisposeScopeAndDependency`、`GetHandlerAsync_WhenResolvedTypeIsNotHandler_ShouldDisposeNewScope`、`PublishAsync_WhenHandlerConstructionFails_ShouldDisposeNewScope` | `passing-regression/eventbus-net8.0-final.trx`、`eventbus-net6.0-final.trx`、`eventbus-net8-fail.trx` |
| `IAsyncEventHandlerFactory` / `EventBusBase.TriggerHandlerAsync` | `EventHandlerScopeLifecycleTest.PublishAsync_WithAsyncOnlyDependency_ShouldAwaitAsyncDisposal`、`PublishAsync_WithSyncOnlyDependency_ShouldKeepSynchronousDisposal`、`PublishAsync_WithDualDisposableDependency_ShouldPreferAsyncDisposalOnce`、`PublishAsync_WhenHandlerFails_ShouldPreserveOriginalAndDisposeAsyncDependency`、`PublishAsync_WhenConstructionAndCleanupFail_ShouldPreserveBothErrors`、`PublishAsync_WhenHandlerAndCleanupFail_ShouldAggregateBothErrors`、`PublishAsync_WithLegacySynchronousFactory_ShouldKeepCompatibility` | 同上 |
| `ExceptionNotifier.NotifyAsync` | `ExceptionNotifierLifecycleTest.NotifyAsync_WithAsyncOnlyDependency_ShouldAwaitAsyncDisposal`、`NotifyAsync_WhenSubscriberFails_ShouldIsolateAndDisposeAsyncDependency`、`NotifyAsync_WhenSubscriberResolutionFails_ShouldDisposeCreatedDependency`、`NotifyAsync_WhenResolutionAndCleanupFail_ShouldPreserveBothErrors` | `passing-regression/core-net8-pass.trx`、`core-net6-pass.trx`、`failing-regression/core-notifier-net8-fail.trx` |
| `BingModuleRegistration.EnsureBuildable` / `BuildBingServiceProvider` | `BingServiceCollectionExtensionsTest.BuildBingServiceProvider_RepeatedBuild_ShouldRejectBeforeReleasingAttachedRuntime`、`BuildBingServiceProvider_AfterRuntimeReleased_ShouldRejectBeforeBuilding`、`BuildBingServiceProvider_IndependentCollectionFailure_ShouldKeepAttachedRuntimeIntact`，以及既有首次校验失败用例 | `passing-regression/core-net8-pass.trx`、`core-net6-pass.trx`、`failing-regression/core-build-net8-fail.trx` |

## Review 修复记录

### Round 1

- Review 状态：NEEDS_FIX
- Fix Scope：recommended
- Review 文件：`ai_docs/tasks/20261023-lifecycle-di-performance-audit/review.md`
- 未修改 `review.md`，未把任何 finding 自行标记为通过。

#### FIX-001

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：`framework/src/Bing.Uow/Bing/Uow/UnitOfWorkScopeManager.cs`、`framework/src/Bing.Uow/AssemblyInfo.cs`、`framework/tests/Bing.Uow.Tests/UnitOfWorkBeginRootFailureTest.cs`
- 根因：取消资源在处理器解析和观察器启动前创建，失败路径没有覆盖所有权交接。
- 修复：启动全过程 try/catch；失败释放未交接 linked/deadline；主异常保留，清理异常附加；正常路径仍交给 root 释放。
- 验证：caller 注册回到 0；Timer 增量 0；主异常与外层 Current 保持；正常释放后源已 Dispose。net6/net8 通过。

#### FIX-002

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 执行状态：COMPLETED
- 修改文件：`framework/src/Bing.EventBus/Bing/EventBus/IocEventHandlerFactory.cs`、`EventHandlerDisposeWrapper.cs`、`framework/tests/Bing.EventBus.Tests/EventHandlerScopeLifecycleTest.cs`
- 根因：`CreateScope` 与包装器接管之间没有失败清理。
- 修复：同步和异步获取入口都在构造/转换失败时释放 scope；清理异常附加到主异常；正常路径仍恰好由包装器释放一次。
- 验证：构造失败、转换失败、清理自身失败、正常路径；真实 LocalEventBus 分发；net6/net8 通过。

#### FIX-003

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：`framework/src/Bing.EventBus/Bing/EventBus/IAsyncEventHandlerFactory.cs`、`IocEventHandlerFactory.cs`、`EventHandlerDisposeWrapper.cs`、`EventBusBase.cs`、测试文件同上。
- 根因：异步分发只使用同步 `using`，包装器只携带同步 `Dispose`。
- 修复：最小可选异步工厂契约；IoC 工厂和包装器支持异步释放；异步分发优先 await；旧接口和同步消费者保持兼容。
- 验证：仅异步、仅同步、双接口依赖；成功、业务异常、构造异常、释放异常；旧同步工厂兼容；net6/net8 通过。

#### FIX-004

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：`framework/src/Bing.Core/Bing/ExceptionHandling/ExceptionNotifier.cs`、`framework/tests/Bing.Core.Tests/ExceptionHandling/ExceptionNotifierLifecycleTest.cs`
- 根因：`NotifyAsync` 使用同步 scope，退出时同步释放。
- 修复：异步 scope + await 释放；解析失败保留主异常并附加清理异常；排序、隔离和日志保持。
- 验证：正常、订阅器异常、解析失败、解析与清理同时失败；仅异步依赖恰好释放一次；net6/net8 通过。

#### FIX-005

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：`framework/src/Bing.Core/Bing/Core/Modularity/BingModuleRegistration.cs`、`framework/tests/Bing.Core.Tests/Modularity/BingServiceCollectionExtensionsTest.cs`
- 根因：`EnsureBuildable` 未拒绝已附加/已释放 runtime，底层校验失败后无条件 `FailConfiguration`。
- 修复：底层构建前拒绝已附加或已释放 runtime；首次构建失败清理语义保持不变。
- 验证：验证/非验证重复构建均提前拒绝；原应用模块 Dispose=0、Shutdown 先于 Dispose 且各一次；独立集合失败隔离；已释放 runtime 拒绝；net6/net8 通过。

### Round 1 汇总

- MUST_FIX：1（FIX-002），COMPLETED。
- SHOULD_FIX：4（FIX-001、003、004、005），COMPLETED。
- PARTIAL：0。
- BLOCKED：0。
- FAILED：0。
- 回归验证：Core/EventBus/Uow 全套、AOP、SQLite、EF Core SQLite、Release solution build、默认 solution test 全部通过；外部数据库/CAP 门控未开启。
- 下一步：交主线程安排独立 Reviewer 复核；不自行判定 PASS。

## Review 修复记录 Round 2

- Review 状态：NEEDS_FIX（独立 Review 2：原 FIX-001—005 全部 CLOSED，唯一 OPEN_ACTIONABLE=FIX-006）
- Fix Scope：recommended
- Review 文件：`ai_docs/tasks/20261023-lifecycle-di-performance-audit/review.md`
- 未修改 `review.md`；Round 2 修改前快照保存在 `artifacts/fix-round-2/worktree-start/review.round2.snapshot.md`。
- 本轮新命令统一通过 `artifacts/fix-round-2/run_bounded.py` 执行，逐次保存 `command.json`（argv/cwd/startedAt/timeoutSeconds）、`result.json`（exitCode/timedOut/endedAt/durationMilliseconds）、原始二进制与 UTF-8 stdout/stderr，以及 TRX。

### FIX-006

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/src/Bing.EventBus/Bing/EventBus/EventBusBase.cs`
  - `framework/tests/Bing.EventBus.Tests/EventHandlerScopeLifecycleTest.cs`
- 根因：Round 1 为兼容异步清理，把所有包装器清理异常都加入共享 `exceptions` 后继续 foreach；这改变了旧同步工厂原本由 `using` 立即抛出并终止分发的控制流。
- 修复：仅旧同步工厂路径在清理失败时立即抛出并终止分发；同一处理器已有业务异常时抛出包含“业务主异常 + 清理异常”的 `AggregateException`。异步工厂路径继续保持原有“清理异常加入列表、继续分发、最终聚合”的策略。未修改公开接口、IoC 异步获取/释放实现或依赖。
- 失败回归（修复前，正式）：
  - net8.0：13 总计，2 失败 / 11 通过，exit 1；`artifacts/fix-round-2/runs/eventbus-net8-fail-round2/`。
  - net6.0：13 总计，2 失败 / 11 通过，exit 1；`artifacts/fix-round-2/runs/eventbus-net6-fail-round2/`。
  - 两个场景分别为“首个旧处理器成功但 Dispose 失败后仍调用第二个”和“业务失败与 Dispose 失败后仍调用第二个”。
- 新增正式回归：
  - `PublishAsync_WhenLegacyCleanupFails_ShouldAbortDispatchAndPreserveCleanupError`：原清理异常可见、secondCalls=0、release=1。
  - `PublishAsync_WhenLegacyHandlerAndCleanupFail_ShouldPreserveBothAndAbortDispatch`：AggregateException 保留两个异常、secondCalls=0、release=1。
- 通过验证：
  - 精准 net8.0 / net6.0：各 13 通过 / 0 失败，exit 0；`runs/eventbus-net8.0-pass-round2/`、`runs/eventbus-net6.0-pass-round2/`。
  - EventBus.Tests 完整套件 net8.0：422 通过 / 1 跳过 / 0 失败，exit 0；`runs/eventbus-full-net8.0-round2/`。
  - EventBus.Tests 完整套件 net6.0：422 通过 / 1 跳过 / 0 失败，exit 0；`runs/eventbus-full-net6.0-round2/`。
- 未重复运行全方案、Uow/Core、SQLite、GC、压力或性能 probe；范围未变，复用 Round 1 证据。

### Round 2 报告修正

- Round 1 精准证据文件名已改为实际名称：`uow-net8.0-final.trx`、`uow-net6.0-final.trx`、`eventbus-net8.0-final.trx`、`eventbus-net6.0-final.trx`。
- perf probe 的“同 scope 循环”明确为直接解析 10 次 DI Transient，不是 SQL Query 工厂 benchmark；60 秒是迭代间协作式截止，没有独立 watchdog，不再表述为外部硬截止。
- Round 1 原始 build/test 日志未保存进程退出码；exit 0 仅为执行会话/`commands.md` 声明，已与原始日志字段区分，未补造日志内容。
- 主线程机械核对的 `artifacts/fix-round-1/evidence-check.json` 已写入：103 份 TRX 为 12,940 通过 / 0 失败 / 1,114 未执行，12 个候选文件身份一致。

### Round 2 汇总

- FIX-006：COMPLETED。
- MUST_FIX：0。
- SHOULD_FIX：1（FIX-006），COMPLETED。
- PARTIAL / BLOCKED / FAILED：0。
- 下一步：交主线程安排同一 Sol/high 独立复核；不自行判定 PASS。

## 迁移与使用说明

1. 自定义 `IEventHandlerFactory` 无需实现新接口；异步分发对旧工厂继续使用同步获取/释放。
2. 旧同步工厂的包装器 `Dispose` 失败会立即终止本次分发，不调用后续订阅器；若该处理器业务与清理同时失败，`AggregateException` 同时保留两个异常。
3. 需要释放仅 `IAsyncDisposable` 依赖的自定义工厂，可实现 `IAsyncEventHandlerFactory`；IoC 工厂已内置。
4. `IEventHandlerDisposeWrapper` 旧成员未变；默认 `EventHandlerDisposeWrapper` 现在也实现 `IAsyncDisposable`。
5. 纯同步 `GetHandler()`/`Dispose()` 入口不承诺释放仅异步依赖；这类依赖应走异步分发或异步工厂入口。
6. Uow 启动失败的清理异常键为 `Bing.Uow.RootCancellationCleanupError`；事件工厂为 `Bing.EventBus.HandlerScopeCleanupError`；异常通知为 `Bing.ExceptionHandling.ScopeCleanupError`。
7. 同一 `IServiceCollection` 的 runtime 已附加或已释放后，`BuildBingServiceProvider` 会在底层构建前抛出 `InvalidOperationException`；请为每个应用创建独立集合。

## 剩余具体问题

- 外部 MySQL/RabbitMQ/CAP 集成未启用；相关外部证据为 BLOCKED_EXTERNAL，不以本地 SQLite 替代。
- 未建立新的性能/内存/容量预算；本次 probe 只提供当前候选观测与清理正确性证据。
- 旧同步释放入口对“仅异步释放依赖”的能力边界保持文档化限制，未通过同步阻塞伪装为异步释放。

## 证据索引

- 工作区起点：`artifacts/fix-round-1/worktree-start/`
- 候选身份：`artifacts/fix-round-1/candidate-identity.json`
- 修复前失败证据：`artifacts/fix-round-1/failing-regression/`
- 精准通过证据：`artifacts/fix-round-1/passing-regression/`
- 受影响套件：`artifacts/fix-round-1/full-suite/`、`full-suite-final/`
- AOP/SQLite：`artifacts/fix-round-1/aop-sqlite/`
- 最终候选：`artifacts/fix-round-1/final-candidate/`、`final-candidate-2/`
- 性能/资源 probe：`artifacts/fix-round-1/perf-probe/`
- Round 2 工作区起点/Review 快照：`artifacts/fix-round-2/worktree-start/`
- Round 2 bounded runner 原始命令与结果：`artifacts/fix-round-2/runs/`
- Round 2 候选身份：`artifacts/fix-round-2/candidate-identity.json`
