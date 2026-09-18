# Bing.AspNetCore.Mvc
[![NuGet](https://img.shields.io/nuget/v/Bing.AspNetCore.Mvc.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Mvc/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.AspNetCore.Mvc.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Mvc/) ![TFM](https://img.shields.io/badge/TFM-net6.0-blue.svg)

> MVC 控制器基类、全局结果过滤与 MVC 选项扩展。
>
> **目标框架**：`net6.0` ｜ **分层**：L6 表现层 ｜ **当前版本**：7.0.0 ｜ **源文件**：16
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

提供 `ResultHandlerAttribute`（自动把返回值包装成 `ApiResult`）、`QueryControllerBase` / `CrudControllerBase`，以及 `MvcOptions.AddBing(services)`。

## 安装

```bash
dotnet add package Bing.AspNetCore.Mvc
```

## 快速上手

```csharp
services.AddControllers(o => o.AddBing(services));
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `MvcOptions.AddBing(this MvcOptions, IServiceCollection)` | `Bing.AspNetCore.Mvc` | 注入模型绑定器、`IRemoteStreamContent` 元数据与输出格式化器 |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `ResultHandlerAttribute` | `Bing.AspNetCore.Mvc.Filters` | 结果自动包装 |
| `BingExceptionFilter` | `Bing.AspNetCore.Mvc.ExceptionHandling` | MVC 异常过滤 |
| `QueryControllerBase` / `CrudControllerBase` | `Bing.AspNetCore.Mvc` | 查询 / CRUD 控制器基类 |

## 与其它包的关系

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.AspNetCore.Mvc

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
