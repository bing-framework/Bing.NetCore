# SQL Mutation 批量执行

## API

`ISqlInsertExecutor`、`ISqlUpdateExecutor` 和 `ISqlDeleteExecutor` 提供 `InsertBatch`、`UpdateBatch`、`DeleteBatch` 及对应异步方法。空集合直接返回 `0`，不会创建事务或执行命令。Update/Delete 直接返回数据库实际影响行数；并发不匹配不会转换为异常。

## 分片

`SqlMutationBatchPlanner` 根据用户 `BatchSize`、`MaxParameterCount`、`MaxSqlLength`、每实体参数数和预计 SQL 长度取最小批容量。无法容纳一个实体时抛出 `InvalidOperationException`。计划保持输入顺序，每批携带独立的 `SqlMutationCommand` 参数快照。

当前第一阶段使用 `PerEntity` 策略：每个实体生成一个参数化 Mutation 命令，批次仅定义命令分组和事务边界。`Auto` 选择该安全路径；显式 `Combined` 会抛出 `NotSupportedException`，直到 Provider 实现多行 Values、单主键 IN 或复合键条件合并 Clause。

## 事务与异步

默认 `UseTransaction=true`。执行器通过 `ISqlTransactionScopeFactory` 创建绑定相同数据库上下文的执行器，在同一个连接和事务中按顺序执行所有命令，成功后提交，失败后回滚。`UseTransaction=false` 时逐命令使用现有执行语义。异步路径按顺序 await，不会在同一个连接上并行操作。

## Provider 约束

调用方可在批量选项中提供已知 Provider 参数数或 SQL 长度上限。后续 Provider 能力接入应将 SQL Server 2100、SQLite 保守限制和 Oracle 不支持标准多行 Values 映射到这些规划输入；未声明限制的 Provider 不应臆造上限。