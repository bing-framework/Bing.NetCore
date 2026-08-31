# SQL 公共 API 治理

## Lambda dev_v6 公共合同

`SqlLambdaQuery` 是当前唯一的非泛型 Lambda 查询描述。根 `ISqlQuery` 仅通过 `From<TEntity>(alias = null, schema = null)` 进入该路径；连续调用会追加根来源。已批准的主版本 Breaking Change 删除 `SqlLambdaQuery<TEntity>`、公开 `SqlMultiLambdaQuery` 和 Legacy 转发路径。新代码不再扩散高元数描述类型、`SqlSubqueryLambdaQuery<TProjection>` 或高元数 `ISqlQuery.From`。

Lambda 查询的结果类型必须由终结方法显式指定：`ToEntity<TResult>`、`ToList<TResult>`、`First<TResult>`、`FirstOrDefault<TResult>`、`Single<TResult>`、`Scalar<TResult>`、`ToPage<TResult>`、`AsEnumerable<TResult>` 及其异步/取消对称入口；非泛型 Raw 查询提供相同的列表、基数、标量和流式终结。高层查询不再提供与 `ToEntity` 重复的 `SingleOrDefault`，也不提供高层 `ToDictionary`；字典结果由 `ToList<TResult>()` 后的 LINQ 转换完成。查询不提供 `.As<TResult>()`、`SelectAs<TResult>()`、`SelectDto<TResult>()` 或后置 `.On(...)`。

非泛型 `Query()`、`Sql(...)`、`SqlInterpolated(...)` 和 `Procedure(...)` 创建的描述在终结方法处选择结果类型；2～7 对象多映射属于隐藏的 Advanced 能力，不作为普通 IntelliSense 主路径。起始阶段固定结果类型的泛型 Query/Raw/Procedure 入口不再作为公共主路径保留。

## Runtime 边界与发布基线

`SqlQueryRuntimeFactory`、`SqlParameterRuntimeBridge` 和 `SqlMutationRuntimeBridge` 仅由官方执行链使用并保持内部化；`SqlBuilderRuntimeBridge` 只保留官方跨程序集所需的窄静态入口，`SqlBuilderExecutionSnapshot` 只公开 SQL 与不可变参数快照，绝不公开 Builder、连接、事务或诊断状态。`SqlQueryPlan` 的 Builder 仅为程序集内部状态，跨程序集执行通过计划、快照和 Count/Data 派生入口完成。`ISqlQueryPlanExecutor`、`ISqlQueryBuilderSource`、`ISqlQueryRuntimeBindingController` 和 `SqlQueryPlan` 是真实跨程序集执行/绑定 SPI，保留最小公开契约。不得为恢复旧调用方式新增兼容转发、过大的 Runtime 接口或关闭 Analyzer。

本次主版本 Breaking Change 已批准将 Lambda 查询收敛为连续 From、二元方法级泛型与原子 Join，并删除旧一元 Lambda、Raw 泛型入口及其 Legacy 转发路径。Raw 主入口为非泛型描述并由终结方法选择结果；新增公开成员进入 `PublicAPI.Unshipped.txt`。已删除符号必须同步从实现、消费者和 Public API 基线移除，不保留兼容包装。

### Shipped/Unshipped 发布审计矩阵

版本依据：仓库 `version.props` 当前为 `7.0.0`；该版本值只作为当前 Shipped 基线的仓库证据，不推断外部包下载或外部消费者实际使用情况。

| 符号/范围 | 基线 | 已发布包版本 | 仓库消费者 | 外部消费者 | 处理批准 |
| --- | --- | --- | --- | --- | --- |
| `Bing.Data.Sql.ISqlQuery.From<TEntity>(string alias = null, string schema = null)` | 当前主路径 | 本次主版本 Breaking Change | Dapper Core、Provider、API Contract | 未知 | 唯一公开 Lambda 根入口；连续调用追加来源 |
| `Bing.Data.Sql.SqlLambdaQuery<TEntity>` | 已删除 | 本次主版本 Breaking Change | 仓库消费者已迁移 | 未知 | 不保留 `[Obsolete]` 或转发包装 |
| `Bing.Data.Sql.SqlMultiLambdaQuery` | 已删除 | 本次主版本 Breaking Change | 公开类型已移除，内部 Core 保留 | 未知 | 文件可保留内部 `SqlLambdaQueryCore`，程序集不得导出旧类型 |
| `Bing.Data.Sql.SqlQueryBase.Query<TResult>` / `Sql<TResult>` / `Procedure<TResult>` | Advanced/内部 | 普通入口已收敛 | Dapper Core Advanced 组合和执行链 | 未知 | 普通 `Query()`/`Sql()`/`Procedure()` 由终结方法选择结果类型 |
| `Bing.Data.Sql.SqlLambdaQuery` 非泛型描述、连续 `From`、方法级泛型 | 当前主路径 | 本次 Unshipped API | Data.Sql、SQLite、SQL Server、Analyzer Contract | 未知 | 新增成员进入 Unshipped；显式 alias 直接测试 |
| Raw 非泛型描述终结方法及 `ToPage/ToPageAsync` | 当前主路径 | 本次 Unshipped API | SQLite Integration、API Contract、Dapper Runtime Bridge | 未知 | SQLite 真实执行验收 |
| 高元数 Lambda 类型、3+ 参数表达式和旧生成器 | 已删除/禁止 | 本次主版本 Breaking Change | Roslyn 负向契约 | 未知 | 不重新引入高元数公共类型或表达式入口 |

验收证据：两个 `PublicAPI.Shipped.txt` 与 HEAD 逐字一致；Data.Sql 默认 Analyzer 无 `RS0016/RS0017/RS0018`；旧/新入口分别由 `SqlQueryApiContractTest`、Dapper Core 编译和 SQLite 真实执行覆盖。外部包清单不可由仓库源码推断，故不能写成“外部消费者不存在”。

直接证据：`Bing.Data.Sql.Tests.SqlQueryApiContractTest` 覆盖来源元数、Lambda/Raw 终结、Runtime 绑定和无第 11 来源契约；`Bing.Data.Sql.Analyzers.Tests` 覆盖消费者编译契约；`Bing.Dapper.Core.Tests` 覆盖官方执行链；`Bing.Dapper.Sqlite.Tests` 与其 Integration 项目覆盖 SQL/参数快照和真实执行。

### Builder 内部协作边界

`Bing.Data.Sql.Builders.Internal.Helper` 聚合 Clause 运行上下文、方言、实体解析、参数和元数据协作，仅由 Bing.Data.Sql 程序集内部的 `WhereClause`、`JoinClause`、`PredicateExpressionResolver` 及其 Clause 组合路径使用。它不是 Provider SPI，也不是第三方扩展入口。`JoinItem.SetDependency(Helper)` 和 `JoinItem.Clone(Helper)` 同样只服务内部 Join 克隆与条件构造，因此在 7.0.0 主版本中一并内部化。

该收敛是已批准的 Breaking Change：不得为旧 `Helper` 新增 facade、`InternalsVisibleTo` 或转发包装。第三方必须通过 `ISqlBuilder`、公开 Fluent API 和已声明的 Provider SPI 扩展；`SqlOperationCompileContractTest.BuilderInternals_WhenUsedByThirdPartyConsumer_ShouldNotCompile` 负责验证该边界，`JoinItemTest.Test_1` 负责验证内部 clone/render 行为未变。

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
