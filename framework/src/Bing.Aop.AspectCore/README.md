# Bing.Aop.AspectCore
[![NuGet](https://img.shields.io/nuget/v/Bing.Aop.AspectCore.svg)](https://www.nuget.org/packages/Bing.Aop.AspectCore/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Aop.AspectCore.svg)](https://www.nuget.org/packages/Bing.Aop.AspectCore/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 基于 AspectCore 的 AOP 基础设施（`[UnitOfWork]`、审计等切面）。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：11
>
> **上游 Bing 包**：`Bing.Core`、`Bing.Validation`
>
> **三方依赖**：`AspectCore.Extensions.AspectScope`、`AspectCore.Extensions.Configuration`、`AspectCore.Extensions.DependencyInjection`

---

## 这是什么

为框架提供动态代理能力。启用后 `[UnitOfWork]`、审计、参数校验等切面才会生效。

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

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| **`EnableAop(Action<IAspectConfiguration>)`** | `Bing.DependencyInjection` | ⚠ 入口叫 `EnableAop`，**没有 `AddAop`** |

## 与其它包的关系

**本包依赖**：`Bing.Core`、`Bing.Validation`

**依赖本包**：`Bing.AspNetCore`、`Bing.Events`

**三方 NuGet**：`AspectCore.Extensions.AspectScope`、`AspectCore.Extensions.Configuration`、`AspectCore.Extensions.DependencyInjection`

## 注意事项

AOP 默认排除 DbContext 与标记 `IgnoreAspect` 的接口。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Aop.AspectCore

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
