# 生产符号与直接测试映射

| 生产符号或行为 | 测试项目与方法 |
| --- | --- |
| `IAsyncPreConfigureServices` / `IAsyncPostConfigureServices` | `Bing.Core.Tests` `ModuleAsyncNamedPhaseInterfacesTest.AsyncInterfaces_RunGlobalPhasesOnceInDependencyOrder`、`ModuleAsyncPhaseBoundaryTest.AsyncInterface_WaitsBeforeRunningFollowingPhase`、`AsyncInterface_FailureAfterAwaitPreservesOriginalException`、`AsyncInterface_CallerCancellationStopsFollowingPhase` |
| `IAsyncOnPreApplicationInitialization` / `IAsyncOnPostApplicationInitialization` | `Bing.Core.Tests` 上述四阶段用例，以及 `ModuleAsyncNamedPhaseInterfacesTest.SyncInitialization_RejectsAsyncInterfacesBeforeAnyHook` |
| `BingModule` 八接口默认实现及旧入口 | `Bing.Core.Tests` `DefaultModule_ImplementsAllPhaseInterfaces_AndLegacyRegistrationWorks`、`LegacyRegistration_RejectsIndependentAsyncConfiguration` |
| `BingModule` 独立异步接口桥接 | `Bing.Core.Tests` `ModuleAsyncNamedPhaseInterfacesTest.AsyncInterfaceCallingBase_DoesNotRepeatOrFallThroughToSync`，覆盖 Pre 与 Post |
| `BingModuleHooks` 回调优先级、继承重写及隐藏方法 | `Bing.Core.Tests` `ModuleAsyncNamedPhaseInterfacesTest.SyncOverrides_ShadowIndependentAsyncInterfaces`、`AsyncOverrides_TakePriorityOverOtherImplementations`（均覆盖 Pre 与 Post）、`InheritedAsyncOverrides_IgnoreHiddenInterfaceMethods`、`HiddenMethods_ImplementAsyncInterfaces` |
| `BingModuleHooks.RequiresAsyncConfiguration` / `RequiresAsyncInitialization` | `Bing.Core.Tests` `ModuleAsyncNamedPhaseInterfacesTest.SyncRegistration_RejectsAsyncInterfacesBeforeAnyHook`、`SyncInitialization_RejectsAsyncInterfacesBeforeAnyHook`、`SyncEntries_RejectPostOnlyAsyncInterfacesBeforeAnyHook` |
| Web 阶段上下文和管道 | `Bing.AspNetCore.Mvc.Tests` `WebModuleLifecycleTest.UseBingAsync_AsyncInterfaces_ShouldShareApplicationBuilderAndComposePipeline` |
| 热更新候选失败与卸载 | `Bing.Core.Tests` `HotPluginHostTest.ReloadAsync_InitializationPhaseFailure_ShouldKeepCurrentGeneration`（含独立异步 Pre/Post 接口）、`ReloadAsync_AsyncConfigurationFailureAtAnyPhase_ShouldKeepCurrentGenerationAndCollectCandidate`、`ReloadAsync_AsyncPostInterface_ShouldPublishThenCollectCandidate` |

以上测试均在 `net8.0`、`net6.0` 下执行。相关现有旧接口、Web 与宿主回归包含在两个测试项目全量测试中。
