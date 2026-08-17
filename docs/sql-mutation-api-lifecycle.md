# SQL Mutation API 生命周期

## 调用链

实体 API 调用 `ISqlEntityMutationCommandBuilderFactory`，由 `DefaultSqlEntityMutationCommandBuilder` 读取实体映射并配置 `SqlInsertBuilder`、`SqlUpdateBuilder` 或 `SqlDeleteBuilder`。专用 Builder 按 Clause 顺序输出 SQL 和 `SqlParam` 快照；执行器将快照交给 `DefaultSqlParameterBinder` 和已有诊断链。

Fluent API 直接使用 Provider 的统一 `ISqlBuilder`。完成 Mutation 后通过 `ToSqlWriteCommand()` 冻结 SQL、参数、Provider 和 Returning 语义，再由 `ISqlExecutor.ExecuteWrite(SqlWriteCommand)` 或异步重载进入相同参数绑定与执行链。

## 验证边界

输入边界在 Fluent 写入时验证非法 Operation 转换、空列、Values 行列数和参数。渲染边界在 `Validate()` 中验证缺少表、列、Set、Values、Where 和 Provider 能力。批量边界分别验证组合命令和 PerEntity 单命令的参数数、SQL 长度及影响行数。

## 演进状态

统一 Query/CRUD Builder、Insert Select、组合 Insert/Delete、Mutation Plan/Getter 缓存、Provider 参数上限、PostgreSQL 优化批量 Update、结构化 UpdateFrom、结构化 DeleteUsing，以及统一 Builder 的 PostgreSQL/SQLite Returning 和 SQL Server Output 已可用。新增能力必须先具备 Provider 消费链、直接测试和文档，不预留无实现的公开占位成员。

UpdateFrom 当前只由 PostgreSQL 声明支持。统一 Builder 与专用 `SqlUpdateBuilder` 共用 `UpdateFromClause`，Clone 保留独立来源状态，New/Clear 移除来源状态；未配置 Update 目标表时调用 UpdateFrom 会由 Operation 状态机立即拒绝。

DeleteUsing 当前只由 PostgreSQL 声明支持。统一 Builder 与专用 `SqlDeleteBuilder` 共用 `DeleteUsingClause`，Clone 保留独立来源状态，New/Clear 移除来源状态；统一 Builder 未配置 Delete 目标表时调用 DeleteUsing 会由 Operation 状态机立即拒绝。目标表 Alias 只在声明该能力的 Provider 路径渲染，其他 Provider 的既有 Delete 方言保持不变。

Returning 结构化投影当前由 PostgreSQL、SQL Server 和 SQLite 统一 Builder 声明支持，覆盖 Insert Values、Insert Select、Update 和 Delete。PostgreSQL 与 SQLite 输出尾部 `RETURNING`；SQL Server 通过可选方言 SPI 输出位于来源/筛选前的 `OUTPUT INSERTED/DELETED`。SQLite 本地执行要求运行时版本不低于 3.35，并由集成测试校验。Clone 保留独立投影，New/Clear 移除投影；未进入 Mutation 状态时调用 Returning 会立即拒绝。普通 `Execute`/`ExecuteAsync` 拒绝带返回结果的命令，避免静默丢弃结果集；查询结果 API 只允许配置了 Returning 的 Mutation。专用 Mutation Builder、实体 CRUD、批量 Mutation 和 SQL Server `OUTPUT INTO` 不在当前合同内。
