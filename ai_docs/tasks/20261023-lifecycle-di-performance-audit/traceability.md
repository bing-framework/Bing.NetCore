# 生产符号 → 测试 → 证据映射

任务：20261023-lifecycle-di-performance-audit；Review Round：1。

所有原始证据位于 `artifacts/fix-round-1/`。测试方法均在 net6.0 与 net8.0 Release 下执行；表中“通过”以对应 TRX 的 `failed="0"` 为准。

## FIX-001 Uow 启动失败资源清理

| 生产符号 | 行为 | 测试方法 | 证据 |
|---|---|---|---|
| `UnitOfWorkScopeManager.BeginRoot` | root 发布前失败释放未交接的 linked/deadline CTS | `UnitOfWorkBeginRootFailureTest.BeginOperation_WhenSaveHandlerConstructionFails_ShouldReleaseCallerRegistration` | `passing-regression/uow-net8.0-final.trx`、`uow-net6.0-final.trx` |
| `UnitOfWorkScopeManager.BeginRoot` | 调用方不可取消时仍释放 deadline 计时器 | `UnitOfWorkBeginRootFailureTest.BeginOperation_WhenCallerCannotCancel_ShouldReleaseDeadlineTimer` | 同上；修复前失败见 `failing-regression/uow-net8-fail-v2.trx` |
| `UnitOfWorkScopeManager.BeginRoot` | 启动失败保持外层 Current | `UnitOfWorkBeginRootFailureTest.Begin_WhenRootStartingObserverFails_ShouldPreserveOuterCurrent` | 同上 |
| `UnitOfWorkScopeManager.CreateRootCancellation` | 正常启动后由 root 释放资源 | `UnitOfWorkBeginRootFailureTest.BeginOperation_OnNormalDispose_ShouldReleaseSourcesAndCallerRegistration` | 同上 |
| `UnitOfWorkScopeManager.CreateRootCancellation` | linked 创建中途失败释放已取得 deadline | 由上述失败路径共享实现；由 handler 失败/正常释放用例覆盖 | 同上 |

## FIX-002 事件工厂 scope 所有权

| 生产符号 | 行为 | 测试方法 | 证据 |
|---|---|---|---|
| `IocEventHandlerFactory.GetHandler` | 构造失败释放同步 scope 与同步依赖 | `EventHandlerScopeLifecycleTest.GetHandler_WhenConstructionFails_ShouldDisposeSyncScopeAndDependency` | `passing-regression/eventbus-net8.0-final.trx`、`eventbus-net6.0-final.trx` |
| `IocEventHandlerFactory.GetHandlerAsync` | 构造失败等待异步依赖释放 | `EventHandlerScopeLifecycleTest.GetHandlerAsync_WhenConstructionFails_ShouldDisposeScopeAndDependency` | 同上；修复前失败见 `failing-regression/eventbus-net8-fail.trx` |
| `IocEventHandlerFactory.GetHandlerAsync` | 类型转换失败释放新 scope | `EventHandlerScopeLifecycleTest.GetHandlerAsync_WhenResolvedTypeIsNotHandler_ShouldDisposeNewScope` | 同上 |
| `IocEventHandlerFactory` + `LocalEventBus.PublishAsync` | 真实分发构造失败释放依赖并保留主异常 | `EventHandlerScopeLifecycleTest.PublishAsync_WhenHandlerConstructionFails_ShouldDisposeNewScope` | 同上 |
| `IocEventHandlerFactory` | 清理失败不覆盖主异常 | `EventHandlerScopeLifecycleTest.PublishAsync_WhenConstructionAndCleanupFail_ShouldPreserveBothErrors` | 同上 |

## FIX-003 异步分发释放

| 生产符号 | 行为 | 测试方法 | 证据 |
|---|---|---|---|
| `IAsyncEventHandlerFactory` | 新可选异步工厂契约 | `EventHandlerScopeLifecycleTest.GetHandlerAsync_*`、`PublishAsync_WithLegacySynchronousFactory_ShouldKeepCompatibility` | 同上 |
| `EventHandlerDisposeWrapper.DisposeAsync` | 仅异步依赖异步释放 | `PublishAsync_WithAsyncOnlyDependency_ShouldAwaitAsyncDisposal` | 同上；修复前失败见 `failing-regression/eventbus-net8-fail.trx` |
| `EventBusBase.TriggerHandlerAsync` | 同步依赖保持同步释放 | `PublishAsync_WithSyncOnlyDependency_ShouldKeepSynchronousDisposal` | 同上 |
| `EventBusBase.TriggerHandlerAsync` | 双接口依赖优先异步且恰好一次 | `PublishAsync_WithDualDisposableDependency_ShouldPreferAsyncDisposalOnce` | 同上 |
| `EventBusBase.TriggerHandlerAsync` | 业务异常保留并异步释放 | `PublishAsync_WhenHandlerFails_ShouldPreserveOriginalAndDisposeAsyncDependency` | 同上 |
| `EventBusBase.TriggerHandlerAsync` | 业务异常与清理异常同时保留 | `PublishAsync_WhenHandlerAndCleanupFail_ShouldAggregateBothErrors` | 同上 |
| `EventBusBase.TriggerHandlerAsync` | 旧同步工厂兼容 | `PublishAsync_WithLegacySynchronousFactory_ShouldKeepCompatibility` | 同上 |

## FIX-004 ExceptionNotifier

| 生产符号 | 行为 | 测试方法 | 证据 |
|---|---|---|---|
| `ExceptionNotifier.NotifyAsync` | 仅异步依赖异步释放 | `ExceptionNotifierLifecycleTest.NotifyAsync_WithAsyncOnlyDependency_ShouldAwaitAsyncDisposal` | `passing-regression/core-net8-pass.trx`、`core-net6-pass.trx`；修复前失败见 `failing-regression/core-notifier-net8-fail.trx` |
| `ExceptionNotifier.NotifyAsync` | 订阅器异常隔离后仍释放 | `NotifyAsync_WhenSubscriberFails_ShouldIsolateAndDisposeAsyncDependency` | 同上 |
| `ExceptionNotifier.NotifyAsync` | 解析失败释放已创建依赖并保留主异常 | `NotifyAsync_WhenSubscriberResolutionFails_ShouldDisposeCreatedDependency` | 同上 |
| `ExceptionNotifier.NotifyAsync` | 解析与清理同时失败保留两者 | `NotifyAsync_WhenResolutionAndCleanupFail_ShouldPreserveBothErrors` | 同上 |

## FIX-005 容器构建隔离

| 生产符号 | 行为 | 测试方法 | 证据 |
|---|---|---|---|
| `BingModuleRegistration.EnsureBuildable` + `BuildBingServiceProvider` | 已附加 runtime 在底层构建前拒绝，原应用不被释放 | `BingServiceCollectionExtensionsTest.BuildBingServiceProvider_RepeatedBuild_ShouldRejectBeforeReleasingAttachedRuntime` | `passing-regression/core-net8-pass.trx`、`core-net6-pass.trx`；修复前失败见 `failing-regression/core-build-net8-fail.trx` |
| `BingModuleRegistration.EnsureBuildable` | 已释放 runtime 拒绝重建 | `BingServiceCollectionExtensionsTest.BuildBingServiceProvider_AfterRuntimeReleased_ShouldRejectBeforeBuilding` | 同上 |
| `BingModuleRegistration` / `BuildBingServiceProvider` | 独立集合失败隔离 | `BingServiceCollectionExtensionsTest.BuildBingServiceProvider_IndependentCollectionFailure_ShouldKeepAttachedRuntimeIntact` | 同上 |
| `BingModuleRegistration.FailConfiguration` | 首次构建校验失败仍清理未初始化模块 | 既有 `BuildBingServiceProvider_ValidateOnBuildFailure_ShouldReleaseConstructedModules`、`...ReleaseAsyncOnlyModulesSynchronously` | `full-suite/core-full-net8.trx`、`core-full-net6.trx` |

## FIX-006 旧同步工厂清理失败边界（Round 2）

| 生产符号 | 行为 | 测试方法 | 证据 |
|---|---|---|---|
| `EventBusBase.TriggerHandlerAsync`（旧同步工厂分支） | 首个旧处理器成功但同步 `Dispose` 失败时立即终止分发，清理异常可见，后续调用 0，释放 1 | `EventHandlerScopeLifecycleTest.PublishAsync_WhenLegacyCleanupFails_ShouldAbortDispatchAndPreserveCleanupError` | `fix-round-2/runs/eventbus-net8.0-pass-round2/`、`eventbus-net6.0-pass-round2/`；修复前失败见 `eventbus-net8-fail-round2/`、`eventbus-net6-fail-round2/` |
| `EventBusBase.TriggerHandlerAsync`（旧同步工厂分支） | 业务失败与同步 `Dispose` 失败同时发生时 `AggregateException` 保留两者并立即终止分发 | `EventHandlerScopeLifecycleTest.PublishAsync_WhenLegacyHandlerAndCleanupFail_ShouldPreserveBothAndAbortDispatch` | 同上 |
| `EventBusBase.TriggerHandlerAsync`（异步工厂分支） | 保持原有策略：清理异常加入列表、继续分发、最终聚合 | 既有 `PublishAsync_WhenHandlerAndCleanupFail_ShouldAggregateBothErrors` 等 | `fix-round-2/runs/eventbus-net8.0-pass-round2/`、`eventbus-net6.0-pass-round2/` |

## 跨模块回归

| 范围 | 结果 | 证据 |
|---|---|---|
| Core.Tests 全套 net8.0 / net6.0 | 653 / 653 通过 | `full-suite/core-full-net8.trx`、`core-full-net6.trx` |
| EventBus.Tests 全套 net8.0 / net6.0（Round 2 当前候选） | 422 通过、1 跳过 / 422 通过、1 跳过 | `fix-round-2/runs/eventbus-full-net8.0-round2/`、`eventbus-full-net6.0-round2/` |
| EventBus.Tests 全套 Round 1 历史 | 420 通过、1 跳过 / 420 通过、1 跳过 | `full-suite-final/bing-eventbus-tests-net8.0.trx`、`net6.0.trx` |
| Uow.Tests 全套 net8.0 / net6.0 | 74 / 74 通过 | `full-suite-final/bing-uow-tests-net8.0.trx`、`net6.0.trx` |
| AOP AspectCore net8.0 / net6.0 | 178 / 178 通过 | `aop-sqlite/aop-net8.trx`、`aop-net6.trx` |
| SQLite Uow/Factory 契约 net8.0 / net6.0 | 79 / 79 通过 | `aop-sqlite/sqlite-uow-net8.trx`、`sqlite-uow-net6.trx` |
| EF Core SQLite 契约 net6.0 | 131 通过 | `aop-sqlite/efcore-sqlite-net6.trx` |
| 最终默认 solution test | 12,940 通过 / 0 失败 / 1,114 未执行 | `final-candidate-2/` 103 个 TRX；主线程机械核对见 `fix-round-1/evidence-check.json` |
| Release solution build | 执行会话声明 exit 0，日志 0 error；原始日志不含进程退出码字段 | `final-candidate-2/solution-build-release.log.txt` |

## 资源 / 性能 probe

| 入口 | 断言 | 证据 |
|---|---|---|
| `perf-probe/Program.cs` | Services 36000/36000、Scopes 3000/3000、Uow 失败 3000、事件失败 3000、Timer 增量 0、Current 泄漏 0 | `perf-probe/run-2.stdout.json`、`run-2.log.txt` |
| 工作负载 | 同一 DI scope 内直接解析 10 次 Transient 服务；不是 SQL Query 工厂 benchmark | `perf-probe/Program.cs` |
| 固定窗口 | 预热 100、3×1000、串行、无外部服务；60 秒为迭代间协作式截止，无独立 watchdog | `perf-probe/cia.md` |

`run-1.log.txt` 为探针编译错误，不作为性能运行证据；未重试成功运行，`run-2` 是唯一成功运行。该 probe 的 exit 0 同样属于执行会话声明，原始日志不嵌入进程退出码。

## Round 2 候选与运行证据

- 工作区起点与 Round 1 报告快照：`fix-round-2/worktree-start/`。
- 候选身份与 12 个生产/测试文件 SHA-256：`fix-round-2/candidate-identity.json`。
- 有界 runner：`fix-round-2/run_bounded.py`；每次运行保存 `command.json`、`result.json`、`stdout.txt`、`stderr.txt`、原始二进制输出和 TRX。
- 运行汇总：`fix-round-2/summary.json`。
- 未覆盖 Round 1 失败/通过证据、旧候选身份或历史 TRX。
