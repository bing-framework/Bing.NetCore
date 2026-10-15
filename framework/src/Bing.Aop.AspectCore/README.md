# Bing.Aop.AspectCore
[![NuGet](https://img.shields.io/nuget/v/Bing.Aop.AspectCore.svg)](https://www.nuget.org/packages/Bing.Aop.AspectCore/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Aop.AspectCore.svg)](https://www.nuget.org/packages/Bing.Aop.AspectCore/) ![TFM](https://img.shields.io/badge/TFM-net6.0-blue.svg)

> 基于 AspectCore 的 AOP 基础设施（属性注入、参数校验和通用拦截）。
>
> **目标框架**：`net6.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **AspectCore**：3.0.0
>
> **上游 Bing 包**：`Bing.Core`、`Bing.Validation.Abstractions`
>
> **三方依赖**：`AspectCore.Extensions.AspectScope`、`AspectCore.Extensions.Configuration`、`AspectCore.Extensions.DependencyInjection`

---

## 这是什么

为框架提供动态代理能力。启用后，属性注入、参数校验和其他适配器拦截器才会生效。

## 安装

```bash
dotnet add package Bing.Aop.AspectCore
```

## 快速上手

```csharp
services.EnableAop(o =>
{
    o.ThrowAspectException = false;
    o.NonAspectPredicates.AddNamespace("DotNetCore.CAP");   // 排除不需要代理的命名空间
});
```

参数校验默认关闭，需要时显式打开中立标记的执行器：

```csharp
services.EnableAop(o => o.EnableBingParameterValidation());
```

`NotNullAttribute`、`NotEmptyAttribute` 和 `Bing.Validation.ValidAttribute` 位于中立契约包；只有启用上述配置后，AspectCore 才会解释这些参数标记。原有 `o.EnableParameterAspect()` 仍可使用。

如果只需要属性注入而不需要动态代理，可以单独启用适配器：

```csharp
services.EnableAutowired();
```

宿主仍需调用 `UseServiceContext()`。`AutowiredAttribute` 定义在 `Bing.Core`，不再继承 AspectCore 类型；`EnableAop()` 会自动启用属性注入。仅调用 `UseServiceContext()` 不会自动启用中立 `[Autowired]`。原生 `[FromServiceContext]` 仍保持兼容。

同一个 `IServiceCollection` 只应调用一次 `EnableAop()`；重复调用会保持第一次注册，后续配置委托不会再次执行。不同宿主的服务集合相互独立，可以分别配置。

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| **`EnableAop(Action<IAspectConfiguration>)`** | `Bing.DependencyInjection` | ⚠ 入口叫 `EnableAop`，**没有 `AddAop`** |
| **`EnableAutowired()`** | `Bing.DependencyInjection` | 只启用 AspectCore 属性注入，不启用动态代理 |

## 与其它包的关系

**本包依赖**：`Bing.Core`、`Bing.Validation.Abstractions`

**依赖本包**：当前仓库中的 `Bing.Events` 等业务组件可按需引用本包；`Bing.AspNetCore` 不再反向依赖属性注入适配器。

**三方 NuGet**：`AspectCore.Extensions.AspectScope`、`AspectCore.Extensions.Configuration`、`AspectCore.Extensions.DependencyInjection`

## 注意事项

AOP 默认排除 DbContext、标记 `IgnoreAspect` 的接口以及带有中立 `[Ignore]` 标记的类型、方法和属性访问器。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- [AspectCore 3.0.0 迁移说明](../../../docs/migrations/aspectcore-3.0.md) —— 目标框架、依赖和工作单元验证边界
- NuGet 包页面：https://www.nuget.org/packages/Bing.Aop.AspectCore

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
