# SQL RC 文档入口

本目录汇总 Bing.Data.Sql 与 Bing.Dapper 的稳定入口、Provider 差异、事务迁移和集成验证方式。既有专题文档继续保留，入口页只负责导航，避免同一行为出现两套说明。

- [Provider 能力](provider-capabilities.md)
- [PostgreSQL Function](postgresql-functions.md)
- [迁移指南](migration-guide.md)
- [集成测试与 RunSettings](../testing/database-integration-tests.md)
- [查询用法](../sqlquery-usage.md)
- [Lambda 查询](../sqlquery-lambda-usage.md)
- [批量 Mutation](../sql-mutation-batch-execution.md)
- [事务 API](../migrations/sql-transaction-api-vNext.md)

根 Query 入口固定为 `Query()`、`Sql()`、`SqlInterpolated()`、`Procedure()`、`From<T>()` 和 `FromSubquery<T>()`。本 RC 不再增加同义 Raw SQL 或 Query 入口。
