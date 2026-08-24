# Lambda 查询使用

`ISqlQuery` 只提供一个 `From<TEntity>(alias = null, schema = null)` Lambda 来源入口，返回非泛型 `SqlLambdaQuery`。连续调用 `From<TEntity>()` 会追加多个根来源；结果类型统一由终结方法选择。

## 单表查询

来源泛型用于描述查询来源，结果类型在终结方法上显式指定：

```csharp
var rows = await query.From<User>("u")
    .Where<User, UserStatus>(user => user.Status, UserStatus.Enabled, "u")
    .OrderBy(user => new object[] { user.Id }, "u")
    .ToListAsync<User>();
```

DTO 投影声明列形状，终结方法声明物化类型：

```csharp
var rows = await query.From<User>("u")
    .Select(user => new UserDto
    {
        Id = user.Id,
        Name = user.Name
    }, "u")
    .ToListAsync<UserDto>();
```

同样的规则适用于 `First<TResult>`、`Single<TResult>`、`Scalar<TResult>`、`ToPage<TResult>`、`AsEnumerable<TResult>` 及其异步版本。

## 连续 Join

Lambda Join 在同一次调用中接收谓词和可选 alias，不再使用后置 `.On(...)`：

```csharp
var rows = await query.From<Order>("order")
    .LeftJoin<Order, Customer>((order, customer) => order.CustomerId == customer.Id,
        rightAlias: "customer", leftAlias: "order")
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
var rows = await query.Query()
    .Select("Id,OrderNo")
    .From("Orders")
    .ToListAsync<Order>();

var row = await query.Sql(
    "Select Id,OrderNo From Orders Where Id=@id",
    new { id = orderId })
    .FirstOrDefaultAsync<Order>();
```

Raw 查询支持 `ToEntity<TResult>`、`ToList<TResult>`、`First<TResult>`、`FirstOrDefault<TResult>`、`Single<TResult>`、标量和同步/异步流式终结方法。`SingleOrDefault` 与 `ToEntity` 的语义重复，已删除；字典结果请先调用 `ToList<TResult>()` 再使用 LINQ `ToDictionary`。2～7 对象多映射继续保留在隐藏的 Advanced 泛型路径中；普通代码使用非泛型 `Sql(...)`、`Query()` 和 `Procedure(...)`。

## 同类型来源

同一实体被多次加入查询时，表达式参数名不参与来源决策；需要明确来源的操作传入 alias：

```csharp
var rows = await query.From<User>("parent")
    .From<User>("child")
    .Where<User, User>(
        (parent, child) => parent.Id == child.ParentId,
        firstAlias: "parent", secondAlias: "child")
    .Select<User, User, UserLinkDto>(
        (parent, child) => new UserLinkDto { ParentId = parent.Id, ChildId = child.Id },
        firstAlias: "parent", secondAlias: "child")
    .ToListAsync<UserLinkDto>();
```

## 迁移对照

| 旧用法 | 最终用法 |
| --- | --- |
| `query.From<User>(null, null)` | `query.From<User>()` |
| `query.Query<User>()` | `query.Query().ToList<User>()` 或其他终结方法 |
| `query.Sql<User>(sql, parameters)` | `query.Sql(sql, parameters).ToList<User>()` 或其他终结方法 |
| `query.Procedure<User>(name, parameters)` | `query.Procedure(name, parameters).ExecuteList<User>()` 或其他 Execute 终结方法 |
| `query.From<User>().SingleOrDefault<User>()` | `query.From<User>().ToEntity<User>()` |
| `query.Sql(sql).ToDictionary<User, TKey, TValue>(...)` | `query.Sql(sql).ToList<User>().ToDictionary(...)` |
| `WhereIf(predicate, condition)` | `WhereIf(condition, predicate)` |

上述删除和重命名属于主版本 Breaking Change，不提供 `[Obsolete]` 转发层。需要 Dapper 多映射时使用隐藏的 Advanced 泛型路径，不把固定结果类型入口作为普通查询代码的主路径。

## 测试和环境

SQLite 集成测试不需要外部服务，会使用临时数据库验证 SQL、参数、物化、分页、流式和取消。其它 Provider 的真实集成测试必须通过项目既有环境变量 Gate，并使用专用测试数据库；连接字符串不得硬编码或提交到仓库。
