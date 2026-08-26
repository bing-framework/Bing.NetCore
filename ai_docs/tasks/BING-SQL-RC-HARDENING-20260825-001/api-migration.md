# API 迁移说明

- task-id: `BING-SQL-RC-HARDENING-20260825-001`
- 版本性质：Breaking Change；未保留 `[Obsolete]` 转发层。

| 删除 API | 迁移方式 |
| --- | --- |
| `DiagnosticsMessage.OperationId` | 使用 `DiagnosticsMessage.ExecutionId`。 |
| `ISqlConditionGroup.Group(...)` | 按语义改用 `AndGroup(...)` 或 `OrGroup(...)`。 |
| `FromClause.SetRoots(...)` | 使用连续 `From<TEntity>()` 或明确的 `FromTable(...)` 来源入口。 |
| `SqlQuery` / `SqlLambdaQueryCore` 高层 `ToDictionary*` | 使用 `ToList<TResult>()` 或 `ToListAsync<TResult>()` 后执行 LINQ `ToDictionary(...)`。 |
| `SqlQuery` / `SqlLambdaQueryCore` 表层 `SingleOrDefault*` | 使用 `ToEntity<TResult>()` / `ToEntityAsync<TResult>()`。 |

## 兼容性处理

已同步 `PublicAPI.Shipped.txt`、`PublicAPI.Unshipped.txt`、反射 API 契约、测试、活动文档和 Release Notes。Runtime SPI 保留实际跨程序集消费者所需的最小 public 合同，不新增生产 `InternalsVisibleTo`。
