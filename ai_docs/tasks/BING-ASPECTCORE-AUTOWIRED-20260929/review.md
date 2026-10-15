<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-ASPECTCORE-AUTOWIRED-20260929
AI_REVIEWED_AT: 2026-09-30T09:08:13.0742009+08:00

# 中立属性注入适配审查

## 结论

实现方向合理：特性归 Core、容器适配器归 AspectCore、类型转发保持类型解析、入口采用 TryAddScoped。不能维持上轮“只有 SDK 门禁未完成”的结论：发现 2 个已复现的功能缺陷，另外存在资源验证、测试覆盖、Sample 和交付文档 4 组缺口，共 6 组可执行 TODO。审查未修改生产代码或既有测试，仅添加审查报告、日志和独立复现探针。

IMPLEMENTATION_STATUS: NEEDS_FIX
TEST_STATUS: EXISTING_SUITES_PASS_BUT_REVIEW_PROBES_REPRODUCE_DEFECTS
RESOURCE_EVIDENCE_STATUS: INCOMPLETE
EXTERNAL_GATE_STATUS: BLOCKED_EXTERNAL
RELEASE_STATUS: BLOCKED
OPEN_ACTIONABLE: 6

## 对计划逐项验收

| 计划 | 状态 | 依据 |
| --- | --- | --- |
| T1 中立特性、类型转发、移除 AspNetCore 引用 | PASS | 当前定义、工程引用及类型限定名测试符合要求；真实旧消费者兼容范围仍需补证据 |
| T2 工厂、筛选、继承、作用域及单次注入 | PARTIAL | 基本场景通过；混合标记重复执行、中间继承漏注入；回收未验证 |
| T3 EnableAutowired/EnableAop/UseServiceContext | PASS | 当前 57 项 AspectCore 测试包含两入口、幂等、自定义工厂保留；Sample 尚未迁移 |
| 测试与资源验收 | PARTIAL | 六个现有测试矩阵通过，遗漏场景见下文；不能将回收标为不适用 |
| 文档与交付 | PARTIAL | 有说明，无完整 Lazy 基类/旧二进制迁移示例；存在错误依赖列表和过强资源结论 |

## 本轮验证

影响范围：公共 Attribute 所属程序集与基类变化、AspectCore 解析回调、AspNetCore 项目依赖。风险 HIGH；未变更框架、生产依赖或包版本。先读取测试与调用链，再顺序执行受影响项目双目标测试、离线还原 Sample 依赖图、构建及运行探针。不是全仓库测试结论。

实际 SDK 为 8.0.425；已列出安装 SDK，未发现 8.0.424。未修改 global.json。

| 验证项目 | net8.0 | net6.0 |
| --- | --- | --- |
| Bing.Core.Tests | 610 通过，0 失败 | 610 通过，0 失败 |
| Bing.Aop.AspectCore.Tests | 57 通过，0 失败 | 57 通过，0 失败 |
| Bing.AspNetCore.Mvc.Tests | 58 通过，1 跳过 | 58 通过，1 跳过 |

日志位于本目录 review-项目名-目标框架.log。集成证据包括真实 AspectCore Host、模块关闭、循环子进程和 MVC 测试；不等同于全部外部系统集成已通过。MVC 的 UploadRawAsync 被跳过，不能算通过。

Sample 离线还原使用本地 NuGet 缓存，构建 0 错误。实际 net6.0 进程在 http://127.0.0.1:18744 监听，GET / 返回 404。独立探针调用 Sample 原有 CreateHostBuilder，StartAsync、HTTP、StopAsync 成功；其实际工厂是 AspectCore.DependencyInjection.PropertyInjectorFactory，而非 Bing 工厂。Sample 只有 AppModule/Startup/Program，模块只调用 AddControllers/UseRouting，没有控制器与端点映射。因此确认“宿主能运行”，不能确认“属性注入及 Lazy 业务消费正常”。

独立 probe/ReviewProbe.csproj 的实测输出：

```text
MIXED bingSets=2 nativeSets=1
MIDDLE_OVERRIDE injected=False
SAMPLE rootStatus=404
SAMPLE factory=AspectCore.DependencyInjection.PropertyInjectorFactory
SAMPLE stopped=true
```

## FIX-001：混合标记重复注入

- 严重程度：HIGH；处理要求：MUST_FIX；状态：OPEN_ACTIONABLE。
- 问题/证据：同一对象的不同属性分别使用 Autowired 和 FromServiceContext，Bing 属性 setter 实际执行 2 次。BingPropertyInjectorFactory.cs:45 的公开 Create 扫描全部标记，AspectCore 原生回调执行一次后，:246 的 Bing 回调再次执行 Bing 属性。
- 影响：重复 setter 副作用、额外 Transient 实例/资源创建，违反每属性一次的要求。已有“双标记同一属性”测试未覆盖此组合。
- 修复目标/要求：统一每次对象激活的注入职责，兼容原生标记；不可用永久实例集合去重制造保留路径。
- 验证方式：正式测试覆盖不同属性混用、同属性双标记、继承标记混用；断言 setter 和 Transient 创建次数均为 1。

## FIX-002：中间基类标记漏注入

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：OPEN_ACTIONABLE。
- 问题/证据：BingPropertyInjectorFactory.cs:212-224 只检查当前属性与 GetBaseDefinition 返回的最初声明。Root 未标记，Middle override 标记 Autowired，Leaf 再 override 时返回未注入。probe 实测 injected=False。
- 影响：多层基类属性注入失效，LazyServiceProvider 等继承属性可能保持 null。
- 修复目标/要求：沿真实 override 链检查中间层标记，同时区分 new 隐藏成员，不把无关属性标记继承过去。
- 验证方式：三层继承、隐藏属性、继承特性与直接标记组合的直接测试。

## FIX-003：补齐工厂缓存/插件回收证据

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：OPEN_ACTIONABLE。
- 问题/证据：三个 ConcurrentDictionary 强持有 Type/PropertyInfo；无静态缓存并不能证明插件上下文可回收。AspectCore 测试没有 WeakReference/AssemblyLoadContext 用例，Core 资源探针使用 Microsoft DI，未走本适配器。
- 影响：当前无法验收用户明确要求的“作用域及插件代释放后新缓存不阻止回收”；execution.md 把资源标为 NOT_APPLICABLE 不成立。
- 修复目标/要求：真实 AspectCore 作用域创建插件对象并触发缓存后释放，检查工厂、服务提供程序与可回收上下文弱引用；区分底层 AspectCore 保留与新增缓存保留。发现明确保留路径后再修复。
- 验证方式：有界 GC 直接回收测试，不以工作集或普通测试通过替代，不要求默认 ALC 卸载。

## FIX-004：补齐行为及消费链测试

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：OPEN_ACTIONABLE。
- 问题/证据：AutowiredContainerBehaviorTest 只断言缺失属性为 null，未验证保留已有值；Scoped 仅比较不同对象；实例单例仅 IsDisposed。已有 AspectCoreBingModuleLifecycleTest 确实验证容器创建单例释放一次，但不足以覆盖 Scoped/Transient 属性依赖。旧兼容用例只 Type.GetType，没有旧编译消费者执行；没有真实 ILazyServiceProvider 属性消费链。
- 影响：计划的生命周期不变、单次释放、Lazy 使用及旧二进制承诺缺少直接依据。
- 修复目标/要求：补上述直接测试，类型归属断言 Bing.Core；旧消费者仅承诺普通特性使用兼容，不将原基类强转等全部 ABI 使用无条件称为兼容。
- 验证方式：真实容器解析与释放计数、调用基类 LazyGetRequiredService、已编译旧夹具的加载/反射/注入。

## FIX-005：Sample 缺少属性注入迁移及成功请求

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：OPEN_ACTIONABLE。
- 问题/证据：Program.cs 调用 UseServiceContext，但 Startup.cs 和 AppModule.cs 均未 EnableAutowired/EnableAop。移除 Bing.AspNetCore 的 Aop 引用后 Sample 也没有直接引用 Bing.Aop.AspectCore。真实运行仅能得到 404。
- 影响：该 Sample 不能证明新适配器与控制器基类 Lazy 属性可用；新增依赖该属性的控制器也不会得到中立标记注入。缺少端点是现有 Sample 边界，不是本轮制造的 HTTP 回归。
- 修复目标/要求：添加明确的适配器引用和注册，提供最小只读端点及映射，使用注入属性或基类 Lazy 服务。
- 验证方式：正常入口启动、HTTP 200 与业务值断言、注入非空、停止与释放；补可重复的 Sample 冒烟测试。

## FIX-006：修正文档、注释及交付结论

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；状态：OPEN_ACTIONABLE。
- 问题/证据：AOP README:53 仍把 Bing.AspNetCore 列为依赖本包；DI 指南:78 写缺失服务留下 null 而非保留原值。缺少普通属性、Lazy 基类和旧二进制迁移完整示例。execution.md 的资源“不适用”与“仅 SDK 未完成”结论过强。新增测试仍有大量单行 summary、DoubleMarkedConsumer._service 无 XML 注释、override 未优先 inheritdoc。
- 修复目标/要求：更新真实依赖图和迁移步骤、准确表述兼容边界及自定义工厂保留行为、补示例与中文注释、逐符号映射到直接测试；修正验收状态。
- 验证方式：示例编译，核对符号与实际方法，注释 diff 不改变业务逻辑。

## 分类与停止条件

- Completed：中立契约/类型名称转发、两个注册入口及现有六组回归；Sample 构建和宿主启动。
- Open Actionable：FIX-001 至 FIX-006。本轮只审查，不自动修复。
- Blocked External：SDK 8.0.424 验证；与代码缺陷分开。
- Blocked Approval：无。
- Not Applicable：Autofac 实现、自动循环解环和新卸载框架不属于本期；现有可回收上下文回收测试属于本期。
- Accepted Limitations：普通 Microsoft DI 不提供属性注入；UseServiceContext 单独使用不启用 Bing 标记。
- Verified Boundaries：Sample 当前返回 404；现有循环依赖由受控子进程验证。
- Deferred：无新增延期项。回调每次为所有服务创建两个注入器的分配成本可后续量测，目前没有性能预算，未将其直接定性为性能故障。
