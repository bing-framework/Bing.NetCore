<!-- AI_REVIEW_STATUS: PASS_WITH_ISSUES -->
AI_TASK_ID: BING-MODULARITY-HARDENING-20260927
AI_REVIEWED_AT: 2026-09-27T21:13:22.9211352+08:00

# 模块化功能与安全审查

## 1. 审查结论

本轮对照 `plan.md`、`execution.md`、当前生产源码、Git Diff、测试映射和最近一次双目标框架验证结果，重点检查模块依赖、扩展入口、并发生命周期、宿主隔离、静态引用和资源释放路径。

当前没有发现已证实的远程可利用漏洞。Round 5 已解除 `BingModuleRegistration`、模块描述符、模块服务描述符及旧入口 `BingBuilder` 对已释放模块和根容器的引用；保留原 `IServiceCollection` 的正常关闭、配置失败、`ValidateOnBuild` 失败和旧入口关闭场景均通过弱引用回收测试。

Round 6 已关闭剩余的两个 `SHOULD_FIX`：公开模块类型判断已与依赖图的真实加载约束统一；启动日志输出失败分支已有直接测试固定原始异常优先级、附加日志异常、缓存清空、模块释放和失败后禁止重试。当前未发现新的可执行缺陷，状态为 `PASS_WITH_ISSUES`，`OPEN_ACTIONABLE=0`；已接受边界和延期范围保持不变。

Round 9 新增的自定义加载器异常契约测试有效，两个目标框架的精准 TRX 均为 23/23 通过，且明确包含空目录和空描述符用例。FIX-012 后续已同步最终证据清单的路径、生成时间、分批 SDK 和 WebApi 构建统计；当前生产实现、测试和证据状态均通过，整体审查状态为 `PASS_WITH_ISSUES`，`OPEN_ACTIONABLE=0`。

安全结论仅适用于启动期组合受信任模块。模块构造函数、依赖提供程序、`AddServices` 和生命周期钩子都能执行任意进程内代码；当前实现不提供不可信插件隔离、热卸载或模块级安全沙箱。

## 2. Findings

### FIX-003：释放后注册记录仍强引用根容器和模块实例

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 状态：CLOSED
- 修复证据：`BingModuleRegistration.ReleaseAsync` 在引用身份去重并完成逆序释放后，解绑旧静态宿主，清除旧入口 builder 的 `_instances`、`_registrationOrder` 和 `_modules`，将 `_provider` 置空，清除每个描述符的实例字段，移除直接持有模块实例的服务描述符，并清空 `_owned`、`_released`。`BingModuleDescriptor` 独立缓存类型和程序集，清除实例后公开元数据仍可读取。
- 代码位置：`framework/src/Bing.Core/Bing/Core/Builders/BingBuilder.cs:61-72`、`framework/src/Bing.Core/Bing/Core/Modularity/BingModuleDescriptor.cs:14-60`、`framework/src/Bing.Core/Bing/Core/Modularity/BingModuleRegistration.cs:205-269`。
- 直接测试：`RetainedServiceCollection_NormalShutdown_AllowsProviderAndModuleCollection`、`RetainedServiceCollection_ConfigurationFailure_AllowsModuleCollection`、`RetainedServiceCollection_ValidateOnBuildFailure_AllowsModuleCollection`、`RetainedLegacyServiceCollection_NormalShutdown_AllowsModuleCollection` 均保留服务集合并验证 provider/module 弱引用可回收；旧入口测试还直接断言 `builder.Modules` 已清空。
- 并发与重复释放判断：模块先在 `_gate` 内加入引用身份集合 `_released`，再在锁外释放；公开关闭入口由 manager 共享正在执行的关闭任务。现有重复关闭、并发关闭和相等性重写模块测试继续通过，未发现重复释放路径。

### FIX-004：公开模块类型判断与实际加载契约不一致

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 状态：CLOSED
- 修复证据：`BingModule.IsBingModule` 现在要求具体类、非抽象、非开放泛型、派生自 `BingModule` 且具有公共无参构造函数，与 `ModuleDependencyGraph.ValidateModuleType` 的真实加载条件一致；XML 文案同步描述该契约。
- 代码位置：`framework/src/Bing.Core/Bing/Core/Modularity/BingModule.cs:41-68`、`framework/src/Bing.Core/Bing/Core/Modularity/ModuleDependencyGraph.cs:193-213`。
- 直接测试：`ModuleGraphTest.IsBingModule_ShouldMatchRuntimeLoadableContract` 覆盖可加载派生类、仅接口实现、开放泛型、无公共无参构造和抽象模块；既有 loader 非法根/依赖测试继续验证实际注册侧约束。

### FIX-005：启动日志失败清理缺少计划要求的直接测试

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 状态：CLOSED
- 修复证据：测试注入真实 `ILoggerFactory` 实现，在 `StartupLogger.Output` 解析 logger 时抛出指定异常；生产路径先保留模块初始化异常，再把日志异常附加到 `Bing.ModuleLogError`，随后执行模块清理并将状态置为 `Failed`。`StartupLogger.Output` 在解析或写入 logger 前清空缓存，因此日志工厂失败后缓存也不会继续持有对象。
- 代码位置：`framework/src/Bing.Core/Bing/Core/Modularity/BingModuleManager.cs:198-250`、`framework/src/Bing.Core/Bing/Logging/StartupLogger.cs:47-80`。
- 直接测试：`ModuleLifecycleTest.StartupLogFlushFailure_PreservesInitializationError_CleansModuleAndPreventsRetry` 断言原始异常消息和阶段、`Bing.ModuleLogError` 引用、空日志缓存、模块释放事件，并通过第二次 `UseBing` 失败及事件不重复验证禁止重试。

### FIX-011：自定义加载器空结果契约缺少直接测试

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 状态：CLOSED
- 修复证据：`NewEntry_ShouldRejectNullCustomLoaderResult` 通过返回 `null` 的真实 `IBingModuleLoader` 命中入口的空目录校验；`NewEntry_ShouldRejectNullModuleDescriptor` 通过包含 `null` 元素的目录命中描述符校验。两个测试均再次调用 `AddBingApplication`，验证第一次配置失败后服务集合不可复用。
- 代码位置：`framework/tests/Bing.Core.Tests/Modularity/ModuleRegistrationTest.cs:277-299`、`framework/src/Bing.Core/Microsoft/Extensions/DependencyInjection/BingApplicationServiceCollectionExtensions.cs:35-65`、`:85-105`。
- 验证证据：Round 9 的 net8.0/net6.0 `ModuleRegistrationTest` 精准 TRX 各 23/23 通过，两个新增方法在两份 TRX 中均为 `Passed`。

### FIX-012：Round 9 最终证据清单元数据不一致

- 严重程度：LOW
- 处理要求：SHOULD_FIX
- 状态：CLOSED
- 修复证据：清单中的 `finalArtifacts` 和 `supplementalArtifacts` 均使用从 `artifacts` 目录可解析的相对路径，复核的 10 个文件全部存在；`generatedAt` 为本地 21:11:54，晚于 Round 9 精准和全量 TRX；`sdkScope=finalArtifacts` 将 `8.0.424` 限定到 Round 8 最终门禁，`supplementalRuntime.sdk=8.0.419` 明确 Round 9 运行时；WebApi 构建同步为 1 个既有 `NETSDK1138` warning，来源指向 Round 8 执行记录。
- 证据位置：`ai_docs/tasks/BING-MODULARITY-HARDENING-20260927/artifacts/evidence-manifest.json:3-39`、`ai_docs/tasks/BING-MODULARITY-HARDENING-20260927/execution.md:542-574`。
- 复核结果：Round 9 Core 当前测试程序集全量 net8.0/net6.0 各 325/325 通过；精准 TRX 各 23/23 通过，两个 FIX-011 测试均为 `Passed`。

## 3. 功能清单

| 功能 | 当前实现 | 主要证据 |
| --- | --- | --- |
| 旧入口兼容 | `AddBing().AddModule<T>()` 保留立即执行 `AddServices` 和历史排序行为 | `BingBuilder.cs`、`BingServiceProviderExtensions.cs` |
| 新应用入口 | `AddBingApplication<TStartupModule>` 先构建完整模块图，再分阶段注册；禁止与旧入口混用 | `BingApplicationServiceCollectionExtensions.cs:26`、`BingModuleRegistration.cs:105` |
| 依赖图校验 | 支持 `IDependedTypesProvider`、非法类型校验、自环和多节点环检测、排除依赖冲突，并保留完整路径 | `ModuleDependencyGraph.cs:44-229` |
| 按需实例化 | 仅构造选中根模块及其依赖，未选模块不构造 | `BingModuleLoader.cs:15-52` |
| 稳定拓扑排序 | 依赖始终优先；无依赖约束的就绪节点按 `Level`、`Order`、类型全名 Ordinal 排序 | `BingModuleLoader.cs:28-32` |
| 扩展契约 | 提供应用选项、可替换加载器、只读模块容器/描述符和生命周期管理器 | `BingApplicationOptions.cs`、`BingModuleDescriptor.cs`、`BingModuleLifecycle.cs` |
| 分阶段配置 | 新入口按全局轮次执行前置配置、`AddServices`、后置配置 | `BingApplicationServiceCollectionExtensions.cs:56-61` |
| 自动 DI 扫描 | 默认只扫描已选模块所属程序集，可通过 `AutoRegisterServices` 关闭 | `DependencyModule.cs:37`、`BingApplicationOptions.cs:30` |
| 同步与异步初始化 | provider/Web 均支持同步与异步入口；异步钩子优先，单模块只走一条路径 | `BingModuleManager.cs:107-238`、`BingApplicationBuilderExtensions.cs:113` |
| 状态与并发控制 | 初始化幂等；失败后禁止重试；生命周期钩子禁止重入；非 Web 异步并发共享任务 | `BingModuleManager.cs:107-178` |
| 短期生命周期作用域 | 新入口在初始化和关闭期间创建短期作用域，回调完成后释放 | `BingModuleManager.cs:194-261`、`:337-384` |
| 关闭与异常聚合 | 按初始化逆序关闭；单个模块失败后继续清理；最终聚合异常 | `BingModuleManager.cs:318-389` |
| Host 停止集成 | `IHostedService.StopAsync` 转发到异步模块关闭流程 | `BingModuleRegistration.cs:239-263` |
| 模块资源所有权 | 框架创建的模块按引用身份只释放一次；构造、配置和初始化失败均执行清理 | `BingModuleRegistration.cs:18-235`、`BingModuleLoader.cs:34-69` |
| 纯异步资源 | 支持仅实现 `IAsyncDisposable` 的模块；同步失败路径等待异步释放 | `BingModuleDisposal.cs:8-16`、`BingModuleManager.cs:120-123` |
| 选项隔离 | 内置选项绑定按服务集合存储在弱键注册器中，不再依赖静态事件订阅 | `OptionsTypeRegistration.cs:15` |
| 静态兼容解绑 | 旧定位器按 services/provider 所有者解绑；解绑后查询安全返回 | `ServiceLocator.cs:35-90`、`BingModuleRegistration.cs:172-188` |
| 启动日志清理 | 转交正式日志后清空缓存；日志异常不遮蔽原始启动异常 | `StartupLogger.cs`、`BingModuleManager.cs:230-242` |
| 容器便捷入口 | `BuildBingServiceProvider` 预解析 manager，使未初始化模块进入容器释放链 | `BingServiceCollectionExtensions.cs:21-37` |

## 4. 已知边界

### LIMIT-001：原生裸容器无法保证释放未激活模块

- 严重程度：MEDIUM
- 状态：ACCEPTED_LIMITATION
- 触发条件：调用原生 `BuildServiceProvider()`，从未解析 manager、Hosted Service，也未调用初始化或显式关闭，然后直接释放根容器。
- 影响：注册期创建的模块不在 Microsoft DI 的实例追踪链中，模块自己的 Timer、事件订阅或非 DI 资源可能继续存活。
- 控制方式：使用 `BuildBingServiceProvider()`、`ShutdownBing/ShutdownBingAsync`，或 Generic Host 停止适配器。

### LIMIT-002：模块代码属于完全信任边界

- 严重程度：HIGH（加载不可信程序集时）
- 状态：ACCEPTED_LIMITATION
- 影响：模块可在构造、依赖枚举、服务注册、初始化和关闭阶段执行任意进程内代码，权限与宿主相同。
- 控制方式：只加载项目引用或 NuGet 提供的受信任模块。程序集扫描范围是发现边界，不是安全隔离。

### LIMIT-003：旧静态服务定位器只支持单宿主语义

- 严重程度：LOW
- 状态：ACCEPTED_LIMITATION
- 影响：同进程并行运行多个旧入口宿主时，全局定位器只能代表最后绑定的宿主。
- 控制方式：新入口不绑定静态定位器；多宿主应用通过生命周期上下文使用当前 provider。

### LIMIT-004：公开静态注册事件仍可能被外部订阅者保留

- 严重程度：LOW
- 状态：ACCEPTED_LIMITATION
- 影响：外部代码订阅 `BingLoader.RegisterType`、捕获应用对象且未退订时，会延长对象生命周期。
- 控制方式：框架自身不再通过该事件绑定选项；外部订阅者负责退订。

### LIMIT-005：生命周期 scoped 服务不能被模块长期保存

- 严重程度：MEDIUM（违反契约时）
- 状态：VERIFIED_BOUNDARY
- 影响：模块若在回调结束后继续保存上下文、短期 provider 或 scoped 服务，可能访问已释放对象或延长其生命周期。
- 控制方式：文档明确禁止；框架在钩子结束后释放作用域。

### DEFERRED-001：当前实现不支持可卸载动态插件

- 状态：DEFERRED
- 说明：未来若引入 collectible `AssemblyLoadContext`、插件目录或频繁动态装卸，需要重新设计程序集缓存、类型缓存和跨上下文引用；当前资源结论不能外推到该场景。

## 5. 未发现的问题

- 未发现依赖递归导致的栈溢出路径；当前使用显式栈并报告完整环路径。
- 未发现内置选项绑定继续通过静态事件持有服务集合或配置的路径。
- 未发现未选择模块被默认加载器实例化的路径。
- 未发现重复初始化再次执行模块钩子的路径。
- 未发现旧宿主关闭误清除新宿主静态绑定的路径。
- 未发现单个关闭异常中断其余模块清理的路径。
- 未发现同一模块被框架重复执行释放的路径。

“未发现”只针对当前改动和已检查调用链，不表示整个仓库或任意第三方模块不存在安全缺陷。

## 6. 计划验收矩阵

| 任务 | 状态 | 当前判断 |
| --- | --- | --- |
| T00 回归夹具与基线 | PASS | 故障模块隔离，Core/MVC 双 TFM 证据存在 |
| T01 依赖解析与校验 | PASS | 图解析、构造校验和公开 `IsBingModule` 的可加载类型契约一致；FIX-004 已关闭 |
| T02 选项静态保留链 | PASS | 集合所属弱键注册器、隔离、reload 和弱引用回收已验证 |
| T03 状态与资源所有权 | PASS | 状态、引用身份唯一释放和终态引用清理完成；FIX-003 已关闭 |
| T04 静态兼容层与日志 | PASS | 所有者解绑、正常日志清空及日志输出失败时的异常优先级与清理均有直接测试；FIX-005 已关闭 |
| T05 描述符、排序、扫描 | PASS | 新入口、可替换 loader、只读描述符、稳定拓扑和程序集边界完成 |
| T06 异步生命周期与宿主 | PASS | 同步/异步、取消、作用域、Web、Host Stop 和逆序关闭完成 |
| T07 资源证据与文档 | PASS | 资源场景、测试映射和 Round 9 最终证据清单均已核对；FIX-012 已关闭 |

## 7. 验证证据

Round 9 独立复审没有修改生产逻辑或测试。生产范围沿用 Round 8 最终门禁，Round 9 仅新增测试与证据文档：

| 验证项 | net8.0 | net6.0 |
| --- | --- | --- |
| Round 8 Core 模块化与旧 DI 定向 | 111 passed / 0 failed | 111 passed / 0 failed |
| Round 8 Core 全量测试 | 323 passed / 0 failed | 323 passed / 0 failed |
| MVC 全量测试 | 47 passed / 0 failed / 1 existing skipped | 47 passed / 0 failed / 1 existing skipped |
| Round 9 ModuleRegistration 精准测试 | 23 passed / 0 failed | 23 passed / 0 failed |
| Round 9 Core 当前测试程序集全量 | 325 passed / 0 failed | 325 passed / 0 failed |

Round 8 最终门禁和 Round 9 补充 TRX 路径均存在，计数与 `execution.md` 一致；两份 Round 9 精准 TRX 都明确记录 FIX-011 的两个测试为 `Passed`。`test-mapping.md` 的生产符号、行为和测试名称可追溯。证据清单中的路径、生成批次、SDK 归属和 WebApi 统计已与实际证据对齐。

## 8. 状态与 TODO

- IMPLEMENTATION_STATUS：PASS
- TEST_STATUS：PASS
- EVIDENCE_STATUS：PASS
- RESOURCE_EVIDENCE_STATUS：PASS
- PERFORMANCE_EVIDENCE_STATUS：NOT_APPLICABLE
- RELEASE_STATUS：NOT_ASSESSED
- MUST_FIX：0
- SHOULD_FIX：0
- OPEN_ACTIONABLE：0

Completed：FIX-011 自定义加载器空目录/空描述符契约测试与双 TFM 精准验证。

Open Actionable：无。

Accepted Limitations：LIMIT-001～LIMIT-004。Verified Boundary：LIMIT-005。Deferred：动态插件目录、运行中热插拔、collectible ALC、不可信插件进程隔离、模块版本依赖求解和完整 ABP 上层生态。

GOAL_STATUS：PASS_WITH_ISSUES
