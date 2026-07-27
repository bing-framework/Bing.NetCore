# SQL 公共 API 治理

## 公开 SPI

Provider SPI 按职责拆分为独立文件：`ISqlProvider`、`ISqlClauseFactory`、`ISqlTableReferenceParser`、`ISqlPaginationRenderer`、`IParameterManagerFactory`、`ISqlParameterLimitProvider` 和 `ISqlBuilderFactory`。拆分不改变命名空间或程序集可见性。

新增公开 API 必须具有独立职责、稳定异常语义和直接单元测试。涉及 SQL 文本的测试必须断言完整 SQL，不以 `Contains` 替代。

## 已移除兼容层

`LegacySqlProvider` 与静态 `SqlClauseContext.Create(...)` 已删除；Clause 运行上下文必须携带实际 `ISqlProvider` 和 `SqlBuilderServices`。

字符串聚合兼容成员 `SqlItem.AggregationFunc`、`ColumnItem.AggregationFunc` 和 `ColumnItem.IsAggregation` 已删除。聚合只使用 `SqlAggregateFunction` 与结构化描述符。

## 变更门槛

修改 Provider、Factory、参数限制、标识符解析、映射或 SQL 格式化时，必须更新 `ai_docs/sql-metadata-test-traceability.md` 中“生产符号到测试方法”的映射，并运行对应 Provider 单元测试。涉及外部数据库执行路径时，SQLite 集成测试必须通过；外部 Provider 集成测试仅在安全环境变量、专用测试库和显式重置授权齐备时执行。