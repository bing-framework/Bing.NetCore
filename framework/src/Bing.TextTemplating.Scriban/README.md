# Bing.TextTemplating.Scriban
[![NuGet](https://img.shields.io/nuget/v/Bing.TextTemplating.Scriban.svg)](https://www.nuget.org/packages/Bing.TextTemplating.Scriban/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.TextTemplating.Scriban.svg)](https://www.nuget.org/packages/Bing.TextTemplating.Scriban/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 文本模板的 Scriban 渲染引擎实现。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：横切 / 工具 ｜ **当前版本**：7.0.0 ｜ **源文件**：2
>
> **上游 Bing 包**：`Bing.TextTemplating`
>
> **三方依赖**：`Scriban`

---

## 这是什么

`ScribanTemplateRenderingEngine` 实现 `ITemplateRenderingEngine`，让 `Bing.TextTemplating` 能用 Scriban 语法渲染模板。

## 安装

```bash
dotnet add package Bing.TextTemplating.Scriban
```

## 快速上手

```csharp
o.RenderingEngines["Scriban"] = typeof(ScribanTemplateRenderingEngine);
o.DefaultRenderingEngine = "Scriban";
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `ScribanTemplateRenderingEngine` | `Bing.TextTemplating.Scriban` | Scriban 引擎实现 |
| `ScribanTemplateDefinitionExtensions` | `Bing.TextTemplating.Scriban` | 模板定义辅助扩展 |

## 与其它包的关系

**本包依赖**：`Bing.TextTemplating`

**三方 NuGet**：`Scriban`

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [横切能力文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/横切能力文档.md) —— 邮件 / 模板 / 锁 / 本地化 / 事件总线的详解与选型
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.TextTemplating.Scriban

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
