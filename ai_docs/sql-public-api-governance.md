# SQL 公共 API 治理

## Lambda V4 公共合同

`SqlLambdaQuery<TSource...>` 的泛型参数只表示来源图，不表示结果类型。`SqlMultiLambdaQuery` 是非泛型组合核心；`Select<TProjection>` 只描述投影形状，不切换查询描述的来源元数，也不恢复默认结果类型。

Lambda 查询的结果类型必须由终结方法显式指定：`ToList<TResult>`、`First<TResult>`、`FirstOrDefault<TResult>`、`Single<TResult>`、`SingleOrDefault<TResult>`、`Scalar<TResult>`、`ToPage<TResult>`、`AsEnumerable<TResult>` 及其异步/取消对称入口。Lambda 查询不提供无泛型终结方法、`.As<TResult>()`、`SelectAs<TResult>()`、`SelectDto<TResult>()`、`ToEntity<TResult>()` 或后置 `.On(...)`。

`Query<TResult>()`、`Sql<TResult>()` 和 `SqlInterpolated<TResult>()` 在 Raw 查询创建时固定最终结果类型，Raw 查询不再提供用于重新选择结果类型的 `<TNextResult>` 单结果、分页或流式终结重载。Raw Fluent 的 2～7 对象多映射是独立能力，仍由创建时确定的 `TResult` 承载。

## Runtime 边界与发布基线

`SqlQueryRuntimeFactory`、`SqlBuilderRuntimeBridge`、`SqlParameterRuntimeBridge`、`SqlMutationRuntimeBridge` 和 `SqlBuilderExecutionSnapshot` 仅由官方 Dapper 执行链使用，已内部化；`ISqlQueryPlanExecutor`、`ISqlQueryBuilderSource`、`ISqlQueryRuntimeBindingController` 和 `SqlQueryPlan` 是真实跨程序集执行/绑定 SPI，保留最小公开契约。不得为恢复旧调用方式新增兼容转发、过大的 Runtime 接口或关闭 Analyzer。

本次 V4 是未发布 API 的有意 Breaking Change：Lambda 旧的无泛型终结迁移为 `.ToList<TEntity>()`、`.First<Dto>()`、`.ToPage<Dto>()` 等显式目标；Raw 旧的 `Query<TResult>().ToList<TNextResult>()` 迁移为入口指定最终类型后调用非泛型终结。`PublicAPI.Shipped.txt` 不回填未发布成员；新公开成员进入 `PublicAPI.Unshipped.txt`，删除成员同步由 API Contract、Analyzer 和消费者编译契约验证。

直接证据：`Bing.Data.Sql.Tests.SqlQueryApiContractTest` 覆盖来源元数、Lambda/Raw 终结、Runtime 绑定和无第 11 来源契约；`Bing.Data.Sql.Analyzers.Tests` 覆盖消费者编译契约；`Bing.Dapper.Core.Tests` 覆盖官方执行链；`Bing.Dapper.Sqlite.Tests` 与其 Integration 项目覆盖 SQL/参数快照和真实执行。

## 公开 SPI

Provider SPI 按职责拆分为独立文件：`ISqlProvider`、`ISqlClauseFactory`、`ISqlTableReferenceParser`、`ISqlPaginationRenderer`、`IParameterManagerFactory`、`ISqlParameterLimitProvider` 和 `ISqlBuilderFactory`。Mutation 的有效扩展点为 `ISqlMutationClauseFactoryProvider`、可选 `ISqlUpdateFromClauseFactory`、可选 `ISqlDeleteUsingClauseFactory`、可选 `ISqlReturningClauseFactory`、可选 `ISqlReturningDialect`、`ISqlBatchUpdateRenderer` 及按操作拆分的参数绑定接口。拆分不改变命名空间或程序集可见性。

`ISqlBuilder` 是 Query 与 CRUD 的统一入口；Marker 接口和 `ISqlOperation` 保留用于 Fluent 约束。公开 `SqlOperationKind` 描述可执行状态，调用方不应依据内部待定 Insert 状态编写分支。

新增公开 API 必须具有独立职责、稳定异常语义和直接单元测试。涉及 SQL 文本的测试必须断言完整 SQL，不以 `Contains` 替代。

## 已移除兼容层

`LegacySqlProvider` 与静态 `SqlClauseContext.Create(...)` 已删除；Clause 运行上下文必须携带实际 `ISqlProvider` 和 `SqlBuilderServices`。

字符串聚合兼容成员 `SqlItem.AggregationFunc`、`ColumnItem.AggregationFunc` 和 `ColumnItem.IsAggregation` 已删除。聚合只使用 `SqlAggregateFunction` 与结构化描述符。

Mutation 占位 API 已删除：Insert/Delete 枚举不再暴露未实现的 `ProviderOptimized`，`ISqlMutationBatchPlanner` 不再伪装为可替换 SPI。已有有效枚举整数保持不变，避免配置和序列化值漂移。

`SqlProviderProfile.Mutation.SupportsUpdateFrom` 具有真实消费链：`UpdateFromClause.Validate` 在 SQL 输出前检查能力，PostgreSQL 明确启用，其他 Provider 默认关闭。该能力具有核心、支持 Provider、不支持 Provider和受控真实执行测试，不属于占位标志。

`SqlProviderProfile.Mutation.SupportsDeleteUsing` 同样由 `DeleteUsingClause.Validate` 在 SQL 输出前消费，当前仅 PostgreSQL 启用。DeleteUsing 具有核心、支持 Provider、不支持 Provider、Roslyn 消费者和受控真实执行测试，不属于占位标志。

`SqlProviderProfile.Mutation.SupportsReturning` 由 `ReturningClause.Validate` 在 SQL 输出前消费，当前由 PostgreSQL、SQL Server 和 SQLite 启用。PostgreSQL 与 SQLite 使用默认尾部 Returning；SQL Server 通过 `ISqlReturningDialect` 调整为 `OUTPUT` 的语句位置和 `INSERTED`/`DELETED` 限定符。三者复用结构化列、实体映射、Clone/Clear 和查询物化边界；SQLite 还具有四种 Mutation 的本地真实执行与 3.35+ 运行时校验。该能力具有核心、三种支持 Provider、未支持 Provider、Roslyn 消费者及受控执行测试，不属于占位标志。

## 变更门槛

修改 Provider、Factory、参数限制、标识符解析、映射或 SQL 格式化时，必须更新 `ai_docs/sql-metadata-test-traceability.md` 中“生产符号到测试方法”的映射，并运行对应 Provider 单元测试。涉及外部数据库执行路径时，SQLite 集成测试必须通过；外部 Provider 集成测试仅在安全环境变量、专用测试库和显式重置授权齐备时执行。

`Bing.Dapper.MySql`、`Bing.Dapper.PostgreSql`、`Bing.Dapper.SqlServer`、`Bing.Dapper.Sqlite` 和 `Bing.Dapper.Oracle` 均以 `Microsoft.CodeAnalysis.PublicApiAnalyzers` 的 `PublicAPI.Shipped.txt` 冻结既有公开与受保护 API。`RS0016`、`RS0017` 和 `RS0018` 是项目级构建错误：已发布 API 的兼容变更必须同步维护 Shipped 基线；待发布 API 只能先进入 Unshipped 基线，发布时再迁移。不得通过关闭规则或空基线规避门禁。
