# 数据访问、SQL 与 ORM Provider

## 功能说明

数据访问有三条路径：EF Core 仓储/工作单元、FreeSQL 仓储/工作单元，以及 `Bing.Data.Sql` 配合 Dapper 的查询、变更命令和事务作用域。`Bing.Data` 提供共同的数据契约；SQL 路径不等同于 ORM 仓储。

## 核心实现逻辑与关键文件

| 路径 | 职责 |
| --- | --- |
| `framework/src/Bing.EntityFrameworkCore/` 及五个 Provider 工程 | 基于 DbContext 的工作单元、存储、仓储、映射与数据库注册。 |
| `framework/src/Bing.FreeSQL/`、`framework/src/Bing.FreeSQL.MySql/` | FreeSQL 工作单元、MySQL 注册、审计与软删除。 |
| `framework/src/Bing.Data.Sql/Bing/Data/Sql/` | Builder、表达式解析、元数据、能力检查、过滤器与变更命令。 |
| `framework/src/Bing.Dapper.Core/` 及五个 Provider 工程 | SQL 执行、数据源路由、连接与事务生命周期。 |

SQL 构建与执行分离：元数据和方言生成参数化 SQL，执行器按具名数据源选择 Provider；事务连接由 `ISqlTransactionScope` 管理。Lambda 支持连续来源、二元 Join 与 DTO 投影，具体表达式及 Provider 边界见[Lambda 查询](../sql/sqlquery-lambda-usage.md)。

## 对外接口与用法

```csharp
using Bing.Dapper;
using Bing.Dapper.MySql;
using Bing.Data.Enums;
using Bing.Data.Sql;

services.AddSqlCore();
services.AddMySqlProvider();
services.AddSqlDataSource("reporting", DatabaseType.MySql, connectionString);
```

`AddMySqlProvider` 来自 MySQL Provider 包。连接串由宿主提供。Query 入口包括 `Query`、`Sql`、`SqlInterpolated`、`Procedure`、`From<T>` 与 `FromSubquery<T>`；事务使用 `ISqlTransactionScopeFactory.Begin` 创建作用域并显式提交。

## 依赖与配置项

EF Core 与 Dapper 均提供 MySQL、SQL Server、PostgreSQL、Oracle、SQLite Provider；FreeSQL 当前只有 MySQL 工程。选择对应驱动与 Provider 包，设置数据源键、连接串及需要的 Provider Key；版本和目标框架以工程及导入的 `.props` 为准。跨库表引用还需要数据库身份和 Provider 能力校验。

## 已知限制与注意事项

- Provider 存在不代表全部 SQL 特性可用，能力拒绝与降级应按[Provider 能力](../sql/provider-capabilities.md)判断。
- FreeSQL 的 IQueryable 异步 Provider 仍未实现，执行器可能退化为同步；不能据此否定 FreeSQL 所有原生异步方法。
- 单次 Lambda 投影最多两个来源参数；更多来源可重复二元 Join 或先投影派生表再组合。未支持表达式会明确拒绝，Right/Full Join 受 Provider 能力限制。
- Dapper 路径不会自动获得 ORM 的审计、仓储与跨 ORM 事务。
- 外部数据库可运行性：待实现/待确认；代码存在与集成环境验收分别记录。

完整接口与升级说明见[SQL 文档索引](../sql/README.md)及[能力矩阵](../getting-started/能力矩阵.md)。
