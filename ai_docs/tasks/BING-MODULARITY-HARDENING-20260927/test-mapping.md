# 最终生产符号 → 直接测试方法映射

所有下列方法均随最终 Core / MVC 测试在 net8.0 和 net6.0 执行通过。测试类名链接为真实源码；完整结果见 execution.md 的证据清单。

## ModuleGraphTest

- 项目：`Bing.Core.Tests`
- 生产符号：`ModuleDependencyGraph.Build / BingModuleLoader.LoadModules / BingModule.GetDependedModuleTypes / BingModuleHelper`
- 行为：依赖环、非法类型、自定义提供程序、菱形图、排除冲突
- 源码：[ModuleGraphTest.cs](../../../framework/tests/Bing.Core.Tests/Modularity/ModuleGraphTest.cs)

- `ModuleGraphTest.LoadModules_ShouldPreserveTopologicalOrderForDiamondGraph`
- `ModuleGraphTest.LoadModules_ShouldRejectSelfCycleWithCompletePath`
- `ModuleGraphTest.LoadModules_ShouldRejectThreeNodeCycleWithCompletePath`
- `ModuleGraphTest.LoadModules_ShouldRejectExcludedDependencyWithConflictChain`
- `ModuleGraphTest.LoadModules_ShouldValidateNullAndInvalidRoots`
- `ModuleGraphTest.LoadModules_ShouldRejectNullDependencyAndProviderResult`
- `ModuleGraphTest.LoadModules_ShouldReadCustomProviderAndInheritedDependency`
- `ModuleGraphTest.IsBingModule_ShouldMatchRuntimeLoadableContract`

## ModuleRegistrationTest

- 项目：`Bing.Core.Tests`
- 生产符号：`BingBuilder.AddModule/AddModules / AddBing / AddBingApplication / IBingModuleLoader / BingModuleDescriptor / IBingModuleContainer / IBingPreConfigureServices / IBingPostConfigureServices`
- 行为：兼容时机与顺序、失败状态、按需构造、混用、分阶段配置及扩展入口
- 源码：[ModuleRegistrationTest.cs](../../../framework/tests/Bing.Core.Tests/Modularity/ModuleRegistrationTest.cs)

- `ModuleRegistrationTest.NewEntry_ShouldNotConstructUnselectedFailureModule`
- `ModuleRegistrationTest.LegacyEntry_ShouldConfigureModuleImmediately_AndBeIdempotent`
- `ModuleRegistrationTest.ConfigurationFailure_ShouldPreventReuseAndDisposeOwnedModules`
- `ModuleRegistrationTest.NewAndLegacyEntries_ShouldNotBeMixed`
- `ModuleRegistrationTest.NewEntry_ShouldRunDependencyBeforeOppositeLevelRoot`
- `ModuleRegistrationTest.NewEntry_ShouldRunPreAddPostInSeparateRounds`
- `ModuleRegistrationTest.NewEntry_AutoRegisterServicesFalse_ShouldSkipDependencyScan`
- `ModuleRegistrationTest.NewEntry_ShouldUseProvidedModuleLoaderInstance`
- `ModuleRegistrationTest.NewEntry_ShouldUseCustomLoaderResultOrder`
- `ModuleRegistrationTest.NewEntry_ShouldRejectInvalidCustomLoaderResult`
- `ModuleRegistrationTest.NewEntry_ShouldRejectNullCustomLoaderResult`
- `ModuleRegistrationTest.NewEntry_ShouldRejectNullModuleDescriptor`
- `ModuleRegistrationTest.NewEntry_ConfigureFailure_ShouldPreventReuse`
- `ModuleRegistrationTest.NewEntry_ShouldKeepExplicitParentDependencyInGraph`
- `ModuleRegistrationTest.LegacySetupFailure_ShouldPoisonRegistration`
- `ModuleRegistrationTest.LegacyAddModules_ShouldKeepPerRootRegistrationOrder`

## OptionsTypeRegistrationTest

- 项目：`Bing.Core.Tests`
- 生产符号：`AddOptionsType / OptionsTypeRegistration / BingLoader.RegisterTypes / InternalServiceCollectionExtensions.GetOrAddTypeFinder`
- 行为：集合隔离、回收、配置重载、外部事件兼容及扫描来源
- 源码：[OptionsTypeRegistrationTest.cs](../../../framework/tests/Bing.Core.Tests/Modularity/OptionsTypeRegistrationTest.cs)

- `OptionsTypeRegistrationTest.AddOptionsType_ShouldIsolateServiceCollections`
- `OptionsTypeRegistrationTest.AddOptionsType_AfterRegisterTypes_ShouldStillBindOptions`
- `OptionsTypeRegistrationTest.AddOptionsType_RepeatedRegistration_ShouldKeepFirstConfiguration`
- `OptionsTypeRegistrationTest.AddOptionsType_ShouldPreservePublicRegisterTypeEvent`
- `OptionsTypeRegistrationTest.AddOptionsType_ShouldNotKeepServiceCollectionAlive`
- `OptionsTypeRegistrationTest.AddOptionsType_ShouldKeepConfigurationReloadBehavior`
- `OptionsTypeRegistrationTest.RegisterTypes_OnAnotherCollection_ShouldNotChangeExistingDescriptors`
- `OptionsTypeRegistrationTest.RegisterTypes_UsesCustomAssemblyFinder`

## ModuleLifecycleTest

- 项目：`Bing.Core.Tests`
- 生产符号：`BingModuleManager / BingModuleRegistration / UseBing/UseBingAsync / ShutdownBing/ShutdownBingAsync / ServiceLocator.Bind/Unbind / StartupLogger.Output`
- 行为：至多一次、异常清理、逆序关闭、取消、隔离、弱引用回收及后台资源停止
- 源码：[ModuleLifecycleTest.cs](../../../framework/tests/Bing.Core.Tests/Modularity/ModuleLifecycleTest.cs)

- `ModuleLifecycleTest.InitializesOnce_AndStopsInReverseOrder`
- `ModuleLifecycleTest.ProviderDispose_ReleasesOwnedModulesOnce`
- `ModuleLifecycleTest.InitializationFailure_CleansStartedAndFailingModule_PreservesError`
- `ModuleLifecycleTest.ShutdownError_DoesNotPreventRemainingCleanup`
- `ModuleLifecycleTest.AsyncInitializer_IsAwaited_AndNotAlsoCalledSynchronously`
- `ModuleLifecycleTest.Cancellation_CleansPartialInitialization_WithoutRetry`
- `ModuleLifecycleTest.PreCanceledInitialization_DoesNotRunHooks_AndCanStartLater`
- `ModuleLifecycleTest.Reentry_IsRejectedWithoutDeadlocking`
- `ModuleLifecycleTest.InitializationScope_IsDisposed_BeforeSuccessfulReturn`
- `ModuleLifecycleTest.AsyncDisposableOnlyModule_UsesAsyncLifecycle_AndDisposesOnce`
- `ModuleLifecycleTest.AsyncShutdown_CancelsBackgroundLoopAndUnsubscribesExactlyOnce`
- `ModuleLifecycleTest.NewApplications_DoNotBindGlobalServiceLocator_AndRemainIsolated`
- `ModuleLifecycleTest.StoppingOlderLegacyHost_DoesNotUnbindNewerHost`
- `ModuleLifecycleTest.StartupLogFlush_ClearsCachedEntries`
- `ModuleLifecycleTest.DisposingProviderDuringAsyncInitialization_CancelsAndCleansModules`
- `ModuleLifecycleTest.ScopeDisposalError_PreservesInitializationFailureAndCleansModule`
- `ModuleLifecycleTest.AsyncDisposableOnlyModule_ProviderDisposeAsync_ReleasesModule`
- `ModuleResourceRegressionTest.AsyncDisposableOnlyModule_AsyncShutdownAndRepeatedDispose_ReleaseOnce`
- `ModuleResourceRegressionTest.AsyncDisposableOnlyModule_ConfigurationFailure_ReleasesAsyncResourceAndPreservesError`
- `ModuleResourceRegressionTest.AsyncDisposableOnlyModule_ConstructorFailure_ReleasesAsyncResourceAndPreservesError`

## Review Round 5 补充映射

| FIX | 最终生产符号 | 关键行为 | 直接测试 |
| --- | --- | --- | --- |
| FIX-003 | `BingModuleRegistration.ReleaseAsync`、`RemoveOwnedModuleDescriptors` | 保留 `IServiceCollection` 时，正常关闭后 provider 和模块实例可回收，重复释放不重复执行 | `ModuleResourceRegressionTest.RetainedServiceCollection_NormalShutdown_AllowsProviderAndModuleCollection` |
| FIX-003 | `BingModuleRegistration.FailConfiguration`、`ReleaseAsync` | 配置失败完成清理后，服务集合不继续保留已构造模块 | `ModuleResourceRegressionTest.RetainedServiceCollection_ConfigurationFailure_AllowsModuleCollection` |
| FIX-003 | `BingServiceCollectionExtensions.BuildBingServiceProvider`、`BingModuleRegistration.FailConfiguration` | `ValidateOnBuild` 失败发生在 provider 返回前时，已构造模块可回收 | `ModuleResourceRegressionTest.RetainedServiceCollection_ValidateOnBuildFailure_AllowsModuleCollection` |
| FIX-003 | `BingModuleDescriptor.ModuleType`、`Assembly`、`ClearInstance` | 释放实例引用后仍保留模块目录元数据，不清空公开描述符集合 | 上述三个 `RetainedServiceCollection` 回收测试及 Core 模块化全量回归 |
| FIX-003 | `BingBuilder.ClearModuleReferences`、`BingModuleRegistration.ReleaseAsync` | 旧 `AddBing` 入口保留服务集合时清除 builder 的模块引用，并保持 provider/module 可回收 | `ModuleResourceRegressionTest.RetainedLegacyServiceCollection_NormalShutdown_AllowsModuleCollection` |
- `BingServiceCollectionExtensionsTest.BuildBingServiceProvider_ShouldAttachManagerForDisposeFallback`
- `BingServiceCollectionExtensionsTest.BuildBingServiceProvider_ShouldUseAsyncDisposeForUninitializedAsyncModule`
- `ModuleLifecycleTest.RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive`

## WebModuleLifecycleTest

- 项目：`Bing.AspNetCore.Mvc.Tests`
- 生产符号：`BingApplicationBuilderExtensions.UseBing/UseBingAsync / BingModuleHostedService.StopAsync`
- 行为：管道分派、Web 顺序、重入/并发、真实宿主停止
- 源码：[WebModuleLifecycleTest.cs](../../../framework/tests/Bing.AspNetCore.Mvc.Tests/Modularity/WebModuleLifecycleTest.cs)

- `WebModuleLifecycleTest.AddBingApplication_WebAndNonWebUseBing_ShouldKeepTheSameDependencyOrder`
- `WebModuleLifecycleTest.UseBing_ShouldDispatchWebAndProviderModules_AndAvoidDuplicateMiddleware`
- `WebModuleLifecycleTest.UseBingAsync_ShouldGiveAsyncWebModuleTheActualApplicationBuilder`
- `WebModuleLifecycleTest.UseBingAsync_WebReentry_ShouldFailAndLeaveTheApplicationFailed`
- `WebModuleLifecycleTest.UseBingAsync_ConcurrentWebInitialization_ShouldRejectTheSecondApplicationContext`
- `WebModuleLifecycleTest.HostStop_ShouldRunAsyncModuleShutdown`

## Review Round 1 补充映射

本轮以 execution.md 的 fix1 证据为准；原审查报告保留，不由修复执行器改写。Theory 的多个用例分别出现在 TRX 中。

| FIX | 最终生产符号 | 关键行为 | 直接测试 |
| --- | --- | --- | --- |
| FIX-001 | ModuleReferenceComparer、BingModuleRegistration.Own/ReleaseAsync | 正常、异常、异步和重复关闭均按实例身份释放 | ModuleResourceRegressionTest.EqualButDistinctModules_NormalShutdownAndRepeatedShutdown_DisposeEachInstanceOnce；EqualButDistinctAsyncModules_AsyncShutdownAndRepeatedShutdown_DisposeEachInstanceOnce；EqualButDistinctModules_ConfigurationFailure_DisposeEachConstructedInstanceOnce |
| FIX-001 | BingBuilder.AddModule | 相等模块按不同类型各配置一次 | ModuleRegistrationTest.LegacyEqualModules_ShouldConfigureEachTypeOnlyOnce |
| FIX-002 | BingBuilder.AddModule/AddModules、FailConfiguration | 环、非法依赖、排除冲突后拒绝复用并释放既有实例 | ModuleRegistrationTest.LegacyGraphFailure_ShouldPoisonBuilderAndReleaseExistingModules（3 个用例） |
| FIX-003 | ModuleDependencyGraph.Build/ReadDependencies/ValidateModuleType | 完整有序路径、环前缀、原提供程序异常保留 | ModuleGraphTest.LoadModules_ShouldRejectThreeNodeCycleWithCompletePath；LoadModules_ShouldValidateNullAndInvalidRoots；LoadModules_ShouldRejectNullDependencyAndProviderResult；LoadModules_ShouldRejectExcludedDependencyWithConflictChain |
| FIX-004 | AddBingApplication、ModuleAssemblyFinder、DependencyModule、BingLoader | 真入口的 selected/unselected 程序集及同程序集共享扫描 | ModuleScanningTest.AddBingApplication_ShouldScanSelectedAssembly_AndIgnoreUnselectedAssembly |
| FIX-004 | AddOptionsType、OptionsTypeRegistration | 真实绑定、IOptionsMonitor 解析、关闭后弱引用回收 | OptionsTypeRegistrationTest.AddOptionsType_ShouldNotKeepServiceCollectionAlive |
| FIX-004 | BingModuleLoader.LoadModules | 后续模块构造失败时释放已构造模块 | ModuleResourceRegressionTest.ConstructorFailure_ReleasesPreviouslyConstructedModules |
| FIX-004 | BingModuleManager.ShutdownAsync/CleanupAsync | 关闭预取消/中途取消仍清理所有模块并汇总异常 | ModuleResourceRegressionTest.ShutdownAsync_PreCanceledToken_StillAttemptsAllHooks_ReleasesAllAndAggregatesErrors；ShutdownAsync_MidFlightCancellation_ContinuesWithRemainingHooks_ReleasesAllAndAggregatesErrors |
| D-01 边界验证 | ShutdownBing | 未初始化时显式关闭也释放一次 | ModuleResourceRegressionTest.ExplicitShutdownBeforeInitialization_ReleasesOwnedModulesOnce |

## Review Round 2 补充映射

| 差异 | 最终生产符号 | 关键行为 | 直接测试 |
| --- | --- | --- | --- |
| D-02 | ModuleDependencyGraph.ValidateModuleType、BingModuleLoader.ReleaseInstance、BingModuleRegistration.ReleaseAsync | 仅异步释放模块可进入异步生命周期；同步配置/构造失败等待异步释放并保留原异常 | ModuleLifecycleTest.AsyncDisposableOnlyModule_ProviderDisposeAsync_ReleasesModule；ModuleResourceRegressionTest.AsyncDisposableOnlyModule_AsyncShutdownAndRepeatedDispose_ReleaseOnce；AsyncDisposableOnlyModule_ConfigurationFailure_ReleasesAsyncResourceAndPreservesError；AsyncDisposableOnlyModule_ConstructorFailure_ReleasesAsyncResourceAndPreservesError |
| D-01 缓解入口 | BingServiceCollectionExtensions.BuildBingServiceProvider | 预解析管理器，使未初始化模块进入同步/异步 provider Dispose 释放路径 | BingServiceCollectionExtensionsTest.BuildBingServiceProvider_ShouldAttachManagerForDisposeFallback；BuildBingServiceProvider_ShouldUseAsyncDisposeForUninitializedAsyncModule |

## Review Round 3 补充映射

| 差异 | 最终生产符号 | 关键行为 | 直接测试 |
| --- | --- | --- | --- |
| FOLLOWUP2-FIX-001 | BingServiceCollectionExtensions.BuildBingServiceProvider、BingModuleRegistration.FailConfiguration | 根容器 `ValidateOnBuild` 失败发生在 provider 返回前时，已构造的同步/异步模块仍释放一次且保留原始异常 | BingServiceCollectionExtensionsTest.BuildBingServiceProvider_ValidateOnBuildFailure_ShouldReleaseConstructedModules；BuildBingServiceProvider_ValidateOnBuildFailure_ShouldReleaseAsyncOnlyModulesSynchronously |
| FOLLOWUP2-FIX-002 | BingModuleDisposal.DisposeSynchronously、BingModuleLoader.LoadModules、BingModuleRegistration.ReleaseAsync | 同步构造/配置失败清理仅异步资源时不捕获调用方同步上下文，释放完成后再返回原始异常 | ModuleResourceRegressionTest.AsyncOnlyConfigurationFailure_DoesNotDeadlockCapturedSynchronizationContext；AsyncDisposableOnlyModule_ConfigurationFailure_ReleasesAsyncResourceAndPreservesError；AsyncDisposableOnlyModule_ConstructorFailure_ReleasesAsyncResourceAndPreservesError |

## Review Round 6 补充映射

| FIX | 最终生产符号 | 关键行为 | 直接测试 |
| --- | --- | --- | --- |
| FIX-004 | `BingModule.IsBingModule`、`BingModule.CheckBingModuleType` | 公开模块类型判断与依赖图的 `BingModule`、公共无参构造约束保持一致 | `ModuleGraphTest.IsBingModule_ShouldMatchRuntimeLoadableContract` |
| FIX-005 | `BingModuleManager.InitializeCoreAsync`、`StartupLogger.Output` | 日志输出失败时保留原始初始化异常、附加日志异常、清空缓存并完成模块清理 | `ModuleLifecycleTest.StartupLogFlushFailure_PreservesInitializationError_CleansModuleAndPreventsRetry` |

### ModuleResourceRegressionTest

项目：Bing.Core.Tests；源码：[ModuleResourceRegressionTest.cs](../../../framework/tests/Bing.Core.Tests/Modularity/ModuleResourceRegressionTest.cs)

- `ModuleResourceRegressionTest.EqualButDistinctModules_NormalShutdownAndRepeatedShutdown_DisposeEachInstanceOnce`
- `ModuleResourceRegressionTest.ExplicitShutdownBeforeInitialization_ReleasesOwnedModulesOnce`
- `ModuleResourceRegressionTest.EqualButDistinctAsyncModules_AsyncShutdownAndRepeatedShutdown_DisposeEachInstanceOnce`
- `ModuleResourceRegressionTest.EqualButDistinctModules_ConfigurationFailure_DisposeEachConstructedInstanceOnce`
- `ModuleResourceRegressionTest.ConstructorFailure_ReleasesPreviouslyConstructedModules`
- `ModuleResourceRegressionTest.ShutdownAsync_PreCanceledToken_StillAttemptsAllHooks_ReleasesAllAndAggregatesErrors`
- `ModuleResourceRegressionTest.ShutdownAsync_MidFlightCancellation_ContinuesWithRemainingHooks_ReleasesAllAndAggregatesErrors`
- `ModuleResourceRegressionTest.AsyncDisposableOnlyModule_AsyncShutdownAndRepeatedDispose_ReleaseOnce`
- `ModuleResourceRegressionTest.AsyncDisposableOnlyModule_ConfigurationFailure_ReleasesAsyncResourceAndPreservesError`
- `ModuleResourceRegressionTest.AsyncDisposableOnlyModule_ConstructorFailure_ReleasesAsyncResourceAndPreservesError`

### ModuleScanningTest

项目：Bing.Core.Tests；源码：[ModuleScanningTest.cs](../../../framework/tests/Bing.Core.Tests/Modularity/ModuleScanningTest.cs)

- `ModuleScanningTest.AddBingApplication_ShouldScanSelectedAssembly_AndIgnoreUnselectedAssembly`

## Review Round 7 补充映射

| FIX | 最终生产符号 | 关键行为 | 直接测试 |
| --- | --- | --- | --- |
| FIX-006 | `ServiceLocator.BindServices/UnbindServices`、`BingModuleRegistration.BindLegacyRegistration/UnbindLegacy` | 旧入口在模块注册阶段可读取当前服务集合；注册失败或宿主停止后按所属服务集合解绑静态兼容状态 | `ModuleRegistrationTest.LegacyEntry_ShouldExposeStaticServiceCollectionDuringRegistration`；`ModuleRegistrationTest.LegacyRegistrationFailure_ShouldUnbindStaticServiceCollection` |
| FIX-007 | `BingModuleLoader.LoadModules`、`IBingModuleLoader` 结果校验 | 自定义加载器的排序结果被实际采用；返回重复描述符时拒绝配置并使集合不可继续使用 | `ModuleRegistrationTest.NewEntry_ShouldUseCustomLoaderResultOrder`；`ModuleRegistrationTest.NewEntry_ShouldRejectInvalidCustomLoaderResult` |
| FIX-008 | `ModuleDependencyGraph.Build`、`BingModuleLoader.LoadModules` | 就绪节点使用 Ordinal 全名稳定排序，多根共享依赖只构造一次，长依赖链不依赖递归调用栈 | `ModuleGraphTest.LoadModules_ShouldUseOrdinalFullNameForReadyTie`；`ModuleGraphTest.LoadModules_ShouldDeduplicateSharedDependencyAcrossRoots`；`ModuleGraphTest.LoadModules_ShouldHandleLongDependencyChainWithoutRecursion` |

## Review Round 8 补充映射

| FIX | 最终生产符号 | 关键行为 | 直接测试 |
| --- | --- | --- | --- |
| FIX-009 | `ServiceLocator.BindServices/SetServiceCollection`、`BingModuleRegistration.BindLegacy/BindLegacyRegistration/UnbindLegacy` | 旧入口切换到不同服务集合时清除旧 provider，注册失败后不保留“新集合 + 旧 provider”的混合绑定，并允许后续宿主重新绑定；旧宿主关闭不清除新宿主共享配置 | `ModuleRegistrationTest.LegacyRegistrationFailure_ShouldNotMixActiveProviderWithNewServices`；`ModuleRegistrationTest.StoppingOlderLegacyHost_ShouldKeepSharedConfigurationOfNewerHost` |
| FIX-010 | Core 旧入口测试集合、`BingServiceCollectionExtensions.BuildBingServiceProvider` | 触碰静态兼容入口的测试串行执行；自动扫描测试通过容器释放路径解绑服务集合，断言不依赖测试执行顺序 | `DependencyInjectionTest.AutoLoad_Scoped`；`DependencyInjectionTest.AutoLoad_Scoped_MultiInjection`；`DependencyInjectionTest.AutoLoad_Singleton`；`DependencyInjectionTest.AutoLoad_Transient`；模块化全量回归 |

## Review Round 9 补充映射

| FIX | 最终生产符号 | 关键行为 | 直接测试 |
| --- | --- | --- | --- |
| FIX-011 | `AddBingApplication`、`ValidateLoadedModules` | 自定义加载器返回空目录或空描述符时立即失败，并将服务集合标记为不可复用 | `ModuleRegistrationTest.NewEntry_ShouldRejectNullCustomLoaderResult`；`ModuleRegistrationTest.NewEntry_ShouldRejectNullModuleDescriptor` |

## 启动期可信插件补充映射

| 生产符号 | 关键行为 | 直接测试 |
| --- | --- | --- |
| `BingPluginSourceList.AddDirectory/AddFolder`、`DirectoryBingPluginSource` | 单目录和一级插件父目录发现；不同服务集合的插件描述符相互隔离 | `PluginLoadingTest.AddBingApplication_ShouldLoadPluginAndScanSelectedPluginAssembly`；`PluginDependencyGraphTest.AddFolder_ShouldOrderPluginsByDependencyBeforeOrdinalName`；`PluginDependencyGraphTest.AddBingApplication_TwoServiceCollections_ShouldKeepPluginContainersIsolated`；`PluginDependencyGraphTest.AddBingApplication_TwoServiceCollections_ShouldIsolatePluginServicesAndConfiguration` |
| `BingPluginLoader.Load`、`BingPluginDescriptor`、`BingPluginDependencyDescriptor`、`IBingPluginContainer` | 入口程序集加载、插件描述符及直接版本依赖冻结、启动模块并入统一模块图、插件程序集自动扫描 | `PluginLoadingTest.AddBingApplication_ShouldLoadPluginAndScanSelectedPluginAssembly`；`PluginLoadingTest.UseBing_ShouldInitializePluginModules`；`PluginDependencyGraphTest.AddFolder_ShouldOrderPluginsByDependencyBeforeOrdinalName` |
| `BingPluginLoader.ParseManifest` | JSON、版本、入口程序集路径及启动模块清单校验 | `PluginManifestValidationTest.AddBingApplication_InvalidManifest_ShouldFailBeforeModuleConstruction`；`PluginManifestValidationTest.AddBingApplication_InvalidVersion_ShouldFailManifestValidation`；`PluginManifestValidationTest.AddBingApplication_EntryAssemblyOutsideDirectory_ShouldFailPathValidation`；`PluginManifestValidationTest.AddBingApplication_UnknownStartupModule_ShouldFailWithPluginContext` |
| `BingPluginLoader.OrderPlugins`、`FindPluginCycle`、`FindDependencyPath`、`ModuleDependencyGraph.Build` | 插件依赖优先、精确版本依赖、入口模块内部环、嵌套缺失依赖和完整环路径诊断 | `PluginDependencyGraphTest.AddFolder_ShouldOrderPluginsByDependencyBeforeOrdinalName`；`PluginDependencyGraphTest.AddBingApplication_MissingPluginDependency_ShouldFailWithDependencyPath`；`PluginDependencyGraphTest.AddBingApplication_NestedMissingPluginDependency_ShouldFailWithCompletePath`；`PluginDependencyGraphTest.AddBingApplication_PluginDependencyCycle_ShouldFailWithCyclePath`；`PluginDependencyGraphTest.AddBingApplication_NestedPluginDependencyCycle_ShouldIncludePrefix`；`PluginDependencyGraphTest.AddBingApplication_PluginModuleDependencyCycle_ShouldFailBeforeConstruction` |
| `BingPluginLoader.BuildAssemblyCatalog/LoadAssembly` | 相同完整程序集身份在多个插件目录间复用，插件入口程序集参与模块图 | `PluginDependencyGraphTest.AddFolder_ShouldOrderPluginsByDependencyBeforeOrdinalName` |

| `BingModuleRegistration.SetPlugins/ReleaseAsync`、`BingPluginContainer`、`IBingPluginContainer` | 插件描述符属于当前服务集合，显式关闭后清空且重复关闭不重复释放 | `PluginResourceRegressionTest.ShutdownBing_ShouldClearPluginDescriptors` |
| `BingPluginLoader.Load`、`BingModuleRegistration.FailConfiguration` | 失败注册不保留插件来源或失败上下文的强引用 | `PluginResourceRegressionTest.FailedRegistration_ShouldNotRetainPluginSource` |

## Round 11 插件验收补充映射

| 生产符号 | 关键行为 | 直接测试 |
| --- | --- | --- |
| `BingModuleManager.InitializeAsync`、`BingModuleManager.CleanupAsync`、`BingModuleRegistration.ReleaseAsync` | 插件异步初始化优先、取消后异步关闭、资源只释放一次、同步入口拒绝异步模块 | `PluginLifecycleTest.UseBingAsync_ShouldRunPluginAsyncLifecycleAndDisposeOnce`；`PluginLifecycleTest.UseBingAsync_Cancellation_ShouldShutdownAndReleasePluginOnce` |
| `BingPluginLoader.BuildAssemblyCatalog` | 相同简单程序集名的不同完整身份在加载前失败 | `PluginDependencyGraphTest.AddBingApplication_AssemblyIdentityConflict_ShouldFailBeforeModuleLoading` |
| `BingPluginLoader.Load` | 成功和异常路径均退订默认加载上下文解析回调 | `PluginResourceRegressionTest.SuccessfulRegistration_ShouldUnsubscribeAssemblyResolver`；`PluginResourceRegressionTest.FailedRegistration_ShouldUnsubscribeAssemblyResolver` |
| `BingPluginLoader.ParseManifest` | 必填字段、启动模块集合和入口文件缺失均在模块构造前失败 | `PluginManifestValidationTest.AddBingApplication_MissingRequiredManifestField_ShouldFail`；`PluginManifestValidationTest.AddBingApplication_MissingStartupModules_ShouldFailManifestValidation`；`PluginManifestValidationTest.AddBingApplication_MissingPluginDependencyField_ShouldFail`；`PluginManifestValidationTest.AddBingApplication_MissingEntryAssembly_ShouldFailManifestValidation` |
| `BingModuleLoader`、`ModuleDependencyGraph` | 插件菱形依赖共享节点只出现一次且依赖先于使用方 | `PluginDependencyGraphTest.AddFolder_ShouldOrderPluginsByDependencyBeforeOrdinalName` |
| `BingPluginLoader`、`BingModuleManager`、`BingModuleRegistration` | 失败注册后的服务集合、选项、插件来源和异常上下文可回收 | `PluginResourceRegressionTest.FailedRegistration_ShouldReleaseApplicationGraph` |
| `BingPluginLoader`、`BingModuleRegistration`、`BingApplicationOptions` | 两个服务集合的插件服务扫描、前置配置值和生命周期状态相互隔离 | `PluginDependencyGraphTest.AddBingApplication_TwoServiceCollections_ShouldIsolatePluginServicesAndConfiguration` |
| `BingApplicationBuilderExtensions.UseBingAsync`、`BingModuleHostedService.StopAsync` | 插件 Web 异步初始化收到实际应用构建器，Generic Host 停止关闭插件 | `PluginWebLifecycleTest.UseBingAsync_ShouldInitializeWebPlugin`；`PluginWebLifecycleTest.HostStop_ShouldShutdownWebPlugin` |
| `BingPluginLoader` 异常上下文和程序集解析 | 清单 ID、程序集、入口类型、完整插件依赖链写入异常数据；已加载程序集按完整身份复用或拒绝 | `PluginManifestValidationTest.AddBingApplication_InvalidVersion_ShouldFailManifestValidation`；`PluginManifestValidationTest.AddBingApplication_InvalidStartupModuleType_ShouldFailWithPluginContext`；`PluginDependencyGraphTest.AddBingApplication_MissingPluginDependency_ShouldFailWithDependencyPath`；`PluginDependencyGraphTest.AddBingApplication_AssemblyIdentityConflict_ShouldFailBeforeModuleLoading`；`PluginResourceRegressionTest.LoadedAssemblyIdentityMismatch_ShouldFailResolver` |

## 可回收插件代补充映射

| 生产符号 | 关键行为 | 直接测试 |
| --- | --- | --- |
| `BingPluginLoader.Load/BuildAssemblyCatalog/LoadAssembly`、`PluginAssemblyResolver` | 独立上下文加载同名不同版本程序集，插件服务进入可回收上下文；默认入口不复用动态上下文程序集，宿主契约程序集保持类型身份 | `HotPluginHostTest.ReloadAsync_ShouldReplaceAssemblyVersionAndCollectOldContext`；`HotPluginHostTest.ReloadAsync_CopiedHostContracts_ShouldShareContractIdentity`；`HotPluginHostTest.ReloadAsync_ThenStaticRegistration_ShouldKeepDefaultContextIsolated` |
| `BingHotPluginHost<TStartupModule>.ReloadAsync` | 新代初始化成功后切换；候选失败时旧代继续服务；插件依赖排序和旧代关闭 | `HotPluginHostTest.ReloadAsync_FailedCandidate_ShouldKeepCurrentGeneration`；`HotPluginHostTest.ReloadAsync_ShouldInitializeDependencyGraphAndCloseOldModules` |
| `BingHotPluginHost<TStartupModule>.RunAsync`、`Generation.Acquire/Release/Retire` | 活动调用持有旧代，退出后完成关闭；重入重新加载和关闭立即失败 | `HotPluginHostTest.ReloadAsync_ShouldWaitForActiveCallBeforeClosingOldGeneration`；`HotPluginHostTest.RunAsync_ReentrantReloadOrDispose_ShouldFailWithoutDeadlock` |
| `BingHotPluginHost<TStartupModule>.DisposeAsync`、`Generation.CloseAsync`、`BingHotPluginInfo` | 停止模块、释放容器、请求卸载且只公开不含程序集引用的信息快照 | `HotPluginHostTest.DisposeAsync_ShouldUnloadCurrentContextAndRejectCalls`；`HotPluginHostTest.ReloadAsync_ShouldInitializeDependencyGraphAndCloseOldModules` |

## 插件版本约束补充映射

| 生产符号 | 关键行为 | 直接测试 |
| --- | --- | --- |
| `BingPluginLoader.ParseVersion/ParseVersionRequirement` | 三段、四段版本兼容；非法区间在模块构造前失败 | `PluginManifestValidationTest.AddBingApplication_InvalidVersion_ShouldFailManifestValidation`；`PluginManifestValidationTest.AddBingApplication_InvalidDependencyRange_ShouldFailManifestValidation` |
| `BingPluginLoader.OrderPlugins`、`PluginDependencyRecord` | 精确版本兼容，NuGet 区间边界与预发布准入判定，错误保留依赖链 | `PluginDependencyGraphTest.AddBingApplication_PluginDependencyRange_ShouldRespectBounds`；`PluginDependencyGraphTest.AddBingApplication_PrereleaseDependency_ShouldRequireExplicitOptIn` |
| `BingPluginDescriptor.VersionText`、`BingPluginDependencyDescriptor.VersionRequirement` | 完整版本与原始约束可观察；数值版本属性保持旧形态 | `PluginDependencyGraphTest.AddBingApplication_PluginDependencyRange_ShouldRespectBounds`；`PluginDependencyGraphTest.AddBingApplication_PrereleaseDependency_ShouldRequireExplicitOptIn` |
| `BingHotPluginInfo.VersionText` | 热更新后的轻量快照保留预发布和构建文本 | `HotPluginHostTest.ReloadAsync_PrereleaseVersion_ShouldPreserveFullVersionText` |

## Round 16 模块完整生命周期补充映射

| 生产符号 | 关键行为 | 直接测试 |
| --- | --- | --- |
| `BingModule` 初始化、关闭虚钩子；`BingModuleHooks.InitializeAsync/ShutdownAsync` | 全模块 Pre/Main/Post 顺序、继承重写识别、同名隐藏方法不抢占旧回调、失败或取消后逆序关闭 | `ModulePhasedLifecycleTest.Initialization_RunsGlobalPhasesInDependencyOrder`；`ModulePhasedLifecycleTest.HiddenMethod_DoesNotReplaceLegacyInitialization`；`ModulePhasedLifecycleTest.PreInitializationFailure_StopsStartedModuleOnly`；`ModulePhasedLifecycleTest.PostInitializationFailure_StopsAllStartedModulesInReverseOrder`；`ModulePhasedLifecycleTest.InitializationPhaseCancellation_StopsAndReleasesModule` |
| `BingModuleManager.InitializeCoreAsync` | 三阶段共享短期作用域并在成功返回前释放；Web 上下文跨阶段共享且主阶段装配管道 | `ModulePhasedLifecycleTest.InitializationPhases_ShareScopeAndDisposeItBeforeReturning`；`WebModuleLifecycleTest.UseBing_WebPhases_ShouldShareApplicationBuilderAndComposePipeline` |
| `BingModule` 服务配置虚钩子；`BingModuleHooks.ConfigureAsync/RequiresAsyncConfiguration`；`AddBingApplicationAsync` | 同步预检拒绝异步重写，异步服务配置和初始化各执行一次 | `ModulePhasedLifecycleTest.AsyncServiceHook_RequiresAsyncRegistration_AndRunsOnce` |
| `BingApplicationServiceCollectionExtensions.BeginRegistration`；`BingModuleRegistration.BeginConfiguration/EnsureBuildable/FailConfigurationAsync` | 配置期间拒绝重入和提前构建；各阶段失败或取消后释放一次且禁止重试 | `ModulePhasedLifecycleTest.AsyncRegistration_InProgress_RejectsReentryAndEarlyBuild`；`ModulePhasedLifecycleTest.AsyncRegistration_Cancellation_ReleasesModuleAndPreventsRetry`；`ModulePhasedLifecycleTest.ServiceConfigurationFailure_StopsLaterStagesAndReleasesModule`；`ModulePhasedLifecycleTest.AsyncServiceConfigurationCancellation_ReleasesModule` |
| `BingServiceRegistrationMode`；`DependencyModule.RegisterConventionServices`；`BingLoader.RegisterTypes` | 提前模式在主配置前扫描；兼容模式保留旧顺序；Post 可替换服务；关闭自动 DI 时事件仍触发一次；选项类型晚注册仍补扫选中程序集 | `ModulePhasedLifecycleTest.ServiceRegistrationMode_ControlsScanTiming`；`ModulePhasedLifecycleTest.EarlyScan_WithAutoRegistrationDisabled_StillRaisesTypeEvent`；`ModuleScanningTest.AddBingApplication_ShouldScanSelectedAssembly_AndIgnoreUnselectedAssembly` |
| `BingHotPluginHost.BuildGenerationAsync` | 候选异步注册与三阶段初始化完成前不切换旧代；Pre/Post 失败候选上下文可回收 | `HotPluginHostTest.ReloadAsync_InitializationPhaseFailure_ShouldKeepCurrentGeneration` |
| `BingBuilder.AddModule` | 旧入口保持即时注册，并明确拒绝新服务配置钩子以提示迁移 | `ModulePhasedLifecycleTest.LegacyEntry_RejectsNewServiceHooksWithMigrationMessage`；`ModuleRegistrationTest.LegacyEntry_ShouldConfigureModuleImmediately_AndBeIdempotent` |

## Review Round 17 生命周期缺口修复映射

| FIX | 最终生产符号 | 关键行为 | 直接测试 |
| --- | --- | --- | --- |
| FIX-013 | `BingModuleManager.InitializeCoreAsync/CleanupAsync` | 按冻结模块序列逆序关闭已开始模块，混合 Pre 钩子和 Post 故障时依赖仍可用 | `ModuleLifecycleReviewFixTest.MixedPreHooks_ShutdownInReverseModuleOrder` |
| FIX-014 | `BingModuleHooks.Overrides/RequiresAsyncConfiguration/RequiresAsyncInitialization/RequiresAsyncShutdown` | 中间基类真实异步重写不会被派生类 new 同名方法遮蔽，同步入口在副作用前拒绝 | `ModuleLifecycleReviewFixTest.HiddenAsyncConfiguration_StillRequiresAsyncRegistration`；`ModuleLifecycleReviewFixTest.HiddenAsyncInitialization_RejectsSyncBeforeAnyPreHook`；`ModuleLifecycleReviewFixTest.HiddenAsyncPreAndPostHooks_ExecuteInheritedVirtualSlots`；`ModuleLifecycleReviewFixTest.HiddenAsyncShutdown_StillRequiresAsyncShutdown` |
| FIX-015 | `BingApplicationServiceCollectionExtensions.AddBingApplicationAsync/ConfigureModulesAsync/ConfigureAsync` | 每个回调及扫描结束后检查取消；取消后注册失败、等待异步释放并拒绝复用 | `ModuleLifecycleReviewFixTest.ConfigurationCallbackCancelsAndReturns_RegistrationFails`；`ModuleLifecycleReviewFixTest.RegistrationEventCancels_RegistrationFails`；`ModuleLifecycleReviewFixTest.CanceledRegistration_AwaitsAsyncModuleDisposal`；`HotPluginHostTest.ReloadAsync_AsyncConfigurationCancellationAtAnyPhase_ShouldKeepCurrentGenerationAndCollectCandidate` |
| FIX-016 | `BingModule.PreConfigureServices/PostConfigureServices/OnApplicationInitializationAsync/OnApplicationShutdown/OnApplicationShutdownAsync`、`BingModuleHooks.HasIndependentInterfaceImplementation` | 显式 base 续接旧接口而不重复或递归；未调用 base 则替代旧接口；隐式映射不重入 | `ModuleLifecycleReviewFixTest.ExplicitBaseCalls_BridgeOldInterfacesOnce`；`ModuleLifecycleReviewFixTest.NewHooksWithoutBase_ReplaceOldInterfaces`；`ModuleLifecycleReviewFixTest.ImplicitInterfaceMapping_BaseCallDoesNotReenterHook`；`ModuleLifecycleReviewFixTest.SyncShutdownBaseCall_BridgesOldInterfaceOnce` |
| FIX-017 | `BingHotPluginHost.BuildGenerationAsync`、`BingModuleHooks.InitializeAsync/ShutdownAsync` | 候选异步配置失败/取消保留旧代；Post 未完成不切换；Web 新异步阶段及 Host Stop 等待新关闭重写 | `HotPluginHostTest.ReloadAsync_AsyncConfigurationFailureAtAnyPhase_ShouldKeepCurrentGenerationAndCollectCandidate`；`HotPluginHostTest.ReloadAsync_AsyncConfigurationCancellationAtAnyPhase_ShouldKeepCurrentGenerationAndCollectCandidate`；`HotPluginHostTest.ReloadAsync_PendingPostInitialization_ShouldKeepCurrentGenerationUntilCandidateCompletes`；`WebModuleLifecycleTest.UseBingAsync_NewAsyncOverrides_ShouldShareApplicationBuilderAndComposePipeline`；`WebModuleLifecycleTest.HostStop_ShouldAwaitAsyncShutdownOverride` |

## Review Round 18 生命周期缺口修复映射

| FIX | 最终生产符号 | 关键行为 | 直接测试 |
| --- | --- | --- | --- |
| FIX-016 | `BingModuleHooks.ConfigureAsync/InitializeAsync/ShutdownAsync`、`BingModule` 基类桥接 | 旧接口直接分派后调用 base 不重复执行；桥接状态在异常后恢复 | `ModuleLifecycleReviewRound18Test.LegacyInterfacesCallingBase_RunOnce`；`ImplicitLegacyInterfacesCallingBase_RunOnce`；`LegacyInterfaceFailure_DoesNotLeaveBridgeActive` |
| FIX-018 | `BingModuleManager.InitializeAsync/AwaitInitializationAsync/CompleteInitializationAsync`、`BingServiceProviderExtensions.UseBingAsync`、`BingApplicationBuilderExtensions.UseBingAsync` | Pre/Main/Post 取消返回已取消任务，同时保留原始异常实例、令牌、模块阶段和清理汇总；并发共享结果，Web 入口不丢诊断且旧初始化时机不变 | `ModuleLifecycleReviewRound18Test.InitializationCancellation_PreservesOriginalException`；`InitializationCancellation_WithShutdownError_PreservesCauseAndCleanup`；`ConcurrentInitializationCancellation_SharesOriginalException`；`PreCanceledInitialization_KeepsCanceledTaskWithoutStartingModules`；`ModuleLifecycleTest.Cancellation_CleansPartialInitialization_WithoutRetry`；`DisposingProviderDuringAsyncInitialization_CancelsAndCleansModules`；`WebModuleLifecycleTest.UseBingAsync_CancellationPreservesModuleCause` |
| FIX-017 | `BingModuleHooks.ConfigureAsync/InitializeAsync/ShutdownAsync`、`BingHotPluginHost.BuildGenerationAsync`、`BingModuleRegistration.ReleaseAsync/FailConfigurationAsync` | 同模块多种钩子按异步新重写优先；热候选 Pre/Main/Post 配置失败或取消时旧代继续服务，候选模块异步释放恰好一次并可回收；前配置失败等待异步释放门控 | `ModuleLifecycleReviewRound18Test.CombinedOverrides_AsyncWinsAndSyncPreflightHasNoSideEffects`；`HotPluginHostTest.ReloadAsync_AsyncConfigurationFailureAtAnyPhase_ShouldKeepCurrentGenerationAndCollectCandidate`；`ReloadAsync_AsyncConfigurationCancellationAtAnyPhase_ShouldKeepCurrentGenerationAndCollectCandidate` |

## Review Round 21 FIX-017 释放计数补充映射

| 最终测试符号 | 关键行为 | 直接测试 |
| --- | --- | --- |
| 热插件释放计数夹具 / `HotPluginHostTest` | 每次进入 `DisposeAsync` 均计数；直接连续调用两次可观察到 2 次，证明夹具能检出重复释放 | `HotPluginHostTest.ConfigurationDisposalFixture_ShouldRecordEveryInvocation` |
| `BingHotPluginHost.BuildGenerationAsync`、候选模块异步释放 | Pre/Main/Post 配置失败与取消共六个场景中，框架释放计数均为 1；失败候选不替换旧代，保留异步释放门控及候选 ALC 回收断言 | `HotPluginHostTest.ReloadAsync_AsyncConfigurationFailureAtAnyPhase_ShouldKeepCurrentGenerationAndCollectCandidate`；`HotPluginHostTest.ReloadAsync_AsyncConfigurationCancellationAtAnyPhase_ShouldKeepCurrentGenerationAndCollectCandidate` |

