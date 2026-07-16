# ISqlQuery 使用说明

## 概述

`ISqlQuery` 是 Bing 数据访问层中的 Sql 查询对象，用于基于实体表达式构建类型安全的 Sql，并提供执行与分页等能力。它内部通过 `ISqlBuilder` 生成 Sql 语句，并继承了执行操作接口 `ISqlQueryOperation` 以及配置接口 `ISqlOptions`，同时实现 `IDisposable` 以管理数据库连接、事务等资源。

在典型场景中，`ISqlQuery` 主要用于：

- 构建复杂查询：多表 Join、动态条件、分组、排序等；
- 实现统一的分页查询（同步/异步）；
- 获取调试 Sql，便于排查问题或与 DBA 协作；
- 配置查询行为，例如执行后是否自动清理 Sql 和参数、是否输出调试日志等。

---

## 接口概览

`ISqlQuery` 的核心定义位于 `framework/src/Bing.Data.Sql/Bing/Data/Sql/ISqlQuery.cs`：

```csharp
public partial interface ISqlQuery : ISqlQueryOperation, ISqlOptions, IDisposable
{
    /// <summary>
    /// 上下文标识
    /// </summary>
    string ContextId { get; }

    /// <summary>
    /// Sql 生成器
    /// </summary>
    ISqlBuilder SqlBuilder { get; }

    /// <summary>
    /// 配置
    /// </summary>
    /// <param name="configAction">配置操作</param>
    void Config(Action<SqlOptions> configAction);

    /// <summary>
    /// 获取 Sql 生成器
    /// </summary>
    ISqlBuilder GetBuilder();

    /// <summary>
    /// 分页查询
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="func">获取列表操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    PagerList<TResult> PagerQuery<TResult>(
        Func<List<TResult>> func,
        IPager parameter,
        int? timeout = null);

    /// <summary>
    /// 分页查询（异步）
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="func">获取列表操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    Task<PagerList<TResult>> PagerQueryAsync<TResult>(
        Func<Task<List<TResult>>> func,
        IPager parameter,
        int? timeout = null);

    /// <summary>
    /// 流式查询
    /// </summary>
    IEnumerable<TEntity> StreamQuery<TEntity>(int? timeout = null);

    /// <summary>
    /// 异步流式查询
    /// </summary>
    IAsyncEnumerable<TEntity> StreamQueryAsync<TEntity>(
        int? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 临时禁用调试日志
    /// </summary>
    ISqlQuery DisableDebugLog();
}
```

### 关键属性

- `ContextId`：当前查询上下文标识，用于日志和诊断，便于跨组件追踪一次完整查询过程。
- `SqlBuilder`：内部使用的 Sql 生成器实例，通常通过 `GetBuilder()` 获取并间接操作，不建议直接替换。

### 重要方法

- `Config(Action<SqlOptions> configAction)`：对当前查询对象进行配置，例如设置执行超时、是否执行后清空等。
- `GetBuilder()`：获取当前查询关联的 `ISqlBuilder` 实例，通常用于高级场景或自定义执行逻辑。
- `PagerQuery` / `PagerQueryAsync`：在给定分页参数 `IPager` 的基础上，统一完成分页逻辑，返回 `PagerList<TResult>`。
- `StreamQuery` / `StreamQueryAsync`：以非缓冲方式读取结果集，适合导出、大结果集扫描等场景；调用方必须自行控制枚举生命周期。
- `DisableDebugLog()`：临时禁用调试日志输出，适合高频调用、对日志量敏感的场景。

---

## 常用扩展方法

`ISqlQuery` 的大部分实际操作能力是通过扩展方法提供的，主要位于：

- `Bing/Data/Sql/Extensions/Extensions.ISqlQuery.Sql.cs`
- `Bing/Data/Sql/Extensions/Extensions.ISqlQuery.Other.cs`

### 1. Select / From / Join

用于构建 `SELECT` 和 `FROM`、`JOIN` 子句。

```csharp
// 设置列名
ISqlQuery Select<TEntity>(bool propertyAsAlias);
ISqlQuery Select<TEntity>(Expression<Func<TEntity, object[]>> columns, bool propertyAsAlias = false);
ISqlQuery Select<TEntity>(Expression<Func<TEntity, object>> column, string columnAlias = null);

// 移除列
ISqlQuery RemoveSelect<TEntity>(Expression<Func<TEntity, object[]>> columns);
ISqlQuery RemoveSelect<TEntity>(Expression<Func<TEntity, object>> column);

// 设置表名
ISqlQuery From<TEntity>(string alias = null, string schema = null);

// 连接表
ISqlQuery Join<TEntity>(string alias = null, string schema = null);
ISqlQuery LeftJoin<TEntity>(string alias = null, string schema = null);
ISqlQuery RightJoin<TEntity>(string alias = null, string schema = null);
```

- `propertyAsAlias`：是否将属性名映射为列别名，例如生成 `t.Name AS Name`。
- `columns`：一组列表达式，如 `x => new object[] { x.Id, x.Name }`。
- `alias` / `schema`：表别名与架构名。

### 2. 条件：Where / Or / On

用于构建 `WHERE` 和 `JOIN ... ON` 条件。

```csharp
ISqlQuery Where<TEntity>(
    Expression<Func<TEntity, object>> expression,
    object value,
    Operator @operator = Operator.Equal);

ISqlQuery Where<TEntity>(Expression<Func<TEntity, bool>> expression);

ISqlQuery Or<TEntity>(params Expression<Func<TEntity, bool>>[] conditions);
ISqlQuery OrIf<TEntity>(Expression<Func<TEntity, bool>> predicate, bool condition);
ISqlQuery OrIf<TEntity>(bool condition, params Expression<Func<TEntity, bool>>[] predicates);
ISqlQuery OrIfNotEmpty<TEntity>(params Expression<Func<TEntity, bool>>[] conditions);

ISqlQuery On<TLeft, TRight>(
    Expression<Func<TLeft, object>> left,
    Expression<Func<TRight, object>> right,
    Operator @operator = Operator.Equal);

ISqlQuery On<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> expression);
```

特点：

- 使用表达式树，字段引用具备编译期类型检查，重构友好；
- `OrIf` / `OrIfNotEmpty` 可根据条件动态添加查询条件，避免大量 if/else；
- `On` 支持按列或按布尔表达式定义连接条件。

### 3. 聚合函数

```csharp
ISqlQuery Count<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null);
ISqlQuery Sum<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null);
ISqlQuery Avg<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null);
ISqlQuery Max<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null);
ISqlQuery Min<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null);
```

用途：在 `SELECT` 子句中添加聚合列，例如 `COUNT(o.Id) AS TotalCount`、`SUM(o.Amount)` 等。

---

## 运行期控制与清理

`Extensions.ISqlQuery.Other.cs` 中提供了一些常用的运行期控制与清理方法。

### 清理行为

```csharp
ISqlQuery ClearAfterExecution(this ISqlQuery sqlQuery, bool value = true);
ISqlQuery Clear(this ISqlQuery sqlQuery);
ISqlQuery ClearSelect(this ISqlQuery sqlQuery);
ISqlQuery ClearFrom(this ISqlQuery sqlQuery);
ISqlQuery ClearJoin(this ISqlQuery sqlQuery);
ISqlQuery ClearWhere(this ISqlQuery sqlQuery);
ISqlQuery ClearGroupBy(this ISqlQuery sqlQuery);
ISqlQuery ClearOrderBy(this ISqlQuery sqlQuery);
ISqlQuery ClearSqlParams(this ISqlQuery sqlQuery);
ISqlQuery ClearPageParams(this ISqlQuery sqlQuery);
```

- 默认情况下，执行后会根据配置自动清空 Sql 和参数；
- 可通过 `ClearAfterExecution(false)` 关闭自动清理，以便多次执行相同查询；
- 也可以按子句粒度清理，例如只清空 `Where` 条件重新构造。

### 调试与克隆

```csharp
ISqlBuilder CloneBuilder(this ISqlQuery sqlQuery);
ISqlBuilder NewBuilder(this ISqlQuery sqlQuery);
string GetDebugSql(this ISqlQuery sqlQuery);
```

- `CloneBuilder()`：复制当前 Sql 生成器，保留结构和参数，适合基于当前查询派生出多个变体；
- `NewBuilder()`：创建一个新的 Sql 生成器，适合在同一上下文中重新构造完全不同的 Sql；
- `GetDebugSql()`：获取带参数值的调试 Sql 字符串，常用于日志和问题排查。

### 过滤器控制

```csharp
ISqlQuery IgnoreFilter<TSqlFilter>(this ISqlQuery sqlQuery)
    where TSqlFilter : ISqlFilter;

ISqlQuery IgnoreDeletedFilter(this ISqlQuery sqlQuery);
```

- `IgnoreFilter<TSqlFilter>`：忽略特定 Sql 过滤器（如多租户、审计、自定义过滤器等）；
- `IgnoreDeletedFilter()`：忽略逻辑删除过滤器，适用于需要查询已逻辑删除数据的后台/审计场景。

---

## 多数据源上下文与运行时切库

当前实现支持以 `dbKey` 作为业务入口在运行期切换数据源，并让连接串、实体映射、参数元数据和诊断上下文同步切换。调用方只传 `dbKey`；`DatabaseType`、连接字符串、只读标识、映射配置和主库策略均由数据源配置解析。

### 1. 注册数据库上下文能力

`AddDatabase<TDatabase>()` 现在会一并注册：

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

services.AddDatabase<AppDatabase>();
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

### 5. 连接和事务所有权

- `SetConnection(connection)`：外部连接，Query/Executor 不负责关闭或释放。
- `SetTransaction(transaction)`：外部事务，Query/Executor 不提交、不回滚、不释放，也不会关闭事务连接。
- `BeginTransaction()`：内部事务，Query/Executor 负责提交、回滚和释放。
- Dapper 执行器默认不会为单条 SQL 自动开启事务；异常时只回滚内部拥有的事务。

需要让多个 Query / Executor 共享一个独立事务时，可使用 `ISqlTransactionScopeFactory`：

```csharp
using var scope = transactionScopeFactory.Begin("reporting");

var executor = scope.CreateExecutor();
executor.ExecuteSql("update users set name=@name where id=@id", new { name = "Tom", id = 1 });

var query = scope.CreateQuery();
var user = query.From<User>().Where<User>(x => x.Id, 1).ToEntity<User>();

scope.Commit();
```

作用域拥有连接和事务。作用域创建的 Query / Executor 会绑定外部事务，不能自行提交或回滚；如果作用域释放前未调用 `Commit()` 或 `Rollback()`，会自动回滚。

如果未来启用主库读取策略，`PrimaryReadStrategy.Transaction` 只适合短查询。当前实现对 `StreamQuery` / `StreamQueryAsync` 会在创建读取器前直接抛出异常，避免在流式场景里静默退回到短事务策略。

### 6. 显式实体映射

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

映射解析顺序为：显式 `EntityMappings` -> 现有 `IEntityMetadata` -> CLR 类型回退。

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
- 参数值可来自匿名对象、字典或普通 POCO；
    - `Add(name, property, null)` 表示显式传入空值，执行时会绑定为 `DBNull.Value`；
    - `Map(name, property)` 表示从源对象读取参数值；
- 使用映射增强时，找不到必需输入值会抛出 `SqlParameterBindingException`；只有未启用映射增强的旧调用路径才保留原有弱元数据行为；
- 执行阶段会把 `DbType`、`Size`、`Precision`、`Scale`、`ProviderTypeName` 等元数据补齐到 ADO 参数；
- 执行诊断消息会包含标准化参数快照和增强参数元数据，便于排查参数类型与映射问题；
- 诊断参数快照不暴露 Dapper 内部参数对象。

### 2. 使用 `AddParam<TEntity>()` 为 Builder 参数补齐元数据

```csharp
sqlQuery.From<User>("u")
    .Where<User>(x => x.Name, "Tom");

sqlQuery.GetBuilder().AddParam<User>("statusCode", x => x.Status, 1);
```

该方式适合：

- 继续沿用现有 Builder / Query 代码；
- 在不改 SQL 片段文本的前提下，补齐特定参数的数据库元数据；
- 让 `GetCountAsync()`、原生 SQL 执行、分页等路径统一走增强参数绑定。

注意：`GetBuilder().GetParams()` 仍然保留轻量参数列表语义，便于调试；真正执行时会自动绑定增强后的数据库参数。

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

## 典型使用示例

> 以下示例中的执行部分（`SomeExecutor.Execute*`）仅为示意，请根据实际项目中 `ISqlQueryOperation` 的实现替换为真实调用方式。

### 示例一：简单列表查询

```csharp
public async Task<List<Order>> GetPaidOrdersAsync(ISqlQuery sqlQuery)
{
    sqlQuery
        .Select<Order>(x => new object[] { x.Id, x.OrderNo, x.CustomerName, x.Amount })
        .From<Order>("o")
        .Where<Order>(x => x.Status, OrderStatus.Paid);

    var builder = sqlQuery.GetBuilder();
    var sql = builder.ToDebugSql();
    var parameters = builder.GetParams(); // 伪代码，按实际 ISqlBuilder 实现调整

    var result = await SomeExecutor.ExecuteQueryAsync<Order>(sql, parameters);
    return result;
}
```

### 示例二：带可选条件的分页查询

```csharp
public async Task<PagerList<OrderDto>> QueryOrdersAsync(
    ISqlQuery sqlQuery,
    OrderQueryParameter parameter)
{
    sqlQuery
        .Select<Order>(x => new object[]
        {
            x.Id, x.OrderNo, x.CustomerName, x.Amount, x.Status
        })
        .From<Order>("o")
        .Where<Order>(x => x.Status, parameter.Status)
        .OrIf<Order>(
            !string.IsNullOrWhiteSpace(parameter.Keyword),
            x => x.OrderNo.Contains(parameter.Keyword)
              || x.CustomerName.Contains(parameter.Keyword));

    var result = await sqlQuery.PagerQueryAsync(
        func: async () =>
        {
            var builder = sqlQuery.GetBuilder();
            var sql = builder.ToDebugSql();
            var parameters = builder.GetParams();
            return await SomeExecutor.ExecuteQueryAsync<OrderDto>(sql, parameters);
        },
        parameter: parameter);

    return result;
}
```

### 示例三：多表 Join 查询

```csharp
public async Task<List<OrderWithCustomerDto>> GetOrderWithCustomerAsync(
    ISqlQuery sqlQuery,
    Guid orderId)
{
    sqlQuery
        .Select<Order>(x => new object[] { x.Id, x.OrderNo, x.Amount })
        .Select<Customer>(x => new object[] { x.Name, x.Phone })
        .From<Order>("o")
        .LeftJoin<Customer>("c")
        .On<Order, Customer>(o => o.CustomerId, c => c.Id)
        .Where<Order>(o => o.Id, orderId);

    var builder = sqlQuery.GetBuilder();
    var sql = builder.ToDebugSql();
    var parameters = builder.GetParams();

    var list = await SomeExecutor.ExecuteQueryAsync<OrderWithCustomerDto>(sql, parameters);
    return list;
}
```

### 示例四：忽略逻辑删除过滤器

```csharp
public async Task<Order> GetDeletedOrderAsync(ISqlQuery sqlQuery, Guid orderId)
{
    sqlQuery
        .IgnoreDeletedFilter()
        .From<Order>("o")
        .Where<Order>(x => x.Id, orderId);

    var builder = sqlQuery.GetBuilder();
    var sql = builder.ToDebugSql();
    var parameters = builder.GetParams();

    return await SomeExecutor.ExecuteSingleAsync<Order>(sql, parameters);
}
```

---

## 使用建议

1. **优先使用表达式扩展方法**：通过 `Select/From/Where/Join/...` 的表达式重载构建 Sql，避免硬编码列名，提高类型安全和可维护性。
2. **合理控制清理行为**：默认自动清理适合大多数场景；需要多次复用查询时，可使用 `ClearAfterExecution(false)` 或手动调用 `Clear*` 系列方法。
3. **善用 GetDebugSql 进行排查**：在开发和问题排查时，输出 `GetDebugSql()` 的结果便于和实际数据库环境对比验证。
4. **谨慎忽略过滤器**：`IgnoreDeletedFilter()` 等方法仅应在确有需要的管理/审计场景使用，避免破坏业务数据约束。
