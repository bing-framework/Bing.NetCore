# ISqlQuery 使用说明

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../ReleaseNotes.md)

本文是 **`ISqlQuery` 的使用手册**：怎么装、怎么注册、怎么拿到实例、四种查询形态怎么用、参数化与分页怎么写。

**本文不覆盖**：

- Lambda 子句（`Where` / `Select` / `Join` 的 Lambda 重载与投影）→ [Lambda 查询](sqlquery-lambda-usage.md)
- Provider 差异与能力边界 → [Provider 能力](provider-capabilities.md)
- 批量写入（InsertBatch / UpdateBatch / DeleteBatch）→ [批量 Mutation](sql-mutation-batch-execution.md)
- 事务 API 的 7.0.0 迁移 → [事务 API](../migrations/sql-transaction-api-vNext.md)

> ⚠ 本文所有签名均已对照源码核对。若本文与源码冲突，**请信任源码**并回来修正本文。

---

## 1. 五分钟上手

### 1.1 装包

`ISqlQuery` 本体在 **`Bing.Data.Sql`**，执行能力在 **`Bing.Dapper.Core`**，具体数据库再装对应的 Provider 包：

| 用途 | 包 |
| --- | --- |
| 查询对象与 SQL 生成器 | `Bing.Data.Sql` |
| Dapper 执行核心 | `Bing.Dapper.Core` |
| MySQL | `Bing.Dapper.MySql` |
| SQL Server | `Bing.Dapper.SqlServer` |
| PostgreSQL | `Bing.Dapper.PostgreSql` |
| Oracle | `Bing.Dapper.Oracle` |
| SQLite | `Bing.Dapper.Sqlite` |

### 1.2 注册

两步：**注册 Provider** + **注册数据源**。

```csharp
using Bing.Dapper.MySql;          // AddMySqlProvider()
using Bing.Data.Enums;            // DatabaseType
using Bing.Dapper;                // AddSqlDataSource()

var services = new ServiceCollection();

// ① 注册 MySQL Provider（内部已调用 AddSqlCore()）
services.AddMySqlProvider();

// ② 注册数据源：连接串直接写
services.AddSqlDataSource("default", DatabaseType.MySql,
    "Server=localhost;Database=app;Uid=root;Pwd=***;");

// 或：从 IConfiguration 的 ConnectionStrings 节读取
// services.AddSqlDataSource(configuration, "default", DatabaseType.MySql,
//     connectionStringName: "Default");
```

> ⚠ **`AddMySqlProvider()` 只注册 Provider，不配置数据源。** 少了第 ② 步，运行时解析不到连接串。多数据源场景请用**具名** `AddSqlDataSource("orders", ...)`，再以 `factory.Create("orders")` 取用。

### 1.3 拿实例

`ISqlQuery` 注册为 **Transient**，且实例持有可变的 Builder、连接与事务状态，**不能跨并发操作共享**。请用工厂创建：

```csharp
public class OrderController(ISqlQueryFactory queryFactory) : ControllerBase
{
    [HttpGet]
    public async Task<List<OrderDto>> List()
    {
        await using var query = queryFactory.Create();   // 不传 key 则用默认数据源
        return await query.From<Order>("o")
            .Where<Order>(o => o.IsDeleted == false)
            .ToListAsync<OrderDto>();
    }
}
```

工厂接口只有两个成员（`Bing.Data.Sql` 命名空间）：

```csharp
public interface ISqlQueryFactory
{
    ISqlQuery Create(string dbKey = null);
}
```

也可以直接注入 `ISqlQuery`（Transient，每次注入都是新实例），但**显式用工厂 + `await using` 是推荐写法**——生命周期边界更清楚。

### 1.4 第一条查询（三种写法等价）

```csharp
// ① Lambda（类型化，推荐）
var list = await query.From<Product>("p")
    .Where<Product>(p => p.Code == "A001")
    .ToListAsync<Product>();

// ② Fluent（字符串）
var list2 = await query.Query()
    .Select("*").From("Product").Where("Code", "A001")
    .ToListAsync<Product>();

// ③ 原生 SQL
var list3 = await query.Sql("Select * From Product Where Code = @code", new { code = "A001" })
    .ToListAsync<Product>();
```

---

## 2. 四种查询形态

`ISqlQuery` 只有 **5 个入口方法**，返回 **4 种查询描述**（`Bing.Data.Sql/ISqlQuery.cs:9-51`）：

```csharp
public partial interface ISqlQuery : IDisposable, IAsyncDisposable
{
    SqlFluentQuery Query();
    SqlTextQuery Sql(string sql, object parameters = null);
    SqlTextQuery SqlInterpolated(FormattableString sql);
    SqlProcedureQuery Procedure(string procedure, object parameters = null);
    SqlLambdaQuery From<TEntity>(string alias = null, string schema = null) where TEntity : class;
    SqlLambdaQuery FromSubquery<TProjection>(SqlSubquery<TProjection> subquery) where TProjection : class;
}
```

| 入口 | 返回类型 | 用途 | 结果类型由谁决定 |
| --- | --- | --- | --- |
| `From<TEntity>(alias, schema)` | `SqlLambdaQuery` | **类型化**查询，走实体映射 | 终结方法的泛型参数 `<TResult>` |
| `FromSubquery<T>(subquery)` | `SqlLambdaQuery` | 以派生表为根来源 | 同上 |
| `Query()` | `SqlFluentQuery` | 字符串 Fluent 拼接 | 同上 |
| `Sql(sql, parameters)` | `SqlTextQuery` | 原生 SQL + 参数对象 | 同上 |
| `SqlInterpolated($"...")` | `SqlTextQuery` | 插值原生 SQL（自动参数化） | 同上 |
| `Procedure(name, parameters)` | `SqlProcedureQuery` | 存储过程 | `Execute*` 方法的泛型参数 |

关键点：**结果类型不在入口处指定，而在终结方法处指定**。这就是文档里反复强调「**结果类型由终结方法决定**」的含义——同一个 `SqlFluentQuery` 可以 `.ToList<A>()` 也可以 `.ToList<B>()`。

---

## 3. 终结方法全表

### 3.1 通用终结方法（`SqlFluentQuery` / `SqlTextQuery` / `SqlLambdaQuery` 共有）

三种查询描述都提供下面这套（签名以 `SqlLambdaQuery` 为例，其余两种参数一致）：

| 同步 | 异步 | 说明 |
| --- | --- | --- |
| `ToList<TResult>(int? timeout = null)` | `ToListAsync<TResult>(int? timeout = null, CancellationToken ct = default)` | 完整物化为 `List<T>` |
| `ToEntity<TResult>(int? timeout = null)` | `ToEntityAsync<TResult>(...)` | 至多一行；零行返回默认值，**多行抛异常** |
| `First<TResult>(int? timeout = null)` | `FirstAsync<TResult>(...)` | 第一行，无行则抛 |
| `FirstOrDefault<TResult>(int? timeout = null)` | `FirstOrDefaultAsync<TResult>(...)` | 第一行或默认值 |
| `Single<TResult>(int? timeout = null)` | `SingleAsync<TResult>(...)` | 唯一一行，非唯一则抛 |
| `Scalar<TResult>(int? timeout = null)` | `ScalarAsync<TResult>(...)` | 首行首列 |
| `ToPage<TResult>(IPager pager = null, int? timeout = null)` | `ToPageAsync<TResult>(IPager pager = null, ...)` | 分页，返回 `PagerList<T>` |
| `AsEnumerable<TResult>(int? timeout = null)` | `AsAsyncEnumerable<TResult>(int? timeout = null, CancellationToken ct = default)` | **流式**读取 |
| `ToSql()` | — | 只渲染 SQL，不执行 |

`timeout` 单位是**秒**，`null` 表示用数据库默认。

### 3.2 Dapper 多映射终结方法

`SqlFluentQuery` 与 `SqlTextQuery` 额外提供 2~7 个对象的多映射重载（`SqlLambdaQuery` **没有**）：

```csharp
List<TResult> ToList<TFirst, TSecond, TResult>(Func<TFirst, TSecond, TResult> map, int? timeout = null);
// …一直到七个类型参数
Task<List<TResult>> ToListAsync<TFirst, TSecond, TResult>(Func<TFirst, TSecond, TResult> map,
    int? timeout = null, CancellationToken ct = default);
```

多映射的分段列默认是 `"Id"`，可用 `SplitOn("ProductId,ItemId")` 改（逗号分隔多个分段列）。

### 3.3 存储过程专有终结方法

`SqlProcedureQuery` **不使用**上面那套，而是 `Execute*` 系列，且返回 `SqlProcedureResult<T>`：

```csharp
SqlProcedureResult<List<TResult>> ExecuteList<TResult>(int? timeout = null);
SqlProcedureResult<TResult>       ExecuteFirst<TResult>(int? timeout = null);
SqlProcedureResult<TResult>       ExecuteFirstOrDefault<TResult>(int? timeout = null);
SqlProcedureResult<TResult>       ExecuteSingle<TResult>(int? timeout = null);
SqlProcedureResult<TResult>       ExecuteSingleOrDefault<TResult>(int? timeout = null);
SqlProcedureResult<TResult>       ExecuteScalar<TResult>(int? timeout = null);
// 以及对应的 ExecuteListAsync / ExecuteFirstAsync / … / ExecuteScalarAsync
```

另有 `Procedure`（过程名）与 `Parameters`（参数快照）两个只读属性。

---

## 4. 流式读取

非缓冲流式读取的入口是 **`AsEnumerable<T>()` / `AsAsyncEnumerable<T>()`**：

```csharp
await using var query = queryFactory.Create();
await foreach (var row in query.From<Order>().AsAsyncEnumerable<Order>())
{
    // 逐行处理，不会一次性把整表读进内存
}
```

> ⚠ **没有 `buffered` 参数。** 缓冲与否由**用哪个终结方法**决定，不能传参控制：
>
> - `ToList*` / `First*` / `Single*` / `ToPage*` / `Scalar*` → 内部固定 `buffered: true`（`SqlQueryBase.QueryPlan.Terminals.cs`）
> - `AsEnumerable*` / `AsAsyncEnumerable*` → 内部固定 `buffered: false`（`SqlQueryBase.QueryPlan.Streaming.cs:72,210`）
>
> 老版本文档里出现过 `ToListAsync(buffered: false)` 的写法，那是**编造的签名**，编译不过。

---

## 5. 分页

### 5.1 用法

```csharp
var pager = new Pager(page: 1, pageSize: 20, order: "CreateTime desc");
PagerList<OrderDto> result = await query.From<Order>()
    .ToPageAsync<OrderDto>(pager);

int total = result.TotalCount;   // 总行数由执行路径自动填充
List<OrderDto> rows = result.Data;
```

`ToPage<T>(IPager pager = null, ...)`：**传 `null` 时使用当前 Builder 上的分页配置**。

### 5.2 取总行数

**不需要单独查一次 `Count`**。`Pager` 上有个 `IsTotalCountKnown`：

- 默认 `false` → 分页执行路径会**自动附带一次计数查询**并回填 `TotalCount`
- 若你已知总数（或明确知道是 0），构造时传进去：`new Pager(1, 20, totalCount: 0, totalCountKnown: true)` —— 这样会**跳过计数查询**

> ⚠ 因为总数为 0 时 `TotalCount` 也是 0，光看值无法区分"没查"和"确实是 0"，所以框架用 `IsTotalCountKnown` 这个独立标志位。已知结果为 0 时**必须显式设 `totalCountKnown: true`**，否则会多跑一次 COUNT。

### 5.3 `PagerList<T>` 的成员

| 成员 | 说明 |
| --- | --- |
| `Page` / `PageSize` | 页码（从 1 开始）/ 每页行数 |
| `TotalCount` / `PageCount` | 总行数 / 总页数 |
| `Order` | 排序条件 |
| `Data` | `List<T>` 数据本体 |
| `Convert<TResult>(...)` | 保留分页信息、转换元素类型 |

---

## 6. 参数化

### 6.1 原生 SQL 的三种写法

```csharp
// ① 匿名对象参数（最常用）
await query.Sql("Select * From Product Where Code = @code", new { code = "A001" })
    .ToListAsync<Product>();

// ② 插值字符串，自动参数化
string code = "A001";
await query.SqlInterpolated($"Select * From Product Where Code = {code}")
    .ToListAsync<Product>();

// ③ 先写 SQL 再补参数
await query.Sql("Select * From Product Where Code = @code")
    .AddParam("code", "A001")
    .ToListAsync<Product>();
```

> ⚠ **不要手写字符串拼接**。`Bing.Data.Sql.Analyzers` 会对 `Sql(...)` / `ExecuteSql(...)` 里的不安全插值报诊断（见 `Bing/Data/Sql/Analyzers/UnsafeInterpolatedSqlAnalyzer.cs:51`）。`SqlInterpolated` 是框架提供的**安全**插值入口——它把插值转成参数，而非拼进文本。

### 6.2 `AddParam` 的两个重载

```csharp
T AddParam<T>(this T source, string name, object value = null) where T : ISqlParameter;
T AddParam<T, TEntity>(this T source, string name, Expression<Func<TEntity, object>> property, object value = null)
    where T : ISqlParameter where TEntity : class;
```

第二个重载会**顺带把实体属性的列元数据**写进参数——当数据库对参数类型敏感（例如 MySQL 的 `decimal`、PostgreSQL 的 `jsonb`）时很有用：

```csharp
await query.Sql("Select * From Product Where Price > @price")
    .AddParam<Product>("price", p => p.Price, 100m)   // 携带 Product.Price 的列元数据
    .ToListAsync<Product>();
```

配套还有 `GetParams()` / `GetSqlParams()` / `GetParam<T>(name)` / `ClearParams()`，用于检查与复用参数。

### 6.3 `SqlParameterMap<TEntity>`（写入侧）

`ISqlQuery` 的查询路径用不到它，但 **`ISqlExecutor.ExecuteSql` 支持**，用于把参数显式映射到实体属性：

```csharp
var map = new SqlParameterMap<Product>()
    .Add("ProductId", p => p.Id, id)
    .Add("Code",      p => p.Code, "A001");

executor.ExecuteSql("Insert Product(ProductId,Code) Values(@ProductId,@Code)", map);
```

输出参数用 `AddOutput(name, property, dbType, size)`。

---

## 7. 存储过程与输出参数

```csharp
await using var query = queryFactory.Create();
var result = await query.Procedure("sp_GetOrderCount", new { tenantId })
    .ExecuteScalarAsync<int>();

int count = result.Result;                                  // 过程返回值
var outputs = result.OutputParameters;                      // 输出参数快照（可能为 null）
```

`OutputParameters` 是 **`ISqlOutputParameterAccessor`**（`Bing.Data.Sql.Runtime.Plans`），提供 `GetValue(name)` / `GetValue<T>(name)` / `TryGetValue<T>(name, out value)`。

两个要点：

- 它是**构造时复制的值快照**（`SqlProcedureResult.cs:19-23`），不依赖 Root Query / Executor / ADO.NET 参数对象的后续状态，可以安全地带出作用域。
- 当参数源**不是框架支持的输出参数模型**时，`OutputParameters` 为 **`null`**——调用方应改用自己参数源的访问方式。

> ⚠ SQLite **不支持存储过程**；PostgreSQL 与 Oracle 的 OUT 参数语义各不相同，详见 [Provider 能力](provider-capabilities.md)。

---

## 8. 事务

查询若需参与事务，请用 `ISqlTransactionScope`，并由**作用域**创建查询对象（而不是外部工厂）：

```csharp
var factory = serviceProvider.GetRequiredService<ISqlTransactionScopeFactory>();
await using (var scope = factory.Create())
{
    var query = scope.CreateQuery();       // 绑定到当前事务
    var executor = scope.CreateExecutor();

    await executor.ExecuteSqlAsync("Insert Product(...) Values(...)", new { ... });
    var list = await query.From<Product>().ToListAsync<Product>();

    await scope.CommitAsync();
}
```

`ISqlTransactionScope` 成员：`IsCompleted` / `CreateQuery()` / `CreateExecutor()` / `Commit()` / `CommitAsync(ct)` / `Rollback()` / `RollbackAsync(ct)`。

> ⚠ **Doris 数据源被强制为只读且 `SupportsTransactions = false`**（`DapperServiceCollectionExtensions.cs:196-207`），对它开本地事务会被拒绝。需要用 MySQL 可写端点时，请显式登记为 `DatabaseType.MySql`。

事务 API 在 7.0.0 有破坏性变更，迁移细节见 [事务 API 迁移](../migrations/sql-transaction-api-vNext.md)。

---

## 9. 调试：只看 SQL 不执行

所有查询描述都提供 `ToSql()`：

```csharp
var description = query.From<Product>("p").Where<Product>(p => p.Code == "A001");
Console.WriteLine(description.ToSql());
// Select ... From `Product` `p` Where `p`.`Code` = @p_1
```

`ToSql()` 会复用已缓存的渲染结果；查询结构一旦变更（`Touch()`），缓存即失效并重新渲染。

---

## 10. 生命周期与并发约束

这是最容易踩的一类问题，因为它**不是每次都报错**。

### 10.1 查询描述是"一次性的"

每个查询描述内部有状态机（`SqlQuery.cs:19-32`）：

```
Draft（可改） → Frozen（已生成执行计划） → Executing → Completed
```

- 调用**任意终结方法**就会进入 `Frozen`；之后再改结构会抛
  `InvalidOperationException("查询已冻结，不能继续修改查询描述。")`（`SqlQuery.cs:98,570`）
- 已经执行完的查询描述**不能复用来再执行一次**不同的查询——请重新 `Create()`

### 10.2 不能并发执行同一个描述

执行用 `Interlocked.CompareExchange` 取租约（`SqlQuery.cs:550`）：

```
InvalidOperationException("当前查询正在执行，不能并发执行同一查询描述。")
```

`await foreach` 尚未结束就发起第二次执行，会命中这条。

### 10.3 不能共享 `ISqlQuery` 实例

接口文档写得很明确（`ISqlQuery.cs:7`）：

> 实例包含可变的 Sql 生成器、连接和事务状态，不能被多个并发操作共享。每个独立操作应使用独立实例。

在 ASP.NET Core 里把 `ISqlQuery` 存进单例字段是错误用法；正确做法是**每次操作都从工厂取一个新的**，并用 `await using` 释放。

### 10.4 需要"同一个查询跑两次"怎么办

`SqlLambdaQuery` 提供 `Clone()`（`SqlLambdaQuery.Terminals.cs:18`，内部走 `_core.Clone()`），返回拥有独立 Builder 状态的副本：

```csharp
var baseQuery = query.From<Order>().Where<Order>(o => o.Amount > 100);
var page1 = await baseQuery.Clone().ToPageAsync<Order>(new Pager(1, 20));
var page2 = await baseQuery.Clone().ToPageAsync<Order>(new Pager(2, 20));
```

---

## 11. 不存在的方法（防误用清单）

下列调用在源码中**均不存在**。照着写会编译失败——它们多半来自旧文档或与其它框架的记忆混淆。

| 你以为有 | 实际情况 |
| --- | --- |
| `ToListAsync(buffered: false)` | ❌ 无 `buffered` 参数。流式请用 `AsEnumerable<T>()` / `AsAsyncEnumerable<T>()` |
| `StreamAsync<T>()` | ❌ 不存在。同上 |
| `GetCountAsync()` | ❌ 不存在。取总数走 `ToPage` 的 `Pager.TotalCount` |
| `ToDynamicList()` | ❌ 不存在。动态结果请用 `ToList<dynamic>()` |
| `ExecuteSql<TEntity>()` 在 `ISqlQuery` 上 | ❌ `ExecuteSql` 属于 **`ISqlExecutor`**，不属于 `ISqlQuery` |
| `Query().ToList<T>()` 之后再改 `Where` | ❌ 已冻结，会抛 `InvalidOperationException` |

另外：`ISqlQuery` **没有** `SetConnection` / `GetConnection` / `SetTransaction` / `BeginTransaction` / `CommitTransaction` / `RollbackTransaction` / `IDbConnectionManager` / `IDbTransactionManager` —— 这些在 7.0.0 已移除，连接与事务改由数据源 + 事务作用域管理，见 [事务 API 迁移](../migrations/sql-transaction-api-vNext.md)。

---

## 12. 完整示例（取自集成测试）

下面这段来自 `framework/tests/Bing.Dapper.MySql.Tests.Integration`，是**真实跑得通**的写法：

```csharp
using var query = _fixture.CreateQuery();

// Fluent + Join + 条件 + 投影
var description = query.Query()
    .Select("p.Code As ProductCode,i.Sku As Sku,i.Quantity As Quantity")
    .AppendFrom("Product p")
    .Join("ProductItem", "i").AppendOn("i.ProductId=p.ProductId")
    .Where("p.ProductId", productId);

var result = await description.FirstOrDefaultAsync<ProductItemProjection>();
```

```csharp
// Lambda + In 条件
var list = _sqlQuery.From<Product>()
    .Select<Product>(true)
    .Where<Product, object>(x => (object)x.Id, new object[] { id, id2 }, Operator.In)
    .ToList<Product>();
```

```csharp
// 计数
int count = _sqlQuery.Query().CountAll().From("Product").Scalar<int>();
```

装配容器的完整代码见 `Bing.Dapper.Sqlite.Tests.Integration/Infrastructure/SqliteIntegrationDatabaseFixture.cs:54-86`——它展示了 `AddSqlCore` + `ConfigureSqlMetadata` + `AddSqlDataSource` + `AddSqliteProvider` 的最小可用组合，是照抄成本最低的模板。

---

## 13. 相关文档

- [Lambda 查询](sqlquery-lambda-usage.md) —— `Where` / `Select` / `Join` 的 Lambda 重载、投影与子查询
- [Provider 能力](provider-capabilities.md) —— 各 Provider 的方言差异与能力边界
- [批量 Mutation](sql-mutation-batch-execution.md) —— 写入侧
- [事务 API 迁移](../migrations/sql-transaction-api-vNext.md) —— 7.0.0 的连接与事务变更
- [集成测试与 RunSettings](../testing/database-integration-tests.md) —— 怎么跑真库验证
- [SQL 文档入口](README.md)
