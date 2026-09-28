# 模块上下文便捷访问与程序集扫描扩展评估计划

- Task ID：BING-MODULE-CONTEXT-SCANNING-20260928。
- 日期：2026-09-28；阶段：评估与规划，尚未实施。
- 前置成果：BING-MODULARITY-HARDENING-20260927 的 Round 22 审查通过；既有生命周期、插件及热更新作为兼容基线。
- 本计划独立保存，避免覆盖已经验收的原计划。用户本轮要求评估此前延期的前两项，并未要求立即实施。
- 规则：仓库 AGENTS.md、已读取的 GOAL_RULES.md 与 create-plan 技能；只创建本计划，不修改生产代码、测试、配置或既有执行报告。

## 1. 评估结论

建议先实施上下文便捷访问，再实施附加程序集和约定 DI 筛选。两者都是增量扩展，不需要重做模块管理器或引入新的应用容器。

| 能力 | 当前真实行为 | 缺口 | 建议范围 | 风险 |
| --- | --- | --- | --- | --- |
| 配置与环境便捷访问 | 配置阶段已有 services.GetConfiguration；初始化/关闭上下文已有短期 ServiceProvider | 缺少上下文统一的配置/环境便捷方法；配置辅助方法存在类型收窄和工厂执行问题 | 复用现有上下文与配置 API，新增环境及生命周期上下文扩展 | 中：现有配置辅助方法的错误行为修正需写迁移说明 |
| 附加程序集和扫描策略 | 应用入口用最终模块程序集替换 IAllAssemblyFinder；约定 DI、选项类型和公开类型事件消费该范围 | 不能明确声明模块的附加程序集；没有应用级约定服务类型筛选 | 声明附加程序集、冻结扫描目录、只对约定 DI 增加类型筛选 | 中高：影响 DI/选项/事件与可回收插件程序集身份 |

本轮不把这两项扩展描述为原生命周期计划的缺陷。第一项基础能力已经部分具备；第二项的默认扫描边界已经实现，新增的是可配置范围和筛选能力。

## 2. 源码依据与调用链

已确认文件均相对仓库根目录：

- `framework/src/Bing.Core/Microsoft/Extensions/DependencyInjection/ServiceCollectionConfigurationExtensions.cs`：已有 GetConfiguration/GetConfigurationOrNull/ReplaceConfiguration。HostBuilderContext 分支将 Configuration 强制收窄为 IConfigurationRoot，会丢掉合法的 IConfiguration 实现。
- `framework/src/Bing.Core/Microsoft/Extensions/DependencyInjection/ServiceCollectionCommonExtensions.cs`：GetSingletonInstanceOrNull 取第一个单例描述符，并可能调用 ImplementationFactory(null)。不能作为新增注册期便捷 API 的安全通用取值器。
- `framework/src/Bing.AspNetCore/Microsoft/Extensions/DependencyInjection/BingAspNetCoreServiceCollectionExtensions.cs`：已有 Web 专用环境访问，缺失时构造 Development 环境；不改成新通用环境 API 的默认行为。
- `framework/src/Bing.Core/Bing/Core/Modularity/BingModuleLifecycle.cs`：初始化及关闭上下文已有 ServiceProvider，初始化另有 HostContext；无需再存根容器或模块实例。
- `framework/src/Bing.Core/Microsoft/Extensions/DependencyInjection/BingApplicationServiceCollectionExtensions.cs`：Prepare 构建模块后将最终模块程序集交给 ModuleAssemblyFinder，随后分轮配置。
- `framework/src/Bing.Core/Bing/DependencyInjection/DependencyModule.cs`：RegisterConventionServices 调用 IDependencyTypeFinder，再执行原有生命周期、替换与接口注册规则。
- `framework/src/Bing.Core/Bing/BingLoader.cs`：RegisterTypes 扫描同一 IAllAssemblyFinder，触发选项绑定与公开事件；晚注册选项使用 RegisterOptionsTypes 补扫。
- `framework/src/Bing.Core/Bing/Core/Modularity/BingPluginLoader.cs`：仅在 Load 的 try/finally 内挂接依赖解析事件。附加程序集如果在该范围之后才首次请求，不能假设插件私有依赖仍能解析。
- `framework/src/Bing.PluginRuntime/Bing/Core/Modularity/BingHotPluginHost.cs`：每代有独立可回收上下文与 DI 容器；宿主对外只返回 ID/版本等不持有插件类型的快照。

当前没有必要增加新的全局 ServiceLocator、应用级根 IServiceProvider 属性或一套并行配置系统。现有 GetWebHostEnvironment 保留原行为；通用环境缺失不能默认为 Development。

## 3. 推荐 API 与明确语义

### 3.1 上下文便捷访问

保留所有现有生命周期方法及构造函数签名，不新增必须实现的接口成员，不新增生命周期阶段。

在初始化和关闭上下文上增加扩展方法：

```csharp
IConfiguration GetConfiguration();
IConfiguration GetConfigurationOrNull();
IHostEnvironment GetHostEnvironment();
IHostEnvironment GetHostEnvironmentOrNull();
```

以上为扩展方法的使用视图，实际签名分别接收 BingModuleInitializationContext/BingModuleShutdownContext。在 IServiceCollection 上只新增缺少的 GetHostEnvironment/GetHostEnvironmentOrNull，配置访问继续使用原有 GetConfiguration 系列，不另造同义入口。

运行期方法直接使用当前上下文的 ServiceProvider，以正常 DI 语义解析；支持由容器执行工厂注册。缺失时 OrNull 返回 null，必需版本抛出有服务类型的明确异常。方法不缓存结果、不创建额外 scope、不持有 provider；生命周期 scope 的现有责任不变。配置仍是原 IConfiguration 对象，reload 由配置系统负责，框架不新增订阅。

注册期没有容器，只读取明确注册的实例，不构建临时 provider，不执行 factory。建议统一规则：

1. 若存在目标服务的注册，采用最后一个描述符；它必须是有效的单例 ImplementationInstance。工厂、ImplementationType 或非单例注册在必需方法中报出“注册期请提供实例，或在运行期通过 DI 获取”的错误；OrNull 返回 null。不得跳过最后一项回退到过时实例。
2. 没有直接 IConfiguration/IHostEnvironment 注册时，才从已注册的 HostBuilderContext 实例取值；Configuration 按 IConfiguration 返回，不要求 IConfigurationRoot。
3. 全部缺失时保持必需方法抛错/OrNull 返回 null；不创建空配置或 Development 环境。

这是对现有配置辅助方法的有意行为纠正：从“HostBuilderContext 优先/首个注册/可能提前执行工厂”变为上述规则，公开签名不变，但需要迁移说明与直接测试。一般 GetSingletonInstanceOrNull<T> 及现有 Web 环境辅助方法不在本次修改范围；配置/通用环境读取通过私有实例读取 helper 实现，不扩大到全仓库单例辅助方法重构。Legacy BindLegacyRegistration 也调用配置 helper，必须纳入回归。

不提供新全局应用上下文单例。模块配置继续接收 IServiceCollection；如果后续实际需要应用身份或运行状态，再单独设计，而不是为了两个便捷方法增加生命周期所有者。

### 3.2 附加程序集与类型筛选

建议新增以下契约，名称以实施前编译及现有符号检查为准：

```csharp
[BingModuleAssembly(typeof(ReportingContractsMarker))]
public class ReportingModule : BingModule { }

// options 为 BingApplicationOptions
options.ServiceScanning.AdditionalAssemblies.Add(typeof(SharedServicesMarker).Assembly);
options.ServiceScanning.ConventionalTypeFilter = type =>
    type.Namespace != null && type.Namespace.StartsWith("Contoso.", StringComparison.Ordinal);
```

- BingModuleAssemblyAttribute：类级、多次声明、支持继承，以 marker Type 所在程序集表达引用；不会把 marker 本身当作模块。读取声明不能执行任意用户 provider 方法。
- BingServiceScanningOptions：由 BingApplicationOptions.ServiceScanning 只读属性提供；包含 AdditionalAssemblies 集合和可选 Func<Type, bool> ConventionalTypeFilter。
- BingModuleDescriptor.Assembly 保留原“模块定义程序集”含义；新增只读 Assemblies 快照，包含该模块本体程序集及继承声明的附加程序集。保留现有描述符构造函数，不要求自定义 IBingModuleLoader 更改签名；框架统一补齐并校验声明元数据。
- AdditionalAssemblies 是应用级补充，不伪造某个模块的所有权；模块级信息放在描述符中，完整扫描目录由内部应用所属对象持有。

扫描集合 = 最终选中模块的本体与声明程序集 + 本次应用的额外程序集。按模块执行序号、声明顺序、应用额外集合顺序合并；以实际 Assembly 身份去重，不只按名称合并跨加载上下文对象。未选中/被排除模块的声明不生效。附加程序集不会自动发现新模块，也不会自动参与模块依赖拓扑。

筛选语义刻意收窄：ConventionalTypeFilter 只对符合原依赖标记/特性规则的候选实现类型应用；false 只跳过该类型的约定 DI 注册。它不影响手工注册、模块发现、选项绑定或公开 RegisterType 事件。后两者仍扫描完整新目录。AutoRegisterServices=false 时不执行此筛选，选项及事件继续工作。

同一程序集被多个模块引用时只扫描一次，按应用整体策略筛选；不提供伪装成模块隔离的按模块冲突规则。更细的选项/公开事件筛选不在首版范围，避免改变现有回调契约。

### 3.3 扫描冻结、插件与失败路径

- 在进入第一个配置钩子前完成选项快照、附加程序集元数据解析和必要类型发现；不允许在 Pre/Post 中通过保留的 options 引用偷偷改变本轮扫描范围。
- 注册期内部扫描记录统一供 DI、类型事件及晚选项补扫使用。保持 Compatible/BeforeConfigureServices 的执行时机，仅改变它们消费的目录，不把类型事件提前到元数据预检阶段。
- 附加插件程序集按当前插件代的候选表及原身份校验规则加载；不能借用另一可回收上下文的对象，也不能把可回收程序集登记到默认宿主全局目录。共享宿主契约沿用现有约定。
- 插件自身应通过模块声明附加程序集；不要让长寿命宿主配置闭包保存旧代 Assembly/Type。应用级 AdditionalAssemblies 只接受默认上下文或当前插件代的程序集；其他可回收代明确拒绝。
- 需要调整内部加载边界，让解析事件覆盖模块图及附加程序集类型元数据预检，再在 finally 中退订，且在执行用户配置钩子之前结束该加载范围。不保留常驻解析事件；不修改公开 IBingModuleLoader 签名。只预加载实际引用的集合，不枚举并注册目录全部 DLL。
- 类型发现失败不得静默忽略缺失类型；为新显式附加程序集报告程序集、声明模块、阶段及 LoaderExceptions。保持原默认程序集扫描兼容行为，不借本次扩展全局改写 AssemblyHelper。
- 类型筛选委托抛错使注册失败，保留类型/阶段诊断并沿用同步/异步清理；不吞掉后继续部分注册。
- Type/Assembly/委托的引用只属于本次应用或插件代；不写静态强引用缓存。筛选完成即释放不再需要的委托，保留晚选项补扫所需的本代目录。停止/失败后释放内部额外元数据；调用方自己保留描述符、程序集、配置闭包仍可阻止协作式卸载，应明确文档责任。

## 4. 实施任务

### Phase A：上下文便捷访问

**CTX-01：配置读取契约与环境读取。**
- 目标：复用现有配置 API，补齐通用环境，并避免注册期构建容器/执行工厂。
- 证据：现有 IConfigurationRoot 收窄与 ImplementationFactory(null) 路径。
- 已确认范围：ServiceCollectionConfigurationExtensions.cs；候选新增私有实例读取 helper 与 ServiceCollectionHostEnvironmentExtensions.cs。
- 步骤：先加直接回归测试；按 §3.1 实现取值及缺失错误；维护配置替换和 Legacy 绑定回归。
- 依赖：无。风险：中，行为纠正可能影响依赖旧优先级/工厂副作用的消费者。
- 验收：非 Root IConfiguration、直接实例、重复注册、HostBuilderContext fallback、工厂不被调用及缺失行为均有直接测试。

**CTX-02：生命周期上下文扩展。**
- 目标：让模块使用 context.GetConfiguration()/GetHostEnvironment()，仍使用当前短期 scope。
- 已确认范围：BingModuleLifecycle.cs、现有 ModulePhasedLifecycleTest；候选新增 BingModuleContextExtensions.cs 与职责级测试类。
- 步骤：增加初始化/关闭两种上下文的扩展；统一中文 XML 注释与缺失契约；增加运行期工厂解析、两个宿主隔离和 scope 生命周期测试。
- 依赖：CTX-01 契约确定。风险：低到中，必须避免缓存 scoped 服务。
- 验收：运行期工厂由 DI 正常执行，配置 reload 可见；无全局引用或新订阅；所有旧签名保留。

### Phase B：附加程序集与约定 DI 筛选

**SCAN-01：声明、只读描述符与扫描快照。**
- 目标：构建确定且可检查的扫描目录。
- 已确认范围：BingApplicationOptions.cs、BingModuleDescriptor.cs、BingApplicationServiceCollectionExtensions.cs；候选新增属性类、扫描选项及内部目录实现。
- 步骤：增加公开契约；解析最终模块声明及继承；按实际程序集身份去重；冻结快照；校验 null/跨代对象；兼容自定义 loader 和现有构造函数。
- 依赖：可在 Phase A 后实施。风险：中，目录顺序影响原服务覆盖顺序。
- 验收：无配置时与旧扫描集合及顺序等价；未选中模块的声明不生效；附加程序集不会偷偷加载模块。

**SCAN-02：接入 DI、选项与事件。**
- 目标：完整扫描集合和筛选语义真正进入主路径。
- 已确认范围：DependencyModule.cs、DependencyTypeFinder.cs、BingLoader.cs、OptionsTypeRegistration.cs 及晚选项补扫入口。
- 步骤：共享冻结目录；保留原候选规则，随后应用 ConventionalTypeFilter；保持两种扫描时机；公开事件每类型每轮一次；晚选项补扫仍使用同一目录，不重复发事件。
- 依赖：SCAN-01。风险：中高，三个消费者容易出现范围不一致。
- 验收：DI 筛选不影响选项/事件；关闭自动 DI 不调用筛选；Post 替换服务仍有效；重复程序集不重复注册/发事件；筛选异常按注册失败处理。

**SCAN-03：插件加载与卸载回归。**
- 目标：附加程序集在正确插件代解析，候选失败不污染旧代。
- 已确认范围：BingPluginLoader.cs、BingHotPluginHost.cs、HotPluginHostTest.cs 及隔离插件夹具；新增附加服务夹具程序集不被测试项目直接引用。
- 步骤：按 §3.3 调整内部临时加载会话；元数据预检后退订；注册失败释放模块并清理额外记录；增加附加程序集版本切换和弱引用验证。
- 依赖：SCAN-01/02。风险：高于前两项，涉及插件类型身份和回收。
- 验收：私有附加依赖可解析；错误报告可定位；两个代同名不同版本互不串用；成功替换/失败候选均可回收；静态解析事件无遗留；其他旧代对象不能进入新代扫描集合。

### Phase C：文档与验收

**DOC-01：迁移、例子和追溯映射。**
- 范围：Core README、docs/migrations/modularity-runtime.md、docs/guides/modularity-plugins.md、候选新 ADR、本任务 execution.md 和 test-mapping.md。
- 步骤：说明配置辅助方法优先级修正和工厂迁移；给出普通模块/插件声明和筛选例子；说明附加扫描不是模块发现或安全隔离；记录生产符号到测试方法。
- 依赖：前述任务。风险：低。
- 验收：每个新公开符号均有直接测试映射，文档默认行为与异常路径和实现一致。

## 5. 验证矩阵与命令

本轮仅规划，没有运行测试。原任务 Core 双框架各 450、MVC 各 53 加 1 skipped 是历史基线，不作为本功能已验证证据。

实施前影响分析：Core 配置获取、生命周期便捷访问、扫描目录与插件预加载；依赖消费者 AspNetCore/MVC 和 PluginRuntime；新增公开契约但保留现有签名；风险 MEDIUM/HIGH（分别对应上下文与插件扫描）。不涉及 SQL/数据库，无需 SQL 基准。生产 TFM、依赖和包版本保持不变。

必要测试组：

1. 配置/环境：缺失、重复、替换、HostBuilderContext、非 Root 配置、factory/type 描述符、factory 未提前执行、运行期工厂正常解析、两个宿主隔离及配置 reload。
2. 上下文：初始化 Pre/Main/Post 与关闭均读取正确当前应用数据；不增加 scope；关闭后配置/宿主引用可回收；不影响现有取消和异常传播。
3. 目录：模块声明继承、应用附加、多模块共享程序集、重复去重、未选中声明、描述符只读性、自定义 loader、不改变模块拓扑。
4. 筛选：两种扫描时机、原 DI 特性与生命周期规则、false 仅影响约定 DI、手工注册保留、AutoRegisterServices=false、Post 替换、选项晚补扫和事件次数。
5. 插件：附加私有程序集、缺失/身份冲突、筛选抛错、两代隔离、失败保留旧代、异步清理等待、关闭/失败 ALC 弱引用回收以及跨代程序集拒绝。
6. 兼容：旧 AddBing、新入口无选项、Web 管道、Generic Host 停止与现有模块资源用例。

使用锁定 SDK 8.0.424，先运行实际新增测试类的定向测试，再在 Phase C 顺序运行：

```powershell
dotnet test framework/tests/Bing.Core.Tests/Bing.Core.Tests.csproj -f net8.0
dotnet test framework/tests/Bing.Core.Tests/Bing.Core.Tests.csproj -f net6.0
dotnet test framework/tests/Bing.AspNetCore.Mvc.Tests/Bing.AspNetCore.Mvc.Tests.csproj -f net8.0
dotnet test framework/tests/Bing.AspNetCore.Mvc.Tests/Bing.AspNetCore.Mvc.Tests.csproj -f net6.0
dotnet build samples/Bing.Samples.WebApi/Bing.Samples.WebApi.csproj
git diff --check
```

SDK 已在本地临时目录安装，不修改 global.json；实施时先确认仍可用。资源验证使用对象弱引用、释放次数和事件退订，不引入工作集/吞吐量门槛或大规模基准。复杂度目标为每个最终程序集只建立一次注册期类型快照，线性枚举类型；筛选委托由调用方保证纯判定和合理成本，框架不重复执行无必要的谓词。

## 6. 边界与完成定义

不增加全局应用容器、根服务定位器、自定义生命周期阶段、远程插件分发、版本求解器、动态 Web 管道或不可信插件隔离。目录和筛选只是服务注册策略，无法限制可信插件任意执行代码。

评估已完成；推荐实施顺序：CTX-01 → CTX-02 → SCAN-01 → SCAN-02 → SCAN-03 → DOC-01。两项能力均能增量加入，但配置取值规则纠正属于明确的行为迁移点，不能宣传为完全无行为变化。

实施完成条件：上述任务及直接测试闭环、原入口默认行为回归通过、附加插件程序集回收有证据、文档和测试映射完成，无未解释的 OPEN_ACTIONABLE。当前只交付计划，尚未声明任何新 API 已实现。
