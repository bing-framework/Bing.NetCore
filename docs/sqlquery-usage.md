# ISqlQuery 使用说明

## 概述

`ISqlQuery` 是 Bing 数据访问层中的根查询对象，用于保存连接、事务和诊断状态，并创建独立的查询描述。结构化查询通过类型化描述构建，原生 SQL 通过固定模板和参数对象创建；根对象同时实现 `IDisposable` 和 `IAsyncDisposable` 管理自有资源。

在典型场景中，`ISqlQuery` 主要用于：

- 创建复杂查询：多表 Join、条件、分组、排序和分页等；
- 执行结构化、原生 SQL 或存储过程查询；
- 在独立 Builder 的受控诊断路径中生成已脱敏的调试 SQL。

---

## 接口概览

`ISqlQuery` 的核心定义位于 `framework/src/Bing.Data.Sql/Bing/Data/Sql/ISqlQuery.cs`：

```csharp
public partial interface ISqlQuery : IDisposable, IAsyncDisposable
{
    SqlFluentQuery<TResult> Query<TResult>();
    SqlTextQuery<TResult> Sql<TResult>(string sql, object parameters = null);
    SqlProcedureQuery<TResult> Procedure<TResult>(string procedure, object parameters = null);
    SqlLambdaQuery<TEntity> From<TEntity>() where TEntity : class;
}
```

### 重要方法

- `Query<TResult>()`：创建指定结果类型的原始字符串 Fluent 查询描述，返回 `SqlFluentQuery<TResult>`。
- `From<TEntity>()`：创建以实体映射为根来源的强类型 Fluent 查询描述。
- `Sql<TResult>(sql, parameters)`：创建原生 SQL 文本查询描述，不重写 SQL 文本或自动附加结构化过滤器。
- `Procedure<TResult>(procedure, parameters)`：创建固定以存储过程命令类型执行的查询描述。

### 查询 DSL 隔离

`SqlFluentQuery<TResult>` 专用于字符串 SQL Builder 操作，例如 `Select("Id,Name")`、`From("orders")`、`AppendWhere(...)`、`HavingRaw(...)`、`SplitOn(...)` 和 Dapper 多映射终结方法。`SqlLambdaQuery<TEntity>` 及其 2～10 来源变体只接受实体映射、Lambda 谓词、类型化 Join、类型化投影和类型化聚合，不暴露原始 `From`、`HavingRaw`、`SplitOn` 或 Dapper 多映射入口。

类型化投影通过 `Select<TProjection>(...)` 显式切换结果类型；不再使用 `.As<TResult>()` 或其他隐式结果类型兼容入口。原生 SQL 文本则使用独立的 `SqlTextQuery<TResult>`，不会与 Fluent Builder 或 Lambda 来源互相继承。

---

## 常用扩展方法

原始 Builder 子句能力由 `ISqlBuilder` 及其扩展方法提供；强类型查询描述提供同等的类型化 `Select`、`Where`、`Join`、`OrderBy` 和分页方法。

### 1. Select / From / Join

用于构建 `SELECT` 和 `FROM`、`JOIN` 子句。

```csharp
// 设置列名
ISqlBuilder Select<TEntity>(bool propertyAsAlias);
ISqlBuilder Select<TEntity>(Expression<Func<TEntity, object[]>> columns, bool propertyAsAlias = false);
ISqlBuilder Select<TEntity>(Expression<Func<TEntity, object>> column, string columnAlias = null);

// 移除列
ISqlBuilder RemoveSelect<TEntity>(Expression<Func<TEntity, object[]>> columns);
ISqlBuilder RemoveSelect<TEntity>(Expression<Func<TEntity, object>> column);

// 设置表名
ISqlBuilder From<TEntity>(string alias = null, string schema = null);

// 连接表
ISqlBuilder Join<TEntity>(string alias = null, string schema = null);
ISqlBuilder LeftJoin<TEntity>(string alias = null, string schema = null);
ISqlBuilder RightJoin<TEntity>(string alias = null, string schema = null);
```

- `propertyAsAlias`：是否将属性名映射为列别名，例如生成 `t.Name AS Name`。
- `columns`：一组列表达式，如 `x => new object[] { x.Id, x.Name }`。
- `alias` / `schema`：表别名与架构名。

### 结构化表引用

实体类型 `From<TEntity>`、`Join<TEntity>` 先解析最终 `SqlTableReference`，再在生成 SQL 时按当前 Provider 渲染。表引用只保存 `Database`、`Schema`、`TableName` 和可选 `Alias`；映射解析不再对 Schema 或表名执行 Provider 特定的兼容转换。

显式字符串 `From("...")`、`Join("...")` 是受控对象名入口，不接受原始 SQL。它们支持一个可选别名，并拒绝分号、控制字符、空名称段和超出 Provider 上限的限定段。原始 SQL 仅可通过 `AppendFrom`、`AppendJoin`、`AppendLeftJoin` 或 `AppendRightJoin` 追加；外部输入不得用于任何对象名字符串。

### 原始 Append 约束

`AppendFrom`、`AppendJoin`、`AppendLeftJoin` 和 `AppendRightJoin` 接受调用方控制的完整原始 SQL。它们不格式化标识符、不解析 Schema、不注册别名，也不会从占位符中自动创建参数；参数必须通过 `AddParam` 显式提供。

第一次 `AppendFrom` 会替换已有的结构化 `From`。连续调用只拼接传入文本，不会补充空格、逗号或 Join 关键字：

```csharp
// 正确：调用方提供逗号和空格。
query.AppendFrom("Orders o").AppendFrom(", Customers c");

// 错误：结果为 From Orders oCustomers c。
query.AppendFrom("Orders o").AppendFrom("Customers c");
```

原始 Join 可完整提供 `On`，也可先添加表表达式再对最后一个 Join 调用 `AppendOn`：

```csharp
query.AppendFrom("Orders o")
    .AppendLeftJoin("Items i")
    .AppendOn("i.OrderId=o.Id")
    .AddParam("TenantId", tenantId);
```

空白 `AppendOn` 是无操作；没有 Join 时传入非空条件会抛出 `InvalidOperationException`，条件不会保存到后续 Join。原始 Join 已包含 `On` 时，后续 `AppendOn` 会以 `And` 追加到同一 Join。

`AppendSelect`、`AppendWhere`、`AppendGroupBy`、`AppendOrderBy` 与 `AppendOn` 不属于完全字节原样的 API：其中的方括号标识符会按当前方言转换。它们的参数同样必须显式 `AddParam`。

各 Provider 的限定名规则如下：

| Provider | 支持的限定部分 |
| --- | --- |
| MySQL / Doris 兼容配置 | `Schema.Table` |
| SQL Server | `Database.Schema.Table` |
| PostgreSQL | `Schema.Table` |
| Oracle | `Schema.Table` |
| SQLite | `Table`，或受控连接中 `AttachedAlias.Table` |

详细说明见 [结构化表引用与跨数据库查询](sql-table-reference-and-cross-database-query.md)。

### 2. 条件：Where / Or / On

用于构建 `WHERE` 和 `JOIN ... ON` 条件。

```csharp
ISqlBuilder Where<TEntity>(
    Expression<Func<TEntity, object>> expression,
    object value,
    Operator @operator = Operator.Equal);

ISqlBuilder Where<TEntity>(Expression<Func<TEntity, bool>> expression);

ISqlBuilder Or<TEntity>(params Expression<Func<TEntity, bool>>[] conditions);
ISqlBuilder OrIf<TEntity>(Expression<Func<TEntity, bool>> predicate, bool condition);
ISqlBuilder OrIf<TEntity>(bool condition, params Expression<Func<TEntity, bool>>[] predicates);
ISqlBuilder OrIfNotEmpty<TEntity>(params Expression<Func<TEntity, bool>>[] conditions);

ISqlBuilder On<TLeft, TRight>(
    Expression<Func<TLeft, object>> left,
    Expression<Func<TRight, object>> right,
    Operator @operator = Operator.Equal);

ISqlBuilder On<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> expression);
```

特点：

- 使用表达式树，字段引用具备编译期类型检查，重构友好；
- `OrIf` / `OrIfNotEmpty` 可根据条件动态添加查询条件，避免大量 if/else；
- `On` 支持按列或按布尔表达式定义连接条件。

### 3. 聚合函数

```csharp
ISqlBuilder Count<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null);
ISqlBuilder Sum<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null);
ISqlBuilder Avg<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null);
ISqlBuilder Max<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null);
ISqlBuilder Min<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null);
```

用途：在 `SELECT` 子句中添加聚合列，例如 `COUNT(o.Id) AS TotalCount`、`SUM(o.Amount)` 等。

统一聚合 API 将结构化列、原始 SQL 参数和可转换表达式分开处理：

```csharp
// 旧便捷 API 保留自动 Alias；限定列使用叶子名称而非 o.Amount。
builder.Sum("o.Amount");

// 新统一 API 未提供 Alias 时不输出 AS。
builder.CountAll();
builder.CountColumn("o.UserId", distinct: true);
builder.Aggregate(SqlAggregateFunction.Sum, "o.Amount");

// 显式 Alias 适用于 DTO 映射等结果契约。
builder.Sum("o.Amount", "Total");

builder.AggregateExpression(
    SqlAggregateFunction.Sum,
    "[o].[Quantity] * [o].[Price]",
    "Total");

// AggregateRaw 完全保留参数 SQL，包括 JSON Path 和字符串方括号。
builder.AggregateRaw(
    SqlAggregateFunction.Count,
    "JsonExtract(o.Data, '$[0]')",
    "JsonCount");

// AggregateExpression 仅转换普通 SQL 上下文的 [] 标识符；字符串和注释保持原文。
builder.AggregateExpression(
        SqlAggregateFunction.Sum,
        "Case When [o].[Amount] > @MinAmount Then [o].[Amount] Else 0 End",
        "Total")
    .AddParam("MinAmount", 100);
```

`Aggregate` 和 `CountColumn` 只接受一个结构化列路径，支持一至三段普通或引用标识符；表达式、函数、注释、分号和多列输入应改用 `AggregateExpression` 或 `AggregateRaw`。引用标识符可包含空格和双写的结束引用符，例如 `[Sales Order].[Order]]Name]`。

`AggregateRaw` 与 `AggregateExpression` 不会自动发现或创建 SQL 参数。调用方必须通过 `AddParam` 显式绑定；未绑定占位符会原样保留，并由数据库执行阶段报告错误。组合 CTE、Union 或子查询时，Builder 会合并显式参数，并仅在同名异值冲突时重命名完整参数 Token。

---

## 查询描述生命周期

根 `ISqlQuery` 保存连接、事务和诊断状态。每次调用 `Query<TResult>()`、`From<TEntity>()`、`Sql<TResult>(...)` 或 `Procedure<TResult>(...)` 都会创建独立查询描述及其专属 Builder；一个描述完成配置后应只用于一次执行路径，不应在并发操作之间共享。

`ISqlQuery` 不提供全局的 `Clear*`、`IgnoreFilter*` 或连接替换扩展。需要构造不同 SQL 时，应创建新的独立查询描述；结构化全局过滤器由 Builder 的数据边界策略统一处理，原生 SQL 则保持调用方控制的固定模板与参数化边界。

---

## 多数据源上下文与运行时切库

当前实现支持以 `dbKey` 作为业务入口在运行期切换数据源，并让连接串、实体映射、参数元数据和诊断上下文同步切换。调用方只传 `dbKey`；`DatabaseType`、连接字符串、只读标识、映射配置和主库策略均由数据源配置解析。

### 1. 注册数据库上下文能力

`AddSqlCore()` 会一并注册：

- `IDatabaseContextAccessor`
- `IDatabaseScopeManager`
- `ISqlDataSourceResolver`
- `IEntityMappingResolver`
- `ITypeConverterResolver`
- `ISqlQueryFactory`
- `ISqlExecutorFactory`

如果需要显式配置多库连接描述与实体映射，建议通过 `ConfigureSqlMetadata(...)` 统一追加配置。新数据源配置使用 `DataSources`：

```csharp
services.ConfigureSqlMetadata(options =>
{
    options.DataSources.DefaultDataSourceKey = "default";
    options.DataSources.DataSources["default"] = new SqlDataSourceDescriptor
    {
        Key = "default",
        DatabaseType = DatabaseType.MySql,
        ConnectionStringName = "DefaultConnection",
        MappingProfile = "default"
    };
    options.DataSources.DataSources["reporting"] = new SqlDataSourceDescriptor
    {
        Key = "reporting",
        DatabaseType = DatabaseType.PgSql,
        ConnectionStringName = "ReportingConnection",
        IsReadOnly = true,
        MappingProfile = "reporting",
        PrimaryReadStrategy = PrimaryReadStrategy.PrimaryDataSource,
        PrimaryDataSourceKey = "default"
    };
});

services.AddSqlCore();
services.AddMySqlQuery();
services.AddMySqlExecutor();
```

`DataSources` 是唯一的数据源配置入口。`ConnectionString` 非空时直接使用；否则框架通过 `ConnectionStringName` 从配置的连接字符串集合读取。

### 2. 使用作用域切换当前数据库上下文

```csharp
public async Task<List<UserDto>> QueryReportingUsersAsync(
    IDatabaseScopeManager _databaseScopeManager,
    ISqlQueryFactory _sqlQueryFactory)
{
    using (_databaseScopeManager.Use("reporting"))
    {
        var query = _sqlQueryFactory.Create<ISqlQuery>();
        var result = await query
            .From<User>()
            .Where<User>(x => x.Status, UserStatus.Enabled)
            .ToListAsync<UserDto>();
        return result;
    }
}
```

要点：

- `DatabaseScopeManager` 支持嵌套作用域，内层释放后会恢复父级上下文；
- `ISqlQueryFactory` / `ISqlExecutorFactory` 会基于当前 `DatabaseContext` 或显式 `dbKey` 解析连接串并创建对应实例；
- 工厂创建出的 Query / Executor 会携带解析后的 `DatabaseContext`，后续 SQL 映射和参数元数据解析不会因外部作用域变化而漂移；
- 同一实体在不同上下文下，SQL 中输出的表名、列名和执行连接可以不同。
- `IsReadOnly` 只作为数据源描述和诊断标识，不会替代数据库权限控制，也不会自动改写 SQL。

### 3. 显式创建指定数据库上下文

如果调用点不依赖当前作用域，也可以在工厂创建时直接指定数据库上下文：

```csharp
var query = _sqlQueryFactory.Create<ISqlQuery>("reporting");
```

该方式会同时影响连接解析、实体映射解析和增强参数元数据解析，避免出现“连接是 reporting，但 SQL 映射仍是 default”的情况。

### 4. EF Core Shared / Independent 查询

EF Core Repository 中的 `Sql` 属性默认使用 Shared 模式：复用当前 `DbContext.Database.GetDbConnection()`，如果 EF 当前存在事务，则同步绑定 `CurrentTransaction.GetDbTransaction()`。该连接和事务都视为外部资源，SQL Query 不会关闭连接，也不会提交或回滚 EF 事务。

当需要完全独立于 EF 工作单元执行 SQL 时，可在 Repository 内调用：

```csharp
var query = CreateIndependentSqlQuery();
```

Independent 模式创建独立 SQL Query，不绑定 EF 连接和事务。它适合报表、旁路查询、只读查询等不应参与当前 EF 事务的场景。Shared 模式保持 `Sql.From<T>()` 等既有写法不变。

EF Core SQL Query Factory 的数据源优先级为：显式 `dbKey`、Ambient `DatabaseContext.DbKey`、`SqlMetadataOptions.DefaultDatabaseContext.DbKey`、`DataSources.DefaultDataSourceKey`、唯一数据源。显式不存在的 `dbKey` 会立即失败，不会回退到默认数据源；最终解析的数据源 Provider 必须与当前 EF Core Provider 一致。

Shared 模式在绑定 EF 连接前会比较最终数据源与 `DbContext` 的物理数据库身份。SQLite 普通 `Data Source=:memory:` 是连接独占内存库，不能用于 Shared 比较；请使用 Independent 模式，或使用命名共享内存 URI，例如 `Data Source=file:reporting?mode=memory&cache=shared`。SQLite 文件路径会按绝对路径比较，命名共享内存名称不同也会拒绝复用；服务器型 Provider 缺少服务器地址或数据库名称时同样会拒绝 Shared 比较，避免误复用连接。Oracle 支持严格单地址 TCP TNS Descriptor，但别名、复杂 Descriptor，以及同时指定 `Service Name` 和 `SID` 的目标均不可安全比较。

### 5. 上下文、读取偏好与租户边界

`DatabaseScopeOptions.ReadPreference` 为可空值。未指定时，数据库 Scope 继承父级读取偏好；显式 `SqlReadPreference.Default` 或 `SqlReadPreference.Primary` 会覆盖父级。内层 Scope 释放后恢复父级上下文，Scope 必须按 LIFO 顺序释放，乱序释放会抛出异常并保持当前栈顶上下文不变。

`IDatabaseContextAccessor.Current` 返回独立深快照，直接修改返回对象不会回写当前异步上下文。需要更新时使用 `accessor.Update(...)` 或重新设置 `Current`。Query、Executor 和事务 Scope 在创建时固定数据源快照，因此外层 Ambient Scope 后续切换不会改变已创建对象的连接、映射或诊断上下文。

`TenantId` 会随 `DatabaseContext` 传播，并可供既有映射路由使用；默认不会根据 TenantId 自动选择 `dbKey`、连接字符串或物理数据库，也不会自动追加租户过滤条件。业务层必须显式建立租户到数据源或过滤器的绑定关系。

### 6. 连接和事务所有权

`ISqlTransactionScope` 是业务代码唯一推荐的事务生命周期入口。Scope 固定一个数据库上下文、连接和事务；它创建的 Query 与 Executor 共享这些资源，Ambient `dbKey` 后续变化不会影响 Scope。

```csharp
await using var scope = await transactionScopeFactory.BeginAsync("reporting");

var executor = scope.CreateExecutor();
await executor.ExecuteSqlAsync(
    "update users set name=@name where id=@id",
    new { name = "Tom", id = 1 });

var query = scope.CreateQuery();
var user = await query.From<User>().Where<User>(x => x.Id, 1).ToEntityAsync<User>();

await scope.CommitAsync();
```

`ISqlTransactionContext` 提供 Scope 的只读事务信息：`TransactionId`、`DbKey`、数据库类型、数据库上下文快照、连接、事务和隔离级别。`DatabaseContext` 每次返回独立快照，调用方不能修改 Scope 内部固定上下文。

| 资源来源 | 所有者 | Query/Executor 行为 |
| --- | --- | --- |
| `ISqlTransactionScope` | Scope | 统一提交、回滚和释放；完成后子对象立即失效。 |
| EF Core Shared | `DbContext` / EF Core | Query 不关闭连接，不提交、回滚或释放 EF 事务。 |
| EF Core Independent | 框架 | 通过 `ISqlDbConnectionFactoryResolver` 创建独立连接，可交给 Scope 管理。 |
| `PrimaryReadStrategy.Transaction` | Query 内部 | 仅用于内部短事务；流式 API 不支持该策略。 |

`SetConnection(connection)`、`GetConnection()`、`SetTransaction(transaction)`、`GetTransaction()`、`BeginTransaction()`、`CommitTransaction()`、`RollbackTransaction()`、`IDbConnectionManager`、`IDbTransactionManager` 和 `ISqlQueryExternalContext` 已在 7.0.0 删除。业务代码必须通过 `ISqlTransactionScopeFactory` 和 `ISqlTransactionScope` 管理事务；Query 与 Executor 不提供替换连接或绑定事务的公开入口。

外部连接或事务绑定属于框架内部能力。绑定时会校验事务连接、固定 `DatabaseContext`、数据库类型和脱敏物理数据库身份；不一致、不可安全比较或试图覆盖自有事务时会立即拒绝。Query 不会接管外部连接或外部事务的提交、回滚、关闭和释放。

需要让多个 Query / Executor 共享一个独立事务时，使用 `ISqlTransactionScopeFactory`：

```csharp
using var scope = transactionScopeFactory.Begin("reporting");

var executor = scope.CreateExecutor();
executor.ExecuteSql("update users set name=@name where id=@id", new { name = "Tom", id = 1 });

var query = scope.CreateQuery();
var user = query.From<User>().Where<User>(x => x.Id, 1).ToEntity<User>();

scope.Commit();
```

作用域拥有连接和事务。作用域创建的 Query / Executor 会绑定外部事务，不能自行提交或回滚；如果作用域释放前未调用 `Commit()` 或 `Rollback()`，会自动回滚。提交失败时框架会尝试回滚；仅提交失败时保留原始提交异常，提交和回滚都失败时抛出聚合异常。

`Commit()`、`Rollback()` 或 Scope `Dispose()` 后，不得继续使用此前通过 Scope 创建的 Query / Executor。它们会立即拒绝获取连接、事务或执行 SQL，防止在已结束的事务之外重新建连执行。需要继续访问数据库时，请创建新的 Scope 或通过工厂创建新的 Query / Executor。

`BeginAsync()`、`CommitAsync()` 和 `RollbackAsync()` 会优先调用 Provider 公开的原生异步成员；只有成员不存在时才同步回退。开始事务失败时，框架仍会释放 Owner Query；若释放也失败，会同时保留开始失败和清理失败信息。

如果未来启用主库读取策略，`PrimaryReadStrategy.Transaction` 只适合短查询。当前实现对 `StreamQuery` / `StreamQueryAsync` 会在创建读取器前直接抛出异常，避免在流式场景里静默退回到短事务策略。

### 6.1 Dapper 连接创建与跨 ORM 兼容

Dapper 的默认连接创建路径为 `DatabaseType -> ISqlDbConnectionFactoryResolver -> IDbConnection`。每个 Provider 的 `Add*Provider`、`Add*Query` 和 `Add*Executor` 都注册对应连接工厂，因此仅注册 Executor 的场景同样可以解析独立连接。

`IDatabaseConnectionAccessor` 是跨 ORM 的只读连接访问契约。`Bing.Data.IDatabase` 继续继承该接口以保持 EF Core、FreeSQL 等历史集成的兼容，不与事务 Scope 合并。`IDatabaseFactory` 和各 Provider `XxxDatabaseFactory` 已在 7.0.0 删除；Dapper 不构造或解析 `IDatabase`，仅通过 `ISqlDbConnectionFactoryResolver` 创建自有连接。

### 7. 显式实体映射

当同一实体需要映射到不同库、不同表或不同列时，可通过 `SqlMetadataOptions.EntityMappings` 显式配置：

```csharp
var options = new SqlMetadataOptions();
options.EntityMappings.Add(new EntityMappingOptions
{
    EntityType = typeof(User),
    DbKey = "default",
    TableName = "users",
    Columns =
    {
        [nameof(User.Status)] = new ColumnMappingOptions
        {
            PropertyName = nameof(User.Status),
            ColumnName = "status"
        }
    }
});

options.EntityMappings.Add(new EntityMappingOptions
{
    EntityType = typeof(User),
    DbKey = "reporting",
    TableName = "users_archive",
    Columns =
    {
        [nameof(User.Status)] = new ColumnMappingOptions
        {
            PropertyName = nameof(User.Status),
            ColumnName = "status_code"
        }
    }
});
```

映射解析顺序为：显式 `EntityMappings` -> `IEntityModelMetadataProvider` -> CLR 类型回退。

---

## 原生 SQL 参数元数据增强

除了原有 `ExecuteSql(string sql, object param)` 用法外，现在还可以通过 `SqlParameterMap<TEntity>` 为原生 SQL 提供实体级参数元数据。

### 1. 使用 `ExecuteSql<TEntity>()` 扩展方法

```csharp
await executor.ExecuteSqlAsync<User>(
    "Update users set name=@name where id=@userId",
    new { name = "Tom", userId = 1 },
    map => map
        .Map("name", x => x.Name)
        .Map("userId", x => x.Id));
```

如果需要显式写入 `null`，使用 `Add` 并传入第三个参数；如果需要从源对象读取值，使用 `Map`：

```csharp
executor.ExecuteSql<User>(
    "update users set name=@name where id=@id",
    new { id },
    map => map
        .Add("name", x => x.Name, null)
        .Map("id", x => x.Id));
```

能力说明：

- 支持“参数名”和“实体属性名”不一致；
- 参数值可来自匿名对象、字典或普通 POCO；`Add(name, property, null)` 表示显式传入空值，执行时会绑定为 `DBNull.Value`；`Map(name, property)` 表示从源对象读取参数值；
- 使用映射增强时，找不到必需输入值会抛出 `SqlParameterBindingException`；只有未启用映射增强的旧调用路径才保留原有弱元数据行为；
- 执行阶段会把 `DbType`、`Size`、`Precision`、`Scale`、`ProviderTypeName` 等元数据补齐到 ADO 参数；
- 执行诊断消息会包含标准化参数快照和增强参数元数据，便于排查参数类型与映射问题；
- 诊断参数快照不暴露 Dapper 内部参数对象。

### 2. 使用 `AddParam<TEntity>()` 为 Builder 参数补齐元数据

```csharp
var query = sqlQuery.From<User>()
    .Where(user => user.Name, "Tom");

query.AddParam("statusCode", x => x.Status, 1);
```

该方式适合：

- 在受控查询描述中补齐参数元数据；
- 在不改 SQL 片段文本的前提下，补齐特定参数的数据库元数据；
- 让 `GetCountAsync()`、原生 SQL 执行、分页等路径统一走增强参数绑定。

注意：`query.GetParams()` 仍然保留轻量参数列表语义，便于调试；真正执行时会自动绑定增强后的数据库参数。

---

## 多数据库边界加固

### ExecutionContext 作用域规则

数据库 Scope 使用 `AsyncLocal` 保存当前执行流的栈帧。`Task.Run` 默认会继承当前上下文，但子流释放继承的 Scope 只恢复子流自身的父上下文，不会改变父流；父流随后仍可独立释放同一 Scope。当前执行流没有该 Scope 帧时重复释放是幂等操作。当前栈中存在该帧但它不是栈顶时，仍会抛出 LIFO 异常。

`ExecutionContext.SuppressFlow()` 创建的任务不会获得 Ambient 数据库上下文，也不能释放父流的 Scope。不要将 Scope 跨越 `IAsyncLifetime.InitializeAsync` 和 `DisposeAsync` 保存。

### 物理数据库身份

EF Core Shared 模式只在数据源与 `DbContext` 身份可安全比较且相等时复用连接。身份解析通过 `ISqlDatabaseIdentityContributor` 扩展；内置贡献者不依赖 Provider 包，并规范以下形式：

- SQLite 文件使用绝对路径；`Data Source=name;Mode=Memory;Cache=Shared` 与等价 `file:name?mode=memory&cache=shared` 识别为同一命名共享内存库；`:memory:` 永远不可用于 Shared。
- MySQL、PostgreSQL、SQL Server 的省略默认端口与显式默认端口视为一致。SQL Server 支持 `tcp:host,port`、`host\\instance` 和独立 `Port` 字段。
- Oracle 仅比较可展开的 EZConnect 或显式主机与 Service Name/SID；TNS 别名与复杂描述符标记为不可比较，Shared 模式会拒绝，而不会猜测它们是否指向同一库。

### 事务状态与异步 API

`ISqlTransactionScope` 的状态为 Active、Committed、RolledBack、Faulted 或 Disposed。仅 Active 状态允许创建子 Query/Executor 或完成事务；提交、回滚及释放会先让 Lease 失效，防止现有子对象在事务结束后继续执行。

资源清理始终会尝试释放全部子对象、事务和 owner Query。一个清理步骤失败不会阻止后续步骤；多个异常以 `AggregateException` 返回。`BeginAsync`、`CommitAsync`、`RollbackAsync` 和 `DisposeAsync` 优先调用 Provider 可用的原生 ADO.NET 异步成员，缺失时才同步回退，不会使用 `Task.Run` 包装同步数据库操作。

### 诊断上下文与租户策略

Before、After 和 Error 诊断消息均携带 Query 创建时固定的 `DbKey`、读取偏好、事务、`MappingProfile` 和参数快照。`TenantId` 默认不输出；只有在当前 Query/Executor 上显式配置后才写入诊断消息：

```csharp
executor.Config(options => options.IncludeTenantIdInDiagnostics = true);
```

SkyAPM 标签会记录映射配置、读取偏好和事务隔离级别；只有诊断消息含租户标识时才会追加租户标签。各诊断事件持有独立消息快照；一维数组参数会复制，避免订阅器修改 Before 事件数据影响 After 或 Error 事件。不要将连接字符串、密码或其他凭据写入诊断订阅器。

---

## 列表、参数集合与流式读取

### 异步列表的 buffered 语义

`ToListAsync<T>()`、`ToDynamicListAsync()`、分页列表和对应的存储过程列表都支持 `buffered` 参数，默认值为 `true`：

```csharp
var users = await query.ToListAsync<User>(buffered: false);
```

`buffered: false` 会将 Dapper 命令标记为非缓冲读取，但框架会在连接与事务仍有效时立即物化成 `List<T>`。因此该选项不返回延迟枚举，也不适合作为大结果集导出 API。

### 可枚举参数集合与输出参数

`SqlParameterCollection` 用于需要同时指定输入、输出、类型和长度的原生 SQL 调用。参数名称会按不含 `@`、`:`、`?` 前缀的标准名称去重，执行时仍会由当前 Provider 生成实际参数。

```csharp
var parameters = new SqlParameterCollection()
    .Add("@name", "Bing", DbType.String, size: 32)
    .AddOutput("result", DbType.Int32);

executor.ExecuteSql("exec usp_save_user @name, @result output", parameters);
var result = executor.OutputParameters.GetValue<int>("result");
```

输出值来自执行完成后的实际 `DbParameter`。实体映射、显式 `null`、`DbType`、长度、精度和 Provider 类型配置会沿同一绑定路径传递到命令对象。

### StreamAsync

对需要逐条消费大结果集的场景，使用 `StreamAsync<T>()`：

```csharp
await foreach (var user in query.StreamAsync<User>(cancellationToken: cancellationToken))
{
    await ExportAsync(user, cancellationToken);
}
```

该 API 使用异步 Reader 逐行读取。完整枚举、提前 `break`、取消和读取异常都会释放 Reader；调用方传入的外部连接和外部事务不会被 Query 关闭、提交或回滚。`PrimaryReadStrategy.Transaction` 不支持流式读取，会在创建 Reader 前抛出异常。

---

## 独立查询描述

根 `ISqlQuery` 保存连接、事务和诊断状态。需要构建并执行单个查询时，使用 `Query<TResult>()` 或 `From<TEntity>()` 创建独立 Fluent 查询描述；每个描述都有独立 Builder，执行时仍复用根查询的连接、事务、诊断和 Trace 上下文。

```csharp
var fluent = sqlQuery.From<Order>();
var text = sqlQuery.Sql<Order>("Select Id,OrderNo From Orders Where Id=@Id", new { Id = orderId });
```

- `Query<TResult>()`：创建指定结果类型的 `SqlFluentQuery<TResult>`，适用于以普通 Builder 子句构造查询。
- `From<TEntity>()`：创建以实体映射初始化的 Lambda Fluent 描述，支持类型化 `Select`、`Where`、`Join`、分页和同步/异步终结方法。
- `Sql<TResult>(sql, parameters)`：创建原生 SQL 文本描述。文本和参数源按原样传递给参数绑定器，不进行 SQL 重写或标识符转换。

原生 SQL 仅应使用调用方控制的固定模板，并通过参数对象绑定外部值；不得将外部输入拼接到 SQL 文本、表名、列名、排序字段或其他结构位置。

## 典型使用示例

### 示例一：简单列表查询

```csharp
public async Task<List<Order>> GetPaidOrdersAsync(ISqlQuery sqlQuery)
{
    return await sqlQuery.From<Order>()
        .Where(order => order.Status, OrderStatus.Paid)
        .ToListAsync();
}
```

### 示例二：带可选条件的限量查询

```csharp
public async Task<List<OrderDto>> QueryOrdersAsync(
    ISqlQuery sqlQuery,
    OrderQueryParameter parameter)
{
    return await sqlQuery.From<Order>()
        .Where(order => order.Status, parameter.Status)
        .Skip(20)
        .Take(20)
        .Select(order => new OrderDto
        {
            Id = order.Id,
            OrderNo = order.OrderNo,
            CustomerName = order.CustomerName,
            Amount = order.Amount,
            Status = order.Status
        })
        .ToListAsync();
}
```

### 示例三：多表 Join 查询

```csharp
public async Task<List<OrderWithCustomerDto>> GetOrderWithCustomerAsync(
    ISqlQuery sqlQuery,
    Guid orderId)
{
    return await sqlQuery.From<Order>()
        .LeftJoin<Customer>("c")
        .On((order, customer) => order.CustomerId == customer.Id)
        .Where((order, customer) => order.Id == orderId)
        .Select((order, customer) => new OrderWithCustomerDto
        {
            Id = order.Id,
            OrderNo = order.OrderNo,
            Amount = order.Amount,
            CustomerName = customer.Name,
            CustomerPhone = customer.Phone
        })
        .ToListAsync();
}
```

### 示例四：原生 SQL 文本查询

```csharp
public Task<Order> GetOrderAsync(ISqlQuery sqlQuery, Guid orderId)
{
    return sqlQuery.Sql<Order>(
            "Select Id,OrderNo,CustomerName,Amount From Orders Where Id=@Id",
            new { Id = orderId })
        .FirstOrDefaultAsync();
}
```

---

## 使用建议

1. **优先使用表达式扩展方法**：通过 `Select/From/Where/Join/...` 的表达式重载构建 Sql，避免硬编码列名，提高类型安全和可维护性。
2. **每次操作创建独立描述**：查询描述持有可变 Builder，不应在并发操作之间复用；需要构造另一条 SQL 时创建新的 `Query<TResult>()`、`From<TEntity>()` 或原生 SQL 描述。
3. **使用调试 SQL 审查参数化结果**：仅在受控诊断路径中调用 Builder 的 `ToDebugSql()`；敏感参数会被遮蔽，外部输入始终应通过参数对象绑定。
4. **保持结构化过滤边界**：结构化查询由数据边界策略处理；原生 SQL 不自动附加过滤器，调用方必须使用固定模板并显式约束访问范围。

---

## 集成测试

SQLite 真实执行测试位于 `framework/tests/Bing.Dapper.Sqlite.Tests.Integration`，默认随测试运行，不依赖外部服务。

MySQL、PostgreSQL 和 SQL Server 集成测试默认跳过。启用其中一个 Provider 时，设置对应开关和 `ConnectionStrings__<Provider>Connection`；`ConnectionStrings__DefaultConnection` 仅用于兼容旧配置，例如：

```powershell
$env:RUN_MYSQL_INTEGRATION_TESTS = "true"
$env:ConnectionStrings__MySqlConnection = "Server=127.0.0.1;Database=bing_dapper_test;User Id=test;Password=..."
dotnet test .\framework\tests\Bing.Dapper.MySql.Tests.Integration\Bing.Dapper.MySql.Tests.Integration.csproj
```

也可设置 `RUN_INTEGRATION_TESTS=true` 一次启用全部外部 Provider。测试数据库名称必须以 `_test`、`_tests` 或 `_integration` 结尾，且不能包含 `prod`、`production`、`master`、`mysql` 或 `information_schema`。本地连接配置应使用环境变量或未跟踪的 `appsettings.Development.json`，不要提交凭据。
