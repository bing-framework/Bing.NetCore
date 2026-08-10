# 结构化表引用

## 目标

SQL 实体映射使用 `Database`、`Schema`、`TableName` 和可选 `Alias` 描述最终对象名称。`SqlTableReference` 由当前 Provider 的 `ISqlObjectNameFormatter` 在最终生成阶段输出。

## 映射配置

```csharp
options.Database = "reporting";
options.Schema = "dbo";
options.TableName = "orders";
```

对象名称不包含连接路由或跨连接链接。SQLite `ATTACH` 的别名不进入实体映射；当应用在受控连接生命周期中显式执行 `ATTACH` 后，可通过字符串 `From("attachedAlias.table")` 或 `Join("attachedAlias.table")` 引用附加库表。

## 兼容性

实体类型重载使用结构化解析路径，并只渲染最终映射结果。字符串 `From(string)` 和 `Join(string)` 仅接受受控对象名：标识符及一个可选别名，不允许分号、控制字符、空名称段或超出 Provider 上限的限定段。原始 SQL 只能通过 `AppendFrom`、`AppendJoin` 及其左右 Join 变体追加。

## 原始 Append 调用约束

`AppendFrom` 用于设置或追加完整原始 From 表达式。第一次调用会替换已有结构化 From，后续调用直接拼接传入内容，不自动增加空格、逗号或其他分隔符。调用方必须负责原始 SQL 的安全性，并使用 `AddParam` 显式绑定所有参数。

```csharp
// 正确
query.AppendFrom("Orders o").AppendFrom(", Customers c");

// 错误：不会由框架猜测插入分隔符
query.AppendFrom("Orders o").AppendFrom("Customers c");
```

`AppendJoin`、`AppendLeftJoin` 和 `AppendRightJoin` 同样保留原始表表达式，不解析别名。可以通过 `AppendOn` 将条件追加到最后一个 Join；空白条件无操作，没有 Join 时非空条件抛出 `InvalidOperationException`。`AppendFrom`/原始 Join 保持调用方文本，其他 `AppendSelect`、`AppendWhere`、`AppendGroupBy`、`AppendOrderBy` 和 `AppendOn` 会对方括号标识符执行 Provider 方言替换。

## MySQL 跨数据库集成测试

跨数据库原始表引用仅在显式测试环境中验证。测试要求同时设置 `RUN_MYSQL_INTEGRATION_TESTS=true`、`BING_INTEGRATION_MYSQL_CROSS_DATABASE=true`、`BING_INTEGRATION_MYSQL_CROSS_DATABASE_NAME` 和 `ALLOW_DATABASE_RESET_FOR_TESTS=true`。第二数据库名称必须符合专用测试库安全约定。

启用后，测试在第二专用库中创建、插入并清理 ``Merchants.Company``，再从普通 MySQL 测试连接执行：

```csharp
query.AppendFrom("`archive_db_test`.`Merchants.Company` As `c`");
```

未启用时该用例明确跳过，不影响常规 MySQL 集成测试。测试不会操作非专用数据库。
