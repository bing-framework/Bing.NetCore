<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-MODULE-CONTEXT-SCANNING-20260928
AI_REVIEWED_AT: 2026-09-28T08:47:04Z

# 模块上下文与扫描扩展：计划差距审查

## 结论

两组主能力均已实现并接入应用注册和生命周期；没有整组能力仍为空白。但尚有 **4 项实现问题及 1 组验收测试缺口，共 5 项 OPEN_ACTIONABLE**，不足以支持 execution.md 中“全部完成、OPEN_ACTIONABLE=0”的结论。当前不是新增远程可利用漏洞的结论。

本轮检查本任务完整 plan.md、execution.md、test-mapping.md、实际生产调用链、相关测试与夹具、工作区及暂存区差异和未跟踪文件。原模块生命周期 T1–T5 的既有验收作为基础；本轮不重审无关模块和已关闭问题。审查未修改生产代码、正式测试、计划或执行报告；仅增加本报告及忽略目录中的临时诊断探针。

- IMPLEMENTATION_STATUS: NEEDS_FIX
- TEST_STATUS: PARTIAL（已有回归通过；新边界存在缺口）
- RESOURCE_EVIDENCE_STATUS: PARTIAL（筛选委托捕获对象保留已复现；附加扫描失败候选回收证据不足）
- PERFORMANCE_EVIDENCE_STATUS: NOT_APPLICABLE
- EXTERNAL_GATE_STATUS: NONE
- RELEASE_STATUS: NOT_ASSESSED
- OPEN_ACTIONABLE: 5
- BLOCKED_APPROVAL / BLOCKED_EXTERNAL: 0
- GOAL_STATUS: STOPPED_REVIEW（本轮仅审查，不自动修复）

## 计划验收矩阵

| 任务 | 状态 | 结论 |
| --- | --- | --- |
| CTX-01 配置与环境读取 | PARTIAL | 配置优先级、非 Root 配置、实例读取已实现；环境缺少计划 §3.1 的 HostBuilderContext 回退，见 FIX-003 |
| CTX-02 生命周期上下文 | PASS | 初始化和关闭的配置/环境扩展已接入当前 provider；无新增静态缓存；有真实阶段、宿主隔离和工厂解析测试 |
| SCAN-01 声明与快照 | PARTIAL | 声明继承、模块/应用附加范围、描述符只读集合、跨代校验已实现；配置时机仍读取可变 options，见 FIX-001 |
| SCAN-02 DI/选项/事件 | PARTIAL | 完整目录共享、筛选边界和晚选项补扫已接入；委托未及时释放，且模式变动可重复/漏发事件，见 FIX-001/002 |
| SCAN-03 插件与卸载 | PARTIAL | 私有附加 DLL 能加载，成功切换有旧代回收证据；类型读取失败诊断及附加程序集版本/失败候选回收验收不足，见 FIX-004/005 |
| DOC-01 文档和映射 | PASS（交付物） | README、迁移、插件指南、ADR、映射已存在；修复后需同步修正完成声明及补充测试映射 |

按完整验收任务计为 2 PASS / 4 PARTIAL / 0 完全未实现。此比例不是代码完成百分比。

## FIX-001：扫描时机未冻结，类型事件可重复或遗漏

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 问题：ConfigureModulesAsync 在 Pre 后和 Post 后分别读取同一个可变 `options.ServiceRegistrationMode`。调用方保留 options 并在配置钩子中修改它，会改变同一轮扫描的执行路径，违背计划 §3.3 的配置前快照要求。
- 证据：`framework/src/Bing.Core/Microsoft/Extensions/DependencyInjection/BingApplicationServiceCollectionExtensions.cs:131`、`:139`、`:156`；`BingLoader.RegisterTypes` 每次调用都会重新触发公开事件，没有本轮事件去重保护。
- 复现：初始 BeforeConfigureServices，在 Post 改为 Compatible，同一类型事件触发 2 次；初始 Compatible，在 Post 改为 BeforeConfigureServices，事件触发 0 次。两条路径均由本轮运行探针确认。
- 影响：事件订阅方可能重复产生副作用；后一条路径也会跳过末尾选项类型扫描。不是模块主动调用 RegisterTypes 所致。
- 修复目标：扫描时机和范围由配置回调完成后的不可变快照决定。
- 修复要求：在首个模块配置钩子前复制运行参数；配置循环只消费快照，不在中途再次读取调用方 options。保留原两种模式和公开事件契约。
- 验证方式：覆盖 Pre/Post 改写原 options、两个方向切换，同一类型事件恰好一次且选项绑定有效；同步/异步入口均验证。

## FIX-002：筛选委托在注册结束后仍被保留

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 问题：`DependencyTypeFinder._conventionalTypeFilter` 为 readonly 字段，查找器以实例单例注册，成功、失败及关闭路径均不清理委托。即使关闭自动 DI，仍注册持有该委托的查找器。
- 证据：`framework/src/Bing.Core/Bing/DependencyInjection/DependencyTypeFinder.cs:18`、`:30`；`BingApplicationServiceCollectionExtensions.cs:122`；`BingModuleRegistration.EndConfiguration/ReleaseAsync` 无相应清理。
- 复现：只保留 IServiceCollection，丢弃调用方的筛选对象引用并进行 GC；AutoRegisterServices=true 与 false 两条路径中对象弱引用均仍存活。引用链为服务集合 → 查找器实例 → 委托 → 捕获对象。
- 影响：本应只在注册期使用的对象图被延长到服务集合/容器生命周期；违反计划 §3.3“筛选完成即释放不再需要的委托”。这不等同于证明存在无法回收的静态全局泄漏。
- 修复目标：应用注册专属路径保存筛选后的类型结果，及时解除不再需要的委托引用。
- 修复要求：成功、异常、取消和自动 DI 关闭路径都处理清理；不改变公开查找器原有无筛选构造函数语义，不靠清空已筛选结果导致后续查询重新包含被排除类型。
- 验证方式：在服务集合/容器仍存活时验证捕获对象可回收；覆盖成功、失败和自动注册关闭，并确认后续查找结果稳定。

## FIX-003：通用宿主环境缺少 HostBuilderContext 回退

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 问题：必需环境方法的 resolver 为 `_ => null`，可选方法只检查直接 IHostEnvironment 注册。计划 §3.1 明确规定无直接配置/环境注册时，从 HostBuilderContext 实例读取。
- 证据：`framework/src/Bing.Core/Microsoft/Extensions/DependencyInjection/ServiceCollectionHostEnvironmentExtensions.cs:17`、`:26`。
- 复现：集合仅注册包含有效 HostingEnvironment 的 HostBuilderContext 实例；GetHostEnvironmentOrNull 返回 null。本轮探针已确认；必需方法沿同一逻辑抛缺失异常。
- 影响：使用计划允许的宿主上下文注册方式时，配置可读取而环境不可读取。
- 修复目标：无直接 IHostEnvironment 描述符时回退到最后一个有效 HostBuilderContext 实例的 HostingEnvironment。
- 修复要求：保持直接注册优先；最后直接注册为工厂/类型/非单例时仍拒绝回退，不执行工厂，不生成默认环境。同步补充迁移说明。
- 验证方式：仅宿主上下文、直接实例优先、多个上下文、无效最后直接注册、上下文工厂不执行及缺失环境场景。

## FIX-004：附加程序集类型发现失败缺少声明者与阶段诊断

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 问题：显式附加程序集 GetTypes 抛 ReflectionTypeLoadException 时，只包装程序集名称和 LoaderExceptions 文本；未记录声明模块/应用来源及扫描阶段。插件加载器仅通过 Bing.ModuleDependencyPath 补充插件信息，这个包装异常没有该键。
- 证据：`framework/src/Bing.Core/Bing/Core/Modularity/BingModuleAssemblyFinder.cs:58`；`BingPluginLoader.cs:138` 至 `:145`；失败随后直接进入 FailConfiguration，未补充此阶段信息。
- 影响：插件入口和 marker 能加载、但附加程序集内某类型的基类/接口依赖缺失时，会失败却缺少计划要求的声明模块、插件 ID/清单及阶段，难以定位多插件部署问题。现有“marker DLL 整体缺失”测试不覆盖该分支。
- 修复目标：保留原异常并补齐附加程序集扫描来源和阶段；可归属插件时带上插件上下文。
- 修复要求：记录程序集到声明模块/应用来源的映射；多模块共享程序集时合理列出来源；异常诊断尽量使用名称/路径字符串，避免引入静态 Type 引用。
- 验证方式：隔离夹具中 marker 可加载，但另一类型依赖缺失；断言程序集、声明者、阶段、插件信息与 LoaderExceptions，并验证模块清理和旧代保留。

## FIX-005：补齐针对新扫描能力的验收证据

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 问题与证据：
  - `HotPluginHostTest.ReloadAsync_AdditionalAssembly_ShouldRegisterServiceAndUnloadWithGeneration`（`:32`）证明单个附加 DLL 成功加载和旧代回收；已有版本切换测试（`:109`）切换的是入口程序集，尚无同名不同版本的附加 DLL 在两代中的身份与服务行为断言。
  - `ReloadAsync_MissingAdditionalAssembly_ShouldKeepCurrentGeneration`（`:63`）验证旧代可用，但未捕获并断言失败候选 ALC 回收。已有配置/初始化阶段失败回收不能代替附加元数据预检失败分支。
  - `ModuleScanningTest.ModuleAssemblyDeclaration_ShouldScanSelectedModulesOnly`（`:86`）构造的未选模块与已选模块都指向测试程序集，且该程序集本就因启动模块进入扫描；不能验证“未选模块声明不扩大范围”。目前没有真实重复附加程序集输入的去重断言，也缺少自定义 loader + 附加声明、描述符不可变和输入冻结的直接测试。
  - FIX-001 至 FIX-004 的针对性边界目前未被现有用例断言。
- 影响：完整回归通过只能证明已覆盖行为，不能验收计划 §4/5 的上述场景；执行报告将资源证据和任务状态写为完全通过过早。
- 修复目标：以隔离夹具补齐上述边界；不要为每个排列组合重复整个旧生命周期矩阵。
- 修复要求：优先让测试可否定对应回归；为未选模块提供唯一的附加服务/选项程序集；资源断言使用弱引用与释放次数；更新 test-mapping.md 和 execution.md，保持本 review.md 为独立证据。
- 验证方式：新增定向测试先在双框架顺序通过，再按实际生产变更影响范围决定是否复跑完整回归。

## 本轮验证及证据复用

影响分析：审查无生产、公开契约、TFM、依赖或正式测试修改；只在 `TestResults/context-scan-review` 创建独立探针，使用上轮生成的 Core 测试输出依赖。关注路径为配置便捷访问、注册参数冻结和筛选闭包保留。风险集中在扫描事件与资源生命周期，无 SQL/数据库或基准测试需求。

本轮使用 SDK 8.0.424，net8.0 临时探针实际输出：

```text
HostEnvironmentFallback=False
FilterTargetRetained(autoRegister=True)=True
FilterTargetRetained(autoRegister=False)=True
TypeEventCount(BeforeConfigureServices->Compatible)=2
TypeEventCount(Compatible->BeforeConfigureServices)=0
```

前三项期望分别为 True、False、False；后两项期望均为 1。FIX-004 为源码调用链确认，未伪称已有运行复现。没有重复执行全量测试；复用本会话上一轮 Core 双框架各 470 通过、MVC 各 53 通过/1 跳过、WebApi 构建通过的证据，不能用它们否定上述未覆盖问题。

## TODO 分组

- Completed：两组主要 API、默认调用链、现有定向与全量回归、迁移文档已交付。
- Open Actionable：FIX-001 至 FIX-005；建议执行顺序为 FIX-001 → FIX-002 → FIX-003 → FIX-004，测试随修复补齐，最后完成 FIX-005 的独立剩余用例与报告核对。
- Blocked Approval / Blocked External：本轮无影响功能修复的阻塞；`.agents/runtime` 写权限问题仅影响工作流元数据，不计作产品功能缺失。
- Not Applicable：性能改善基准、第三方依赖漏洞扫描和完整 ABP 上层生态验收。
- Accepted Limitations：启动期默认上下文不可卸载；热更新为整组插件代切换；插件可信；宿主自行持有旧代对象仍可能阻止卸载。
- Verified Boundaries：本轮明确验证了上述三种边界的当前失败行为，不宣称一般容量边界。
- Deferred：自定义生命周期阶段、插件在线分发与多版本求解、不可信插件进程隔离、动态 Web 管道替换，共 4 个后续方向；不计入本期 5 项 TODO。

No-Progress Check: CHANGED（新差异有源码和运行证据）。Next Action: STOP_REVIEW，后续修复消费 FIX-001 至 FIX-005。
