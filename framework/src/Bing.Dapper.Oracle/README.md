# Bing.Dapper.Oracle
[![NuGet](https://img.shields.io/nuget/v/Bing.Dapper.Oracle.svg)](https://www.nuget.org/packages/Bing.Dapper.Oracle/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Dapper.Oracle.svg)](https://www.nuget.org/packages/Bing.Dapper.Oracle/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.1-blue.svg)

> Dapper Oracle Provider：方言、Builder 与 SqlProvider（Provider Key `bing.oracle`）。
>
> **目标框架**：`netstandard2.1` ｜ **分层**：L3 数据访问实现层 ｜ **当前版本**：7.0.0 ｜ **源文件**：12
>
> **上游 Bing 包**：`Bing.Dapper.Core`
>
> **三方依赖**：`Oracle.ManagedDataAccess.Core`、`Microsoft.CodeAnalysis.PublicApiAnalyzers`

---

## 这是什么

让 `Bing.Data.Sql` 生成符合 Oracle 语法的 SQL，并用 Oracle.ManagedDataAccess.Core 执行。安装后调用 `AddOracleProvider()` 即可（内部会自动 `AddSqlCore()`）。

## 安装

```bash
dotnet add package Bing.Dapper.Oracle
```

## 快速上手

```csharp
services.AddOracleProvider();
services.AddSqlDataSource("main", DatabaseType.Oracle, connectionString);
```

## 写入回传

`ReturningInto` 支持 INSERT VALUES、UPDATE 和 DELETE 的单行多列回传。UPDATE 和 DELETE 可用 `ReturningManyInto` 回传多行。两种命令都使用 `ExecuteReturning` 或 `ExecuteReturningAsync`，由调用方逐行投影。

```csharp
var command = executor.CreateUpdateBuilder()
    .Update(new SqlTableReference { TableName = "orders" })
    .Where("Status", "Pending")
    .Set("Status", "Ready")
    .BuildCommand()
    .ReturningManyInto(100, new OracleReturningColumn("Id", DbType.Int64));

var ids = await executor.ExecuteReturningAsync(command,
    static row => row.GetValue<long>("Id"), cancellationToken: cancellationToken);
```

`maxRows` 设置客户端输出集合的行数上限。超限时 Oracle 抛出 `ORA-20060` 并撤销本次写入，共享事务内的先前操作仍由事务所有者管理。没有匹配行时返回空列表。列表保留 Oracle 给出的顺序，返回顺序没有排序保证。

批量输出接受 `DbType.Byte`、`Int16`、`Int32`、`Int64`、`Decimal`、`Single`、`String`、`AnsiString`、`StringFixedLength`、`AnsiStringFixedLength`、`Binary` 和 `Date`。

`AnsiStringFixedLength` 用于 `CHAR`，`StringFixedLength` 用于 `NCHAR`。按物理列为字符列指定字符长度，为 `RAW` 指定字节长度。固定字符回传保留尾部空格。`Single` 用于 `BINARY_FLOAT`，用 `GetValue<float?>` 读取可空列，保留次正规数、无穷和 `NaN`。

数据库先收集写入影响的行，再检查回传上限，因此需用 `Where` 控制每批匹配规模。

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddOracleProvider()` | `Bing.Dapper.Oracle` | 注册 Oracle Provider（Provider Key `bing.oracle`） |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `OracleSqlProvider` / `OracleDialect` / `OracleBuilder` | `Bing.Dapper.Oracle` | Provider 三件套 |

## 与其它包的关系

**本包依赖**：`Bing.Dapper.Core`

**三方 NuGet**：`Oracle.ManagedDataAccess.Core`、`Microsoft.CodeAnalysis.PublicApiAnalyzers`

## 注意事项

⚠ Provider 包**不配置默认数据源**，多 Provider 场景必须用具名数据源。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Dapper.Oracle

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
