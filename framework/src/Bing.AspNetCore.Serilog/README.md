# Bing.AspNetCore.Serilog
[![NuGet](https://img.shields.io/nuget/v/Bing.AspNetCore.Serilog.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Serilog/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.AspNetCore.Serilog.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Serilog/) ![TFM](https://img.shields.io/badge/TFM-net6.0-blue.svg)

> Serilog 在 ASP.NET Core 中的集成与日志增强中间件。
>
> **目标框架**：`net6.0` ｜ **分层**：L6 表现层 ｜ **当前版本**：7.0.0 ｜ **源文件**：3
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

`UseBingSerilogEnrichers()` 把租户 / 用户 / 客户端 / 关联 ID 注入 Serilog 的 `LogContext`。

## 安装

```bash
dotnet add package Bing.AspNetCore.Serilog
```

## 快速上手

```csharp
app.UseAuthentication();
app.UseBingSerilogEnrichers();   // ⚠ 必须在 UseAuthentication() 之后
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `UseBingSerilogEnrichers(this IApplicationBuilder)` | `Microsoft.AspNetCore.Builder` | 注入租户/用户/客户端/关联 ID |

## 与其它包的关系

## 注意事项

⚠ 注册顺序：必须在 `UseAuthentication()` **之后**，否则拿不到用户信息。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.AspNetCore.Serilog

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
