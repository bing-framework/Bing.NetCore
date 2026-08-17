# SQL Mutation 批量执行

## API

`ISqlExecutor` 提供同步与异步批量 CRUD：`InsertBatch`、`UpdateBatch` 与 `DeleteBatch`。空集合直接返回 `0`，不会创建事务或执行命令；逻辑删除实体的 Delete 批量操作按实体更新删除状态。

`ISqlExecutor.CreateWriteBuilder()` 创建独立的统一 Builder。`ToSqlWriteCommand()` 只接受已完成的 `InsertValues`、`InsertSelect`、`Update` 或 `Delete` 状态，并冻结 Provider、SQL 与参数；`ExecuteWrite` 与 `ExecuteWriteAsync` 执行该独立命令。`None`、`Select` 和未完成 Insert 状态在命令创建前被拒绝，Builder 的增强参数元数据通过参数绑定器完整保留。

## 策略

- Insert `Auto`：Provider 支持标准多行 Values 时使用组合命令，否则逐实体执行。
- Insert `MultiRowValues`：要求 Provider 明确支持标准多行 Values。
- Update `Auto`：存在匹配的 Provider Renderer 时使用优化命令，否则逐实体执行。
- Update `ProviderOptimized`：要求 Provider 注册优化 Renderer；当前 PostgreSQL 使用 `UPDATE ... FROM (VALUES ...)`。
- Delete `Auto`：单主键且无并发列时使用 `IN`，其他情况使用按实体配对的复合条件。
- Delete `InPredicate`：只允许无并发列的单主键实体。
- Delete `CompositePredicate`：为每个实体保留主键与并发令牌的配对关系。
- `PerEntity`：每个实体生成一条独立参数化命令。

Insert/Delete 不公开没有实现闭环的 `ProviderOptimized` 占位策略。

## 分片

组合 Insert、Combined Delete 和 Provider 优化 Update 按 `BatchSize`、Provider 参数上限和最终 SQL 长度分片。配置 `MaxSqlLength` 时，执行器通过实际渲染和二分搜索选择可容纳的最大实体数。

PerEntity 不会把多条独立 SQL 的参数数或长度错误累计为一条数据库命令。它先逐命令验证 Provider 参数上限与 `MaxSqlLength`，再仅按 `BatchSize` 进行执行分组。

## 影响行数

单实体并发 Mutation 在 `Throw` 模式下要求实际影响 `1` 行。Combined Delete 和 Provider 优化 Update 在批次层要求实际影响行数等于该批实体数，不能对组合 SQL 套用单实体一行规则。`ReturnAffectedRows` 返回数据库实际影响行数。

## 事务与异步

默认 `UseTransaction=true`。执行器通过 `ISqlTransactionScopeFactory` 在同一连接和事务中顺序执行所有命令，成功后提交，失败后回滚。异步回滚使用 `CancellationToken.None`，避免调用令牌取消后跳过回滚。`UseTransaction=false` 时不提供跨命令原子性。
