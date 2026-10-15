# 数据库审计与业务事务

> **适用版本**：框架 `7.0.0`｜源码核对：2026-10-13

`Bing.Auditing.Persistence` 提供 MySQL/SQLite 审计存储。默认 `AddBingAuditing()` 仍使用日志存储；需要可靠落库的宿主显式选择 `AddBingDatabaseAuditing`。

## 普通请求审计

```csharp
using Bing.Auditing.Persistence;
using MySqlConnector;

services.AddBingDatabaseAuditing(
    AuditLogDatabase.MySql,
    () => new MySqlConnection(configuration.GetConnectionString("Audit")),
    options =>
    {
        options.AllowedPropertyNames.Add("Quantity");
        options.AllowedPropertyNames.Add("Status");
    });
services.AddBingHttpAuditing();
```

连接工厂每次必须返回新的、未打开的连接。存储负责打开、事务提交和释放自己的连接；不要返回业务上下文正在使用的连接。HTTP 审计在请求完成后保存到独立事务，保存失败沿用中间件的告警或显式抛错策略。它不能使已经提交的业务回滚，不能作为库存等关键操作的事务审计。

HTTP 审计应放在认证之后。请求主体包含多个 `ClaimsIdentity` 时，与 HTTP 限流保持相同规则：按身份顺序选择第一个 `IsAuthenticated` 的身份，用户、用户名称、租户和客户端声明全部从该身份读取。未认证身份不参与选择；选定身份缺少某个声明时，该字段保持 `null`，不会从后续已认证身份补齐，避免不同认证来源被拼成同一审计主体。宿主应确定认证方案合并后的身份顺序，并通过 `UserIdClaimType`、`UserNameClaimType`、`TenantClaimType`、`ClientIdClaimType` 配置该身份的声明类型。

HTTP 保存失败后仍等待作用域异步释放。保存和释放同时失败时，按发生顺序、按异常对象引用去重，将完整 `Exception[]` 附于最终保留异常的 `Data["Bing.HttpAuditCleanupErrors"]`。已有请求异常时保留同一个原始对象；没有请求异常且开启 `ThrowOnStoreFailure` 时抛出首个清理异常。默认策略只告警，日志只输出错误数量、有限类型及关联标识，不输出异常消息或对象。`BingHttpAuditingOptions.SaveTimeout` 可以设置保存等待预算；预算耗尽只记录 `TimeoutException` 诊断，底层保存任务仍会完成后才释放审计作用域，不会因为预算耗尽取消任务或提前释放连接。实现可选 `ICancellableAuditingStore` 时，中间件会把 `HttpContext.RequestAborted` 传给存储；旧 `IAuditingStore` 继续使用兼容的无令牌入口。请求取消只表示存储可以结束等待，宿主仍需等待实际任务和资源释放；开启 `ThrowOnStoreFailure` 时，超过预算会在实际保存和释放完成后传播该诊断。

`SaveTimeout` 默认是 `Timeout.InfiniteTimeSpan`，保持无限等待。例如可通过 `services.AddBingHttpAuditing(options => options.SaveTimeout = TimeSpan.FromSeconds(5))` 配置诊断预算。`SaveWithBudgetAsync` 在预算耗尽时收集超时，随后仍等待同一次 `SaveAsync` 实际结束，再异步释放审计作用域；不会重发保存或将保存移交后台。实现可选接口的存储收到请求令牌后，可以自行取消数据库等待；中间件不会把取消当成已经完成清理。诊断日志在保存和释放完成后输出，因此该预算不是 HTTP 请求耗时上限，也不能让永久挂起的存储自行退出。宿主仍须安排底层数据库超时及停止等待。

没有原始业务异常且 `ThrowOnStoreFailure = true` 时，即使迟到的保存最终成功，仍会在清理完成后传播超时异常。已有业务异常时保留原异常，并把超时及后续保存、释放异常附于 `Bing.HttpAuditCleanupErrors`。这不会撤回已经提交的业务或已发送的响应。

数据库 Provider 由宿主选择，存储没有新增 Provider 生产依赖。部署阶段用 `AuditLogSchema.GetCreateStatements(database)` 建立 `bing_audit_log` 及 `bing_audit_log_archive`；运行期不自动建表。已有环境应执行 `deploy/migrations/auditing/005-audit-log-archive.mysql.sql` 或对应 SQLite 脚本。表结构升级和保留期限仍需通过部署迁移管理，建表语句不构成滚动升级方案。

普通审计保留可以通过 `IAuditLogRetentionStore.DeleteBeforeAsync` 分批执行：

```csharp
var retention = serviceProvider.GetRequiredService<IAuditLogRetentionStore>();
var deleted = await retention.DeleteBeforeAsync(
    DateTime.UtcNow.AddDays(-30), batchSize: 500, cancellationToken);
```

截止时间必须是 UTC，单批最多 10,000 条；调用方应在每批完成后重新检查积压，不应在后台建立无界删除循环。存储会为普通记录使用独立连接和事务，并保留关键审计记录。`IsCritical` 标记由关键事务写入路径设置；旧表需要先执行 `deploy/migrations/auditing/001-critical-flag.mysql.sql`，租户范围清理还应执行 `deploy/migrations/auditing/004-tenant-retention-index.mysql.sql` 或对应的 SQLite 脚本。迁移前必须备份并复核历史记录，因为旧版本无法区分普通和关键审计，历史行默认标记为普通。归档到冷存储、租户差异化留存和授权器的具体策略仍由宿主负责。

面向 HTTP 或管理后台时，应注册带授权器的高层入口，避免把低层存储直接暴露给调用方：

```csharp
services.AddBingAuditLogRetention<AuditRetentionAuthorizer>();

var retention = serviceProvider.GetRequiredService<IAuditLogRetentionService>();
var deleted = await retention.DeleteBeforeAsync(
    DateTime.UtcNow.AddDays(-30), batchSize: 500, cancellationToken);
```

`AuditRetentionAuthorizer` 负责当前用户、租户、角色和保留策略判断；授权失败不会打开数据库连接。`IAuditLogRetentionStore` 仍保留为基础设施契约，后台任务也应通过 `IAuditLogRetentionService` 进入授权流程。冷归档、租户差异化留存和授权器的具体身份来源仍由宿主实现。

需要把主表中的普通审计移到归档表时，显式注册并调用归档服务：

```csharp
services.AddBingAuditLogArchive<AuditRetentionAuthorizer>();

var archive = serviceProvider.GetRequiredService<IAuditLogArchiveService>();
var archived = await archive.ArchiveBeforeAsync(
    DateTime.UtcNow.AddDays(-90), batchSize: 500, cancellationToken);
```

归档使用与保留清理相同的授权器，但不会自动启动后台任务。每一批会在存储自己的同一事务中先写入 `bing_audit_log_archive`，再从主表删除；失败或取消会回滚两步。归档表保留原始审计明细和 `ArchivedAtUtc`；如果归档表已经存在相同 `(TenantId, AuditId)`，本批会因唯一约束失败并保留主表记录，避免审计身份复用时静默丢失新内容。不提供默认删除任务。调用方应按业务允许的重放窗口保留审计幂等记录和归档数据，使用有限批次循环并监控容量；不能把归档成功解释为跨存储备份或无限期保留。

确认归档历史已经超过业务重放和审计留存窗口，并完成外部备份后，可以显式清理归档表：

```csharp
services.AddBingAuditLogArchiveRetention<ArchiveRetentionAuthorizer>();

var archiveRetention = serviceProvider.GetRequiredService<IAuditLogArchiveRetentionService>();
var deleted = await archiveRetention.DeleteBeforeAsync(
    DateTime.UtcNow.AddDays(-365), batchSize: 500, cancellationToken);
```

该清理使用独立授权器，只删除 `ArchivedAtUtc` 早于截止时间的归档记录，不触及主审计表；单批最多 10,000 条，失败或取消由存储事务回滚。调用方应有限批次执行并监控容量。删除归档历史会缩短可重放窗口，不能在业务幂等记录、监管留存或备份未满足时执行。

需要由受信任后台周期执行归档历史清理时，可以注册默认关闭的可选调度器：

```csharp
services.AddBingAuditLogArchiveRetentionScheduler<ArchiveRetentionAuthorizer>(options =>
{
    options.Enabled = true;
    options.Interval = TimeSpan.FromHours(6);
    options.RetentionPeriod = TimeSpan.FromDays(365);
    options.BatchSize = 500;
    options.RunImmediately = false;
    options.UseCoordinationLease = true;
    options.CoordinationKey = "bing:audit-archive-retention";
    options.CoordinationWaitTimeout = TimeSpan.Zero;
    options.CoordinationLeaseDuration = TimeSpan.FromMinutes(5);
});
```

该调度器每轮只调用 `IAuditLogArchiveRetentionService` 删除一个有界批次，不绕过独立授权器，也不自动创建冷存储备份。启用前必须确认归档表的重放窗口、外部备份和业务幂等记录已经满足；启动阶段会预检归档清理服务，缺少服务或其依赖会直接失败，避免应用启动后才在后台循环中报错；停止时会等待当前删除调用实际结束。启用 `UseCoordinationLease` 后必须注册 `ILockLeaseProvider`，多节点应使用 Redis 等分布式实现，并使用与普通审计清理不同的协调键。租约只降低重复调度，不替代数据库事务和留存策略。

需要由受信任后台周期执行清理时，可以注册可选调度器：

```csharp
services.AddBingAuditLogRetentionScheduler<SystemAuditRetentionAuthorizer>(options =>
{
    options.Enabled = true;
    options.Interval = TimeSpan.FromHours(1);
    options.RetentionPeriod = TimeSpan.FromDays(30);
    options.BatchSize = 500;
    options.RunImmediately = false;
    options.UseCoordinationLease = true;
    options.CoordinationKey = "bing:audit-retention";
    options.CoordinationWaitTimeout = TimeSpan.Zero;
    options.CoordinationLeaseDuration = TimeSpan.FromMinutes(5);
});
```

调度器默认关闭，每轮只删除一个有界批次，并且仍通过 `IAuditLogRetentionService` 执行授权。`SystemAuditRetentionAuthorizer` 必须明确代表受信任的后台身份，不能把普通请求授权器直接复用为系统清理权限。启用后会在 `StartAsync` 预检高层清理服务及协调租约提供程序，缺少注册会直接拒绝启动，不把配置错误延迟为后台日志。宿主停止时会等待当前删除任务实际结束；等待超时或取消不会提前释放数据库连接。`UseCoordinationLease` 默认关闭；启用后必须注册 `ILockLeaseProvider`，否则宿主启动会直接失败；已经注册但本轮未获取租约的节点直接跳过，不会无限等待。多节点部署应使用 Redis 等分布式实现，并为 `CoordinationLeaseDuration` 配置有限租期；本地锁只能协调同一进程。租约只负责降低重复调度，不能代替存储层幂等或保护已经超过租期的长事务。该入口只负责普通审计的分批清理，不实现冷归档、租户差异化留存或关键审计删除。

需要由租户管理员清理单个租户时，使用独立的租户范围契约，避免把全局清理权限误用于单租户操作：

```csharp
services.AddBingAuditLogTenantRetention<TenantAuditRetentionAuthorizer>();

var retention = serviceProvider.GetRequiredService<IAuditLogTenantRetentionService>();
var deleted = await retention.DeleteBeforeAsync(
    trustedTenantId, DateTime.UtcNow.AddDays(-30), batchSize: 500, cancellationToken);
```

`IAuditLogTenantRetentionAuthorizer` 会收到同一个租户标识，`DbAuditingStore` 也会在删除语句中使用参数化的租户条件；关键审计、其他租户和截止时间之后的记录不会被删除。租户标识必须非空、不可以全部为空白、不超过 128 个字符，并按原值匹配，首尾空白和 Unicode 大小写不会被修剪或规范化。该入口仍不提供 HTTP 权限判断、冷归档或无界后台循环，授权器必须从可信认证上下文获取租户，而不能信任请求正文或可修改的查询参数。

## 授权后的审计元数据查询

数据库存储还实现了 `IAuditLogQueryStore`，用于在宿主已经完成授权后读取审计元数据：

```csharp
var queryStore = serviceProvider.GetRequiredService<IAuditLogQueryStore>();
var entries = await queryStore.QueryAsync(new AuditLogQuery
{
    TenantId = trustedTenantId,
    FromUtc = DateTime.UtcNow.AddDays(-1),
    Limit = 100
}, cancellationToken);
```

查询必须显式提供租户标识，`Limit` 最大为 1000，结果按 UTC 执行时间和审计标识稳定排序，并支持应用、用户、关联标识、时间范围和关键审计过滤。返回的 `AuditLogEntry` 不含 `DetailsJson`，存储层不判断当前用户是否可以读取租户或关键审计；控制器、应用服务或后台任务必须在调用前完成租户、角色和用途授权。需要展示受控明细时，应单独设计授权、脱敏和分页策略，不要把数据库连接或原始明细直接暴露给 HTTP 客户端。

面向业务入口时，可以注册授权查询服务统一执行授权：

```csharp
services.AddBingAuditLogQuery<AuditQueryAuthorizer>();

var queryService = serviceProvider.GetRequiredService<IAuditLogQueryService>();
var entries = await queryService.QueryAsync(new AuditLogQuery
{
    TenantId = trustedTenantId,
    Limit = 100
}, cancellationToken);
```

`AuditQueryAuthorizer` 负责当前用户、租户、角色和查询用途判断；授权失败或调用方预取消不会打开数据库连接。`IAuditLogQueryStore` 仍保留为低层基础设施契约，返回结果只包含安全元数据，不包含明细 JSON。

Admin 两套宿主提供了一个默认关闭的受保护元数据入口。只有同时启用数据库审计和查询配置，并配置独立角色时才注册：

```json
{
  "Auditing": {
    "DatabaseEnabled": true,
      "Query": {
        "Enabled": true,
        "Role": "audit-reader",
        "AllowCrossTenant": false,
        "CrossTenantRole": "tenant-audit-reader"
      }
  }
}
```

启用后使用 `GET /internal/auditing/logs?tenantId=...`。查询必须显式提供租户标识；当前入口不支持全局查询。默认 `AllowCrossTenant=false`，当前用户只能查询自己的租户；设为 `true` 时还必须配置 `CrossTenantRole`，并由该独立角色授权访问其他指定租户。端点仍要求宿主认证中间件和网关 ACL，返回值只有 `AuditLogEntry` 元数据，不提供明细、请求正文或异常内容。查询失败、取消和未授权不会打开审计数据库连接。

## 关键操作审计

EFCore 和 FreeSQL 都提供 `SaveTransactionalAuditAsync`，要求工作单元已经有活动事务。操作不创建第二个事务，不提交或释放业务事务。

```csharp
using Bing.Auditing;
using Bing.Auditing.Persistence;
using Bing.EntityFrameworkCore.Auditing;

await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
try
{
    // 在此事务中完成库存条件扣减、业务幂等登记和 CAP 出箱。
    await dbContext.SaveTransactionalAuditAsync(
        auditingStore, // ITransactionalAuditingStore
        auditId,       // 当前租户内稳定且唯一，最长 128 字符
        new AuditLogInfo
        {
            TenantId = trustedTenantId,
            UserId = authenticatedUserId,
            ApplicationName = "Inventory",
            CorrelationId = correlationId,
            ExecutionTime = DateTime.UtcNow
        },
        cancellationToken);
    await transaction.CommitAsync(cancellationToken);
}
catch (Exception original)
{
    try
    {
        await transaction.RollbackAsync(CancellationToken.None);
    }
    catch (Exception rollbackError)
    {
        original.Data["AuditRollbackError"] = rollbackError;
    }
    throw;
}
```

FreeSQL 使用 `unitOfWork.SaveTransactionalAuditAsync(...)`，显式调用 `GetOrBeginTransaction()` 后才能保存。工作单元的 Commit/Rollback 仍遵循 FreeSQL 的同步 API，不把返回 `Task` 解释为所有数据库操作都不阻塞。

审计写入失败必须向上传播并回滚业务，禁止捕获后继续提交。唯一键是 `(TenantId, AuditId)`，重复标识会抛出数据库约束异常，不会被静默视为成功；先通过业务幂等记录确认是否已提交，避免未知事务结果下盲目重做业务。存储不会自动把整笔业务变为幂等，也不接管 CAP 事务。

## 持久化内容与资源

记录包括租户、审计 ID、应用、用户、UTC 执行时间、时长、HTTP 方法/状态、关联 ID，以及经过投影的操作和实体变更明细。默认不保存 URL、请求参数、评论、扩展属性或异常正文；异常仅记录受长度限制的类型名称。租户标识保留原始大小写、空格和 Unicode，不执行 Trim 或规范化；库存和关键审计必须使用相同的身份校验规则。

属性值默认全部排除。`AllowedPropertyNames` 按 Ordinal 比较，宿主只应加入经过评估的非敏感字段；名单不会自动发现密码或令牌，主动加入敏感字段意味着宿主扩大了采集范围。默认字段明细限制 512 字符、UTF-8 JSON 限制 65536 字节、明细总数限制 100。文本可以截断，数量或总字节超限会拒绝保存，不悄悄丢弃整条关键审计。审计 ID 和用户标识最长 128 字符且不能含首尾空白；租户标识最长 128 个字符并按原值保存，不截断或规范化标识以免混淆身份。

规则在存储创建时建立快照。JSON 只序列化选定的字符串和值，不序列化任意实体、异常对象或插件类型，也不建立静态反射缓存。存在活动 `IAuditingManager` 作用域时，EFCore 工作单元会从 `ChangeTracker` 自动收集实体变更，FreeSQL 工作单元会从 `EntityChangeReport` 收集提交后的变更通知；两者都会跳过常见敏感字段并限制值长度。两套 ORM 对实体元数据、属性读取或值格式化异常采用尽力采集：无法读取的实体或属性会跳过，不能把已提交业务误报为保存失败；宿主原有实体变化回调仍按原语义传播异常。FreeSQL 通知可能发生在物理提交后，EFCore 则在保存前构造内存快照。该自动快照用于采集内存审计明细，不能替代关键审计与业务修改同事务的显式持久化。

自有事务异常保留原始异常，清理异常附于 `Data["Bing.AuditCleanupErrors"]`；提交确认失败标记 `TransactionOutcome.Unknown`，清理失败不会替换原始错误。关键审计的事务结果由外层工作单元判断。`netstandard2.0` 下打开连接和 SQL 执行使用异步 API，事务开始、提交、回滚和 Dispose 使用同步 API。取消不能保证 SQL 未执行；写入后发现取消会继续抛出，调用方必须等待并回滚，不能提前释放活动资源。

## 模型迁移与验收边界

`EntityChangeInfo.ChangeType` 原先错误地声明成 `EntityChangeInfo`，现修正为 `EntityChangeType`。使用该成员的源码应改为 `Created`、`Updated` 或 `Deleted`；旧编译消费者和插件必须重新编译，该修正具有二进制契约影响。

当前直接测试覆盖 SQLite 持久化、安全投影、普通连接所有权、普通/关键记录保留清理，两套 ORM 的实体新增/修改/删除快照，以及同事务提交/回滚。HTTP 中间件的可选取消传递、慢存储等待和资源安全语义也有 net6/net8 直接测试。两套 Admin 的[整单库存业务](inventory-orders.md)已接入库存、关键审计和 CAP 出箱，并有真实 MySQL/RabbitMQ 定向验证；各批次结果见执行报告。该关键业务路径不自动替换普通 `IAuditingStore`。慢存储长期容量测量、冷归档、删除授权和长期预算仍需宿主交付；测试通过不扩展为完整生产验收。
