# Lambda 查询使用

当变量静态类型为 `ISqlQuery` 时，`From<TEntity>()` 和 `From<TEntity>(alias)` 保持已发布的一元泛型兼容返回类型；要进入 dev_v6 非泛型主路径，请显式调用 `From<TEntity>(alias, schema)`，没有别名或架构时传入 `null, null`。下例使用两参数入口，以避免依赖旧兼容返回类型。

## 单表查询

来源泛型用于描述查询来源，结果类型在终结方法上显式指定：

```csharp
var rows = await query.From<User>(null, null)
    .Where(user => user.Status, UserStatus.Enabled)
    .OrderBy(user => new object[] { user.Id })
    .ToListAsync<User>();
```

DTO 投影声明列形状，终结方法声明物化类型：

```csharp
var rows = await query.From<User>(null, null)
    .Select(user => new UserDto
    {
        Id = user.Id,
        Name = user.Name
    })
    .ToListAsync<UserDto>();
```

同样的规则适用于 `First<TResult>`、`Single<TResult>`、`Scalar<TResult>`、`ToPage<TResult>`、`AsEnumerable<TResult>` 及其异步版本。

## 连续 Join

Lambda Join 在同一次调用中接收谓词和可选 alias，不再使用后置 `.On(...)`：

```csharp
var rows = await query.From<Order>(null, null)
    .LeftJoin<Order, Customer>((order, customer) => order.CustomerId == customer.Id, "customer")
    .Join<Customer, Payment>((customer, payment) => customer.Id == payment.CustomerId)
    .Select<Customer, Payment, OrderDetails>((customer, payment) => new OrderDetails
    {
        CustomerName = customer.Name,
        PaymentId = payment.Id
    })
    .ToListAsync<OrderDetails>();
```

每次 Join 都使用两个来源的谓词并在调用阶段原子提交；需要继续追加来源时，重复调用二元 `Join<TLeft, TRight>` 或 `LeftJoin<TLeft, TRight>`。投影 Lambda 最多接收两个来源；需要组合更多来源时，应先通过二元投影创建派生表，再使用二元 Join 继续组合，不要编写三元或更高参数的 Lambda。

`CrossJoin<TJoin>()` 不接收谓词。需要 `RightJoin` 或 `FullJoin` 时，调用阶段会按当前 Provider 能力配置拒绝不支持的操作。

## Raw Fluent 与原生文本

Raw 文本查询的主入口不固定结果类型，结果类型在终结方法处选择：

```csharp
var rows = await query.Query<Order>()
    .Select("Id,OrderNo")
    .From("Orders")
    .ToListAsync();

var row = await query.Sql(
    "Select Id,OrderNo From Orders Where Id=@id",
    new { id = orderId })
    .FirstOrDefaultAsync<Order>();
```

Raw 查询支持 `ToEntity<TResult>`、`ToList<TResult>`、`ToDictionary<TResult,TKey,TValue>`、标量和同步/异步流式终结方法。2～7 对象多映射仍由已发布的低层泛型描述提供；已发布的泛型 `Sql<TResult>` 兼容入口继续保留，新代码应使用非泛型 `Sql(...)`。

## 测试和环境

SQLite 集成测试不需要外部服务，会使用临时数据库验证 SQL、参数、物化、分页、流式和取消。其它 Provider 的真实集成测试必须通过项目既有环境变量 Gate，并使用专用测试数据库；连接字符串不得硬编码或提交到仓库。
