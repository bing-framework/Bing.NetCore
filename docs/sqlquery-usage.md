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
