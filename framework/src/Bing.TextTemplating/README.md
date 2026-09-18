# Bing.TextTemplating
[![NuGet](https://img.shields.io/nuget/v/Bing.TextTemplating.svg)](https://www.nuget.org/packages/Bing.TextTemplating/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.TextTemplating.svg)](https://www.nuget.org/packages/Bing.TextTemplating/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 文本模板抽象：模板定义、内容提供与渲染引擎注册。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：横切 / 工具 ｜ **当前版本**：7.0.0 ｜ **源文件**：16
>
> **上游 Bing 包**：`Bing.Core`
>
> **三方依赖**：`System.Collections.Immutable`

---

## 这是什么

提供模板定义发现（`ITemplateDefinitionProvider`）、内容提供（`ITemplateContentProvider`）、渲染前内容贡献（`ITemplateContentContributor`）以及渲染引擎抽象（`ITemplateRenderingEngine`）。

⚠ **本包没有任何 `IServiceCollection` 扩展**，需要手动装配。

## 安装

```bash
dotnet add package Bing.TextTemplating
```

## 快速上手

```csharp
services.Configure<BingTextTemplatingOptions>(o =>
{
    o.DefinitionProviders.Add<MyTemplateDefinitionProvider>();
    o.RenderingEngines["Scriban"] = typeof(ScribanTemplateRenderingEngine);
    o.DefaultRenderingEngine = "Scriban";
});
// 并自行注册 ITemplateDefinitionManager / ITemplateContentProvider / ITemplateRenderer

await templateRenderer.RenderAsync("WelcomeEmail", new { Name = "张三" });
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `BingTextTemplatingOptions` | `Bing.TextTemplating` | `DefinitionProviders` / `ContentContributors` / `RenderingEngines` / `DefaultRenderingEngine` |
| `ITemplateDefinitionProvider` / `TemplateDefinitionProviderBase` | `Bing.TextTemplating` | 声明有哪些模板 |
| `ITemplateContentProvider` / `ITemplateContentContributor` | `Bing.TextTemplating` | 内容来源与渲染前注入 |
| `ITemplateRenderer` / `BingTemplateRenderer` | `Bing.TextTemplating` | 渲染门面 |

## 与其它包的关系

**本包依赖**：`Bing.Core`

**依赖本包**：`Bing.TextTemplating.Scriban`

**三方 NuGet**：`System.Collections.Immutable`

## 注意事项

⚠ 没有 `AddTextTemplating`——不要照着别的模块找注册入口。`RenderingEngines` 的键名必须与模板定义里声明的引擎名一致。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [横切能力文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/横切能力文档.md) —— 邮件 / 模板 / 锁 / 本地化 / 事件总线的详解与选型
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.TextTemplating

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
