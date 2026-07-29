# SQL Mutation Clause 设计

## 范围

Mutation SQL 使用独立的 `SqlMutationContext`，不复用查询 Builder 的可变状态。上下文拥有 `ISqlProvider`、方言、`IParameterManager`、`SqlBuilderServices` 和执行上下文；Provider 与 Services 可共享，Clause 与参数管理器属于每个 Builder 实例。

## Clause 顺序

- Insert：`IInsertClause` -> `IInsertColumnsClause` -> `IValuesClause`
- Update：`IUpdateClause` -> `ISetClause` -> `IMutationWhereClause`
- Delete：`IDeleteClause` -> `IMutationWhereClause`

每个 Clause 实现 `ISqlContent.AppendTo(StringBuilder)`，并支持 `Clear()`、Clone 和 `ISqlValidatable.Validate(SqlValidationContext)`。Builder 在输出前统一验证；Update/Delete 未显式调用 `AllowAllRows()` 时必须存在 Where 条件。

## Fluent 与生命周期

`InsertInto/Columns/Values`、`Update/Set/Where`、`DeleteFrom/Where` 扩展方法通过操作 Marker 和 Accessor 泛型约束绑定到对应 Builder。`New()` 共享 Provider 和 Services，但创建空 Clause 与参数管理器；`Clone()` 复制 Clause 和参数；`Clear()` 清除 Clause 与参数，后续参数从 `_p_0` 重新分配。

## Provider 扩展

默认使用 `DefaultSqlMutationClauseFactory`。Provider 如需替换局部 Clause，可实现 `ISqlMutationClauseFactoryProvider` 并只提供差异工厂；专用 Builder 的 `New()` 与 `Clone()` 会保留该 Factory。Provider 不应重写完整 Mutation Builder。

## 兼容入口

原实体 Insert/Update/Delete 仍通过 `DefaultSqlMutationBuilder` 解析元数据，但 SQL 由专用 Clause Builder 生成。实体 Where 条件使用标准 `ICondition` 组合，以保留已有 Provider 的完整 SQL 空格与参数输出契约。