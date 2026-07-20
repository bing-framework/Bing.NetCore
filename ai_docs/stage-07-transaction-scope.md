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

## 2026-07-16 上下文固定与 Doris 边界

- 事务开始时通过主库读取偏好解析并复制完整数据库上下文；事务子 Query 和 Executor 复用该快照、同一连接和同一事务，不再读取后续 Ambient Context。
- 数据源快照会复制连接名称、映射配置、读写策略与本地事务能力，避免运行期间修改描述符导致事务上下文漂移。
- `SqlDataSourceDescriptor.SupportsTransactions` 默认值为 `true`。Doris 使用 `DatabaseType.MySql` 和独立 Mapping Profile 接入时应设为 `false`，本地事务开始会明确抛出不支持异常。Doris 复用 MySQL 协议、方言与参数格式，但不默认具备完整 MySQL 的事务、更新、锁和批量写能力。

## 2026-07-17 子对象生命周期

- Scope 创建的 Query 与 Executor 共享内部事务 lease 和 Scope 的 `TransactionId`，诊断事件可关联到同一事务。
- `Commit`、`Rollback` 或 `Dispose` 开始时即使 lease 失效；此前创建的子对象再次获取连接、事务或执行命令会抛出明确异常，不能重新建连或脱离已结束的事务运行。
- Scope 结束会释放其创建的子对象；子对象只将连接和事务视为外部资源，不会提前释放 Scope 所拥有的 ADO 资源。

## 本轮补充

- 提交或回滚后保留完成态，重复同一完成操作保持幂等且不会重复释放资源；交叉提交/回滚会抛出明确异常。
- 显式 `Dispose` 或 `DisposeAsync` 后，后续提交、回滚及创建子对象会抛出 `ObjectDisposedException`。
- `Bing.Dapper.SqlServer.Tests` 已覆盖同步和异步完成态；SQLite 真实集成测试验证完成后子对象创建仍遵循释放语义。