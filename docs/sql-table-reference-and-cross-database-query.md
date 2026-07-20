# 结构化表引用与跨数据库查询

## 目标

SQL 实体映射将数据库连接选择、物理对象位置和逻辑命名分离。`DbKey` 决定连接和事务上下文；`Catalog`、`PhysicalSchema`、`AttachedAlias` 与 `DatabaseLink` 决定对象限定；`LogicalSchema` 只参与命名策略；`TableName` 是原始实体表名，`ResolvedTableName` 是最终物理表名。

`FullTableName` 仅作为旧 API 的兼容信息，不是 SQL 渲染来源。新 SQL 由 `SqlTableReference` 和当前 Provider 的 `ISqlObjectNameFormatter` 在最终生成阶段输出。

## 映射配置

```csharp
options.Catalog = "reporting";
options.PhysicalSchema = "dbo";
options.LogicalSchema = "order";
options.LogicalTableNamingMode = LogicalTableNamingMode.Prefix;
```

旧 `Schema` 属性仍保留一个兼容周期。`SchemaCompatibilityMode.Auto` 在 MySQL/Doris 下按逻辑前缀解释，在 SQL Server、PostgreSQL 和 Oracle 下按物理架构解释。新配置应使用明确字段，避免 Provider 切换后改变语义。

## 跨数据库规则

同一执行上下文中的所有类型化表引用必须拥有同一 `DbKey`。不同 `DbKey` 表示不同的连接候选项，框架会拒绝将它们放入同一 Join，调用方应拆分查询或采用应用层组合。

同一 `DbKey` 允许不同 `Catalog`，但仍受 Provider 能力限制。MySQL/Doris 和 SQL Server 支持跨 Catalog；PostgreSQL 不接受 Catalog；Oracle 使用数据库链接；SQLite 使用已附加数据库的别名。格式化器逐段引用标识符并拒绝非法分隔符，不能用对象名称 API 传递任意 SQL 片段。

## SQLite ATTACH

SQLite 的附加数据库属于单个连接。应用必须在拥有该连接的范围内执行 `ATTACH DATABASE`，以受控常量设置别名，并在不再使用时 `DETACH DATABASE`。`AttachedAlias` 只在 SQLite 引用格式化中生效，不能将独立 DbKey 的连接、事务或连接生命周期合并。

## 兼容性

`From(string)`、`Join(string)` 和已有的 `SqlItem`、`NameItem`、`JoinItem` 保持原始文本行为，不会自动解析或改写。实体类型重载优先使用结构化解析路径；这使映射缓存、Provider 能力检查和跨 DbKey 拒绝在不改变旧字符串行为的前提下生效。
