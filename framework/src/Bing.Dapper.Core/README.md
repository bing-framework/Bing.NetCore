# Bing.Dapper.Core
[![NuGet](https://img.shields.io/nuget/v/Bing.Dapper.Core.svg)](https://www.nuget.org/packages/Bing.Dapper.Core/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Dapper.Core.svg)](https://www.nuget.org/packages/Bing.Dapper.Core/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> Dapper 执行核心：把 `Bing.Data.Sql` 落地为可执行查询与执行器。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L3 数据访问实现层 ｜ **当前版本**：7.0.0 ｜ **源文件**：38
>
> **上游 Bing 包**：`Bing.Data.Sql`
>
> **三方依赖**：`Dapper`、`Microsoft.Bcl.AsyncInterfaces`、`Microsoft.Extensions.Configuration.Abstractions`、`Microsoft.CodeAnalysis.PublicApiAnalyzers`

---

## 这是什么

提供 `ISqlQuery` / `ISqlExecutor` 的 Dapper 实现、Provider 路由（`SqlProviderRuntime`）、数据源注册与事务作用域。**7.0.0 起，连接与事务统一由 `ISqlTransactionScope` 管理**。

## 安装

```bash
dotnet add package Bing.Dapper.Core
```

## 快速上手

```csharp
// 注册：核心 + Provider + 数据源
services.AddSqlCore();
services.AddMySqlProvider();
services.AddSqlDataSource("reporting", DatabaseType.MySql, connectionString);

// 事务（7.0.0 唯一公开事务入口）
using var scope = transactionScopeFactory.Begin("reporting");
var executor = scope.CreateExecutor();
executor.ExecuteSql(sql, parameters);
scope.Commit();
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddSqlCore()` | `Bing.Dapper` | 注册解析器/过滤器/工厂等核心服务 |
| `AddSqlDataSource(key, DatabaseType, connectionString, providerKey)` | `Bing.Dapper` | 注册具名数据源 |
| `AddSqlServerProvider()` / `AddMySqlProvider()` / `AddPostgreSqlProvider()` / `AddOracleProvider()` / `AddSqliteProvider()` | `Bing.Dapper.*` | 各库 Provider（内部自动 `AddSqlCore()`） |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `ISqlQuery` / `ISqlExecutor` | `Bing.Data.Sql` | 查询与执行器 |
| `SqlProviderRuntime` | `Bing.Data.Sql` | Provider 路由（具体类，无接口） |
| `ISqlTransactionScopeFactory` / `ISqlTransactionScope` | `Bing.Data.Sql` | 7.0.0 起唯一的事务生命周期对象 |

## 与其它包的关系

**本包依赖**：`Bing.Data.Sql`

**依赖本包**：`Bing.Dapper.MySql`、`Bing.Dapper.Oracle`、`Bing.Dapper.PostgreSql`、`Bing.Dapper.SqlServer`、`Bing.Dapper.Sqlite`、`Bing.EntityFrameworkCore`

**三方 NuGet**：`Dapper`、`Microsoft.Bcl.AsyncInterfaces`、`Microsoft.Extensions.Configuration.Abstractions`、`Microsoft.CodeAnalysis.PublicApiAnalyzers`

## 注意事项

⚠ 7.0.0 已删除 `IDbConnectionManager` / `IDbTransactionManager` / `IDatabaseFactory` 以及 `ISqlQuery` 上的 `GetConnection`/`BeginTransaction` 等 API。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Dapper.Core

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
