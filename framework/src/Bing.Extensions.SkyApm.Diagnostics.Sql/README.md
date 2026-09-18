# Bing.Extensions.SkyApm.Diagnostics.Sql
[![NuGet](https://img.shields.io/nuget/v/Bing.Extensions.SkyApm.Diagnostics.Sql.svg)](https://www.nuget.org/packages/Bing.Extensions.SkyApm.Diagnostics.Sql/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Extensions.SkyApm.Diagnostics.Sql.svg)](https://www.nuget.org/packages/Bing.Extensions.SkyApm.Diagnostics.Sql/) ![TFM](https://img.shields.io/badge/TFM-net6.0-blue.svg)

> 把 `Bing.Data.Sql` 的 SQL 查询接入 SkyAPM / SkyWalking 链路追踪。
>
> **目标框架**：`net6.0` ｜ **分层**：横切 / 可观测性 ｜ **当前版本**：7.0.0 ｜ **源文件**：2
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

注册一个 `ITracingDiagnosticProcessor`（`SqlQueryTracingDiagnosticProcessor`），让 SQL 执行出现在 SkyWalking 的调用链里。

## 安装

```bash
dotnet add package Bing.Extensions.SkyApm.Diagnostics.Sql
```

## 快速上手

```csharp
using SkyApm.Diagnostics.Sql;   // ⚠ 命名空间不是 Bing.*

// 在 SkyAPM 的注册构建器上（不是 IServiceCollection）
extensions.AddSqlQuery();
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddSqlQuery(this SkyApmExtensions)` | **`SkyApm.Diagnostics.Sql`** | ⚠ 挂在 SkyAPM 构建器上，**不是** `services.AddXxx` |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `SqlQueryTracingDiagnosticProcessor` | `SkyApm.Diagnostics.Sql` | 诊断处理器 |

## 与其它包的关系

## 注意事项

⚠ 如果你写 `services.AddSqlQuery(...)` 编译不过——这不是 bug，它本来就不在 `IServiceCollection` 上。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [横切能力文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/横切能力文档.md) —— 邮件 / 模板 / 锁 / 本地化 / 事件总线的详解与选型
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Extensions.SkyApm.Diagnostics.Sql

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
