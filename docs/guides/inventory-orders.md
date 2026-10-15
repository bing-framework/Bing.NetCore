# 整单库存、关键审计与 CAP 出箱

> **适用版本**：框架 `7.0.0`｜源码核对：2026-10-13

`Bing.Data.Inventory` 提供整单结果存储；两套 Admin 数据适配提供 `AdminInventoryService`，把库存扣减、整单首次结果、关键审计和 CAP 出箱放入同一个业务事务。库存正确性来自数据库条件更新及唯一约束，Redis 锁不作为必要条件。

## 注册和部署

EFCore Admin 和 FreeSQL Admin 均可在已有 CAP 配置上显式启用：

```json
{
  "Inventory": {
    "Enabled": true
  }
}
```

默认关闭。RabbitMQ 及数据库仍使用对应宿主的受控配置，见[集群宿主配置](cluster-host-configuration.md)。自定义宿主可以调用 `services.AddAdminInventory()`，但必须同时注册该 ORM 的真实 Admin 工作单元、事务操作管理器、CAP 和消息总线。两套适配使用相同命名空间，只选择一种数据实现，不在同一宿主混用。

首次建表由迁移执行以下脚本返回的 SQL：

- `InventorySchema.GetCreateStatements(InventoryDatabase.MySql)`：库存和单项请求表。
- `InventoryOrderSchema.GetCreateStatements(InventoryDatabase.MySql)`：整单首次结果表和可选有序完成投影表。
- `AuditLogSchema.GetCreateStatements(AuditLogDatabase.MySql)`：关键审计表。

CAP 出箱继续使用 CAP 自己的消息表，不再建立第二套出箱。注册服务不会创建或修改数据库。

`InventorySchema.GetCreateStatements(InventoryDatabase.MySql)` 为 `TenantId`、`WarehouseId` 和 `RequestId` 创建 `VARBINARY(512)`，为 `Sku` 创建 `VARBINARY(1024)`，并显式使用 `ENGINE=InnoDB` 与 `utf8mb4`。SQLite 对这些身份列使用 `TEXT COLLATE BINARY`。`DbInventoryStore` 在 MySQL 绑定身份时使用严格 UTF-8 字节，读取 `byte[]` 后无损解码；`InventoryDeductionRequest` 仍按 UTF-16 长度限制租户、仓库和请求标识为 128、SKU 为 256，保留大小写、尾空格和组合字符原值，并拒绝无效代理字符。

`CREATE TABLE IF NOT EXISTS` 只适用于首次建表，不会升级已有字段、索引或排序规则。已有 MySQL `bing_inventory_stock` 和 `bing_inventory_request` 表必须先备份并核对存量身份，再人工执行[精确身份迁移脚本](../../deploy/migrations/inventory/002-exact-identities.mysql.sql)。迁移前必须暂停所有旧版本和新版本的写入、消费及后台任务；脚本先执行 `CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_bin` 并切换到 InnoDB，再改为 `VARBINARY`。MySQL DDL 不具备事务回滚语义，脚本不承诺一键回滚；必须保持维护状态，直到两张表迁移完成且所有实例升级，不能让新旧 schema 混跑。旧排序规则已经合并的历史身份无法由迁移恢复，发现此类数据应先完成业务核对。

## 调用和作用域

每次操作使用新的服务作用域。租户、用户和跟踪 ID 来自经过授权的调用上下文，不能直接信任请求正文中的租户标识。

```csharp
using Bing.Admin.Data.Inventory;
using Bing.Data.Inventory;
using Microsoft.Extensions.DependencyInjection;

await using var operationScope = scopeFactory.CreateAsyncScope();
var inventory = operationScope.ServiceProvider.GetRequiredService<AdminInventoryService>();
var request = new InventoryOrderRequest(trustedTenantId, warehouseId, businessRequestId,
    new[]
    {
        new InventoryOrderLine("sku-a", 3),
        new InventoryOrderLine("sku-b", 2)
    });
var result = await inventory.ExecuteAsync(request, authenticatedUserId, traceId, cancellationToken);
```

`scopeFactory` 是 `IServiceScopeFactory`；每次操作结束等待作用域释放。订单存储默认注册为 Scoped，复用本作用域的 `IInventoryStore`，允许调用方提前注册自定义 Scoped 存储。自定义注册通过 `TryAdd` 保留，调用方必须保证数据库语法和事务一致；不要自行把订单存储注册为会捕获 Scoped 依赖的单例。

`AdminInventoryService` 每个实例只执行一次操作，拒绝已有待提交事务回调的工作单元。不要在操作中混入其他业务保存、外部事务或额外 `CommitAsync`。失败后丢弃该工作单元和作用域，不在同一实例上隐式重试。

推荐从宿主解析 `AdminInventoryOrderExecutor`，由它为每次尝试创建新的作用域，并在返回结果或开始下一次尝试前等待异步释放：

```csharp
var executor = host.Services.GetRequiredService<AdminInventoryOrderExecutor>();
var result = await executor.ExecuteAsync(request, authenticatedUserId, traceId, cancellationToken);
```

执行器只持有作用域工厂，不持有工作单元或 Scoped 库存服务。它最多尝试四次，重试间隔为 20、40、80 毫秒；仅原生 MySQL 1213、事务明确 `RolledBack` 且事务与作用域清理均无错误时可重试。锁等待超时、未知提交结果、调用方取消及清理失败不自动重试。

## 首次结果与失败语义

整单身份是 `(TenantId, OrderId)`；内容包含仓库、SKU 和数量。明细建立不可变快照并按 SKU 的 Ordinal 顺序执行，最多 1000 项，数量必须为正数，SKU 不可重复。相同身份不能用于不同内容；明细输入顺序不同仍视为同一请求。

- 成功返回 `Applied`。重复请求返回 `Duplicate = true` 及首次扣减后的各 SKU 余额，不重新扣减，不再次写审计和出箱。
- 任一 SKU 不足，数据库保存点回滚本整单之前已扣减的所有明细，并提交 `Insufficient` 首次结果、关键审计和完成事件。
- 补货后重复原请求仍返回首次不足结果。新的业务尝试需要新的、经过业务批准的请求身份；这与未知提交后的恢复不同。
- 审计、出箱、配置或数据库异常以及执行中取消向上传播；工作单元负责整笔回滚和清理。取消不代表 SQL 没有执行，必须等待操作结束再释放资源。

整单内部单项请求 ID 使用 `order-` 加稳定摘要。此命名空间保留给整单存储，不要通过单项接口构造或复用这些内部身份。撤销、返还、部分发货与库存预占需要独立业务规则，本入口不自动实现。

兼容完成事件主题固定为 `bing.admin.inventory.completed`，内容包含租户、仓库、订单 ID、结果、SKU/数量和首次余额，同时携带可选跟踪信息；该主题没有连续序号，不能把它视为按实体有序处理。启用 `OrderedEvents:Enabled` 后，库存事务还会发布
`bing.admin.inventory.completed.ordered.v1`，按 `inventory-order` 流和订单 ID 分配序号。只有同时启用库存、Consumer 角色和 CAP 消费开关时，Admin 才启动
`admin-inventory-v1` 有序消费者；处理器只写入 `bing_inventory_ordered_projection`，不重复扣减库存。CAP 至少一次投递，业务消费者仍需以稳定消费者及事件 ID 在业务事务内登记收据。
Admin 库存有序处理器在 JSON 解析前限制正文为最多 65536 个 UTF-8 字节，超过上限的消息会被拒绝并保留 CAP 重试/失败语义；通用有序事件不自动套用该业务上限。

## 未知提交后的只读查询

`TransactionOutcome.Unknown` 表示无法确认数据库提交结果，不能换订单 ID 重做。待原操作结束后，用权威写库的独立连接和原始请求内容查询；不要用存在复制延迟的只读副本判断是否可以重试。

```csharp
await using var queryScope = scopeFactory.CreateAsyncScope();
var orders = queryScope.ServiceProvider.GetRequiredService<IInventoryOrderStore>();
await using var queryConnection = new MySqlConnector.MySqlConnection(connectionString);
await queryConnection.OpenAsync(cancellationToken);
var original = await orders.FindAsync(queryConnection, request, cancellationToken);
```

该查询不登记请求、不扣库存、不写审计或出箱。已有结果时返回首次结果；请求内容不同则报冲突。`null` 只表示本次查询没有看到结果，不证明原事务已经回滚、操作已经停止或可以立即重做。业务恢复需结合原操作终态和权威持久状态。

直接调用 `AdminInventoryService` 不会自动重试；`AdminInventoryOrderExecutor` 提供上述有界死锁重试。未知结果可显式选择 `ExecuteWithRecoveryAsync`，其回调收到同一个原始请求，只负责通过独立连接查询原结果。回调返回 `null` 或抛异常时保留原始失败，不重新扣减；查询失败附于 `Bing.InventoryRecoveryException`。

业务成功但作用域释放失败时，仍抛释放异常，以 `Committed` 标明事务结果，并将结果附于 `Bing.InventoryCommittedResult`。查询恢复确认已提交但仍有清理错误时，原异常继续抛出，恢复结果附于 `Bing.InventoryRecoveredResult`；作用域清理错误附于 `Bing.InventoryScopeCleanupException`。调用方必须分别处理业务提交和资源清理，不能因恢复成功忽略释放失败。清理尚未结束时，执行器不会返回成功，也不会启动下一轮；不以超时强制释放活动资源。

## 审计与验收边界

关键审计使用稳定的整单审计身份，与库存和 CAP 出箱同事务提交。审计写入失败会回滚整单。默认只记录安全投影的业务/实体元数据，不自动采集任意实体或请求正文；普通 HTTP 审计仍需另行配置可靠存储，见[数据库审计指南](auditing-persistence.md)。

SQLite 直接测试覆盖身份、首次结果、保存点不足回滚、异常、取消及 Scoped 依赖隔离。`InventoryIdentityMySqlTest` 使用随机专用测试库验证四类身份的大小写、尾空格、组合字符和非 BMP 字符，以及 latin1/MyISAM 旧表迁移后的首次收据和回滚；需启用 MySQL 门控、`ALLOW_DATABASE_RESET_FOR_TESTS=true`，并让专用测试账号拥有该随机测试库命名空间的创建/清理权限，不对生产账号授权。真实 MySQL/RabbitMQ 两套 Admin 测试另行验证完整事务、独立作用域及双进程竞争。真实死锁、提交确认丢失与出箱后台恢复的本批证据见下文；历史 FreeSQL 偶发竞争失败根因及长期容量仍需单独验收，不能用有序消费者双进程或一次内存变化替代。


## 提交确认丢失与出箱恢复

两套 Admin 测试使用只连接本机专用 MySQL 的协议代理：数据库返回真实 COMMIT 成功报文后，代理丢弃确认并断连。数据库已经提交，但调用方收到的异常仍标明 `Unknown`。测试通过独立权威写库连接查询原始请求，核对首次余额、订单、审计和出箱数量；恢复不重新扣库存，也不隐藏断连后发生的清理错误。

确认丢失可能导致 CAP 事务的即时发送缓冲没有完成刷新。业务数据和出箱已同事务持久化，不需要换订单 ID 或手工重新发布目标消息。后台恢复测试在停止故障宿主后先确认原出箱仍为 `Scheduled`，再启动直连写库的独立 CAP 宿主，等待原消息 ID 变为 `Succeeded`，并验证正常订阅者实际收到 RabbitMQ 消息。

测试恢复宿主使用 `FailedRetryInterval = 1` 和 `FallbackWindowLookbackSeconds = 5` 加速扫描；这些值用于隔离测试。生产回看窗口应覆盖正常事务时长和运行延迟，避免过早扫描尚在正常发送的消息。CAP 仍是至少一次投递，业务消费者必须在自身事务内登记幂等收据；原出箱只有一行不代表只会收到一次。

故障代理关闭 TLS、压缩和连接池，并允许测试标记所需的用户变量；此证据验证提交确认丢失，不覆盖生产 TLS、网络故障切换或所有取消/释放组合。对应直接方法为 `InventoryOrder_WhenCommitAcknowledgementIsLost_ShouldRecoverOriginalCommittedResult`、`InventoryOrder_WhenCommitAcknowledgementIsLost_ShouldPreserveRecoveryAndCleanupFailures`、`InventoryOrder_WhenCommitAcknowledgementIsLost_ShouldRecoverOutboxThroughBackgroundScan`。各批 TRX 和完整门控结果以任务执行记录为准。

## 真实死锁验收

两套 Admin 测试通过独立数据库事务按相反行锁顺序制造实际 InnoDB 死锁。库存事务收到原生 1213 后，由真实工作单元回滚，执行器等待作用域释放并创建新工作单元；一次故障可在下一轮成功，持续故障在四次上限停止。

对应方法为 `InventoryOrder_WhenInnoDbDeadlocks_ShouldAwaitCleanupAndRetryFreshScope` 和 `InventoryOrder_WhenInnoDbDeadlocksPersist_ShouldStopAfterFourCleanAttempts`。断言包含工作单元及事务身份、释放次数、`RolledBack`、库存、整单、关键审计和 CAP 出箱；不以 `SIGNAL` 或构造异常代替真实死锁。死锁专项两套各2/2，对应批次完整门控29/28。加入提交确认丢失与后台恢复后，最新完整 MySQL/CAP 门控32/32及31/31，两套各19个必需方法全部通过，实际SDK8.0.425。详细批次和后续变更以任务 execution.md 为准，不能把本批通过推断为所有提交故障均已验证。
