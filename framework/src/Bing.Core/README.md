# Bing.Core
[![NuGet](https://img.shields.io/nuget/v/Bing.Core.svg)](https://www.nuget.org/packages/Bing.Core/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Core.svg)](https://www.nuget.org/packages/Bing.Core/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 框架地基：模块系统、约定式依赖注入、程序集查找、异常体系与选项绑定。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：179
>
> **上游 Bing 包**：无（本包是叶子/基础包）
>
> **三方依赖**：`Bing.Utils`、`Bing.Utils.Collections`、`System.Runtime.Loader`、`Microsoft.Extensions.Options`、`Microsoft.Extensions.Options.ConfigurationExtensions`、`Microsoft.Extensions.Logging`、`Microsoft.Extensions.Localization`、`Microsoft.Extensions.DependencyModel`、`Microsoft.Extensions.Hosting.Abstractions`

---

## 这是什么

`Bing.Core` 是整个 Bing.NetCore 框架的**根**，所有其它包最终都依赖它。它提供四件东西：

1. **模块系统** —— `IBingModule` / `BingModule` / `ModuleLevel` / `[DependsOnModule]`，配合 `AddBing().AddModule<T>()` 实现"两阶段启动"（`AddServices` 注册 → `UseModule` 装配管道）。
2. **约定式 DI** —— 实现 `ISingletonDependency` / `IScopedDependency` / `ITransientDependency` 即自动注册；`[Dependency(...)]` 可精细控制，`[IgnoreDependency]` 排除。
3. **异常体系** —— `Warning` / `BusinessException` / `ConcurrencyException` / `BingException`，统一携带错误码与 HTTP 状态码。
4. **基础设施** —— 程序集与类型查找器、选项批量绑定 `AddOptionsType`、`ILog`/`ILock` 等公共契约的默认实现。

## 安装

```bash
dotnet add package Bing.Core
```

## 快速上手

```csharp
using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

// ① 定义一个模块
[DependsOnModule(typeof(AspNetCoreModule))]
public class AppModule : BingModule
{
    public override ModuleLevel Level => ModuleLevel.Application;
    public override IServiceCollection AddServices(IServiceCollection services)
    {
        services.AddControllers();
        return services;
    }
}

// ② 宿主起来（Web）
services.AddBing().AddModule<AppModule>();
app.UseBing();

// ③ 宿主起来（控制台 / WinForm）
services.AddBing().AddModule<AppModule>();
serviceProvider.UseBing();
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddBing(Action<BingOptions>)` | `Microsoft.Extensions.DependencyInjection` | 框架总入口，返回 `IBingBuilder` |
| `IBingBuilder.AddModule<T>()` / `AddModules(params Type[])` | `Bing.Core.Builders` | 添加模块并递归拉入依赖，立即执行 `AddServices` |
| `UseBing(this IServiceProvider)` | `Microsoft.Extensions.DependencyInjection` | 非 Web 场景：遍历模块执行 `UseModule` |
| `EnableAop(Action<IAspectConfiguration>)` | `Bing.DependencyInjection` | 启用 AOP（注意入口叫 `EnableAop`，没有 `AddAop`） |
| `AddLocalLock()` | `Bing.Locks` | 进程内锁 `ILock` → `LocalLock` |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `IBingModule` / `BingModule` | `Bing.Core.Modularity` | 模块契约与基类 |
| `ModuleLevel` | `Bing.Core.Modularity` | Core=1 / Framework=10 / Application=20 / Business=30 |
| `ISingletonDependency` 等三个标记接口 | `Bing.DependencyInjection` | 约定式 DI 生命周期 |
| `[Dependency]` / `[IgnoreDependency]` | `Bing.DependencyInjection` | 注册行为精细控制 |
| `Warning` / `BusinessException` | `Bing.Exceptions` / `Bing` | 异常基类 |

## 与其它包的关系

**依赖本包**：`Bing.Aop.AspectCore`、`Bing.AspNetCore.Abstractions`、`Bing.Biz.OAuthLogin`、`Bing.Caching`、`Bing.Data`、`Bing.ExceptionHandling`、`Bing.Localization.Abstractions`、`Bing.Logging`、`Bing.MultiTenancy.Abstractions`、`Bing.Security`、`Bing.TextTemplating`、`Bing.Uow`

**三方 NuGet**：`Bing.Utils`、`Bing.Utils.Collections`、`System.Runtime.Loader`、`Microsoft.Extensions.Options`、`Microsoft.Extensions.Options.ConfigurationExtensions`、`Microsoft.Extensions.Logging`、`Microsoft.Extensions.Localization`、`Microsoft.Extensions.DependencyModel`、`Microsoft.Extensions.Hosting.Abstractions`

## 注意事项

模块启动顺序**只按** `Level → Order → FullName` 排序，`[DependsOnModule]` 只负责递归纳入依赖，**不决定顺序**。想控制同级别内的先后，请重写 `Order`。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Core

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
