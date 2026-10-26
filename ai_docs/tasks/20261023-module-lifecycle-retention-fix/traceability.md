# 生产符号到验证映射

## 真实生命周期所有权（Review1 FIX-001）

| 生产符号 / 行为 | 直接职责测试 | 结果证据 |
|---|---|---|
| `BingModuleManager._scopeFactory` + `CreateLifecycleScope()`：同一应用只解析并缓存 factory | `ModuleLifecycleRetentionTest.NormalLifecycle_ShouldOwnInitAndShutdownScopes_AndReuseFactory`；`PrivateHelperContract_ShouldResolveFactoryOnce_AndCreateDistinctDisposableScopes` 只作为私有 helper 单元契约 | `artifacts/review-fix-round-1/l1-net8-final/`、`l1-net6-final/` |
| 初始化阶段通过缓存 factory 创建 scope，返回前释放一次 | `NormalLifecycle_ShouldOwnInitAndShutdownScopes_AndReuseFactory`；`InitializationHookFailure_ShouldDisposeInitAndCleanupScopesOnce`；`CancellationAfterEnteringInitialization_ShouldDisposeInitAndCleanupScopesOnce` | 同上 |
| 显式关闭阶段复用同一 factory 创建第二个独立 scope，并各释放一次 | `NormalLifecycle_ShouldOwnInitAndShutdownScopes_AndReuseFactory`；重复 `Shutdown`/`Dispose` 后计数不变 | 同上 |
| 初始化钩子已创建 scope 后失败：保留原异常，清理 scope 先释放，初始化 scope 后释放 | `InitializationHookFailure_ShouldDisposeInitAndCleanupScopesOnce` | 同上 |
| 进入异步初始化后由信号取消：保留 OCE，清理顺序与失败路径一致 | `CancellationAfterEnteringInitialization_ShouldDisposeInitAndCleanupScopesOnce`；使用 `Entered` 信号，不使用固定 sleep | 同上 |
| 已完成初始化后 provider `Dispose` 不创建关闭 scope | `ProviderDisposeAfterInitialization_ShouldNotCreateShutdownScope` | 同上 |
| 自定义 `IServiceProvider`/factory 下只解析一次、两个 scope 不同、各自释放 | `PrivateHelperContract_ShouldResolveFactoryOnce_AndCreateDistinctDisposableScopes` | 同上 |

## 既有行为与其他证据

| 生产符号 / 行为 | 验证 | 结果证据 | 边界 |
|---|---|---|---|
| `ModuleLifecycleTest.Create<T>` 的 Recorder singleton factory | 本测试类的 `CreateHelper_ShouldReturnSameRecorderInstance_FromSingletonFactory` | `artifacts/review-fix-round-1/l1-net8-final/`、`l1-net6-final/` | 只证明本测试 helper 的实例身份；原 helper 的身份由既有实际事件断言覆盖，不重复造测试 |
| 原弱引用回归：重复关闭应用 | `ModuleLifecycleTest.RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive` 原断言 | `artifacts/candidate-repeated-net6-final/`、`candidate-repeated-net8-final/` | 原候选已执行证据；新候选全量结果另见下表 |
| 原弱引用回归：正常关闭保留 service collection | `ModuleResourceRegressionTest.RetainedServiceCollection_NormalShutdown_AllowsProviderAndModuleCollection` 原断言 | `artifacts/candidate-retained-net6-final/`、`candidate-retained-net8-final/` | 原候选已执行证据；新候选全量结果另见下表 |
| 受控编译事件确认：`IServiceScopeFactory` 与 `Recorder` | 复制 Astra 探针调用两个原测试 | `artifacts/astra-probe-final/candidate/summary.json` | 仅对原候选 DLL 已执行；不替代新候选全量结果 |
| 完整 `Bing.Core.Tests` 与消费者（上一轮） | 既有初始化、关闭、重入、异步、legacy 与 `Bing.Tests` 装配回归 | `artifacts/full-core-net6-final/`、`full-core-net8-final/`、`consumer-bing-tests-net6-final/`、`consumer-bing-tests-net8-final/` | 生产源码未变范围复用；新候选全量结果另见下表 |

## 最终构建恢复后的 L4 检查点

| 检查项 | 结果 | 证据 |
|---|---|---|
| `dotnet build Bing.All.slnx -c Release --no-restore` | PASS：exit 0，PID 31924，6.21s，未超时 | `artifacts/final-candidate-after-build-recovery/build/`、`checkpoint-summary.json` |
| `dotnet test Bing.All.slnx -c Release --no-build --no-restore ...` | PASS：exit 0，PID 61944，76.58s，未超时 | `artifacts/final-candidate-after-build-recovery/test/`、`trx/` |
| TRX 逐项聚合（UnitTestResult） | 103 TRX；6 空 TRX；14008 结果；0 Failed | `trx-summary.json`、`checkpoint-summary.json` |
| net6.0 | 6722 Passed / 0 Failed / 593 NotExecuted | 同上 |
| net8.0 | 6172 Passed / 0 Failed / 521 NotExecuted | 同上 |
| 两个 GC 原方法 | net6/net8 均 Passed | `trx-summary.json` 的 `specific` |
| `ModuleLifecycleRetentionTest` 六项 | net6/net8 各 6 项均 Passed | `trx-summary.json` 的 `specific` |
| 构建/源身份 | 起点与结束源身份一致；关键 DLL hash 已记录 | `freeze.json`、`source-identity-after.json`、`post-build-identity.json` |
| 原 checkpoint | build FAIL / test NOT_STARTED 原样保留 | `artifacts/final-candidate-checkpoint/` |
| CS2012 恢复 | 两 Admin csproj 修复；Sol/high 专项 PASS；最小并行 build PASS | `build-lock-diagnosis/`、`build-recovery-review.md` |

## 证据边界

- FIX-001 的真实生命周期计数来自 `artifacts/review-fix-round-1/l1-net6-final/` 与 `l1-net8-final/`；失败尝试目录保留，不混入最终通过。
- `PrivateHelperContract_...` 只验证私有 helper 的解析/创建/释放契约，不能替代真实 `Initialize`/`Shutdown` 所有权证据。
- 新候选全量结果来自 `artifacts/final-candidate-after-build-recovery/`；`NotExecuted` 是跳过，未计为通过，空 TRX 未计为通过。
- 两个 GC 原方法和新增六项已在新候选 TRX 中单独核对；旧精准/Probe/Core/消费者结果只按原输入范围复用。
- 历史正式 FAIL、历史排他归因未知、永久泄漏未证明、外部数据库/CAP/缓存等未验证，均不因新候选本地 PASS 而改写。
