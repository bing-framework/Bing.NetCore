# 多数据源业务 Uow 迁移指南

本文面向从单个 ISqlTransactionScope 或 ORM 自有工作单元迁移到业务级多数据源协调的调用方。新增 API 是 companion scope；旧 IUnitOfWorkManager、FreeSQL 原生 Uow 和显式单库 SQL 事务仍保持各自职责。

## 注册

在应用组合根注册 SQL Provider、数据源和业务 scope。Query/Executor 工厂仍按项目现有生命周期注册，Query/Executor 保持 transient：

    services.AddSqlCore();
    services.AddSqliteProvider();
    services.AddSqlDataSource("orders", DatabaseType.Sqlite, ordersConnectionString);
    services.AddSqlDataSource("reporting", DatabaseType.Sqlite, reportingConnectionString);
    services.AddUnitOfWorkScopes();

EF Shared 仓储若需要与默认 Dapper 资源共用事务，再显式增加 context 映射：

    services.AddDbContext<OrderUnitOfWork>(options => options.UseSqlite(ordersConnectionString));
    services.AddEfCoreSqlUnitOfWork<OrderUnitOfWork>("orders");

OrderUnitOfWork 必须继承 Bing EF Core 的 UnitOfWorkBase，并在当前应用 DI scope 中只有一个 scoped context 实例。映射键是显式 dataSourceKey；`AddEfCoreSqlUnitOfWork` 会幂等补上 `AddUnitOfWorkScopes`。同一 context 类型不能映射到多个数据源，同一数据源也不能映射不同 context 类型；预先注册的自定义 scope manager 保持优先。未映射的 Shared 与 Independent 保持原行为。

## 业务边界与首次执行

通过 scoped IUnitOfWorkScopeManager 开始一个 root。独立工厂创建的 Query、仓储 Query 和 Executor 可在同一 root 中共享同一冻结数据源配置的连接及本地事务：

    public async Task ReconcileAsync(int orderId, CancellationToken cancellationToken)
    {
        await using var root = _scopes.Begin();
        using var applicationQuery = _queries.Create("orders");
        using var repositoryQuery = _queries.Create("orders");
        using var orderExecutor = _executors.Create("orders");
        using var reportExecutor = _executors.Create("reporting");

        await orderExecutor.ExecuteSqlAsync(
            "Update Orders Set Reconciled = 1 Where Id = @orderId", new { orderId },
            cancellationToken: cancellationToken);
        _ = await applicationQuery.Sql("Select Count(*) From OrderLines Where OrderId = @orderId", new { orderId })
            .ScalarAsync<int>(cancellationToken: cancellationToken);
        _ = await repositoryQuery.Sql("Select Count(*) From Orders Where Id = @orderId", new { orderId })
            .ScalarAsync<int>(cancellationToken: cancellationToken);
        await reportExecutor.ExecuteSqlAsync("Insert Into ReconcileLog(OrderId) Values (@orderId)",
            new { orderId }, cancellationToken: cancellationToken);

        await root.CompleteAsync(cancellationToken);
    }

创建 Query 不会访问数据库，也不会决定其 root。首次执行时才冻结 Uow 亲和性：尚未执行的 Query 可加入当时的 root；已在 Uow 外执行过的 Query 不迁移到后续 root；已加入一个 root 的 Query 不能跨 root 或在该 root 外继续执行。Builder、过滤器与 MappingProfile 不因连接共享而共享状态。

资源匹配使用冻结的数据源键、Provider、租户、完整连接配置、路由选项和连接 resolver 身份。能安全比较的同源配置在 root 内共用资源；不透明或未知配置使用 Query 专属资源，但该资源仍由 root 提交/回滚/释放，不回退为 Uow 外自动提交。自定义 resolver 和集成认证不需要额外身份接口；root 通过可信内部路径借用自己创建的连接，不要求 opaque 身份可比较。真正由调用方传入的外部连接仍执行严格的物理身份校验。

连接打开前会按创建时冻结的数据源快照完成准入：验证可比较的物理库身份、只读与事务能力、主库路由要求以及 root 事务模式。标记为需要重定向到主库的冻结 replica 不会在执行时悄悄换源；非事务 root 若要求 `PrimaryReadStrategy.Transaction` 也会在打开连接前拒绝。Dapper、映射 EF Query-first 与 SaveChanges-first 都遵守同一准入边界。

首次真实执行遇到已取消的 token 时，如果当前有参与 root，会先将 root 标记为 rollback-only，再取消执行；此路径不打开连接。尚未开始执行的描述参数验证错误不会毒化 root。该规则适用于 scalar/list、Executor、分页、stream 首次 `MoveNext`、Multiple 及映射 EF 两个异步 SaveChanges overload。

同一资源具有非阻塞的独占执行租约；不要并行使用共享同一连接的 Query/Executor。独立并行操作应各自创建 DI scope、manager 和业务 root。requiresNew 会暂挂外层 root，内层创建新的 Query/Executor；EF 同 context 的新 root 不能复用外层 scoped context，应使用独立业务 DI scope/context。

## 提交、回滚与多数据源限制

每个数据源使用自己的本地事务。Complete 按资源首次登记顺序逐个提交；若后一个库提交失败，较早已提交的库无法自动撤销，因此它不是分布式事务或跨库原子提交。失败、取消、未完成 scope 的释放都会回滚所有尚未提交资源并尽力清理。

默认 `InsertBatch`、`UpdateBatch`、`DeleteBatch` 的同步与异步路由在参与 root 中复用该 Executor 首次执行时冻结的数据源、root 资源和执行租约。root 是唯一物理提交者；事务 root 中即使批次选项 `UseTransaction=false`，命令仍 enlist 在 root 事务里。若批次要求 `UseTransaction=true` 而 root 是非事务模式，则在执行命令前拒绝并标记 rollback-only。批次命令、取消或受影响行数验证失败也会使参与 root rollback-only，即使业务代码捕获异常，root 也不能提交之前的写入。没有参与 root 时默认批次继续使用旧的批次事务；显式 `Owned`、`External`、`Independent` 或 `ISqlTransactionScopeFactory` owner 仍按各自所有权独立运行。

只读汇总可传 UnitOfWorkScopeOptions { IsTransactional = false }。它仍统一管理资源和释放，但不创建本地事务；调用方不得依赖 Rollback 撤销其中的写入。没有活动业务 scope 时，Dapper Query/Executor 继续使用旧的独立执行/自动提交行为。

ISqlTransactionScopeFactory 创建的显式单库事务是另一个事务 owner，不会自动加入业务 root。需要将多个普通 Query/Executor 纳入同一业务 root 时，不要在其间另开该显式 scope。

## EF Core Shared 提交语义

显式映射后，默认 Dapper 与 StoreBase.Shared 都按“数据源键 + 当前 scoped context”取同一个 EF resource。支持两种顺序：应用 Dapper Query 先执行，或仓储 Shared Query 先执行。两条路径使用同一 context connection 和 Database.CurrentTransaction；Store/Query 释放只归还借用，不关闭或释放 EF 拥有的 context/connection。

映射 root 中的四个 EF SaveChanges 重载以及旧 Commit/CommitAsync 执行 flush，把已跟踪改动写入 EF 当前事务，不物理提交该事务。其他独立连接在 root 完成前仍看不到这些行。root Complete 是物理提交边界；root 回滚、异常或取消会撤销已 flush 的 EF 与 Dapper 写入。Complete 不会自动保存仅跟踪但未显式 SaveChanges 的实体。映射 context 首次保存时若 Current 已 Completed、RolledBack 或 Faulted，会拒绝而不回退到旧的自动提交路径；已完成且没有待保存变更的 late Commit 仍返回 0，不重复物理提交或通知。

SaveChanges(false) 保持 EF acceptAllChangesOnSuccess: false 语义。事务动作收到 EF resource 的同一 IDbTransaction。SaveChangesAfter 与领域事件提交后清理只在 EF resource 物理提交成功后执行；ICommitAfter 和 root 全局完成回调要等所有资源都成功提交。多资源部分提交时，只运行已提交资源的资源回调，全局回调不运行。回调异常不会重新提交已完成的 resource。root 回滚不发提交通知；重复 flush 不重复派发已处理领域事件，回滚保留其待提交清理状态。

Admin CAP 派生 context 覆盖 TransactionCommit 管理 CAP 自己的事务。若映射业务 root 中存在事务动作或领域事件，该覆盖路径会在派发前拒绝加入；需要这些 CAP 动作时继续采用既有 CAP-owned Uow 路径，不要让两个 owner 同时提交同一 context。没有业务 root 的既有 CAP 路径保持原行为。

## FreeSQL 与后台消费者

FreeSQL QueryStoreBase.Sql 走默认 Dapper Query 时可加入业务 SQL root，仓储释放 Query 不会关闭 root 拥有的连接。FreeSQL native ORM CRUD 仍属于 FreeSQL 自身 Uow，不会被 Dapper root enlist；按现有 native Uow 显式提交。消费者回归分别使用 FreeSQL 与 Dapper 的临时 SQLite 文件，验证 Dapper rollback 不会回滚 FreeSQL 已提交的 native 操作；该测试不声称两个 ORM 共享一个物理事务。

后台 worker 每轮创建新的异步 DI scope 并解析 scoped operation；Hangfire job 通过现有 job activator 的 scope 解析同一类 operation。operation 在调用内 Begin/Complete root。并行任务分别建立 DI scope，不共享 Query、Executor 或 scoped manager。仓库测试验证这些 scope/root 相互独立，不启动 Hangfire server。

## 局部实现证据

注册示例：[Bing.Uow README](../../../framework/src/Bing.Uow/README.md)。SQL API 片段：[SQL Query 用法](../../../docs/sql/sqlquery-usage.md)。本机 SQLite 与外部门控：[数据库集成测试](../../../docs/testing/database-integration-tests.md)。
