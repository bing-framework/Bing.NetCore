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
