# Bing.EntityFrameworkCore
[![NuGet](https://img.shields.io/nuget/v/Bing.EntityFrameworkCore.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.EntityFrameworkCore.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore/) ![TFM](https://img.shields.io/badge/TFM-net6.0-blue.svg)

> EF Core 数据访问核心：`UnitOfWorkBase : DbContext`、`RepositoryBase` / `StoreBase`。
>
> **目标框架**：`net6.0` ｜ **分层**：L3 数据访问实现层 ｜ **当前版本**：7.0.0 ｜ **源文件**：39
>
> **上游 Bing 包**：`Bing.Data.Sql`、`Bing.Dapper.Core`、`Bing.Ddd.Domain`、`Bing.Auditing`
>
> **三方依赖**：`Microsoft.Bcl.HashCode`、`Microsoft.EntityFrameworkCore`、`Microsoft.EntityFrameworkCore.Relational`、`Microsoft.EntityFrameworkCore.Abstractions`

---

## 这是什么

把 EF Core 接入框架的仓储与工作单元体系。⚠ **运行时命名空间与包名不同**：类型在 **`Bing.Datas.EntityFramework.Core`**。

## 安装

```bash
dotnet add package Bing.EntityFrameworkCore
```

## 快速上手

```csharp
// 具体库由 Provider 包提供，例如：
services.AddMySqlUnitOfWork<IAdminUnitOfWork, AdminUnitOfWork>(connection);
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddEfCoreSqlQueryFactory()` | `Bing.EntityFrameworkCore` | 注册 `IEfCoreSqlQueryFactory` 与库标识解析 |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `UnitOfWorkBase` | `Bing.Datas.EntityFramework.Core` | 继承 `DbContext`，同时实现 `IUnitOfWork` |
| `RepositoryBase` / `StoreBase` / `CompactRepositoryBase` | `Bing.Datas.EntityFramework.Core` | 仓储与存储实现 |

## 与其它包的关系

**本包依赖**：`Bing.Data.Sql`、`Bing.Dapper.Core`、`Bing.Ddd.Domain`、`Bing.Auditing`

**依赖本包**：`Bing.EntityFrameworkCore.MySql`、`Bing.EntityFrameworkCore.Oracle`、`Bing.EntityFrameworkCore.PostgreSql`、`Bing.EntityFrameworkCore.SqlServer`、`Bing.EntityFrameworkCore.Sqlite`

**三方 NuGet**：`Microsoft.Bcl.HashCode`、`Microsoft.EntityFrameworkCore`、`Microsoft.EntityFrameworkCore.Relational`、`Microsoft.EntityFrameworkCore.Abstractions`

## 注意事项

具体数据库的 `AddXxxUnitOfWork` 入口在 `Bing.EntityFrameworkCore.<数据库>` 包里。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.EntityFrameworkCore

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
