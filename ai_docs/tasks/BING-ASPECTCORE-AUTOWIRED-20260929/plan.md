# AspectCore 中立属性注入适配计划

## 目标

将 `AutowiredAttribute` 下沉到 `Bing.Core`，改为不依赖 AspectCore 的普通特性；由 `Bing.Aop.AspectCore` 通过自定义 `IPropertyInjectorFactory` 实现属性注入，同时保持现有源码、旧程序集类型解析和原生 `FromServiceContextAttribute` 兼容。

## 实施任务

### T1：中立注入契约

- 在 `Bing.Core` 定义 `Bing.Aspects.AutowiredAttribute : Attribute`，保持命名空间、类型名和属性/字段使用范围。
- 从 `Bing.Aop.AspectCore` 删除原定义，并用 `TypeForwardedTo` 转发到 `Bing.Core`。
- `Bing.AspNetCore` 直接使用 `Bing.Core` 特性，移除仅由属性注入产生的 AspectCore 项目引用。

### T2：AspectCore 属性注入器

- 实现作用域级 `BingPropertyInjectorFactory : IPropertyInjectorFactory`，不调用 AspectCore 默认工厂。
- 同时识别 `AutowiredAttribute` 和 `FromServiceContextAttribute`。
- 仅注入公开、实例、可写、非索引属性，支持继承属性，不注入字段。
- 使用当前作用域服务提供程序；缓存随作用域释放，避免静态持有插件类型。
- 服务缺失保留原值，setter 异常原样传播，同一属性只注入一次。

### T3：注册入口与兼容行为

- 新增 `EnableAutowired(IServiceCollection)`，使用 `TryAddScoped` 注册自定义工厂。
- `EnableAop` 自动调用 `EnableAutowired`。
- 仅使用属性注入的宿主调用 `EnableAutowired`；仅 `UseServiceContext` 不自动启用中立特性。
- 不改变 `ILazyServiceProvider`、生命周期或基类属性签名，不自动解决属性循环依赖。

### T4：测试、文档和验证

- 增加契约、类型转发、注册、属性行为、原生特性、作用域、释放和循环测试。
- 更新属性注入、架构依赖和迁移文档及测试映射。
- 顺序验证 Core、AspectCore、MVC 的 `net8.0`/`net6.0`，并构建 WebApi；使用锁定 SDK `8.0.424`。

## 验收标准

- `AutowiredAttribute` 不引用 AspectCore；现有源码用法保持不变。
- 旧程序集限定名可解析，原生 `FromServiceContextAttribute` 继续工作。
- AspectCore 使用当前作用域注入，无永久类型缓存和根容器捕获。
- 受影响测试和示例构建通过；无依赖或版本升级。
