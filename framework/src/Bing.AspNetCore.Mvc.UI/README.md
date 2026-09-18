# Bing.AspNetCore.Mvc.UI
[![NuGet](https://img.shields.io/nuget/v/Bing.AspNetCore.Mvc.UI.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Mvc.UI/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.AspNetCore.Mvc.UI.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Mvc.UI/) ![TFM](https://img.shields.io/badge/TFM-net6.0-blue.svg)

> Razor → 静态 HTML 生成与路由分析。
>
> **目标框架**：`net6.0` ｜ **分层**：L6 表现层 ｜ **当前版本**：7.0.0 ｜ **源文件**：8
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

把 Razor 页面/视图渲染成 HTML 字符串（生成静态页、邮件模板、预览等场景），并附带路由分析能力。

## 安装

```bash
dotnet add package Bing.AspNetCore.Mvc.UI
```

## 快速上手

```csharp
using Bing.AspNetCore.Mvc.UI.Extensions;

services.AddRazorHtml();

// 注入 IRazorHtmlGenerator 使用
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddRazorHtml()` | `Bing.AspNetCore.Mvc.UI.Extensions` | 注册 `IRazorHtmlGenerator` 与 `IRouteAnalyzer`（均 Scoped） |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `IRazorHtmlGenerator` / `DefaultRazorHtmlGenerator` | `Bing.AspNetCore.Mvc.UI.RazorPages` | HTML 生成器 |
| `IRouteAnalyzer` / `RouteAnalyzer` | `Bing.AspNetCore.Mvc.UI.RazorPages` | 路由分析 |

## 与其它包的关系

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.AspNetCore.Mvc.UI

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
