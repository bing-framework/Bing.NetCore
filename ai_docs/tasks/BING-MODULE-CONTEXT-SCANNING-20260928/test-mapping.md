# 生产符号到测试方法映射

测试项目除注明外均为 `framework/tests/Bing.Core.Tests`，在 `net8.0` 和 `net6.0` 执行。

| 生产符号或路径 | 关键行为 | 直接测试方法 |
| --- | --- | --- |
| `ServiceCollectionConfigurationExtensions.GetConfiguration`、`GetConfigurationOrNull` | 最后一次实例注册优先，保留 `IConfiguration` 接口类型；缺失时区分必需和可选读取 | `ModuleContextAccessTest.GetConfiguration_UsesLastDirectInstanceAndPreservesInterfaceType`、`GetConfiguration_FallsBackToHostBuilderContextInstance`、`GetConfiguration_InvalidLastRegistration_ShouldNotRunFactoryOrUsePreviousInstance` |
| `ServiceCollectionConfigurationExtensions.ReplaceConfiguration` | 替换后返回新实例 | `ModuleContextAccessTest.ReplaceConfiguration_UsesReplacementAndMissingConfigurationIsExplicit` |
| `ServiceCollectionHostEnvironmentExtensions.GetHostEnvironment`、`GetHostEnvironmentOrNull` | 直接实例优先；缺少直接注册时回退到最后一个宿主构建上下文；不执行工厂 | `ModuleContextAccessTest.GetHostEnvironment_UsesLastInstanceAndDoesNotRunFactory`、`GetHostEnvironment_FallsBackToLastHostBuilderContext`、`GetHostEnvironment_DirectRegistrationPrecedenceAndInvalidRegistration`、`GetHostEnvironment_FallbackDoesNotExecuteFactoriesAndMissingEnvironmentThrows` |
| `BingModuleContextExtensions.GetConfiguration`、`GetConfigurationOrNull`（初始化、关闭重载） | 使用当前上下文作用域解析，支持配置 reload、宿主隔离和缺失语义 | `ModuleContextAccessTest.ModuleContexts_ResolveScopedServicesFromCurrentProvider`、`ModuleContexts_MissingServices_ReturnNullOrThrow`、`ModuleLifecycle_UsesCurrentContextAcrossAllStages` |
| `BingModuleContextExtensions.GetHostEnvironment`、`GetHostEnvironmentOrNull`（初始化、关闭重载） | 使用当前上下文作用域解析，不缓存宿主环境 | `ModuleContextAccessTest.ModuleContexts_ResolveScopedServicesFromCurrentProvider`、`ModuleContexts_MissingServices_ReturnNullOrThrow`、`ModuleLifecycle_UsesCurrentContextAcrossAllStages` |
| `BingModuleAssemblyAttribute`、`BingModuleDescriptor.Assemblies` | 继承声明生效，未选模块不扩大扫描范围，所选模块暴露不可修改的程序集快照 | `ModuleScanningTest.ModuleAssemblyDeclaration_ShouldScanSelectedModulesOnly`、`UnselectedModuleDeclaration_ShouldNotScanItsUniqueAssembly`、`HotPluginHostTest.ReloadAsync_AdditionalAssembly_ShouldRegisterServiceAndUnloadWithGeneration` |
| `BingApplicationOptions.ServiceScanning`、`BingServiceScanningOptions.AdditionalAssemblies` | 应用附加程序集加入扫描目录、重复去重、跨代程序集被拒绝 | `ModuleScanningTest.ModuleAssemblyDeclaration_ShouldScanSelectedModulesOnly`、`AdditionalAssemblyFromForeignContext_ShouldBeRejected` |
| `BingServiceScanningOptions.ConventionalTypeFilter`、`DependencyTypeFinder.CompleteRegistration`、`BingApplicationServiceCollectionExtensions.CompleteDependencyScan` | 仅筛选约定 DI；注册结束释放框架原查找器的委托，追加其他查找器时仍保留原筛选结果；成功、异常、异步取消可回收捕获对象；关闭自动 DI 时不调用筛选器 | `ModuleScanningTest.ConventionalFilter_ShouldOnlyAffectAutomaticServices`、`ConventionalFilter_ShouldReleaseCapturedObject`、`ConventionalFilter_WithAppendedFinder_ShouldReleaseCapturedObject`、`DisabledAutomaticRegistration_ShouldKeepOptionsAndSkipFilter`、`ConventionalFilterFailure_ShouldRejectRegistration` |
| `BingApplicationServiceCollectionExtensions.ConfigureModulesAsync` | 注册时机在配置回调后冻结，Pre/Post 修改原选项不重复或漏发类型事件，追加的程序集不进入本轮扫描 | `ModuleScanningTest.RegistrationModeMutation_ShouldNotRepeatOrSkipTypeEvents` |
| `BingModuleAssemblyFinder`、`BingLoader.FindTypes` | 冻结目录共享给约定 DI、选项与公开类型事件；重复程序集去重；自定义加载器和晚选项补扫沿用同一目录 | `ModuleScanningTest.ModuleAssemblyDeclaration_ShouldScanSelectedModulesOnly`、`AddBingApplication_ShouldScanSelectedAssembly_AndIgnoreUnselectedAssembly` |
| `BingModuleAssemblyFinder` 的类型发现失败诊断、`BingPluginLoader.Load` 的错误归属 | 附加类型依赖缺失时报告扫描阶段、程序集、声明模块、插件及清单 | `HotPluginHostTest.ReloadAsync_AdditionalTypeDependencyMissing_ShouldDiagnoseAndCollectCandidate` |
| `BingPluginLoader.Load` 的临时解析会话 | 插件私有附加程序集进入本代扫描；同名不同版本的附加 DLL 随插件代切换；失败候选和旧代上下文可回收 | `HotPluginHostTest.ReloadAsync_AdditionalAssembly_ShouldRegisterServiceAndUnloadWithGeneration`、`ReloadAsync_AdditionalAssemblyVersions_ShouldSwitchAndCollectOldContext`、`ReloadAsync_MissingAdditionalAssembly_ShouldKeepCurrentGeneration`、`ReloadAsync_AdditionalTypeDependencyMissing_ShouldDiagnoseAndCollectCandidate` |
| `IPreConfigureServices`、`IPostConfigureServices`、`IOnPreApplicationInitialization`、`IOnPostApplicationInitialization` 与 `BingModuleHooks` | 显式与隐式接口分派、全局依赖顺序、重写优先、新旧配置接口优先级、旧入口兼容及初始化失败清理 | `ModuleNamedPhaseInterfacesTest.ExplicitInterfaces_RunOnceInDependencyOrder`、`ImplicitInterfaces_RunAllNamedPhases`、`Overrides_ReplaceNamedInterfaces`、`NewInterfacesCallingBase_RunOnceAndTakePriority`、`LegacyRegistration_RejectsNamedConfigurationInterfaces`、`LegacyRegistration_RunsNamedInitializationInterfaces`、`ExplicitPreInitializationFailure_ShutsDownStartedModule` |

`ModuleScanningTest` 中扫描模式参数均覆盖 `Compatible` 与 `BeforeConfigureServices`；附加程序集的选项注册同时覆盖早注册与晚注册。现有 Core、MVC 和 WebApi 回归覆盖旧入口及 Web 宿主路径。

## FIX-006 / FIX-007 直接回归映射

| 最终生产符号 | 关键行为 | 测试项目与方法 |
| --- | --- | --- |
| `BingModuleRegistration.ReleaseAsync`、`RemoveOwnedModuleDescriptors` | 真实只读集合下释放一次，清空插件描述符；可变集合移除失败仍清内部引用 | MVC `WebModuleResourceReleaseTest.ShutdownBingAsync_AfterWebBuild_ShouldReleaseModuleAndClearPluginsOnce`、`StopAsync_AfterWebBuild_ShouldReleaseModuleAndClearPluginsOnce`；Core `ModuleDescriptorCleanupFailureTest.InitializationFailure_WhenRemoveAtThrows_ShouldPreserveCauseAndFinishCleanup` |
| `BingModuleManager.InitializeCoreAsync` | 保留原始异常、附加清理错误、进入 Failed 并禁止重试；重复关闭/释放 | MVC `WebModuleResourceReleaseTest.UseBingAsync_FailureAfterWebBuild_ShouldPreserveCauseAndFinishCleanup`；Core `ModuleDescriptorCleanupFailureTest.InitializationFailure_WhenRemoveAtThrows_ShouldPreserveCauseAndFinishCleanup` |
| `BingPluginLoader.Load`、`PreloadPrivateDependencies` | 默认上下文预加载方法体私有依赖和传递依赖；配置/初始化/服务调用成功，DI 扫描范围不扩大 | Core `PluginPrivateDependencyTest.DefaultContext_ShouldLoadPrivateClosureWithoutScanningIt` |
| `BingPluginLoader.PreloadPrivateDependencies` 与热更新清理 | 私有程序集属于本代，旧代与失败候选可回收；失败不替换旧代 | Core `PluginPrivateDependencyTest.HotContext_ShouldLoadPrivateClosureAndCollectOldGeneration`、`FailedCandidate_ShouldCollectPrivateClosureAndKeepOldGeneration` |
| `BingPluginLoader.Load` 解析事件及身份校验 | 成功/失败退订，缺失与冲突仍带诊断 | Core `PluginResourceRegressionTest.SuccessfulRegistration_ShouldUnsubscribeAssemblyResolver`、`FailedRegistration_ShouldUnsubscribeAssemblyResolver`、`LoadedAssemblyIdentityMismatch_ShouldFailResolver`；`PluginDependencyGraphTest.AddBingApplication_AssemblyIdentityConflict_ShouldFailBeforeModuleLoading`；`HotPluginHostTest.ReloadAsync_AdditionalTypeDependencyMissing_ShouldDiagnoseAndCollectCandidate` |

以上方法均包含在本轮双框架完整回归中；新增独立夹具项目仅服务于测试，无新增生产依赖。

## 插件热更新状态与失败诊断

| 最终生产符号 | 关键行为 | 测试项目与方法 |
| --- | --- | --- |
| `BingHotPluginHost.Status`、`BingHotPluginHostStatus.Current`、`BingHotPluginGenerationStatus.Id`、`Plugins` | 同版本切换使用新代 ID；快照不持有插件程序集，旧代可回收 | Core `HotPluginObservabilityTest.ReloadAsync_SameVersion_ShouldChangeGenerationIdAndCollectOldContext` |
| `BingHotPluginHostStatus.Retiring`、`BingHotPluginGenerationStatus.ActiveCalls`、`State`、`IsRetiring`、`BingHotPluginGenerationState` | 正在运行的旧代显示 Draining 和活动调用数；进入关闭钩子显示 Closing，结束后移除退役代 | Core `HotPluginObservabilityTest.ReloadAsync_ActiveCall_ShouldExposeRetiringGenerationUntilCallEnds`、`ReloadAsync_PendingShutdown_ShouldExposeClosingGeneration` |
| `BingHotPluginReloadFailureInfo.FromException`、`AttemptId`、`PreviousGenerationId`、`CurrentGenerationId`、`Phase`、`Switched`、`BingHotPluginReloadPhase` | 保留原异常类型，区分等待取消、候选失败和已切换后的旧代关闭失败 | Core `HotPluginObservabilityTest.ReloadAsync_CanceledBeforeTurn_ShouldReportUnchangedGeneration`、`ReloadAsync_FailedCandidate_ShouldReportUnchangedGeneration`、`ReloadAsync_OldShutdownFailure_ShouldReportSwitchedGeneration` |
| `BingHotPluginHost.ReloadAsync`、`Generation.Snapshot`、`Generation.MarkClosing` | 新旧代状态变更与切换结果采用同一代 ID；旧代排空期间仍可用新代服务 | Core `HotPluginObservabilityTest.ReloadAsync_ActiveCall_ShouldExposeRetiringGenerationUntilCallEnds`、`ReloadAsync_PendingShutdown_ShouldExposeClosingGeneration`、`ReloadAsync_OldShutdownFailure_ShouldReportSwitchedGeneration` |

上述新测试在 `net8.0` 与 `net6.0` 各执行 7 个案例；宿主状态快照只含不可修改的标识、计数、枚举和插件 ID/版本信息。
