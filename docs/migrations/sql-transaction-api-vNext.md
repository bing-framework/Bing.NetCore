# SQL 事务 API 迁移到 7.0.0

## 概要

7.0.0 收敛 Dapper 的连接和事务公开 API。`ISqlTransactionScope` 是唯一的公开事务生命周期对象，Dapper 自有连接统一通过 `ISqlDbConnectionFactoryResolver` 创建。

## 已删除 API

- `IDbConnectionManager`、`IDbTransactionManager`
- `ISqlQueryExternalContext`
- `ISqlQuery` 及实现上的 `GetConnection`、`SetConnection`、`GetTransaction`、`SetTransaction`
- `BeginTransaction`、`CommitTransaction`、`RollbackTransaction`
- `IDatabaseFactory` 和 `MySqlDatabaseFactory`、`PostgreSqlDatabaseFactory`、`SqlServerDatabaseFactory`、`OracleDatabaseFactory`、`SqliteDatabaseFactory`
- Provider Query/Executor 基类上的 `IDatabase` 构造参数与 `CreateDatabase`、`CreateDatabaseFactory` 扩展点

## 事务迁移

原先由 Query 或 Executor 直接开始和完成事务的代码：

```csharp
query.BeginTransaction();
executor.ExecuteSql(sql, parameters);
query.CommitTransaction();
```

改为由 Scope 管理：

```csharp
using var scope = transactionScopeFactory.Begin("reporting");
var executor = scope.CreateExecutor();

executor.ExecuteSql(sql, parameters);
scope.Commit();
```

异步调用使用 `BeginAsync`、`CommitAsync` 和 `await using`。未完成的 Scope 在释放时自动回滚。

## 连接迁移

应用代码不再向 Query 或 Executor 注入、替换或读取 `IDbConnection`。使用数据源元数据和 Provider 注册声明连接：

```csharp
services.AddSqlCore();
services.AddSqliteProvider();
services.AddSqlDataSource("reporting", DatabaseType.Sqlite, connectionString);
```

通过 `ISqlQueryFactory.Create<TQuery>("reporting")` 或 `ISqlTransactionScopeFactory.Begin("reporting")` 选择数据源。Dapper 在需要自有连接时调用 `ISqlDbConnectionFactoryResolver`。

## 跨 ORM 说明

`IDatabaseConnectionAccessor` 和 `IDatabase` 未删除，仍服务于 EF Core、FreeSQL 等跨 ORM 集成。Dapper 不再解析或包装这些契约。EF Core Shared 模式的连接与事务绑定是框架内部行为；应用代码仍通过 EF Core 工作单元管理其生命周期。

## 验收

迁移后应确认：

- 业务代码不再引用已删除类型或方法。
- 所有需要原子性的 Dapper 操作都位于一个 `ISqlTransactionScope` 中。
- 多数据源场景通过 `dbKey`、数据源元数据和 Scope 选择目标数据库。
- Dapper 测试替身通过 `ISqlDbConnectionFactoryResolver` 提供自有连接，而不是实现 `IDatabase`。