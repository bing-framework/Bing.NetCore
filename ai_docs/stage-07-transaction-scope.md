# 阶段七：SQL 事务作用域最终 API

## 完成项

- 删除 `ISqlTransactionScopeFactory.Create`，统一为 `Begin` 和 `BeginAsync`。
- `ISqlTransactionScope` 现在提供事务 ID、异步提交、异步回滚和 `IAsyncDisposable`。
- Scope 创建的 Query 与 Executor 继续绑定同一事务；未完成 Scope 释放时自动回滚。
- 所有 Executor 同步和异步 SQL、存储过程路径均支持 `PrimaryReadStrategy.Transaction`：成功提交内部短事务，异常回滚，且只关闭框架拥有的连接。
- 为 `netstandard2.0` 公共异步处置契约添加 `Microsoft.Bcl.AsyncInterfaces` 引用。

## 测试

- 同步提交作用域拥有的事务。
- 未完成 Scope 释放时自动回滚。
- `BeginAsync`、`CommitAsync`、事务 ID 和 `await using` 生命周期。
- 主库短事务成功提交和异常回滚；外部事务在异常时不被回滚或关闭。

## 验证

- `Bing.Dapper.SqlServer.Tests`：178 passed。
- `Bing.Dapper.MySql.Tests`：156 passed。
- `Bing.Dapper.PostgreSql.Tests`：124 passed。
- `Bing.Dapper.Sqlite.Tests`：4 passed。
- `Bing.Dapper.Oracle.Tests`：106 passed。

## 风险

- `BeginAsync` 在 `netstandard2.0` 上以同步开始事务并返回已完成任务实现；底层 Provider 的真正异步打开和开始事务需要在后续提高目标框架或采用 Provider 专用适配器时处理。