# 依赖注入与 AOP

> **适用版本**：框架 `7.0.0`｜源码核对：2026-10-13

## 功能说明

框架提供约定式 Microsoft DI 注册、`ILazyServiceProvider` 延迟解析，以及 AspectCore 3.0 的属性注入、忽略拦截、参数验证和工作单元适配。`AutowiredAttribute`、验证标记和工作单元标记位于中立契约包，业务契约无需依赖 AspectCore。

## 核心实现

- 约定注册：`framework/src/Bing.Core/Bing/DependencyInjection/DependencyModule.cs`
- 延迟解析：`framework/src/Bing.Core/Bing/DependencyInjection/LazyServiceProvider.cs`
- 属性注入器：`framework/src/Bing.Aop.AspectCore/Bing/DependencyInjection/BingPropertyInjectorFactory.cs`
- AOP 注册：`framework/src/Bing.Aop.AspectCore/Bing/DependencyInjection/AspectCoreExtensions.cs`
- 工作单元适配：`framework/src/Bing.Ddd.Application.AspectCore/`

## 用法

```csharp
services.EnableAop(options => options.EnableBingParameterValidation());
services.EnableBingUnitOfWork();
hostBuilder.UseServiceContext();
```

只需属性注入时可调用 `services.EnableAutowired()`。普通 Microsoft DI 不执行 `[Autowired]`；优先使用构造函数注入，`ILazyServiceProvider` 适合延迟取得低频依赖，不能用来掩盖循环依赖。

## 依赖与配置

- 中立标记：`Bing.Core`、`Bing.Validation.Abstractions`、`Bing.Ddd.Application.Contracts`。
- 执行器：`Bing.Aop.AspectCore` 和 `Bing.Ddd.Application.AspectCore`，目标框架以项目文件为准。
- AspectCore 宿主必须启用 `UseServiceContext()`。

## 限制与注意事项

- 属性注入仅处理公开、实例、可写、非索引属性；字段和非公开 setter 不注入。
- 缺失的属性服务保留原值；必须存在的依赖应使用构造函数注入。
- `ValidateOnBuild` 不能完整发现工厂、动态解析、属性注入和运行时 `Lazy<T>` 环。
- 多接口转发同一可释放实例可能被底层容器重复记录；资源服务应使用单一所有者和非释放型适配器。
- 详细所有权与循环依赖边界见[模块 DI 与资源所有权指南](../guides/modularity-di-resources.md)。

