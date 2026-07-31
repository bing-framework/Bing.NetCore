# SQL 公共 API 治理

## 公开 SPI

Provider SPI 按职责拆分为独立文件：`ISqlProvider`、`ISqlClauseFactory`、`ISqlTableReferenceParser`、`ISqlPaginationRenderer`、`IParameterManagerFactory`、`ISqlParameterLimitProvider` 和 `ISqlBuilderFactory`。Mutation 的有效扩展点为 `ISqlMutationClauseFactoryProvider`、可选 `ISqlUpdateFromClauseFactory`、可选 `ISqlDeleteUsingClauseFactory`、可选 `ISqlReturningClauseFactory`、可选 `ISqlReturningDialect`、`ISqlBatchUpdateRenderer` 及按操作拆分的参数绑定接口。拆分不改变命名空间或程序集可见性。

`ISqlBuilder` 是 Query 与 CRUD 的统一入口；Marker 接口和 `ISqlOperation` 保留用于 Fluent 约束。公开 `SqlOperationKind` 描述可执行状态，调用方不应依据内部待定 Insert 状态编写分支。

新增公开 API 必须具有独立职责、稳定异常语义和直接单元测试。涉及 SQL 文本的测试必须断言完整 SQL，不以 `Contains` 替代。

## 已移除兼容层

`LegacySqlProvider` 与静态 `SqlClauseContext.Create(...)` 已删除；Clause 运行上下文必须携带实际 `ISqlProvider` 和 `SqlBuilderServices`。

字符串聚合兼容成员 `SqlItem.AggregationFunc`、`ColumnItem.AggregationFunc` 和 `ColumnItem.IsAggregation` 已删除。聚合只使用 `SqlAggregateFunction` 与结构化描述符。

Mutation 占位 API 已删除：Insert/Delete 枚举不再暴露未实现的 `ProviderOptimized`，`ISqlMutationBatchPlanner` 不再伪装为可替换 SPI。已有有效枚举整数保持不变，避免配置和序列化值漂移。

`SqlProviderCapabilities.SupportsUpdateFrom` 具有真实消费链：`UpdateFromClause.Validate` 在 SQL 输出前检查能力，PostgreSQL 明确启用，其他 Provider 默认关闭。该能力具有核心、支持 Provider、不支持 Provider和受控真实执行测试，不属于占位标志。

`SqlProviderCapabilities.SupportsDeleteUsing` 同样由 `DeleteUsingClause.Validate` 在 SQL 输出前消费，当前仅 PostgreSQL 启用。能力对象保留原三参数 CLR 构造签名并新增四参数重载；既有 Provider 源码和已编译消费者无需因新增标志重编译。DeleteUsing 具有核心、支持 Provider、不支持 Provider、Roslyn 消费者和受控真实执行测试，不属于占位标志。

`SqlProviderCapabilities.SupportsReturning` 由 `ReturningClause.Validate` 在 SQL 输出前消费，当前由 PostgreSQL、SQL Server 和 SQLite 启用。能力对象在五参数构造之外继续保留原三参数和四参数 CLR 构造签名。PostgreSQL 与 SQLite 使用默认尾部 Returning；SQL Server 通过 `ISqlReturningDialect` 调整为 `OUTPUT` 的语句位置和 `INSERTED`/`DELETED` 限定符。三者复用结构化列、实体映射、Clone/Clear 和查询物化边界；SQLite 还具有四种 Mutation 的本地真实执行与 3.35+ 运行时校验。该能力具有核心、三种支持 Provider、未支持 Provider、Roslyn 消费者及受控执行测试，不属于占位标志。

## 变更门槛

修改 Provider、Factory、参数限制、标识符解析、映射或 SQL 格式化时，必须更新 `ai_docs/sql-metadata-test-traceability.md` 中“生产符号到测试方法”的映射，并运行对应 Provider 单元测试。涉及外部数据库执行路径时，SQLite 集成测试必须通过；外部 Provider 集成测试仅在安全环境变量、专用测试库和显式重置授权齐备时执行。
