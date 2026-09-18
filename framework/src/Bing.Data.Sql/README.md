# Bing.Data.Sql
[![NuGet](https://img.shields.io/nuget/v/Bing.Data.Sql.svg)](https://www.nuget.org/packages/Bing.Data.Sql/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Data.Sql.svg)](https://www.nuget.org/packages/Bing.Data.Sql/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> ORM 无关的流利 SQL 引擎：`ISqlQuery` / `ISqlBuilder` / `IDialect` / `ISqlProvider`。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L3 数据访问实现层 ｜ **当前版本**：7.0.0 ｜ **源文件**：361
>
> **上游 Bing 包**：`Bing.Data.Sql.Analyzers`
>
> **三方依赖**：`Microsoft.CodeAnalysis.PublicApiAnalyzers`

---

## 这是什么

框架体量最大的工程（360+ 源文件）。它**不依赖任何 ORM**，负责把链式调用渲染成具体数据库的 SQL 与参数，并被 EF Core / FreeSQL / Dapper 三条数据访问链路复用。

核心能力：流利查询与变更 API、Lambda 表达式解析、方言（Dialect）抽象、实体映射与缓存、表引用与跨库查询。

## 安装

```bash
dotnet add package Bing.Data.Sql
```

## 快速上手

```csharp
// 通过工厂创建查询（数据源名由 AddSqlDataSource 注册）
var query = sqlQueryFactory.Create("reporting");

var list = query
    .Select("Id, Name")
    .From("User")
    .Where("Age", 18, Operator.GreaterEqual)
    .OrderBy("Id")
    .ToList<UserDto>();

// 参数化插值（避免拼接 SQL）
query.SqlInterpolated($"select * from User where Name = {name}");
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `Query()` / `Sql()` / `SqlInterpolated()` / `Procedure()` | `Bing.Data.Sql` | 非泛型描述入口；结果类型后置到 `ToList<TResult>` 等终结方法 |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `ISqlQuery` / `ISqlBuilder` | `Bing.Data.Sql` / `.Builders` | 查询与构建器 |
| `IDialect` / `DialectBase` | `Bing.Data.Sql` | 数据库方言抽象 |
| `ISqlProvider` | `Bing.Data.Sql` | Provider 抽象 |
| `ISqlTransactionScopeFactory` | `Bing.Data.Sql` | 事务作用域工厂 |

## 与其它包的关系

**本包依赖**：`Bing.Data.Sql.Analyzers`

**依赖本包**：`Bing.Dapper.Core`、`Bing.EntityFrameworkCore`

**三方 NuGet**：`Microsoft.CodeAnalysis.PublicApiAnalyzers`

## 注意事项

⚠ 7.0.0 有大规模 API 收敛（属破坏性变更，且不提供 `[Obsolete]` 过渡）：
- 起始阶段泛型结果改为**非泛型描述 + 终结方法**（`ToList<TResult>` / `ToEntity<TResult>`）；
- 删除 `As<TResult>()`，DTO 投影改用 `Select<TProjection>`；
- 删除高层 `FromTable` 与 `ClearSelect()`；
- `WhereIf` 改为**条件优先**；高层 `ToDictionary` / `SingleOrDefault` 已删除；
- 受影响的可选参数被拆成显式重载，**依赖反射/源生成器的调用方不能再假定默认参数元数据存在**。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Data.Sql

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
