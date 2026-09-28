# 模块化可靠性、扩展性与资源生命周期改进计划

## 1. 范围与当前状态

- Task ID：`BING-MODULARITY-HARDENING-20260927`。
- 日期：2026-09-27；本地基准提交：`2c36316e210ad2081520fe56e7413c748bad92dd`；分析开始时工作区干净。
- 当前阶段：分析与计划完成，尚未修改生产代码、测试或配置；下述测试为实施验收要求，不代表已执行。
- 审查对象：`Bing.Core` 模块发现、依赖解析、服务注册、初始化、配置绑定、静态引用，以及 `Bing.AspNetCore` 的模块初始化桥接。不是后台管理系统的菜单/权限 Module 实体，也不是全仓库安全审计。
- 规则依据：根 `AGENTS.md`、用户级执行规则、`C:/Users/jianx/.codex/GOAL_RULES.md`、项目 `.agents/skills/create-plan/SKILL.md`。
- 测试与文档盘点已委派 Luna 子代理；主代理负责调用链、资源所有权、ABP 对比和方案集成。当前工具没有名为 `luna_worker` 的自定义角色，采用同名子任务及 Luna 模型完成只读盘点。

**结论：现有设计适合受控、单宿主的启动期模块组合，基本抽象合理，但尚不是生命周期完整、可隔离、可安全反复启动的模块运行时。** 已存在可确认的循环依赖缺陷、失败状态污染和静态事件保留路径。扩展新业务模块较容易，扩展模块加载策略、生命周期和隔离机制较困难。没有发现证据足以宣称存在远程可利用漏洞；循环依赖导致可用性故障也不等于已证明远程拒绝服务漏洞。

## 2. 实际调用链与完成度

```text
services.AddBing()
  → 注册核心服务、设置静态 IConfiguration
  → BingBuilder 构造：扫描所有模块类型并立即 Activator.CreateInstance
  → AddCoreModule：逐个 AddModule，立即 AddServices
  → BingLoader.RegisterTypes：触发静态 RegisterType 事件
  → AddModule<AppModule>()
      → BingModule.GetDependModuleTypes 递归依赖
      → 按 Level / Order 排序新增模块并立即 AddServices
  → 构建 IServiceProvider
  → Web UseBing：GetAllModules 按 Level / Order / FullName 排序后分派
  → 非 Web UseBing：按容器中的注册顺序遍历
  → 未提供统一 Shutdown / 模块释放流程
```

| 能力 | 状态 | 证据与判断 |
| --- | --- | --- |
| 两阶段启动与模块继承 | 已实现 | `BingModule.AddServices/UseModule`、`AspNetCoreBingModule`，真实样例有调用，不是 Stub |
| 依赖纳入和去重 | 部分完成 | 能递归纳入，但无环检测；共享依赖仍可能反复遍历 |
| 依赖先于使用方执行 | 未实现，属于现有设计边界 | ADR-0001 明确选择 Level 排序；不可当成未遵守已有拓扑契约 |
| 稳定一致的注册/启动顺序 | 部分完成 | Builder、Web、非 Web 三处算法不一致，跨 AddModule 调用不能重排已执行注册 |
| 自动 DI 与配置绑定 | 已实现基础能力 | 扫描范围为全局程序集，不随选中模块裁剪；选项绑定使用静态订阅 |
| 重复初始化保护、失败状态机 | 未实现 | 两个 UseBing 均无运行状态检查；Enabled 依赖子类是否调用基类 |
| 关闭、异步停止、失败清理 | 未实现 | 核心契约无停止方法，相关入口没有宿主停止桥接 |
| 模块级直接测试 | 明显不足 | Core 仅枚举测试及 AddBing 的 DI 间接测试；MVC 测试主要通过宿主间接启动 |
| 插件来源/按应用描述符/可替换调度 | 未形成完整能力 | Finder 可替换，但 Builder 加载、排序、注册集中在私有实现 |
| 热卸载与不可信插件隔离 | 未实现 | 普通程序集加载和进程级 Type/Assembly 缓存不是可卸载/沙箱机制 |

完成度使用上表逐项判断，不给无验收依据的主观百分比。发布改进版前至少需闭环 Phase 1 的缺陷及对应资源/宿主测试。

## 3. Findings：证据、触发条件与边界

以下行号基于本次基准版本；路径相对于仓库根目录。

| ID / 优先级 / 状态 | 证据 | 触发与影响 | 闭环条件 |
| --- | --- | --- | --- |
| MOD-001 / P1 / OPEN_ACTIONABLE | `framework/src/Bing.Core/Bing/Core/Modularity/BingModule.cs:33,47-49`；`Builders/BingBuilder.cs:104` | A→B→A 或自依赖时，每层新建 HashSet 且不判断递归栈，无限递归可造成进程栈溢出；实际入口不走另一个 Helper | 自环/多点环抛带路径的确定异常；共享 DAG 去重且不误判；验证在服务注册前完成 |
| MOD-002 / P1 / OPEN_ACTIONABLE | `framework/src/Bing.Core/Bing/Configuration/Extensions.ServiceCollection.cs:19-34`；`Bing/BingLoader.cs:15,33` | 每次 AddOptionsType 增加静态匿名订阅，捕获 services/configuration，无退订；宿主结束后仍可被静态事件持有，再次 AddBing 会回调旧集合 | 内置绑定不依赖静态订阅；两个宿主配置隔离；旧集合/配置可回收；新启动不修改旧集合 |
| MOD-003 / P1 / OPEN_ACTIONABLE | `Builders/BingBuilder.cs:45-55,157-158` | 扫描到的所有模块都先实例化；未选择模块构造异常也会阻止启动；外部实例注册没有框架释放所有权，模块自持 Timer/订阅等可继续存活 | 未选中模块不构造；框架创建模块有唯一释放责任；正常/失败/重复关闭时最多释放一次 |
| MOD-004 / P1 / OPEN_ACTIONABLE | `Builders/BingBuilder.cs:93-104,157-158`；Core `BingServiceProviderExtensions.cs:62-77`；Web `BingApplicationBuilderExtensions.cs:98-132` | 先写入模块状态/描述再 AddServices；抛错后同类型重试可能被直接跳过；UseBing 重复执行可重复订阅/启动后台工作；初始化失败无清理 | 明确 Failed 状态，失败集合禁止继续构建/重试；重复初始化不重复副作用；失败清理已启动模块 |
| MOD-005 / P1 / OPEN_ACTIONABLE | `Bing/DependencyInjection/ServiceLocator.cs:19,24-29,84-101,205-209`；`DependencyModule.cs` 两个生命周期方法；`ServiceCollectionApplicationExtensions.cs:27` | 静态定位器及 IConfiguration 跨宿主覆盖、持有引用；未找到宿主停止自动清理调用；最后宿主可持续被保留。不是“每次一定积累全部历史宿主” | 新运行时按 provider 隔离；兼容静态入口有明确单宿主限制及所属者核对后解绑；释放 A 不清除 B；禁止代替宿主释放根容器 |
| MOD-006 / P2 / OPEN_ACTIONABLE | `Builders/BingBuilder.cs:113`；Core `BingServiceProviderExtensions.cs:50-55,67`；Web `...BingApplicationBuilderExtensions.cs:111` | 同 Level/Order 时注册与 Web 初始化顺序可能不同，非 Web 又用注册顺序；按依赖顺序启动尚不属于现有契约 | 先冻结兼容模式行为；新模式使用同一拓扑结果驱动注册和启动；依赖优先，Level/Order/名称仅打破无依赖节点的平局 |
| MOD-007 / P2 / OPEN_ACTIONABLE | `BingModuleHelper.cs:29-44` 对比 `BingModule.cs:36`；`BingModuleTypeFinder.cs:24-29` | Helper 支持 IDependedTypesProvider，实际路径只读取 DependsOnModuleAttribute；扫描排除被继承的实类，显式依赖该基类时找不到实例 | 统一解析路径并验证自定义 Provider；显式选择/依赖不被全扫描的替换规则静默丢弃；继承替换冲突有明确诊断 |
| MOD-008 / P2 / OPEN_ACTIONABLE | `Builders/BingBuilder.cs:168-174`；`DependencyModule.AddServices`；`InternalServiceCollectionExtensions.cs:31-35` | AddModules 的排除仅过滤根候选，依赖仍能重新拉入；未加载模块程序集里的 DI 类型仍全局注册；自定义 IAllAssemblyFinder 与提前构造的 DependencyTypeFinder 可能使用不同扫描源 | 排除与依赖冲突可诊断；新增显式扫描范围模式；自定义扫描来源一致，不静默改变旧全扫描模式 |
| MOD-009 / P2 / DEFERRED | `Bing/Reflection/DirectoryAssemblyFinder.cs:13,46-52`；`AssemblyManager.cs:26-31,79-97` | 多不同目录造成静态缓存条目增长；Type/Assembly 强引用阻碍未来 collectible ALC 回收；固定程序集单宿主不等同于无界泄漏 | 当前记录不支持卸载；未来插件任务验证缓存上限、作用域与 ALC 卸载，不纳入此次默认修复 |
| MOD-010 / P2 / OPEN_ACTIONABLE | `Bing/Logging/StartupLogger.cs` 的 LogInfos/Output；Web UseBing 中 Output 被注释 | 启动日志缓存不清空，异常对象可被保留；一次有限启动属于不必要常驻，不自动等同持续泄漏 | 正常启动转交正式日志并清空；失败先输出诊断再清空；测试证明相关 State/Exception 不再被缓存持有 |

补充边界：`BingModule.IsBingModule` 接受 IBingModule，但 Builder 仅接受 BingModule，且 CheckBingModuleType(null) 的报错访问空类型；合并 MOD-007 修复输入验证和契约说明，不扩大为整个 DI 系统重写。`AspNetCoreBingModule.UseModule(app)` 调用 base 实现，且样例覆写不调用 base；不能仅靠 Enabled 保证执行状态。

内存结论分层：MOD-002 有源码可追踪的静态根保留链，但尚未用堆快照量化；MOD-003 的资源泄漏取决于模块是否持有需要释放的资源；MOD-005 确认跨宿主全局状态问题；MOD-009 是指定工作负载下的缓存/卸载风险。仓库内未找到 AddOptionsType 的业务调用，不能声称现有样例已经发生该泄漏。

## 4. ABP 对照与建议

本次在线核对 ABP 官方仓库 `dev` 分支，访问时间 2026-09-26～27。它是移动分支，只作为架构参考，不声称对应固定发布版本。采用概念与契约，独立实现，不直接复制实现代码。

| 能力 | ABP 官方源码依据 | Bing 建议 |
| --- | --- | --- |
| 模块图与描述符 | [ModuleLoader](https://github.com/abpframework/abp/blob/dev/framework/src/Volo.Abp.Core/Volo/Abp/Modularity/ModuleLoader.cs) 构造描述符并按依赖排序，支持额外插件来源 | 增加应用级模块目录和统一依赖图；优先解决正确性，插件目录能力后置 |
| 生命周期调度 | [ModuleManager](https://github.com/abpframework/abp/blob/dev/framework/src/Volo.Abp.Core/Volo/Abp/Modularity/ModuleManager.cs) 通过 Contributor 分派同步/异步启动和逆序关闭，异常携带模块与阶段信息 | 提取调度器、生命周期状态和上下文；逆序停止并继续清理其他模块，集中报告清理错误 |
| 多阶段服务配置 | [AbpApplicationBase](https://github.com/abpframework/abp/blob/dev/framework/src/Volo.Abp.Core/Volo/Abp/AbpApplicationBase.cs) 在全部模块间分轮 Pre/Configure/Post，初始化和停止建立 scope | 提供新入口收集完整图后分阶段注册；初始化作用域短期存在，模块不长期缓存 scoped 对象 |
| 可覆写的扩展钩子 | [AbpModule](https://github.com/abpframework/abp/blob/dev/framework/src/Volo.Abp.Core/Volo/Abp/Modularity/AbpModule.cs) 暴露注册与应用生命周期钩子 | 保留旧 AddServices/UseModule，新增可选接口或虚方法，不向 IBingModule 强加抽象成员 |
| 启动期插件 | [FolderPlugInSource](https://github.com/abpframework/abp/blob/dev/framework/src/Volo.Abp.Core/Volo/Abp/Modularity/PlugIns/FolderPlugInSource.cs) 提供目录插件发现 | 第二阶段之后再评估 IModuleSource；目录加载不等于热卸载，更不等于安全沙箱 |

资源责任依据：[Microsoft DI 指南](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines) 说明容器创建的服务由容器释放，外部实例需要调用方管理，并提示根容器解析 disposable transient 的保留风险。因此必须为手工创建的模块明确所有者，且不能粗暴 Dispose 宿主传入的 provider。

不要求照搬 ABP 全生态。权限、租户、审计、事件总线等是上层能力，当前先确保模块内核能可靠组织这些能力。不把本次任务扩成业务模块重构。

## 5. 架构与兼容策略

1. **第一优先级是缺陷修复。** 收敛依赖解析、移除内置静态订阅、补齐失败清理；不立即改变所有旧模块的排序语义。
2. **保留旧入口。** `AddBing().AddModule<T>()` 的立即注册行为和旧排序模式保留。新模块运行时采用新增入口（候选名 `AddBingApplication<TStartupModule>`，最终名称实施前审查），一次收集完整依赖图再统一配置；不可把旧入口暗改成延迟注册。
3. **拓扑约束优先。** 新入口中 B 被 A 依赖，B 必须先于 A；Level、Order、FullName 的 Ordinal 比较仅用于当前就绪节点。旧模式的逆序依赖输出诊断，并给出迁移方式，不把已有 ADR 当成偶然 Bug 改掉。
4. **生命周期状态由运行时维护。** Configuring/Configured/Initializing/Initialized/Stopping/Stopped/Failed；模块 Enabled 为旧兼容信息。相同初始化请求不重复执行；并发异步请求共享结果；重入报确定错误；失败不自动重试已污染的集合/管道。
5. **资源所有权。** 框架只释放自己创建的模块，至多一次；模块注册的 DI 服务仍由宿主管理。停止钩子与 Dispose 是不同责任，先逆序停止，再释放模块自身资源。同步关闭遇到仅异步清理能力，明确要求异步关闭，不静默遗漏或阻塞等待。
6. **启动失败不能万能回滚。** 不宣称可撤销任意 AddServices 外部副作用或已装配中间件；失败后整套服务集合/应用构建器应废弃。已开始初始化的模块参与尽力清理，清理错误不遮蔽原始错误。
7. **Web 管道不能放到晚期 HostedService 才配置。** UseBing(app) 保持在管道构建时执行；宿主适配只负责统一调度与停止。无 Generic Host 的使用方提供显式关闭入口；直接 provider.Dispose 的所有权路径须有回归测试。
8. **跨宿主隔离渐进迁移。** 新运行时不依赖 ServiceLocator/静态 IConfiguration；旧静态 API 仅保留单宿主兼容，不承诺可表达两个宿主。解绑必须按所属实例核对，不能简单全局置空导致误伤另一个宿主。
9. **模块与程序集扫描不是同一边界。** 新模式按选中模块程序集注册；同一程序集多个模块仍共享程序集扫描范围，应提供显式服务注册/关闭自动扫描选项，不能宣称天然模块级安全隔离。
10. **不可信代码不在进程内隔离。** 本期只支持受信任模块；需要执行第三方不可信插件时另做进程隔离设计。热更新、ALC 卸载、依赖版本冲突与插件市场均后置。

## 6. 实施任务

### Phase 0：冻结契约与验证基础

**T00：建立回归夹具及行为基线**

- 目标：使发现、图解析、注册、Web/非 Web 启动和资源释放可独立验证。
- 现状/证据：现有测试仅间接覆盖 DI/宿主；SDK 锁定 8.0.424，但本机未安装该 SDK。
- 修改范围：已确认 `framework/tests/Bing.Core.Tests`、`framework/tests/Bing.AspNetCore.Mvc.Tests`；候选新增 Modularity 测试目录与受控 Finder。不要让测试用的故障模块被其他测试全局扫描。
- 步骤：准备依赖环、菱形图、同级乱序、构造失败、注册失败、启动失败、Disposable 模块；冻结旧入口正常行为；新测试名称映射到 §7；危险栈溢出不在测试 runner 中重现。
- 依赖：恢复仓库指定 SDK 的可用性后执行测试；不改 global.json 或包版本来掩盖环境缺失。
- 验证：Core 定向测试；Web 夹具检查；隔离的内存测试不得靠清空全部静态状态掩盖保留路径。
- 风险：测试模块污染全局扫描；两 TFM 构建产物共享输出目录，顺序执行测试。
- 验收：新夹具不会影响已有测试；基线失败与产品缺陷分开记录，未运行项明确标识。

### Phase 1：可靠性与资源问题闭环

**T01：统一依赖解析与类型校验（MOD-001、MOD-007）**

- 目标：循环依赖安全失败、解析语义一致、显式模块可定位。
- 范围：已确认 `BingModule.cs`、`BingModuleHelper.cs`、`BingModuleTypeFinder.cs`、`BingBuilder.cs`；候选 internal 图解析器。
- 步骤：使用 visiting/visited 集合与路径记录，避免无界递归；统一 IDependedTypesProvider，继承特性按明确规则读取；验证 null、抽象类、开放泛型、无公共无参构造；图校验成功后再写注册状态。扫描时的派生替换仅作为旧自动发现规则，显式依赖冲突需诊断，不默默替换。
- 依赖：T00；保持旧正常注册模式。
- 验证：自环、三点环、共享依赖、继承、自定义属性、非法类型、确定错误消息；适量长链验证不会依赖调用栈深度。
- 风险：旧系统接受的错误模块图现在会提前失败；文档明确该行为修正。
- 验收：验证失败时无部分模块注册；有效依赖仅处理一次；生产路径不再存在两套互相冲突的解析实现。

**T02：移除选项绑定的静态保留链（MOD-002）**

- 目标：配置绑定属于当前 IServiceCollection，并允许前后调用顺序的明确兼容。
- 范围：已确认 `Bing/Configuration/Extensions.ServiceCollection.cs`、`BingLoader.cs`、`ServiceCollectionApplicationExtensions.cs`；候选集合内的选项绑定注册器。
- 步骤：将内置回调移至集合所属注册器；保持旧调用 AddOptionsType→AddBing 的行为；定义 AddBing 后追加配置的处理；同一集合重复注册不重复绑定，不把重复配置合并规则静默改为“最后胜出”。保留公开 RegisterType 的外部兼容事件，本期内置代码不再向它订阅，文档说明外部订阅责任。
- 依赖：T00。
- 验证：两个集合/配置不串用、重复调用、先后调用顺序、配置 reload；NoInlining 辅助函数返回 WeakReference，丢弃强引用后有界 GC 验证旧集合可回收。
- 风险：事件触发时机变化影响消费者；测试保障公开事件仍按既有 RegisterTypes 行为调用。
- 验收：内置绑定无静态事件订阅；宿主 B 扫描不修改 A；未构建 provider 的配置注册也不留下全局根。

**T03：按需实例化、状态与资源所有权（MOD-003、MOD-004）**

- 目标：只创建选中模块，失败不伪装成功，初始化及释放至多一次。
- 范围：已确认 `BingBuilder.cs`、两处 UseBing、`IBingModule.cs`、`BingModule.cs`、`AspNetCoreBingModule.cs`；候选应用级 runtime/owner 与可选停止接口。
- 步骤：源集合保存 Type/描述符而非全部实例；记录创建/配置/初始化状态；停止采用逆序尽力清理；将 owner 通过容器工厂创建以获得可靠释放回调，owner 不释放根 provider；Web/非 Web 接入相同状态协调器；注册失败后拒绝继续使用当前构建器。
- 依赖：T01。
- 验证：未选中故障构造函数不执行；重复 Add/Use/Stop、失败中途清理、构造/注册失败释放、单个关闭异常不阻断其余清理；同模块停止与 Dispose 次数断言；直接 provider.Dispose 路径。
- 风险：旧调用重复 UseBing 不再重复装配；同步/异步清理不得双执行；不得依赖子类调用 base 来维护状态。
- 验收：所有框架创建模块的所有权可说明；失败不能重入“成功”；正常停止和异常退出不遗留测试资源。

**T04：静态兼容层解绑与启动缓存清理（MOD-005、MOD-010）**

- 目标：宿主结束后框架不继续持有该宿主对象图，保持其他宿主有效。
- 范围：已确认 `ServiceLocator.cs`、`DependencyModule.cs`、`ServiceCollectionApplicationExtensions.cs`、`StartupLogger.cs`、两处初始化入口；候选 owner 关联的兼容注册句柄。
- 步骤：为旧全局入口添加带所属者的绑定/解绑；禁止框架直接 Dispose 永久单例 ServiceLocator 作为可重复解绑方案；新代码依赖应用上下文；启动日志输出后清空并释放异常对象引用；不迁移全仓库所有 ServiceLocator 调用。
- 依赖：T02、T03。
- 验证：A/B 顺序创建及不同关闭顺序、旧 provider 释放后无误访问；记录单宿主兼容限制；弱引用证明 services、configuration、测试资源在关闭后可回收；ValidateScopes 开启场景。
- 风险：全局配置同时服务多应用的语义无法完全兼容，应使用新入口隔离；日志失败时仍要保留原始启动异常。
- 验收：所属者核对正确；新运行时不从静态定位器解析；缓存不持有已输出日志对象图。

### Phase 2：可扩展运行时（新增入口，兼容迁移）

**T05：描述符、拓扑顺序和扫描边界（MOD-006、MOD-008）**

- 目标：新入口能真正按依赖关系启动，加载与排序策略可替换。
- 范围：已确认 Builders/Modularity、`BingOptions.cs`、`InternalServiceCollectionExtensions.cs`、`DependencyModule.cs`；候选 `IBingModuleLoader`、`IBingModuleContainer`、只读描述符及创建选项（名称待 API 审查）。
- 步骤：新入口一次指定启动根/显式模块来源，完成全图验证再执行配置；拓扑顺序使用稳定平局规则；暴露只读类型、依赖、来源、顺序与状态；排除必需依赖时报出依赖链；实现自定义 Finder 一致注入；全局扫描模式保留作为旧入口兼容。
- 依赖：T01、T03；新增公开 API 在实施阶段做契约审查，默认不改变原有接口形状。
- 验证：依赖优先于相反 Level、同级确定性、多个根共享依赖、排除冲突、自定义 Finder、未选中程序集服务不进入新模式、同程序集共享扫描限制、旧模式基线。
- 风险：从立即 AddServices 改为统一配置是行为变化，只在新入口采用；同一服务集合禁止混用两个模式。
- 验收：注册、Web 初始化和非 Web 初始化使用同一已冻结模块序列；替换 loader 不需要派生/复制 BingBuilder 私有实现。

**T06：异步分阶段生命周期与宿主适配**

- 目标：支持异步初始化/关闭、前后配置扩展和可控的失败诊断。
- 范围：已确认 Core Modularity 与 AspNetCore 桥接；候选生命周期接口、context、manager、Generic Host 停止适配。
- 步骤：对所有模块分轮执行 PreConfigure→AddServices→PostConfigure；新增异步初始化/停止及 CancellationToken；旧同步钩子默认适配，每次仅选择一条执行路径；context 提供短期 scope，Web 管道装配保持在 UseBing(app) 时；异常包含模块、阶段与依赖路径；同步入口不得静默吞掉仅异步模块。
- 依赖：T03、T05。
- 验证：阶段顺序、异步等待/取消、重复/并发初始化、重入失败、逆序关闭、启动失败清理、scope 释放；验证模块没有捕获已经释放的 scoped 服务；TestServer 管道验证。
- 风险：Core 是 netstandard2.0，Task 优先；IAsyncDisposable 可用性须检查现有依赖，不擅自新增包或升级 TFM。
- 验收：旧模块无需改写即可工作；异步资源有明确停止入口；宿主启动/关闭实际触发对应流程。

### Phase 3：验证、文档与交付

**T07：针对性资源验证与文档收口**

- 目标：用直接证据回答是否泄漏、如何扩展和如何迁移。
- 范围：上述测试项目；已确认 Core/AspNetCore README、`docs/architecture/架构文档.md`、ADR-0001、使用文档、最佳实践；候选新增模块运行时 ADR、迁移说明；任务 `execution.md` 与生产符号→测试映射。
- 步骤：执行 §7；资源探针验证生命周期结束对象可回收和释放计数，另记录固定工作负载分配/耗时作为观察，不发明正式 MB/延时门槛；修正文档把依赖纳入说成拓扑排序及 ConfigureServices/Initialize 术语漂移的问题；新增包含同步、异步及无 Host 关闭的示例。
- 依赖：T01–T06；可先在 Phase 1 检查点执行相关子集。
- 验证：受影响测试项目两 TFM 顺序运行、消费者构建、UTF-8/链接/diff 检查。
- 风险：GC/工作集噪声不能直接证明泄漏；采用根引用/回收性和实际资源停止证据，不用单次工作集下降作为唯一验收。
- 验收：每个修复关联真实测试方法及执行结果；无未解释的 OPEN_ACTIONABLE；环境限制与实现结论分开记录；不自动提交/发布。

## 7. 验证矩阵与真实命令

已确认项目配置：Core 为 netstandard2.0（framework.props），AspNetCore 为 net6.0，两个测试项目从 framework.tests.props 得到 net8.0/net6.0。2026-09-27 运行 `dotnet --list-sdks` 和 `dotnet --version`，确认仓库所需 **8.0.424 缺失**，仓库目录中 SDK 解析失败；当前测试状态为 NOT_RUN，环境分类 BLOCKED_EXTERNAL。需要安装匹配 SDK 后执行，不修改仓库锁定版本。

实施前影响分析：生产项目 Bing.Core/Bing.AspNetCore；运行路径发现→注册→启动→关闭；Phase 2 新增公共 API；涉及依赖 Core 的消费者；不涉及 SQL 输出、Provider、数据库或包版本；风险 HIGH。本次仅新增计划文档，验证范围为 UTF-8/内容与 diff 检查，不运行全量业务测试。

命令从仓库根目录运行；定向 filter 只在对应测试类真实创建后使用。

```powershell
dotnet test framework/tests/Bing.Core.Tests/Bing.Core.Tests.csproj -f net8.0 --filter FullyQualifiedName~Modularity
dotnet test framework/tests/Bing.Core.Tests/Bing.Core.Tests.csproj -f net8.0
dotnet test framework/tests/Bing.Core.Tests/Bing.Core.Tests.csproj -f net6.0
dotnet test framework/tests/Bing.AspNetCore.Mvc.Tests/Bing.AspNetCore.Mvc.Tests.csproj -f net8.0
dotnet test framework/tests/Bing.AspNetCore.Mvc.Tests/Bing.AspNetCore.Mvc.Tests.csproj -f net6.0
dotnet build framework/src/Bing.Core/Bing.Core.csproj
dotnet build framework/src/Bing.AspNetCore/Bing.AspNetCore.csproj
dotnet build samples/Bing.Samples.WebApi/Bing.Samples.WebApi.csproj
git diff --check
```

| 生产职责/符号 | 候选新增直接测试（不是现有测试） | 项目 |
| --- | --- | --- |
| BingModule / 图解析器 | ModuleGraphTest.RejectsSelfCycle / ReportsCyclePath / DeduplicatesDiamond / HonorsCustomDependencyProvider / RejectsInvalidTypeBeforeMutation | Bing.Core.Tests |
| BingBuilder | ModuleRegistrationTest.DoesNotConstructUnselectedModule / RejectsReuseAfterConfigurationFailure / DetectsExcludedDependency / ResolvesExplicitBaseModule | Bing.Core.Tests |
| AddOptionsType / BingLoader | OptionsTypeRegistrationTest.IsolatesTwoCollections / DoesNotRetainOldCollection / PreservesReload / SupportsRegistrationOrder | Bing.Core.Tests |
| 模块 runtime / owner | ModuleLifecycleTest.InitializesOnce / RejectsReentrancy / CleansUpAfterFailure / StopsInReverseOrder / ContinuesCleanupAfterError / ProviderDisposeReleasesOwnedModulesOnce | Bing.Core.Tests |
| ServiceLocator 兼容层 | ModuleHostIsolationTest.StoppingOldHostDoesNotClearNewHost / CollectsStoppedHost / NewRuntimeDoesNotUseGlobalLocator | Bing.Core.Tests |
| 启动日志 | ModuleStartupLoggingTest.FlushClearsEntries / FailureDoesNotRetainException | Bing.Core.Tests |
| 新 loader/container | ModuleOrderingTest.DependencyOverridesLevel / StableReadyNodeOrdering / LegacyOrderingUnchanged；ModuleDiscoveryTest.RespectsConfiguredFinder / LimitsAutomaticRegistration | Bing.Core.Tests |
| 异步及 scope | AsyncModuleLifecycleTest.AwaitsHooks / CancelsInitialization / SharesConcurrentInitialization / DisposesInitializationScope / StopsAsyncOnlyModule | Bing.Core.Tests |
| Web UseBing | WebModuleLifecycleTest.DispatchesWebHook / MatchesCoreModuleOrder / DoesNotDuplicateMiddleware / StopsOnHostShutdown | Bing.AspNetCore.Mvc.Tests |

先 L0/L1，再受影响项目 L2，Web 与宿主边界 L3；公共 API 检查点构建受影响消费者，不能仅因为修改 Core 就连接所有外部数据库。若实际实施触及 SQL Provider 路径，再按 AGENTS 追加 SQLite 集成门槛。Core 模块启动不是 SQL 热路径，不更新 Bing.Data.Sql.Benchmarks；仅用局部模块图与生命周期资源探针，无需百万行资源矩阵。

资源测试使用固定数量宿主的重复创建/关闭并检查 WeakReference、停止次数、事件回调次数。每个同构实验最多一次诊断、一次确认；不在未获正式阈值时反复调整内存/耗时限制。基准与修复后的源码身份、TFM、工作负载记录在 execution.md，未变范围复用证据。

## 8. 停止条件与后续边界

- Completed：源码调用链核查、文档/测试盘点、ABP 官方对比、计划落盘。
- Open Actionable：MOD-001～008、MOD-010，共 9 项实施问题；当前已关闭 0/9，不表示本轮计划未完成。
- Blocked Approval：尚未做公开 API 最终审查；任何删除旧入口、改变默认排序、全局迁移不在默认实施授权内。按新增入口的兼容方案推进可避免强制破坏性升级。
- Blocked External：本地缺少 global.json 指定 SDK，代码测试未运行；不因该状态反复修代码。
- Not Applicable：远程漏洞利用证明、SQL 基准、生产容量与发布动作不是本次规划验证项。
- Accepted Limitations：现有 Level 排序是明确旧契约；静态兼容 API 仅适合单宿主；只支持受信任启动期模块。
- Verified Boundaries：尚无资源实验结果，不把推断写成已验证容量边界。
- Deferred：MOD-009、热加载/卸载、不可信插件进程隔离、模块版本依赖解析、完整 ABP 生态对齐。

实施时只由 OPEN_ACTIONABLE 触发后续 Fix；同根因无新证据连续两轮停止，自动 Review/Fix 最多三轮。不要因 NOT_VERIFIED/BLOCKED/发布状态不通过自动加修。每轮输出影响范围、已执行验证与分类 TODO。

本轮仅产出计划。项目 create-plan 技能要求“然后停止，不自动进入实施阶段”。计划通知直接在当前对话交付；workflow-notify.mjs 会调用飞书通知工具，当前没有发送外部消息的明确授权，故不运行该脚本。
