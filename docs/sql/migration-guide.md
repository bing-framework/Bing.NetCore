# RC 迁移指南

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../ReleaseNotes.md)

## 事务工厂

事务工厂使用显式重载：

```csharp
factory.Begin();
factory.Begin(dataSourceKey);
factory.Begin(dataSourceKey, isolationLevel);

await factory.BeginAsync(cancellationToken);
await factory.BeginAsync(dataSourceKey, cancellationToken);
await factory.BeginAsync(dataSourceKey, isolationLevel, cancellationToken);
```

`dataSourceKey` 表示逻辑数据源标识。旧的 `dbKey` 可选参数形式不再作为新的公共 API 设计入口；调用方应按上表迁移。

## 业务多数据源 Uow

`IUnitOfWorkScopeManager` 是新增的 companion API，不替换旧 `IUnitOfWorkManager`，也不改变 `ISqlTransactionScope` 的显式单库所有权。安装 `Bing.Uow` 与 Dapper Provider 后，在组合根注册：

```csharp
services.AddSqlCore();
services.AddSqliteProvider();
services.AddSqlDataSource("orders", DatabaseType.Sqlite, ordersConnectionString);
services.AddSqlDataSource("reporting", DatabaseType.Sqlite, reportingConnectionString);
services.AddUnitOfWorkScopes();
```

业务服务通过 scoped `IUnitOfWorkScopeManager` 开始 root；在当前 DI scope 内分别创建 Query/Executor。匹配依据是冻结的数据源键、Provider、租户、连接配置、路由选项和连接 resolver 实例。Query、Builder、过滤器和 MappingProfile 仍各自独立；创建对象不会打开连接，首次执行才固定 Uow 亲和性。

```csharp
await using var root = scopes.Begin();
using var applicationQuery = queries.Create("orders");
using var repositoryQuery = queries.Create("orders");
using var orderExecutor = executors.Create("orders");
using var reportingExecutor = executors.Create("reporting");

await orderExecutor.ExecuteSqlAsync("Update Orders Set Status = @status Where Id = @id",
    new { status = "Processed", id }, cancellationToken: cancellationToken);
_ = await repositoryQuery.Sql("Select Count(*) From OrderLines Where OrderId = @id")
    .ScalarAsync<int>(cancellationToken: cancellationToken);
await reportingExecutor.ExecuteSqlAsync("Insert Into ReportQueue(OrderId) Values (@id)",
    new { id }, cancellationToken: cancellationToken);
await root.CompleteAsync(cancellationToken);
```

每个数据源有自己的本地事务，root 按资源登记顺序提交；某个库提交失败时，先前已提交的库不能自动撤销。未能安全比较的连接配置仍由当前 root 管理，但使用 Query 专属资源，不保证与另一 Query 共享物理事务；它不会退回无 Uow 自动提交。`requiresNew` 暂挂外层 root 并创建新 root，但仍使用同一个 Microsoft DI scope；必须创建新的 Query/Executor，外层已执行 Query 不能迁入。它也不会重新解析已注入的 `DbContext`，外层 pending/已 claim 的 EF Context 不能被内层自动保存。需要新的 manager、Context 和 Query 时，使用下文的 `IBingOperationExecutor`。只读汇总可选择非事务 scope，但不能依赖 rollback 撤销写入。

### EF Core Shared 桥接

需要让默认 Dapper 与仓储 `StoreBase.Shared` 共用 EF context 时，必须显式映射数据源键；EF context 仍由当前 DI scope 创建和释放。可编译的组合根示例见 [Sample Startup.ConfigureServices](../../samples/Bing.Samples.SqlUnitOfWork/Startup.cs)。其中 `UseSqlite` 来自 `Microsoft.EntityFrameworkCore.Sqlite`，不是 Bing 自有 API；Sample 项目以真实 EF SQLite provider 引用编译该注册代码。

`OrderUnitOfWork` 必须是 Bing EF Core `UnitOfWorkBase` 派生类型。该映射支持应用 Dapper Query 先执行和仓储 Shared Query 先执行；二者最终复用同一 context、连接和 EF `CurrentTransaction`。未映射的 Shared 及 `Independent` 保持原所有权，不自动参与业务 SQL root。

映射 scope 中的四个 `SaveChanges` 重载以及旧 `Commit`/`CommitAsync` 负责 flush 到 EF 当前事务，不提前物理提交。提交后回调、`SaveChangesAfter` 和领域事件清理等提交后工作延迟到 root 资源提交成功；root 回滚不会发出提交通知。手工模式下 root `Complete` 不替代 `SaveChanges`。启用 operation 保存后，最终 handler 会检查当前 DI scope 中已解析的 mapped `UnitOfWorkBase` 实例，只 flush 有待处理状态的 Context；未解析的映射不会为此被创建。显式 flush 后出现的新变更仍会在完成前保存；没有新状态时不重复 flush 或通知。保存失败会阻止资源物理提交，迟到的 Context 状态会令 root 失败。handler 不调用任意 legacy `IUnitOfWorkManager.Commit`。

`SaveChanges(false)` 保留 EF 的未接受状态；如果仍有未接受的更改，operation 最终 handler 会拒绝自动提交。调用方应按自身业务策略显式接受或回滚这部分状态。没有 SQL 数据源映射的 EF Context 不能通过 operation root 隐式独立写入；存在 pending 状态时会在其他资源物理提交前被拒绝。`AddEfCoreSqlUnitOfWork<TContext>(dataSourceKey)` 会注册映射及 operation 保存适配；`AddEfCoreOperationSaving()` 只注册发现/保存 handler，不会为 unmapped Context 创建数据源映射。

Admin CAP 通过覆盖 `TransactionCommit` 自管 CAP 事务。映射 root 中存在事务动作或领域事件时，框架会在派发前拒绝该覆盖路径；需要 CAP 动作时继续使用其旧的 CAP-owned Uow 路径，不要把同一 context 同时交给两个事务 owner。无 root 的旧 CAP 路径保持兼容。

### 显式业务操作与自动保存

`AddUnitOfWorkScopes()` 只注册 scope manager，不启用 operation 保存，也不改变旧的手工保存契约。需要显式执行器时，在组合根调用 `services.AddBingOperations()`；它注册 operation manager/executor，并让 owner root 使用已注册的保存 handler。executor 每次创建新的 DI scope，先开始 root，再从新 scope 解析业务服务；完成/回滚并释放 root 后，释放 DI scope。调用方仍要为普通业务方法明确调用 executor，框架不会依据 HTTP 方法名或业务方法名自动开启 root。

需要对 `[UnitOfWork]` 标记方法启用 AspectCore 自动 owner/participant 时，另用 `Bing.Ddd.Application.AspectCore` 包并显式注册 `services.EnableAop()` 与 `services.EnableBingAutomaticUnitOfWork(...)`。后者包含 `AddBingOperations()` 注册；仅调用 `EnableBingUnitOfWork()` 保持旧代理语义，不等于启用新自动边界。无活动 root 的标记调用拥有 root；活动 root 中的标记调用作为 participant，participant 失败即使被捕获也会把 root 标为 rollback-only。`IUnitOfWorkOperationManager.BeginOperation` 可显式创建相同 owner/participant 生命周期。`IUnitOfWorkScopeManager.Current` 继续表示 root；当前 participant 的操作令牌由 `IUnitOfWorkExecutionScope.CancellationToken` 提供。

root 创建时可用 `UnitOfWorkScopeOptions.Timeout` 设置期限；它与 caller token 构成协作取消令牌，业务及实际异步 I/O 必须继续传递该令牌。超时不会强制中断忽略取消的用户代码。启用 `AddBingOperations()` 后，注册的 EF handler 也会影响同容器中手动建立的 owner root：已解析且映射的 `UnitOfWorkBase` 若仍有 tracked changes，会在资源物理提交前保存。旧容器只注册 `AddUnitOfWorkScopes()` 时，未调用 `SaveChanges` 的 tracked entity 仍不会落库。切换 opt-in 前，先检查是否依赖此旧行为。

executor 在 scope 释放后才返回 `TResult`；只返回已物化的 DTO/标量值。不要把 `DbContext`、Query、reader、stream、`IAsyncEnumerable` 或延迟枚举带出操作 scope。新 operation 处理器只处理当前 DI scope 中已解析的 EF Context；unmapped pending 写入、同 scope requiresNew 的外层 pending/claimed 状态会被拒绝。Sample 将显式执行器置于 `SqlUow:OperationsEnabled`（默认 `false`）下，可运行主例为 [`Bing.Samples.SqlUnitOfWork`](../../samples/Bing.Samples.SqlUnitOfWork/README.md)；HTTP 路由为 `POST /api/operation-scenarios/{scenario}`。

### FreeSQL 与后台 scope

FreeSQL `QueryStoreBase.Sql` 使用默认 Dapper Query 时可以加入业务 SQL root；FreeSQL 原生 ORM CRUD 仍由 FreeSQL 自己的 `IUnitOfWork` 管理，不会因 Dapper root 自动入 enlist。调用方需按原有 FreeSQL 提交边界处理 native CRUD。仓储释放其 SQL Query 不关闭 root 拥有的共享连接。FreeSQL 原生 operation 适配未包含在本阶段，属于独立 B04 计划；不能由 Dapper Query 可 enlist 推定为 FreeSQL 全 ORM 支持。

Sample 默认关闭 operation executor，保留原手工 service 路径。启用 `SqlUow:OperationsEnabled` 后，真实 HTTP、Hosted `BackgroundService` worker 和 Hangfire Server 都把工作交给 `IBingOperationExecutor`；executor 的每项工作使用独立 DI scope，并在 Begin 后重新解析 `OperationScenarioService`。队列/Hangfire 参数通过标量快照传播 Trace 与数据库路由上下文，不传 Context、Query 或 manager。取消使用 HTTP request、host shutdown 或 Hangfire 提供的调用方令牌；每个操作完成、失败或取消后释放自己的 root 与 DI scope。Hangfire 示例使用 MemoryStorage，不表示持久化调度服务。

CAP 多源发布/consumer 场景与上述 SQLite operation host 是不同的入口。发布端 Outbox 行证据不能单独证明消息已由独立 consumer 收取并产生副作用；新 opt-in operation 到 consumer 的闭环只按阶段 B 执行记录中对应的 MySQL/Rabbit 证据声明，不用旧 consumer 场景或“启动了 host”推断成功。

## `ToEntity`

`ToEntity` / `ToEntityAsync` 保留为当前查询描述的唯一高层实体终结入口，其实际语义是零行返回默认值、单行返回实体、多行由底层 SingleOrDefault 语义处理。本文档不新增同义 `SingleOrDefault` 查询描述 API。

## 集成测试配置

本机外部 Provider 使用未跟踪的 `integration.runsettings.local`，通过 `Invoke-ProviderIntegrationTests.ps1 -Settings <path>` 显式加载。受保护 CI 只使用 Provider 专属 secret；不能使用默认连接、生产库或 `RUN_INTEGRATION_TESTS` 全局 gate。

## 实体映射配置版本发布

`DefaultEntityMappingResolver` 在构造时深复制 `EntityMappingOptions` 及列配置。构造完成后直接修改原配置对象不会改变解析结果。

需要在运行时更新映射时，使用 `VersionedEntityMappingResolver.PublishMappings` 发布完整配置集合。发布成功后版本递增；新建 Query 或 Builder 捕获新版本，已创建对象及其 Clone 继续使用原快照，避免单次 SQL 构建混用新旧表名或列名。发布失败不会替换当前版本。

逻辑标识 `DbKey`、`MappingProfile` 和 `TableRouteKey` 采用去除首尾空白、大小写无关的匹配规则。Database、Schema 和 TableName 保留原始内容及大小写，并按精确值隔离缓存。

自定义解析器若需要参与固定版本生命周期，应实现 `IEntityMappingSnapshotProvider`。默认实现继续按 `DatabaseType` 隔离 Provider 行为；依赖额外上下文的自定义解析器必须自行保证缓存键完整性。
