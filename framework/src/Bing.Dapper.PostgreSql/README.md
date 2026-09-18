# Bing.Dapper.PostgreSql
[![NuGet](https://img.shields.io/nuget/v/Bing.Dapper.PostgreSql.svg)](https://www.nuget.org/packages/Bing.Dapper.PostgreSql/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Dapper.PostgreSql.svg)](https://www.nuget.org/packages/Bing.Dapper.PostgreSql/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> Dapper PostgreSql Provider：方言、Builder 与 SqlProvider（Provider Key `bing.postgresql`）。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L3 数据访问实现层 ｜ **当前版本**：7.0.0 ｜ **源文件**：14
>
> **上游 Bing 包**：`Bing.Dapper.Core`
>
> **三方依赖**：`Npgsql`、`Microsoft.CodeAnalysis.PublicApiAnalyzers`

---

## 这是什么

让 `Bing.Data.Sql` 生成符合 PostgreSql 语法的 SQL，并用 Npgsql 执行。安装后调用 `AddPostgreSqlProvider()` 即可（内部会自动 `AddSqlCore()`）。

## 安装

```bash
dotnet add package Bing.Dapper.PostgreSql
```

## 快速上手

```csharp
services.AddPostgreSqlProvider();
services.AddSqlDataSource("main", DatabaseType.PostgreSql, connectionString);
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddPostgreSqlProvider()` | `Bing.Dapper.PostgreSql` | 注册 PostgreSql Provider（Provider Key `bing.postgresql`） |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `PostgreSqlSqlProvider` / `PostgreSqlDialect` / `PostgreSqlBuilder` | `Bing.Dapper.PostgreSql` | Provider 三件套 |

## 与其它包的关系

**本包依赖**：`Bing.Dapper.Core`

**三方 NuGet**：`Npgsql`、`Microsoft.CodeAnalysis.PublicApiAnalyzers`

## 注意事项

⚠ Provider 包**不配置默认数据源**，多 Provider 场景必须用具名数据源。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Dapper.PostgreSql

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
