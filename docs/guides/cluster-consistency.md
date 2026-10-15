# 集群事件与运行治理使用边界

> **适用版本**：框架 `7.0.0`｜源码核对：2026-10-13

本文区分当前可用能力和仍需实施的事务、顺序及部署能力。业务正确性不能依赖进程内锁、本地事件或 Redis 租约。

## 本地事件与分布式事件

`Bing.Events` 是当前推荐的事件入口。`AddDefaultEventBus()` 只在本进程调用处理器；多副本部署时不会自动广播到所有节点。跨节点事件使用 `AddCapEventBus()` 和 `IMessageEventBus`，按业务订阅者使用稳定的消费者组。CAP 可能重复投递，消费者必须以“逻辑消费者 + 事件 ID”作为数据库唯一键，并与业务修改在同一事务提交。

`Bing.EventBus` 是另一套旧契约，不能与 `Bing.Events` 的同名接口混用。需要迁移旧 `IMessageEventBus` 的宿主可引用 `Bing.Events.EventBusAdapter`，在 `AddCapEventBus(...)` 后显式调用 `AddLegacyCapMessageEventBusAdapter()`。适配器只转发旧接口的一次发布，不自动同时调用本地总线；已有旧接口注册不会被覆盖。新消息可在 `MessageEvent` 设置 `TenantId`、`Stream`、`EntityKey`、`Sequence`、`ContractVersion` 和 `TraceId`，CAP 消息头携带这些可选元数据，接收端可用 `ClusterEventHeaders.Read(...)` 解析。带序号的事件必须同时提供业务流及实体键。

当前适配器和元数据不自动完成 CAP 订阅幂等或数据库顺序游标。需要连续序号时显式启用 [有序事件入口](ordered-events.md)，不能只设置 Sequence 消息头便宣称已保证业务顺序。`Send = false` 沿用已有事务操作登记路径；Admin 的 EFCore、FreeSQL 工作单元已在专用 MySQL/CAP/RabbitMQ 环境验证业务记录、出箱与实际消费。事务回调新增操作会在下一轮执行，单次提交最多 32 轮，超限或回调失败即停止并清除待执行回调。业务事务失败后应丢弃当前工作单元，不在同一作用域隐式重试。两套 ORM 的 `PublishEventsAsync` 默认仍为空；派生工作单元可重写该钩子执行保存后通知。通知失败时抛出 `PostCommitNotificationException`，其 `InnerException` 保留原始错误；无外部事务时本次保存已提交，外部事务仍可能随后回滚。应先确认最终事务结果，再按事件 ID 补发或补偿通知，不应无条件重放业务保存。

CAP 消息头的元数据字段有固定长度上限：事件 ID 为 128，租户、业务流和实体键各为 200，序号为 20，契约版本为 100，跟踪 ID 为 128。发送端超限抛出 `ArgumentException`，读取外部消息头超限抛出 `FormatException`；这些限制只约束元数据，不改变旧消息中缺少可选字段时的兼容读取行为。

本轮有序链路验证记录为：Admin EFCore 16/16、Admin FreeSQL 15/15，FreeSQL 有序 MySQL 集成在串行化执行后 68/68，Sample Lazy 冒烟 1/1。`OrderedEventPublisher` 通过 EFCore `IDbContextTransaction` 包装事务的 4 项 net6 定向测试已纳入映射。结果只适用于记录的环境和目标框架，不等同于蓝绿动态切流、强杀恢复或锁定 SDK 的全量验收。

有序事件的 `ReceiveAsync` 只对内置存储的原生 MySQL 1213、已确认回滚且清理无错的入箱事务做最多三次重试，四次尝试之间等待 20/40/80 ms，调用方取消会停止等待。业务处理、1205、未知提交和自定义存储不自动重试。CAP 重投与此入口重试叠加，需协调总重试预算、消费超时及事件 ID 幂等策略。七项原生错误形状测试已有 net6/net8 各 7/7 的记录，但 `SIGNAL` 注入不是实际死锁或提交断连，不能扩大验证结论。细节见[有序事件指南](ordered-events.md)。

消费端可显式使用 `EventConsumptionStore`，把稳定的逻辑消费者名称和事件 ID 写入与业务更新相同的 `DbTransaction`；返回 `false` 表示目标消费收据已存在，此时跳过业务更新。消费者名称和事件 ID 必须非空、不得包含首尾空白，长度上限分别为 200 和 128 个 Unicode 字符；非 BMP 和多字节 UTF-8 字符按原样比较，不做大小写或 Unicode 规范化。非目标唯一键、检查约束及表结构错误继续抛出，不作为重复吞掉。不能先提交收据再单独提交业务，也不能以 CAP 消息表清理代替业务收据保留策略。两套 Admin ORM 已用真实 MySQL/CAP/RabbitMQ 验证重复投递、失败回滚和重试；EFCore 另验证了并发事务竞争。

MySQL 部署前建立收据表，二进制排序规则确保事件标识按原样比较：

```sql
CREATE TABLE `bing_event_consumption` (
    `Consumer` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
    `EventId` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
    `ProcessedAtUtc` datetime(6) NOT NULL,
    PRIMARY KEY (`Consumer`, `EventId`)
);
```

仓库提供 `EventConsumptionSchema.GetCreateStatements(EventConsumptionDatabase.MySql)` 和 `deploy/migrations/events/001-event-consumption.mysql.sql`，后者同时建立按消费者、处理时间和事件 ID 排序的留存索引。SQLite 本地或测试环境可使用 `EventConsumptionSchema.GetCreateStatements(EventConsumptionDatabase.Sqlite)` 或 `deploy/migrations/events/001-event-consumption.sqlite.sql`。已有数据库必须在停写、备份和核对存量身份后执行迁移；`CREATE TABLE IF NOT EXISTS` 只覆盖首次建表，不会自动补字段、索引或排序规则。

SQLite 测试或单机部署可建立同名 `Consumer`、`EventId`、`ProcessedAtUtc` 列和 `(Consumer, EventId)` 复合主键，并使用 `EventConsumptionDatabase.Sqlite`。`TryRecordAsync` 不创建、提交或释放调用方事务；先登记、再执行业务、最后一起提交，失败时一起回滚。逻辑消费者名称在蓝绿版本间保持稳定，事件 ID 需由发布方持续提供。收据保留时间必须覆盖允许重放的期限，未确定重放上限时不要按固定天数自动删除。

确认重放期限已经结束后，可在受信运维任务中显式解析 `IEventConsumptionRetentionStore`，使用调用方拥有的事务调用 `DeleteBeforeAsync` 按消费者和 UTC 截止时间分批清理。内置实现将删除限制在 `ProcessedAtUtc` 以前的收据，并保证单批不超过 10,000 条；它不注册后台调度、不执行授权、不归档业务依据，也不清理其他消费者。清理前必须确认业务幂等记录、审计和外部副作用的重放保护仍然可用，取消或回滚后应保留收据并在后续任务中重新评估。

面向管理端或运维 API 时，建议注册 `AddBingEventConsumptionRetention<TAuthorizer>()` 并使用 `IEventConsumptionRetentionService`。该服务在低层删除前执行调用方授权，但仍沿用调用方传入的业务事务；宿主必须显式注册 `IEventConsumptionRetentionStore`，框架不会自行创建连接、开启第二事务或启动清理调度器。授权器应校验消费者、租户、重放窗口和操作角色，不能只根据截止时间放行。

注册 `AddDomainEventDispatcher(...)` 后，EFCore 工作单元会从当前 `ChangeTracker` 自动发现实现 `IHasDomainEvents` 的聚合根；FreeSQL 的 `StoreBase` 写入入口会自动登记新增、修改和删除的聚合，直接使用 FreeSQL 原生 API、原始 SQL 或自定义存储器时仍需调用 `RegisterDomainEventSource(source)`。领域事件在保存事务内按最多 32 轮分发，处理器可追加后续事件；成功保存后才移除已处理事件，失败时保留事件并要求丢弃本次工作单元。处理器不得在分发期间移除或替换已读取的事件。该自动路径拒绝已经开启的外部事务，因为工作单元不能确认外部事务的最终结果。没有注册分发器时保留原有行为，不自动清空事件。处理器内若发送 CAP 消息，应使用现有事务出箱路径；Admin 的 EFCore、FreeSQL 工作单元均已验证业务写入、领域事件、CAP 出箱和真实消费的端到端链路，以及业务失败时出箱回滚。不同逻辑消费者各自提交事务，不存在跨消费者的统一事务。

对于缓存刷新，建议用共享版本号和过期回源保持正确性。进程内事件只能通知当前节点，不应作为每个在线节点必须收到的可靠广播。

## 锁与库存

`LocalLock.TryAcquireLeaseAsync(key, waitTimeout, cancellationToken)` 支持跨线程等待与释放；旧 `LockTake` 的 `expiration` 参数仍表示等待超时。锁状态在持有者和等待者全部退出后回收。

`CSRedisDistributedLock.TryAcquireLeaseAsync(key, waitTimeout, leaseDuration, cancellationToken)` 分别表达等待与租期。获取采用 Redis 原子 `SET NX PX`，释放时比较持有者后删除；旧 `LockTake` 的 `expiration` 是租期。建议用 `await using` 管理异步租约。租期到期或 Redis 故障切换后，不能继续假定锁独占。

旧的 `LockExtensions.LockTak/LockTakAsync` 便捷入口现在为每次成功获取生成唯一持有者，并在当前异步上下文关联 `LockRelease/LockReleaseAsync`；同一父异步流派生的并发任务会隔离各自的持有者记录，但不能把无参数释放入口转移到无关异步流。需要跨线程、跨作用域或明确所有权时，优先使用 `ILockLeaseProvider` 的 `TryAcquireLeaseAsync` 并通过 `ILockLease` 释放。自定义 `ILock`/`IDistributedLock` 无需实现租约接口，未提供租约能力时调用扩展会明确抛出 `NotSupportedException`。

`ExecuteWithLock*` 中，调用方提供的持有者值会用于获取和释放；`LockExtensions.ExecuteWithLock*` 每次调用使用唯一持有者并自动释放。`LockTak/LockRelease` 通过当前异步上下文关联同一次获取和释放，需要跨流转移所有权时应改用显式 `ILockLease`。

`Bing.Data.Inventory.DbInventoryStore` 已提供 ORM 无关的基础扣减：在调用方事务内以租户和请求 ID 唯一键登记请求，使用租户、仓库、SKU 及余额条件更新，并检查受影响行数。`InventoryBatchOperations` 使用结构化二元请求键，按租户、仓库、SKU 的 Ordinal 顺序执行，允许不同租户复用请求 ID。两套 ORM 提供活动事务适配；两套 Admin 新增[整单库存业务](inventory-orders.md)，同事务维护首次结果、关键审计和 CAP 出箱，后项不足回滚之前所有明细。真实双进程竞争、原生 1213 有界重试、COMMIT 确认丢失及原生 CAP 后台出箱恢复已有双 ORM 证据；其余组合故障与历史偶发失败观察仍需分别收口。

## 限流

本地限流按进程独立计额，适合单机或每实例额度；Redis 限流使用服务端时间和原子脚本，共享同一策略及分桶额度。两者均有最大键数与闲置回收限制，超额立即拒绝，不排队。示例：

```csharp
services.AddBingLocalRateLimiting(100, 10, TimeSpan.FromMinutes(10), 10000);
// 集群场景改用：
// services.AddBingRedisRateLimiting("api", 100, 10, TimeSpan.FromMinutes(10), 10000);
// services.Configure<BingRateLimitOptions>(options => options.PolicyName = "api");

app.UseRouting();
app.UseAuthentication();
app.UseBingRateLimiting();
```

HTTP 中间件按策略、稳定路由和认证主体分桶；匿名主体使用 `Connection.RemoteIpAddress`。代理部署必须先配置 `ForwardedHeaders` 的受信代理名单，不要在限流前使用无代理校验的 `UseRealIp()`。主体包含多个 `ClaimsIdentity` 时，默认按顺序选择第一个 `IsAuthenticated` 的身份，并从该身份同时读取用户和租户声明；未认证身份的声明不会参与分桶，主身份未认证时继续查找后续第一个已认证身份。可通过 `TenantClaimType` 和 `IncludeTenantInKey` 调整租户声明；`UserIdClaimType` 默认是 `ClaimTypes.NameIdentifier`，保留原始 JWT 声明的宿主可设为 `sub`。`KeySelector` 可以完全接管主体键，其返回值的信任责任属于宿主。

默认带租户主体对租户和用户/IP 身份分别增加长度前缀，避免标识内的 `|user:` 等字符合并不同分桶。此前带租户的默认键会改变；蓝绿期间必须协调切换，旧实例和新实例的两种键不能保证共享额度。旧桶按已有闲置回收/TTL 淘汰，新桶会获得初始额度，不是迁移保留原剩余额度。无租户和自定义 `KeySelector` 格式保持不变；宿主需要连续额度时应先统一所有实例的键协议和发布策略。

匹配路由默认使用模板，未匹配请求共享 `unmatched` 分桶；`RouteKeySelector` 可以显式提供稳定业务路由。随机 URL 不再创建独立分桶。超额返回 429 与 `Retry-After`；限流器异常返回 503；请求取消继续传播。Redis 脚本在容量达到上限时至多每秒遍历一次该策略的分桶，并返回最早可回收分桶的实际等待时间，`maxKeys` 应按实际容量配置。`MaxKeyLength` 必须至少为 32 个字符；组合键超过该配置时使用保留 `sha256:` 前缀且长度受限的 SHA-256 摘要，摘要不降低身份数量，不能代替容量限制。基础设施异常默认拒绝并记录日志；只有显式配置 `FailOpenOnInfrastructureError` 才降级放行。调用方在 Redis 操作开始前取消时不会访问 Redis；如果取消发生在 Redis 已完成原子扣减之后，令牌不会被伪造回滚，业务应按幂等请求处理重试。

Admin EFCore/FreeSQL 宿主通过 `RateLimiting:Enabled` 显式启用共享 Redis 策略，默认关闭，保持旧宿主行为。两个宿主使用稳定策略名 `Bing.Admin`，蓝绿版本应使用相同容量及补充参数；不一致的 Redis 策略配置会拒绝请求。配置示例：

```json
{
  "RateLimiting": {
    "Enabled": true,
    "Capacity": 100,
    "RefillTokensPerSecond": 10,
    "IdleTimeoutSeconds": 600,
    "MaxKeys": 10000,
    "KnownProxies": ["10.0.0.5"]
  }
}
```

启用时容量、补充率、闲置时间和最大键数必须显式提供有效值；`KnownProxies` 可省略，此时使用连接地址且不处理转发头。若确需使用 `UseRealIp`，必须在 `RealIpOptions.TrustedProxies` 中填写部署中真实代理的 IP；空名单会拒绝所有转发头，解析时从转发链右侧跳过已知代理并取第一个非可信地址。不要复制示例地址，也不要仅凭请求头放行。旧 EFCore Admin 的无条件 `UseRealIp()` 已移除；需要代理客户端地址的部署应迁移到 ASP.NET Core 受信 `ForwardedHeaders` 或显式 `TrustedProxies`。Redis 初始化仍复用宿主的 `CacheModule`。两个独立 `dotnet` 进程共享同一策略和分桶额度的本机 Redis 验证已经完成；生产 Redis 断连、恢复、主从切换以及真实 HTTP 认证/可信代理链仍需另行验收。

## 有序消费指标

`AddBingOrderedConsumer` 会注册按逻辑消费者隔离的 `IOrderedEventMetrics` 快照，记录待处理流数、待处理事件数、最老积压年龄、成功、缺口、失败、重试、在途数、最近处理耗时和 `ScanFailureCount`。主扫描、全局积压统计或最老年龄统计失败时不会把对应旧数值伪装成零：主扫描失败保留上一份积压快照，单项统计失败只保留对应的全局数量或年龄，并递增 `ScanFailureCount`；监控必须把该计数作为数据新鲜度信号。内置数据库存储实现可选的 `IOrderedEventStoreStatistics` 后，`PendingEvents` 表示当前消费者全局待处理事件数；实现 `IOrderedEventStorePendingAgeStatistics` 后，`OldestPendingEventAge` 表示基于收件箱 `CreatedAtUtc` 的年龄。旧自定义存储没有这些契约时分别回退为最近一页流数或空年龄，缺少年龄时 Meter 不发布负数哨兵值。快照不包含事件 ID、实体键等高基数标签；当前只提供进程内读取，生产环境仍需接入统一指标平台，并为扫描失败、最老事件告警、消息留存和归档配置容量策略。消费者页面内按 `maxConcurrency` 补充空闲槽位，页面全部完成后再推进稳定分页键；连续满页达到 `maxPagesPerScan`（默认 16）后强制回绕到首页，避免较小的新流在持续积压时长期等待。该预算只改变扫描游标，不终止不响应取消的处理器；宿主仍需等待真实排空完成。

`PendingStreams` 仍是最近一页扫描发现的流数；只有内置统计存储提供的 `PendingEvents` 才能解释为当前事务快照下的全局事件积压。`RetryCount` 只统计恢复事务已确认提交的次数；提交结果未知不增加，已确认提交后清理失败仍增加并传播清理异常。Worker 的在途计数在作用域创建、处理器解析、执行和异步释放失败时均会退出，不把解析失败遗留为活动事件。

## 审计

`services.AddBingAuditing()` 注册作用域审计管理器及默认日志存储。`IAuditingManager.BeginScope()` 支持嵌套异步作用域，`SaveAsync()` 对同一作用域只发起一次保存。可能与异步保存并发退出的调用方应使用 `await using var scope = auditingManager.BeginAsyncScope()`；释放会等待已启动的保存并传播保存失败。默认日志只写状态与关联标识，不记录完整请求体、参数或异常详情。

`Bing.AspNetCore` 已提供可选 `AddBingHttpAuditing` / `UseBingHttpAuditing`，只采集请求元数据和异常，不读取请求正文；默认审计存储仍为日志存储。显式 `AddBingDatabaseAuditing` 可以启用 MySQL/SQLite 持久化，EFCore/FreeSQL 的 `SaveTransactionalAuditAsync` 可以复用业务事务写入关键审计。审计写入失败时必须回滚业务，普通请求的独立保存不能替代同事务关键审计。默认不保存参数、异常正文和未列白名单的属性值，规则和迁移说明见[数据库审计指南](auditing-persistence.md)。

Admin 的普通请求数据库审计通过 `Auditing:DatabaseEnabled=true` 显式启用，连接取自 `ConnectionStrings:DefaultConnection`；默认关闭时不会创建数据库连接。宿主注册阶段会拒绝缺少连接字符串的配置，并沿用白名单、字段长度、负载大小和明细数量限制。启用前需要执行审计表迁移，生产保留、归档和清理不能由应用启动隐式完成。

EFCore 会在保存前填充实体审计字段，FreeSQL 的 `StoreBase` 也会在注册 `IAuditPropertySetter` 后执行字段设置；两套 ORM 已接入实体变更自动快照。普通审计已提供数据库落库、按批次留存、归档和安全元数据查询入口，并在隔离 MySQL 中验证了单实例 SQL 路径；生产环境仍需配置保留窗口、备份恢复、跨节点清理协调和容量告警。整单库存入口已在两套 Admin 工作单元中组合库存、首次结果、关键审计和 CAP 出箱，并有真实 MySQL/RabbitMQ 定向证据；各批次验证范围分别记录，不能将 SQLite 或定向通过等同于完整集群验收。

HTTP 审计的 `BingHttpAuditingOptions.SaveTimeout` 默认无限等待。有限预算到期只收集超时诊断，仍等待实际保存结束后释放作用域；不会强制取消存储、把保存移交后台或缩短请求的最大等待时间。日志及可选故障传播在保存和释放完成后执行。没有原始业务异常且开启 `ThrowOnStoreFailure` 时，迟到保存即使成功仍会传播超时；有原始业务异常时保留它，并附加清理异常。蓝绿停止仍须等待活动保存完成，再释放根容器和数据库资源，预算到期不能作为排空完成证明。

## HTTP 排空与就绪

`Bing.AspNetCore.Hosting` 提供可选的 `AddBingTrafficGate` / `UseBingTrafficGate`。它维护在途 HTTP 请求计数，排空后拒绝新请求并让 `/health/ready` 返回非 200；宿主停止时等待配置的超时时间。超时只结束等待，不强行终止仍在执行的请求，也不代表 CAP 消费或定时任务已经排空。

有序消费者使用自己的 `BackgroundService.StopAsync` 排空协议。处理器忽略停止令牌时会抛出带有 `Bing.OrderedEventConsumerDrainIncomplete` 标记的取消异常，并保留活动作用域；调用方需要在处理器结束后再次使用未取消令牌等待消费者退出，不能先释放根容器。该协议只覆盖 Bing 有序消费者，不覆盖 CAP 原生回调、Hangfire、SSE/WebSocket 或脱离请求的后台任务。

部署编排可以解析 `IEnumerable<IOrderedEventConsumerRuntime>`，按稳定的 `ConsumerName` 选择消费者，先调用 `BeginDrain()` 停止新扫描，再调用 `WaitForDrainAsync(...)` 等待 `IsRunning=false` 且 `InFlightCount=0`。返回 `false` 或收到排空未完成异常时只表示等待尚未完成，不能切换业务身份、删除旧作用域或释放根容器；CAP、Hangfire 和其他后台任务仍需各自排空。

## 蓝绿发布

[部署模板](../../deploy/bluegreen/README.md)提供 Compose/Nginx 和 Kubernetes 两套切流脚本及静态检查。当前 Admin 已接入 `/health/ready` 和 HTTP 流量闸门，但消费者/定时任务角色开关及动态切流、排空和回滚仍未验收，模板不能直接作为可发布镜像的证据。HTTP 和 CAP 消费者必须分别切换；绿版 Web 预热期间不得参与生产消费。数据库先扩展再收缩，消息契约需要新旧版本共存。

两 Admin 的 RabbitMQ 凭据需由部署配置提供，Dashboard 默认关闭；EFCore 的旧完整请求响应采集默认关闭。配置项和兼容迁移见[集群宿主配置](cluster-host-configuration.md)，不会因模板环境变量存在而自动获得消费角色隔离。

## 待实施闭环

1. 两套 ORM 已具备事务内领域事件分发、显式 CAP 出箱和可选业务消费收据；跨消费者的业务补偿策略仍需按具体流程设计。
2. 有序事件序号、收件箱、游标、保存点失败标记、失败重试及后台入口已有；继续完成生产宿主 CAP 接线、并发/断连恢复验收、授权补偿与容量策略。测试宿主的真实 CAP 链路不等于生产接线。
3. 两套 Admin 已验收整单库存、唯一请求、关键审计及 CAP 同事务组合，包含真实 MySQL 双进程竞争、原生 1213 有界重试、COMMIT 确认丢失后的原请求恢复及 CAP 后台出箱扫描。各批次证据见[库存指南](inventory-orders.md)；剩余组合故障和历史偶发失败观察不能由这些通过结果自动关闭。
4. HTTP 审计、数据库安全投影、两 ORM 关键库存事务审计和用途驱动原子闸门已有；自动实体变更快照已接入两套 ORM，HTTP 审计提供可选 `SaveTimeout` 等待预算并在预算耗尽后继续等待实际保存；普通请求数据库审计、留存和归档已有实现，生产仍需完成保留窗口、备份恢复、跨节点协调和长期容量验证。
5. Admin HTTP 就绪/排空已接线；继续完成宿主角色开关、真正依赖预热、消费/定时任务排空和两种部署环境的动态切流与回滚。

进程强制终止或断电时，托管清理无法保证执行。恢复必须依靠数据库事务、CAP 出箱、业务幂等和可追踪的待处理状态。

## 事务结果诊断

`exception.GetTransactionOutcome()` 返回 Committed、RolledBack 或 Unknown，保留原始异常类型。已提交后的 CAP Flush 或清理失败不能当成业务回滚；未知结果先查幂等记录。失败后丢弃工作单元，按事件 ID 补偿通知，不直接重做业务。清理错误保留在聚合异常或异常 Data 中。

有序事件业务失败时，协调器保持游标锁，在同一事务内回滚业务保存点并提交失败标记。`Committed` 仅表示外层物理事务已提交；当 `Bing.OrderedEventBusinessRolledBack=true` 且 `Bing.OrderedEventFailureRecorded=true` 时，提交的是失败标记，不是业务写入。提交结果 `Unknown` 时，即使没有 `FailureRecorded` 标志也要查询实际持久状态；取消不写失败标记。原业务异常继续抛出，标记、回滚、释放的异常附于 `Data`。保存点不能清除 ORM 跟踪状态、已登记事务回调或 CAP 缓冲，失败后必须丢弃当前业务上下文。详情见[有序事件指南](ordered-events.md)。

## CAP 宿主启动与停止

使用 `AddCapEventBus` 时，Bing 等待 CAP 的 `BootstrapAsync` 结束后才完成宿主启动。CAP 8.0.1 的原生消费者在启动中收到取消时可能释放取消源，随后启动结束又重置释放状态；Bing 的宿主衔接把停止延后到启动任务结束，防止正常宿主停止进入这个窗口。实现基于原生公开启动契约，不修改 CAP 版本或容器。

启动请求取消后仍等待安全关闭；停止请求的等待令牌取消时，停止任务继续执行，不把等待超时标成清理完成。调用方应再次等待停止完成后才释放根容器；直接在启动/清理仍进行时释放容器超出此保障。同步和异步释放共用一次关闭任务，启动原始异常与关闭异常分别保留。

此机制只证明启动方法和停止方法完成。CAP 原生处理器的监听任务及业务在途请求另有生命周期，不能据此宣称已实现蓝绿排空；RabbitMQ 主题绑定仍通过真实消息探针确认。直接调用原生 AddCap 或消费者 Start 不受 Bing 宿主衔接保护，底层行为特征测试继续保留。

Admin MySQL/CAP 验收脚本在测试运行失败后保留已通过的连接预检、失败项目、测试退出码和实际生成的 TRX；脚本错误不再写成环境未配置。测试退出码为0但 TRX 未达门槛时，诊断仍标记失败，不能以“命令成功”代替全数通过。

## CAP 消费取消迁移

需要取消后持久化重试的宿主，在 `AddCapEventBus(...)` 后显式调用 `EnableBingCapConsumerCancellationRetry()`。CAP 8.0.1 原生执行器对取消异常的处理不能作为失败重试保证；Bing 适配器保留原取消异常并转入普通失败路径。两个 Admin 已在 `CapModule` 显式启用，基础 `AddCapEventBus` 不隐式修改外部宿主的行为。

宿主自动排空默认无限等待；需要有限停止预算时可使用重载配置 `BingCapConsumerOptions.DrainTimeout`。预算到期会报告 `Bing.CapConsumerDrainIncomplete`，但只结束关闭等待，不强制终止仍在执行的消费回调、事务或作用域。业务结束后应使用未取消令牌重新等待实际关闭完成；该配置不覆盖 Hangfire、定时任务或 CAP 原生调用器的排空。

已有过滤器处理取消时保持其结果；未处理的取消才转换。原生自定义执行器应继承 `BingSubscribeInvoker`，保留 `GetInstance` 等扩展点；未知工厂不会被覆盖或为检测而执行。接口派发、启用规则和完整示例见 [Bing.Events README](../../framework/src/Bing.Events/README.md#消费取消的显式失败重试)。

同事务收据、业务提交与 CAP 的接收状态是独立证据：提交前取消须回滚业务和收据；提交后取消须用原事件收据阻止重复写入。CAP 原接收记录失败后重试不等于 RabbitMQ broker 重新入队，也不证明宿主停止时所有消费已排空。真实验证结果以本任务执行报告为准，不能将测试清理辅助器作为生产关闭框架。
