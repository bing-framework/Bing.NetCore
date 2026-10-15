<!-- AI_EXECUTION_STATUS: PARTIAL -->
AI_TASK_ID: BING-ASPECTCORE-AUTOWIRED-20260929
AI_EXECUTION_FINISHED_AT: 2026-09-29T23:33:40+08:00

# 执行记录

## 状态

- `IMPLEMENTATION_STATUS: PASS`
- `TEST_STATUS: PASS_ACTUAL_SDK`
- `PERFORMANCE_EVIDENCE_STATUS: NOT_APPLICABLE`
- `RESOURCE_EVIDENCE_STATUS: PARTIAL_DIRECT_EVIDENCE`
- `EXTERNAL_GATE_STATUS: BLOCKED_EXTERNAL`
- `RELEASE_STATUS: BLOCKED`
- `GOAL_STATUS: STOPPED_BLOCKED`

实现已完成，受影响测试在实际可用的 .NET SDK `8.0.425` 下通过。仓库 `global.json` 锁定 `8.0.424`，该 SDK 未安装，因此没有宣称锁定版本验证通过，也未修改锁定文件。任务状态脚本尝试登记时因 `.agents/runtime` 权限返回 `EPERM`，本报告作为人工执行记录保留。

## 变更范围

- `Bing.Core` 新增容器无关的 `Bing.Aspects.AutowiredAttribute`。
- `Bing.Aop.AspectCore` 删除旧特性定义，增加 `TypeForwardedTo`，新增作用域属性注入工厂和服务解析回调。
- `EnableAutowired()` 使用 `TryAddScoped` 注册工厂，并由 `EnableAop()` 自动启用；重复启用保持幂等并尊重调用方预注册工厂。
- `Bing.AspNetCore` 移除仅由属性注入产生的 AspectCore 项目引用。
- 增加中立契约、旧程序集限定名、注册、属性行为、作用域、释放、循环依赖和生命周期回归测试。
- 更新属性注入指南、架构说明、Core/AOP README 和测试探针。

## 关键行为映射

| 生产符号 | 直接验证 |
| --- | --- |
| `Bing.Aspects.AutowiredAttribute` | `AttributeReflectionTest.AutowiredAttribute_AttributeUsage_ShouldTargetPropertyAndField`、`AutowiredAttribute_ShouldBeContainerNeutralAttribute` |
| `TypeForwardedTo(AutowiredAttribute)` | `AttributeReflectionTest.AutowiredAttribute_ShouldResolveThroughLegacyAspectCoreAssemblyName` |
| `AspectCoreExtensions.EnableAutowired` | `AutowiredContainerBehaviorTest.EnableAutowired_ShouldRegisterBingPropertyInjectorFactory`、`EnableAutowired_WhenCalledRepeatedly_ShouldRegisterAdapterOnce`、`EnableAutowired_ShouldRespectCustomPropertyInjectorFactory` |
| `BingPropertyInjectorFactory` | `AutowiredContainerBehaviorTest` 的公开属性、继承属性、缺失服务、已有值、setter 异常、索引器、非公开 setter、作用域释放和可回收类型缓存测试 |
| `BingPropertyInjectorCallback` | `AutowiredContainerBehaviorTest.Resolve_ShouldInjectAutowiredPropertyButNotField`、`Resolve_WhenDifferentPropertiesUseBothMarkers_ShouldSetEachOnce`、`Resolve_WhenPropertyHasBothMarkers_ShouldSetItOnce`、`Resolve_ShouldInjectInheritedNativePropertyOnce`、`Resolve_ShouldInjectNativeFromServiceContextProperty` |
| 模块与 Lazy 服务链 | `AspectCoreBingModuleLifecycleTest.ModuleService_ShouldUseInjectedLazyServiceProvider` |
| 循环依赖边界 | `AutowiredCycleProcessTest` 及 `Bing.Aop.AspectCore.CycleProbe` 受控子进程探针 |

## 验证结果

按影响范围顺序执行，所有命令均设置独立的 `DOTNET_CLI_HOME`，没有修改 `global.json`：

| 验证 | 结果 |
| --- | --- |
| `Bing.Aop.AspectCore.Tests` `net8.0` | 64/64 通过 |
| `Bing.Aop.AspectCore.Tests` `net6.0` | 64/64 通过 |
| `Bing.Core.Tests` `net8.0` | 610/610 通过 |
| `Bing.Core.Tests` `net6.0` | 610/610 通过 |
| `Bing.AspNetCore.Mvc.Tests` `net8.0` | 58 通过，1 个既有跳过 |
| `Bing.AspNetCore.Mvc.Tests` `net6.0` | 58 通过，1 个既有跳过 |
| `samples/Bing.Samples.WebApi` 构建与 HTTP 冒烟 | 构建成功，0 个错误；`GET /sample/lazy` 返回 200 且 `injected=true` |
| `git diff --check` | 通过；仅报告既有换行规范提示 |

构建输出中的 `NETSDK1138` 来自使用已结束支持期的 `net6.0`，`ValidAttribute.cs` 的 XML 参数警告和 MVC 测试的 `ASP0019` 也属于既有警告，与本次属性注入改动无关。

## 未完成门禁与限制

- 锁定 SDK `8.0.424` 未安装，锁定版本的同一测试矩阵状态为 `BLOCKED_EXTERNAL`。
- 属性注入仍依赖 AspectCore 的 `UseServiceContext()`；仅调用该宿主扩展不会启用中立 `AutowiredAttribute`。
- AspectCore 2.4 的属性解析路径不会自动释放瞬时可释放属性服务；当前记录为底层容器所有权限制，生产资源应使用作用域/单例或显式所有者。
- 本期没有实现 Autofac 执行器、属性循环自动解环或程序集卸载；这些不在已批准范围内。

## 审查修复记录

本轮根据 `review.md` 修复并验证以下问题：

- FIX-001：解析回调不再对混合标记对象重复执行 Bing 注入；不同属性分别使用 `[Autowired]` 和 `[FromServiceContext]` 时，各自 setter 均只调用一次。
- FIX-002：属性标记沿真实 override 链检查，中间基类声明的 `[Autowired]` 可传递到末级重写属性，同时保留 `new` 隐藏成员的区分。
- FIX-003：新增可回收动态类型触发工厂缓存后释放作用域的测试。该证据覆盖本适配器缓存不会继续持有类型；默认加载上下文插件的完整卸载仍不属于本期保证。
- FIX-004：新增缺失服务保留已有属性值、作用域服务释放一次，以及由 Bing 模块注册并通过 `[Autowired] ILazyServiceProvider` 再解析模块依赖的真实宿主测试；同时确认并记录 AspectCore 瞬时可释放属性服务的底层所有权限制。旧二进制完整消费夹具仍是后续增强项。
- FIX-005：Sample 增加 `Bing.Aop.AspectCore` 引用、`EnableAutowired`、控制器 DI 注册和端点映射；实际请求确认 Lazy 属性已注入。
- FIX-006：修正 AOP 依赖说明、属性缺失值行为文案、Sample 使用说明，并为新增测试私有字段补充中文 XML 注释。

仍需单独处理的外部门禁是仓库锁定的 .NET SDK `8.0.424` 未安装；本轮所有实际执行使用 `8.0.425`，未修改 `global.json`。
