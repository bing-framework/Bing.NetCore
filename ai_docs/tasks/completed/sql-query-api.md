# SQL 查询 API 演进摘要

<a id="sql-lambda-query-api-v4"></a>

本页合并历史任务 `sql-lambda-query-api-v4` 的计划、执行记录与独立 Review，记录其可复用实现和后续 API 演进边界。V4 的高元数公开 API 已由 V6 方案替代，不代表当前仍有这些类型或入口。

## 当前契约

- `ISqlQuery.From<TEntity>(alias, schema)` 返回非泛型 `SqlLambdaQuery`；连续 `From<TEntity>()` 追加来源，Lambda 操作使用一元或二元表达式。
- 多表查询可用连续的二元 `Join<TLeft, TRight>` 组合。V6 删除了 `SqlMultiLambdaQuery`、高元数 arity 类型和对应生成器；不得将 V4 的 `From<T1,...,T10>` 或三元以上 Lambda 描述为现行 API。
- 1～10 表仍有逐次二元 Join 的 SQLite 集成行为覆盖，这只说明来源链执行覆盖，不等于存在 1～10 元公开 Lambda 签名。

关键当前实现：

- `framework/src/Bing.Data.Sql/Bing/Data/Sql/ISqlQuery.cs`：结构化查询入口。
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery.cs`：非泛型 Lambda 描述；`From<TEntity>` 追加实体来源。
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/FromClause.cs`：结构化根来源追加与来源登记。
- `framework/tests/Bing.Data.Sql.Tests/SqlQueryApiContractTest.cs`：`From_WhenPublicApiInspected_ShouldExposeTypedAndSubqueryEntryPointsOnly`、`LambdaQuery_WhenPublicTypesInspected_ShouldExposeOnlyNonGenericDescription`、`LambdaQuery_WhenPublicApiInspected_ShouldUseMethodLevelUnaryOrBinaryExpressions` 锁定当前公开边界。
- `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/SqlQuery/SqliteExecutionIntegrationTest.cs`：`Lambda_WhenTenSourcesAreJoinedSequentially_ShouldMaterializeChainWithoutCartesianRows` 验证逐次二元 Join 的十表链路。

## 可复用实现

V4 将结构化来源图、Provider 能力检查与 Join 提交收敛到候选状态：先解析候选谓词、参数及别名，成功后一次替换共享状态；失败时恢复参数、别名、Operation 与 Join 列表，避免留下部分来源或参数。

关键实现与职责测试：

- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Clauses/JoinClause.cs`：类型化实体 Join 与类型化派生表 Join 的候选验证、提交和失败回滚。
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Core/SqlBuilderBase.cs`：`ReplaceParameterManager()` 同步替换 Builder 与共享 Clause 上下文使用的参数状态。
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Core/SqlClauseContext.cs`：`ParameterManagerState` 为延迟创建的 Clause 提供共享参数状态引用。
- `framework/tests/Bing.Data.Sql.Tests/Builders/SqlBuilderTest.Join.cs`：`TypedJoin_WhenCustomParameterProbeFails_ShouldKeepStateUnchangedAndAllowRetry`、`TypedJoin_WhenCustomAdvancedParameterProbeFails_ShouldKeepMetadataStateUnchangedAndAllowRetry`、`TypedJoin_WhenCustomParameterCommitFails_ShouldRestoreStateAndAllowRetry`、`TypedSubqueryJoin_WhenCustomParameterRenderFails_ShouldKeepStateUnchangedAndAllowRetry`、`TypedSubqueryJoin_WhenCustomAdvancedParameterRenderFails_ShouldKeepMetadataStateUnchangedAndAllowRetry`。

FIX-011 覆盖实体 Join 使用普通及增强第三方参数管理器时的失败原子性；FIX-012 将派生表渲染、参数重命名和 On 谓词解析也放入候选参数状态，失败后可从原编号重试。两者均有直接故障注入测试，未依赖对第三方管理器执行 `Clear/Add` 补偿。

## V4 验收记录

<a id="sql-lambda-query-api-v4-review"></a>

`sql-lambda-query-api-v4` 独立 Review Round 7 结论为 `PASS`，FIX-011 与 FIX-012 均标记 `RESOLVED`，Phase 0～10 按 V4 原计划通过。该 PASS 是当时本地计划范围的验收结论，不等于当前 API 仍保留 V4 高元数签名，也不代表外部数据库 Gate 已通过。

V4 最终记录的本地验证结果：

- SQL Core `2394/2394`；Custom Provider `38/38`；SQLite Unit `218/218`、SQLite Integration `266/266`。
- Analyzer `19/19`；Dapper Core `262/262`；MySQL `354/354`、PostgreSQL `268/268`、SQL Server `550/550`、Oracle `180/180` Provider Unit。
- FIX-012 派生表相关专项双 TFM `14/14`；Release 全解构建记录为 `0 errors / 133 warnings`。
- MySQL、PostgreSQL、SQL Server、Oracle 外部数据库 Integration 未运行，状态为 `NOT_RUN_EXTERNAL_GATE_MISSING`；不得据 Provider Unit 结果推断外部数据库执行通过。

## 历史性能与限制

V4 曾以 BenchmarkDotNet `0.14.0` 在同一 Windows/.NET 8 主机上运行 Root 与 Join `1/2/5/10` 来源 FormalHost 场景，记录 Mean、Median、分配量、GC 与 Ratio。该结果是当时的单机基线，不是跨环境性能保证；任务没有据单次数据声称完成生产热路径优化。V6 改变了公开 API 和来源组合方式，因此这些数字不能直接当作 V6 性能结论。

外部 Provider 集成、跨主机性能比较及 V6 API 的独立性能结论：待实现/待确认。
