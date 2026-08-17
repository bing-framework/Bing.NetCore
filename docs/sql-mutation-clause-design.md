# SQL Mutation Clause 设计

## 统一 Builder

`ISqlBuilder` 是 Query、Insert、Update、Delete 的统一 Fluent Builder。`SqlBuilderBase` 持有查询 Clause、Mutation Clause、唯一 `IParameterManager`、唯一执行上下文和 `SqlOperationKind` 状态；五个官方 Provider Builder 复用该实现。

专用 `SqlInsertBuilder`、`SqlUpdateBuilder`、`SqlDeleteBuilder` 继续服务实体映射和批处理命令生成。两条路径共享 Clause Factory、Provider 服务和参数模型。

## 状态

公开状态为 `None`、`Select`、`InsertValues`、`InsertSelect`、`Update`、`Delete`。`InsertInto` 后内部处于待定状态，调用 `Values` 或查询投影后确定 Insert 类型。非法组合在 Clause 写入时立即失败；`Clone` 保留状态并隔离参数，`New` 与 `Clear` 回到 `None`。

## Clause 顺序

- Insert Values：`InsertClause` -> `InsertColumnsClause` -> `ValuesClause` -> 可选 `ReturningClause`
- Insert Select：`InsertClause` -> `InsertColumnsClause` -> `SelectClause` -> `FromClause` -> `JoinClause` -> 查询 `WhereClause` -> `GroupByClause` -> `OrderByClause` -> 可选 `ReturningClause`
- Update：`UpdateClause` -> `SetClause` -> 可选 `UpdateFromClause` -> `MutationWhereClause` -> 可选 `ReturningClause`
- Delete：`DeleteClause` -> 可选 `DeleteUsingClause` -> `MutationWhereClause` -> 可选 `ReturningClause`

Insert Select 使用查询 Where，从而保留别名、Join、子查询和过滤器语义。Update/Delete 使用独立 Mutation Where，并在没有条件且未调用 `AllowAllRows()` 时拒绝渲染。

PostgreSQL UpdateFrom 使用结构化 `SqlTableReference` 来源表。目标表和来源表通过 Alias 引用；`SetFrom(targetColumn, sourceColumn)` 与 `WhereFrom(targetColumn, sourceColumn)` 只接受单段结构化列标识符，并由当前方言引用。当前合同不接受 Raw Set、Raw From、多来源或 Join。

PostgreSQL DeleteUsing 同样使用单个结构化 `SqlTableReference` 来源表。`WhereUsing(targetColumn, sourceColumn)` 要求目标表和来源表均具有 Alias，且只接受单段结构化列标识符。Using 本身不构成 Delete 条件；未配置 Where 且未显式调用 `AllowAllRows()` 时仍拒绝渲染。当前合同不接受 Raw Using、多来源、Join 或子查询。

PostgreSQL 和 SQLite Returning 是四种 Mutation 的结构化尾部投影。字符串重载只接受单段列标识符；实体表达式重载输出物理列名，并以 CLR 属性名作为结果 Alias。PostgreSQL UpdateFrom/DeleteUsing 配置目标 Alias 时，返回列自动限定为目标表。SQLite Returning 要求数据库运行时不低于 3.35；项目绑定版本由本地集成测试直接校验。当前合同不接受 `*`、Raw、表达式、聚合或子查询。

SQL Server Output 复用同一 `Returning(...)` 结构化投影入口，但由 `ISqlReturningDialect` 将 Clause 放在数据来源或筛选子句之前，并按操作输出 `INSERTED` 或 `DELETED` 限定符。Insert Values/Insert Select 位于 Values/Select 之前，Update/Delete 位于 Where 之前。调用方不直接配置伪表限定符。

## 投影与参数

Insert Select 仅在目标列数和查询投影数都可证明时校验一致性。结构化列和聚合具有确定数量；`*` 与无法可靠分析的 Raw 投影将数量标记为未知并交由数据库验证。

查询和 Mutation Clause 共享同一个参数管理器。自动参数通过 `GenerateName()` 跳过已占用名称，子查询合并发生冲突时稳定重命名并保留 `SqlParam` 元数据。

## Provider 扩展

默认使用 `DefaultSqlMutationClauseFactory`。Provider 如需替换局部 Clause，可实现 `ISqlMutationClauseFactoryProvider`；批量 Update 差异通过 `ISqlBatchUpdateRenderer` 扩展。Provider 不应复制完整 Mutation Builder。

`ISqlUpdateFromClauseFactory`、`ISqlDeleteUsingClauseFactory` 与 `ISqlReturningClauseFactory` 是可选扩展，不向既有 `ISqlMutationClauseFactory` 强加新成员。Provider 必须分别通过 `SqlProviderProfile.Mutation.SupportsUpdateFrom`、`SupportsDeleteUsing` 和 `SupportsReturning` 明确声明支持；未声明的 Provider 在渲染前抛出 `NotSupportedException`。

支持非尾部返回结果语法的 Provider 实现可选 `ISqlReturningDialect`，声明 `End` 或 `BeforeSource` 位置并解析关键字和列限定符。未知位置或空关键字在渲染时明确失败，不能静默省略结果子句。
