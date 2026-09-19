# SQL RC 文档入口

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../ReleaseNotes.md)

本目录汇总 Bing.Data.Sql 与 Bing.Dapper 的稳定入口、Provider 差异、事务迁移和集成验证方式。既有专题文档继续保留，入口页只负责导航，避免同一行为出现两套说明。

- [Provider 能力](provider-capabilities.md)
- [PostgreSQL Function](postgresql-functions.md)
- [迁移指南](migration-guide.md)
- [集成测试与 RunSettings](../testing/database-integration-tests.md)
- [查询用法](sqlquery-usage.md) —— `ISqlQuery` 主手册：装包注册、四种形态、终结方法全表、参数化、分页、事务、流式、生命周期
- [Lambda 查询](sqlquery-lambda-usage.md) —— 仅 `From<T>()` 及其 Lambda 子句与多映射。其余内容不重复，见查询用法
- [批量 Mutation](sql-mutation-batch-execution.md)
- [事务 API](../migrations/sql-transaction-api-vNext.md)

根 Query 入口固定为 `Query()`、`Sql()`、`SqlInterpolated()`、`Procedure()`、`From<T>()` 和 `FromSubquery<T>()`。本 RC 不再增加同义 Raw SQL 或 Query 入口。

两篇查询文档的分工：**查询用法**覆盖通用面（注册 / 四种形态 / 终结方法 / 参数化 / 分页 / 事务 / 流式 / 生命周期），**Lambda 查询**只覆盖 `From<T>()` 的 Lambda 子句与多映射。交叉内容一律以查询用法为准，Lambda 篇不再重复。
