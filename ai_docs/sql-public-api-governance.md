# SQL 公共 API 治理

## Lambda dev_v6 公共合同

`SqlLambdaQuery` 是新主路径的非泛型 Lambda 查询描述。根 `ISqlQuery` 通过 `From<TEntity>(alias, schema)` 进入该主路径；当 alias/schema 均为空时必须显式传入 `null, null`。已发布的 `From<TEntity>(string alias = null)` 会优先匹配零/一参数调用并返回 `SqlLambdaQuery<TEntity>`，该入口和 `SqlMultiLambdaQuery` 仅作为兼容路径保留。新代码不再扩散高元数描述类型、`SqlSubqueryLambdaQuery<TProjection>` 或高元数 `ISqlQuery.From`。

Lambda 查询的结果类型必须由终结方法显式指定：`ToEntity<TResult>`、`ToList<TResult>`、`First<TResult>`、`FirstOrDefault<TResult>`、`Single<TResult>`、`SingleOrDefault<TResult>`、`Scalar<TResult>`、`ToPage<TResult>`、`AsEnumerable<TResult>` 及其异步/取消对称入口；非泛型 Raw 查询提供 `ToEntity<TResult>`、`ToList<TResult>`、`ToDictionary<TResult,TKey,TValue>`、标量和流式终结。查询不提供 `.As<TResult>()`、`SelectAs<TResult>()`、`SelectDto<TResult>()` 或后置 `.On(...)`。

非泛型 `Sql(...)` 和 `SqlInterpolated(...)` 创建的 Raw 描述在单结果、列表、字典、标量和流式终结方法处选择结果类型；2～7 对象多映射属于低层兼容能力，继续由已发布的 `SqlTextQuery<TResult>`/`SqlFluentQuery<TResult>` 承载。已发布的 `Sql<TResult>()` 和 `SqlInterpolated<TResult>()` 继续作为兼容入口保留，但新代码不得继续扩散该固定结果类型路径。

## Runtime 边界与发布基线

`SqlQueryRuntimeFactory`、`SqlParameterRuntimeBridge` 和 `SqlMutationRuntimeBridge` 仅由官方执行链使用并保持内部化；`SqlBuilderRuntimeBridge` 只保留官方跨程序集所需的窄静态入口，`SqlBuilderExecutionSnapshot` 只公开 SQL 与不可变参数快照，绝不公开 Builder、连接、事务或诊断状态。`SqlQueryPlan` 的 Builder 仅为程序集内部状态，跨程序集执行通过计划、快照和 Count/Data 派生入口完成。`ISqlQueryPlanExecutor`、`ISqlQueryBuilderSource`、`ISqlQueryRuntimeBindingController` 和 `SqlQueryPlan` 是真实跨程序集执行/绑定 SPI，保留最小公开契约。不得为恢复旧调用方式新增兼容转发、过大的 Runtime 接口或关闭 Analyzer。

本次 dev_v6 只对经发布状态审计确认的未发布高元数来源和表达式收敛为连续 From、二元方法级泛型与原子 Join；已发布一元 Lambda 和 Raw 泛型入口不从 Shipped 基线删除。Raw 新主入口改为非泛型描述并由终结方法选择结果；新公开成员进入 `PublicAPI.Unshipped.txt`。任何已发布符号的删除都必须先有包版本、消费者和批准证据，不能只通过修改 Public API 基线掩盖破坏。

### Shipped/Unshipped 发布审计矩阵

版本依据：仓库 `version.props` 当前为 `7.0.0`；该版本值只作为当前 Shipped 基线的仓库证据，不推断外部包下载或外部消费者实际使用情况。

| 符号/范围 | 基线 | 已发布包版本 | 仓库消费者 | 外部消费者 | 处理批准 |
| --- | --- | --- | --- | --- | --- |
| `Bing.Data.Sql.ISqlQuery.From<TEntity>(string alias = null)` | Shipped | `7.0.0` 基线 | Dapper Core、SQLite/SQL Server Provider、API Contract | 未知，按已发布 ABI 处理 | 保留；不得删除 |
| `Bing.Data.Sql.ISqlQuery.From<TEntity>(string alias, string schema)` | Unshipped | 无已发布版本证据 | 新版 Data.Sql/SQLite/SQL Server/EF Core 消费者、API Contract | 未知 | 非泛型主路径；新代码显式使用，避免与兼容重载发生静态解析歧义 |
| `Bing.Data.Sql.SqlLambdaQuery<TEntity>` 及 Shipped 一元成员 | Shipped | `7.0.0` 基线 | SQLite/SQL Server 迁移前兼容调用、API Contract | 未知，按已发布 ABI 处理 | 保留兼容入口；新代码不扩散 |
| `Bing.Data.Sql.SqlMultiLambdaQuery` 兼容基类 | Unshipped | 无已发布版本证据 | 当前兼容包装内部使用 | 未知 | 仅登记为 Unshipped，不宣称已发布 |
| `Bing.Data.Sql.SqlQueryBase.From<TEntity>(string alias = null)` | Shipped | `7.0.0` 基线 | Dapper Core 及其 Provider 测试 | 未知，按已发布 ABI 处理 | 保留；不得删除 |
| `Bing.Data.Sql.SqlLambdaQuery` 非泛型描述、连续 `From`、方法级泛型 | Unshipped | 无已发布版本证据 | 新版 Data.Sql/SQLite/SQL Server/EF Core 消费者 | 未知 | 作为未发布主路径登记；不进入 Shipped |
| Raw 非泛型 `SqlTextQuery` 终结方法及 `ToPage/ToPageAsync` | Unshipped | 无已发布版本证据 | SQLite Integration、API Contract、Dapper Runtime Bridge | 未知 | 作为未发布能力登记；SQLite 真实执行验收 |
| 高元数 Lambda 类型、3+ 参数表达式和旧生成器 | 计划内删除 | 无已发布版本证据 | 旧迁移测试已移除 | 未知 | 仅在未发布证明成立后删除；当前不从已发布 ABI 删除 |

验收证据：两个 `PublicAPI.Shipped.txt` 与 HEAD 逐字一致；Data.Sql 默认 Analyzer 无 `RS0016/RS0017/RS0018`；旧/新入口分别由 `SqlQueryApiContractTest`、Dapper Core 编译和 SQLite 真实执行覆盖。外部包清单不可由仓库源码推断，故不能写成“外部消费者不存在”。

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
