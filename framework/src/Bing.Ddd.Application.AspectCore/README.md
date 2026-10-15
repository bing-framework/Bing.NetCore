# Bing.Ddd.Application.AspectCore

[![Bing.Ddd.Application.AspectCore](https://img.shields.io/nuget/v/Bing.Ddd.Application.AspectCore.svg)](https://www.nuget.org/packages/Bing.Ddd.Application.AspectCore/) [![Bing.Ddd.Application.AspectCore](https://img.shields.io/nuget/vpre/Bing.Ddd.Application.AspectCore.svg)](https://www.nuget.org/packages/Bing.Ddd.Application.AspectCore/) [![Bing.Ddd.Application.AspectCore](https://img.shields.io/nuget/dt/Bing.Ddd.Application.AspectCore.svg)](https://www.nuget.org/packages/Bing.Ddd.Application.AspectCore/)

`Bing.Ddd.Application.AspectCore` 是应用层工作单元标记的 AspectCore 执行器。工作单元特性本身位于 `Bing.Ddd.Application.Contracts`，因此应用服务契约和 DTO 不需要引用 AspectCore。

## 使用

执行器所在宿主需要同时启用 AspectCore 服务上下文和工作单元适配器：

```csharp
services.EnableAop();
services.EnableBingUnitOfWork();
hostBuilder.UseServiceContext();
```

`EnableBingUnitOfWork` 可以重复调用。它识别接口、实现方法和类型上的 `[UnitOfWork]`，调用成功后执行 `CommitAsync`，随后调用实现对象的 `ICommitAfter.CommitAfter`。目标异常、取消或提交异常都不会执行提交后回调。

## 标记位置与范围

```csharp
public interface IOrderService
{
    [UnitOfWork]
    Task SaveAsync();
}
```

`UnitOfWorkAttribute.Scope` 使用 `BingUnitOfWorkScope`：`Aspect` 默认只在最外层调用提交，`Nested` 和 `None` 映射到 AspectCore 对应范围。接口或实现类型上的标记由 AspectCore 的类型验证进入代理；仅实现方法声明标记时，适配器会在当前作用域补充接口代理。需要接口方法拦截时服务应以接口注册，类代理仍受 AspectCore 可重写成员规则限制。

## 依赖关系

- `Bing.Ddd.Application.Contracts`：中立 `[UnitOfWork]` 和 `BingUnitOfWorkScope`，目标框架 `netstandard2.0`。
- `Bing.Aop.AspectCore`：AspectCore 服务上下文、属性注入和基础拦截能力，目标框架 `net6.0`。
- `Bing.Ddd.Application.AspectCore`：本包的工作单元选择器和拦截器，目标框架 `net6.0`。

不启用本适配器时，`[UnitOfWork]` 只是一项契约标记，不会自动提交工作单元。
