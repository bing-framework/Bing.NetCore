# Bing.Data.Sql Lambda 查询设计

## 结果类型规则

`SqlLambdaQuery` 是 dev_v6 的非泛型 Lambda 主描述，来源通过连续 `From<TEntity>(alias, schema)` 追加，不再编码在查询类型的泛型参数中。由于已发布的 `ISqlQuery.From<TEntity>(string alias = null)` 必须保留，`ISqlQuery` 上的零/一参数调用仍返回 `SqlLambdaQuery<TEntity>` 兼容描述；新代码从根入口进入非泛型主路径时应显式传入 `null, null` 或实际 alias/schema。投影只描述列形状，最终结果类型由终结方法显式指定：

```csharp
var query = sqlQuery.From<Order>(null, null)
    .Select(order => new OrderSummary
    {
        Id = order.Id,
        Total = order.Total
    });

var rows = await query.ToListAsync<OrderSummary>();
```

Lambda 与非泛型 Raw 查询的列表、单行、标量、分页和同步/异步流方法均在终结方法显式接收 `TResult`。已发布的泛型 Raw 入口和低层多映射类型仅作为兼容路径保留；Dapper 2～7 对象多映射继续通过低层泛型描述提供。

## 来源与 Join

根入口支持连续 `From<TEntity>(alias, schema)`、`FromTable(...)` 和 `FromSubquery(...)`。类型化 Join 使用二元方法泛型，将新增来源和完整谓词作为一次操作提交：

```csharp
var rows = await sqlQuery.From<Order>(null, null)
    .Join<Order, Customer>((order, customer) => order.CustomerId == customer.Id)
    .Join<Customer, Invoice>((customer, invoice) => customer.Id == invoice.CustomerId)
    .Select<Customer, Invoice>((customer, invoice) => new object[]
    {
        customer.Name,
        invoice.Number
    })
    .ToListAsync<OrderSummary>();
```

`LeftJoin`、`RightJoin` 和 `FullJoin` 采用相同的原子调用形式；Lambda API 不提供后置 `.On(...)`。`CrossJoin` 不接收 Lambda 谓词。重复实体应使用显式 alias，查询会对自动 alias 保持稳定注册。

Join 在调用阶段完成 Provider 能力、别名、映射、表引用、参数和谓词预检；失败不会提交 Join、参数或来源图。派生表 Join 还会校验 Provider、数据源、租户、映射配置和参数快照。

## Provider 边界

Right/Full Join 是否可用由冻结的 `SqlProviderProfile` 决定。SQLite 和 MySQL 的不支持能力应在对应 Lambda Join 调用时拒绝；SQL Server 等支持 Provider 才能生成相应 SQL。原始 Builder 的低层能力校验仍在 SQL 渲染验证阶段执行。

结构化 Lambda 不接受外部原始 SQL。需要原生文本时，新代码使用 `Sql(固定模板, 参数对象)`，所有外部值通过参数绑定，不拼接到 SQL、表名、列名或排序表达式中；已发布的 `Sql<TResult>` 仅供兼容调用方和低层多映射使用。

## 生命周期与验证

每个查询描述拥有独立 Builder 和 QueryContextId。配置阶段为 Draft；首次终结执行时生成冻结的 `SqlQueryPlan` 快照，执行期间为 Executing，完成后为 Completed。`ToSql()` 只渲染，不冻结；冻结后修改和同一描述的并发执行会拒绝，流式租约直到枚举结束或提前 Dispose 才释放。

分页从同一冻结计划派生 Count/Data 计划，共享 QueryContextId，各次执行分别生成 ExecutionId 和阶段标识。计划不持有连接、事务或可变分页 Builder；参数快照深复制常见数组、字典和集合值。

查询实例缓存只保存 ShapeVersion、SQL 和参数布局，参数值、租户值、连接、事务、诊断 ID 不进入缓存。`WhereIf(false)` 不修改版本，失败的 Join/WhereGroup 不提交状态；Provider、Mapping、Tenant 或动态过滤指纹不稳定时旁路缓存。

诊断优先使用 `Activity.Current` 的 Trace/Span，其次使用 Core 关联标识，再回退 `TraceIdContext.Current`。Before/After/Error 共享同一个 ExecutionId，QueryContextId、Phase 和身份标签不写入 SQL 文本。

纯 Builder、API 反射和失败状态测试位于 `framework/tests/Bing.Data.Sql.Tests`。SQLite 真实执行测试位于 `framework/tests/Bing.Dapper.Sqlite.Tests.Integration`，覆盖真实 SQL、Dapper 物化、分页、流式和取消。MySQL、PostgreSQL、SQL Server、Oracle 外部集成测试只在显式环境 Gate 满足时运行，不硬编码连接信息。
