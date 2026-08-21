# Lambda 查询使用

## 单表查询

来源泛型用于描述查询来源，结果类型在终结方法上显式指定：

```csharp
var rows = await query.From<User>()
    .Where(user => user.Status, UserStatus.Enabled)
    .OrderBy(user => new object[] { user.Id })
    .ToListAsync<User>();
```

DTO 投影声明列形状，终结方法声明物化类型：

```csharp
var rows = await query.From<User>()
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
var rows = await query.From<Order>()
    .LeftJoin<Customer>((order, customer) => order.CustomerId == customer.Id, "customer")
    .Join<Payment>((order, customer, payment) => order.Id == payment.OrderId)
    .Select((order, customer, payment) => new OrderDetails
    {
        OrderId = order.Id,
        CustomerName = customer.Name,
        PaymentId = payment.Id
    })
    .ToListAsync<OrderDetails>();
```

每个新增来源都会扩展 Lambda 参数列表，最多支持十个来源。连续 Join 的每个谓词应引用当前参数列表中的实际来源；不要依赖逗号来源隐式形成连接条件。

`CrossJoin<TJoin>()` 不接收谓词。需要 `RightJoin` 或 `FullJoin` 时，调用阶段会按当前 Provider 能力配置拒绝不支持的操作。

## Raw Fluent 与原生文本

Raw 查询在创建时确定结果类型：

```csharp
var rows = await query.Query<Order>()
    .Select("Id,OrderNo")
    .From("Orders")
    .ToListAsync();

var row = await query.Sql<Order>(
    "Select Id,OrderNo From Orders Where Id=@id",
    new { id = orderId })
    .FirstOrDefaultAsync();
```

Raw 查询不接受用于重新选择结果类型的 `<TNextResult>` 终结重载。多对象映射使用 `SqlFluentQuery<TResult>` 的既有映射方法，并保持入口处确定的 `TResult`。

## 测试和环境

SQLite 集成测试不需要外部服务，会使用临时数据库验证 SQL、参数、物化、分页、流式和取消。其它 Provider 的真实集成测试必须通过项目既有环境变量 Gate，并使用专用测试数据库；连接字符串不得硬编码或提交到仓库。
