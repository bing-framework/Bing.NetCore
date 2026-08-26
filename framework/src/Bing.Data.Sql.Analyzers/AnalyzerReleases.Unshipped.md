## New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
BINGSQL002 | Bing.Data.Sql.Security | Warning | 检测插值值传入 `ISqlQuery.Sql(...)`、`ISqlExecutor.ExecuteSql(...)` 或 `ExecuteSqlAsync(...)` 的普通文本入口；参数对象和 `SqlInterpolated(...)` 保持安全路径。 |
