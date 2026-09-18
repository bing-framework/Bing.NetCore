# Bing.EntityFrameworkCore.PostgreSql
[![NuGet](https://img.shields.io/nuget/v/Bing.EntityFrameworkCore.PostgreSql.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore.PostgreSql/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.EntityFrameworkCore.PostgreSql.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore.PostgreSql/) ![TFM](https://img.shields.io/badge/TFM-net6.0-blue.svg)

> EF Core PostgreSql Provider：AddPgSqlUnitOfWork<TService,TImpl>() 一行接入。
>
> **目标框架**：`net6.0` ｜ **分层**：L3 数据访问实现层 ｜ **当前版本**：7.0.0 ｜ **源文件**：5
>
> **上游 Bing 包**：`Bing.EntityFrameworkCore`
>
> **三方依赖**：`Npgsql.EntityFrameworkCore.PostgreSQL`

---

## 这是什么

为 PostgreSql 提供 EF Core 工作单元注册（内部 `AddDbContext` + 对应 Provider 配置），底层使用 Npgsql.EntityFrameworkCore.PostgreSQL。

## 安装

```bash
dotnet add package Bing.EntityFrameworkCore.PostgreSql
```

## 快速上手

```csharp
services.AddPgSqlUnitOfWork<IAdminUnitOfWork, AdminUnitOfWork>(
    Configuration.GetConnectionString("DefaultConnection"));
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddPgSqlUnitOfWork<TService,TImpl>(services, connection, ...)` | `Bing.Datas.EntityFramework.PgSql` | 注册 DbContext 工作单元 + `IUnitOfWork` ⚠ **不是** `AddPostgreSqlUnitOfWork` |

## 与其它包的关系

**本包依赖**：`Bing.EntityFrameworkCore`

**三方 NuGet**：`Npgsql.EntityFrameworkCore.PostgreSQL`

## 注意事项

注册后 `IUnitOfWork` / `IRepository<>` 即可注入使用。 ⚠ **不是** `AddPostgreSqlUnitOfWork`

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.EntityFrameworkCore.PostgreSql

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
