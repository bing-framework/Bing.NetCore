# SQL Mutation API 生命周期

## 调用链

实体 API 调用 `ISqlMutationBuilderFactory`，由 `DefaultSqlMutationBuilder` 读取实体映射并配置 `SqlInsertBuilder`、`SqlUpdateBuilder` 或 `SqlDeleteBuilder`。专用 Builder 按 Clause 顺序输出 SQL 和 `SqlParam` 快照；执行器将快照交给已有 Dapper 参数绑定与诊断链。

## 验证边界

输入边界在 Fluent 写入时验证空列、Values 行列数和参数。渲染边界在 `Validate()` 中验证缺少表、列、Set、Values、Where 和 Provider 能力。批量边界在 Planner 中验证负数、零容量及无法容纳单实体的限制。

## 演进状态

当前公开专用 Builder、Clause Factory、Fluent、生命周期和批量串行执行已可用。合并式 Insert/Delete、Mutation Plan/Getter 缓存、Provider 自动参数上限、Returning/Output、UpdateFrom/DeleteUsing 仍是后续增量；这些能力必须通过 Provider 局部 Clause 扩展实现，不应在执行器中拼接方言 SQL。