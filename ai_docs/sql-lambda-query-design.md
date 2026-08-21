# Bing.Data.Sql Lambda 查询设计

## 结果类型规则

`SqlLambdaQuery<TSource1,...,TSourceN>` 的泛型参数只表示来源图，支持 1～10 个来源，不表示结果类型。投影方法只负责声明列：

```csharp
var query = sqlQuery.From<Order>()
    .Select(order => new OrderSummary
    {
        Id = order.Id,
        Total = order.Total
    });

var rows = await query.ToListAsync<OrderSummary>();
```

Lambda 查询的列表、单行、标量、分页和同步/异步流方法均显式接收 `TResult`。Raw Fluent/Text 查询在 `Query<TResult>()` 或 `Sql<TResult>()` 创建时固定结果类型，其终结方法不再接受另一个结果泛型参数。Dapper 2～7 对象多映射继续由 `SqlFluentQuery<TResult>` 提供。

## 来源与 Join

单根和多根入口分别支持 `From<TEntity>()` 与 `From<T1,...,T10>()`。类型化 Join 将新来源和完整谓词作为一次操作提交：

```csharp
var rows = await sqlQuery.From<Order>()
    .Join<Customer>((order, customer) => order.CustomerId == customer.Id)
    .Join<Invoice>((order, customer, invoice) => customer.Id == invoice.CustomerId)
    .Select((order, customer, invoice) => new OrderSummary
    {
        Id = order.Id,
        CustomerName = customer.Name,
        InvoiceNo = invoice.Number
    })
    .ToListAsync<OrderSummary>();
```

`LeftJoin`、`RightJoin` 和 `FullJoin` 采用相同的原子调用形式；Lambda API 不提供后置 `.On(...)`。`CrossJoin` 不接收 Lambda 谓词。重复实体应使用显式 alias，查询会对自动 alias 保持稳定注册。

Join 在调用阶段完成 Provider 能力、别名、映射、表引用、参数和谓词预检；失败不会提交 Join、参数或来源图。派生表 Join 还会校验 Provider、数据源、租户、映射配置和参数快照。

## Provider 边界

Right/Full Join 是否可用由冻结的 `SqlProviderProfile` 决定。SQLite 和 MySQL 的不支持能力应在对应 Lambda Join 调用时拒绝；SQL Server 等支持 Provider 才能生成相应 SQL。原始 Builder 的低层能力校验仍在 SQL 渲染验证阶段执行。

结构化 Lambda 不接受外部原始 SQL。需要原生文本时使用 `Sql<TResult>(固定模板, 参数对象)`，所有外部值通过参数绑定，不拼接到 SQL、表名、列名或排序表达式中。

## 生命周期与验证

每个查询描述拥有独立 Builder，但复用根查询的连接、事务、诊断和数据源快照。查询描述不应跨线程并发修改或复用为多个可变构建流程；需要另一条 SQL 时创建新的描述或 Clone Builder。

纯 Builder、API 反射和失败状态测试位于 `framework/tests/Bing.Data.Sql.Tests`。SQLite 真实执行测试位于 `framework/tests/Bing.Dapper.Sqlite.Tests.Integration`，覆盖真实 SQL、Dapper 物化、分页、流式和取消。MySQL、PostgreSQL、SQL Server、Oracle 外部集成测试只在显式环境 Gate 满足时运行，不硬编码连接信息。
