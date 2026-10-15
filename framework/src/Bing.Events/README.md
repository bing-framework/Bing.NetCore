# Bing.Events
[![NuGet](https://img.shields.io/nuget/v/Bing.Events.svg)](https://www.nuget.org/packages/Bing.Events/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Events.svg)](https://www.nuget.org/packages/Bing.Events/) ![TFM](https://img.shields.io/badge/TFM-net6.0-blue.svg)

> 事件总线：本地事件 + 基于 DotNetCore.CAP 的分布式事件。
>
> **目标框架**：`net6.0` ｜ **分层**：横切 / 事件 ｜ **当前版本**：7.0.0 ｜ **源文件**：11
>
> **上游 Bing 包**：`Bing.Aop.AspectCore`、`Bing.Data`
>
> **三方依赖**：`DotNetCore.CAP`

---

## 这是什么

统一的事件总线抽象，两层发布：

- `ISimpleEventBus` / `IEventBus` —— 本地事件（进程内）；
- `IMessageEventBus` —— 分布式集成事件（基于 **DotNetCore.CAP 8.0.1**）；具体业务库与 Outbox 同事务仍需按宿主验证。

订阅端用 `ICapSubscribe` + **`[EventHandler]`**（框架特性，继承 CAP 的 `TopicAttribute`），**不是** `[CapSubscribe]`。

## 安装

```bash
dotnet add package Bing.Events
```

## 快速上手

```csharp
// 注册
services.AddCapEventBus(o =>
{
    o.UseEntityFramework<AdminUnitOfWork>();   // 出箱表与业务同库同事务
    o.UseRabbitMQ(x => { x.HostName = "localhost"; /* ... */ });
    o.Version = "admin";
    o.FailedRetryCount = 5;
});
// 或进程内：services.AddDefaultEventBus();

// 发布
await messageEventBus.PublishAsync(new OrderCreatedEvent(order.Id));

// 订阅
public class OrderHandler : ICapSubscribe
{
    [EventHandler("order.created")]
    public async Task HandleAsync(OrderCreatedEvent e) { /* ... */ }
}
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddDefaultEventBus()` | `Bing.Events.Default` | 进程内事件总线 |
| `AddCapEventBus(Action<CapOptions>, Action<CapBuilder> = null)` | `Bing.Events.Cap` | CAP 分布式事件总线 |

`AddCapEventBus` 将 CAP 宿主启动衔接到 `IBootstrapper.BootstrapAsync`：`Host.StartAsync` 等待该启动流程结束，停止在启动结束后执行，避免 CAP 8.0.1 启动期间取消造成取消源提前释放。重复注册不会增加第二个 CAP 宿主；其他 HostedService 保留原顺序。

蓝绿 Web 角色可以在完成 `AddCapEventBus` 后调用 `services.EnableBingCapPublisherOnly()`。该入口只移除 CAP 原生消费者注册和接收重试处理器，保留 Dispatcher、存储初始化和出箱发布；消费者必须由使用相同逻辑组名的独立工作进程运行。Admin 宿主的 `BING_HOST_ROLE=consumer` 会跳过 HTTP 业务模块和 EFCore Hangfire，并通过 Generic Host 启动模块生命周期，但不能替代完整的消费者镜像、就绪探针和排空协议。未调用时保持完整发布与消费行为，不能把 `BING_CAP_CONSUMER_ENABLED=false` 之外的环境变量当作自动角色隔离。

停止超时只结束调用方等待，清理任务继续运行。应重新等待 `StopAsync` 完成后再释放根容器；不响应取消的处理器仍可能延长清理。该衔接不等于 RabbitMQ 订阅就绪或 CAP 在途业务全部排空，仍需就绪探针及业务排空协议。直接使用原生 `AddCap`、`IBootstrapper` 或 `IConsumerRegister.Start` 不经过此宿主衔接。

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `IEventBus` / `ISimpleEventBus` / `IMessageEventBus` | `Bing.Events` / `.Messages` | 总线契约 |
| `[EventHandler]` | `Bing.Events` | 订阅特性（继承自 CAP `TopicAttribute`） |
| `IEventConsumptionStore` / `EventConsumptionStore` | `Bing.Events.Consumption` | 在业务事务内按消费者和事件 ID 登记首次消费 |
| `IEventConsumptionRetentionStore` / `IEventConsumptionRetentionService` | `Bing.Events.Consumption` | 按明确留存窗口分批清理消费收据，服务入口先执行授权 |
| `EventConsumptionSchema` | `Bing.Events.Consumption` | 获取 MySQL/SQLite 收据表和索引建表脚本 |

## 与其它包的关系

**本包依赖**：`Bing.Aop.AspectCore`、`Bing.Data`

**三方 NuGet**：`DotNetCore.CAP`

## 注意事项

仓库里还有一套遗留的 `Bing.EventBus`（命名空间 `Bing.EventBus`），它与本包都有 `IEventBus` / `IMessageEventBus`，同时 using 会歧义。新代码请使用 `Bing.Events`。必须保留旧 `IMessageEventBus` 的宿主可以引用 `Bing.Events.EventBusAdapter`，在注册 CAP 后显式调用 `services.AddLegacyCapMessageEventBusAdapter()`；适配器不自动双派发。

`MessageEvent` 的可选 `TenantId`、`Stream`、`EntityKey`、`Sequence`、`ContractVersion` 和 `TraceId` 经 `ClusterEventHeaders` 写入 CAP 消息头。有序事件需要正序号、业务流和实体键。该元数据不提供序号分配或顺序保证。

为限制不受信任消息头的大小，事件 ID、租户、业务流和实体键分别最多 128、200、200、200 个字符，序号最多 20 个字符，契约版本最多 100 个字符，跟踪 ID 最多 128 个字符。发送端超限抛出 `ArgumentException`，读取远端消息头超限抛出 `FormatException`；未携带这些可选字段的旧消息仍按兼容路径读取。

CAP 消费者可显式使用 `new EventConsumptionStore(EventConsumptionDatabase.MySql)`，在业务写入使用的同一个 `DbTransaction` 中调用 `TryRecordAsync(transaction, consumer, eventId)`；返回 `false` 时跳过业务写入。调用方负责创建收据表、提交或回滚事务，并保证消费者名称和事件 ID 在重放期间稳定。可使用 `EventConsumptionSchema.GetCreateStatements(database)` 获取本地或测试建表脚本；已有生产环境应执行 `deploy/migrations/events/001-event-consumption.mysql.sql` 或对应 SQLite 脚本，应用启动不会自动修改表结构。详细表结构与部署约束见[集群事件指南](../../../docs/guides/cluster-consistency.md)。

`EventConsumptionStore` 同时提供可选的 `IEventConsumptionRetentionStore`。调用方只有在确认允许重放的期限已经结束后，才能在自有事务中调用 `DeleteBeforeAsync(transaction, consumer, cutoffUtc, batchSize)`；清理按消费者、UTC 截止时间和最多 10,000 条批次执行。该接口没有默认后台调度器、授权器或归档策略，不能用 CAP 消息表清理替代业务幂等收据留存；未确定重放窗口时不要调用它。清理事务由调用方提交或回滚，取消只终止本次数据库等待，不能把未完成清理报告为成功。

管理端或运维入口应注册 `AddBingEventConsumptionRetention<TAuthorizer>()`，解析 `IEventConsumptionRetentionService` 后调用同一个事务内的 `DeleteBeforeAsync`。服务会先执行授权，再调用低层存储；低层 `IEventConsumptionRetentionStore`、数据库连接和事务仍由宿主显式提供，重复注册不会覆盖已有授权器，也不会启动后台清理。

## 按实体有序消费

有序事件显式启用：`OrderedEventPublisher` 在已有 CAP 业务事务内分配序号；订阅者等待 `OrderedEventCoordinator.ReceiveAsync` 持久化后返回；`AddBingOrderedConsumer<THandler>` 在后台使用同一数据库事务执行业务并推进游标。若处理器还要在同一业务事务中发布 CAP 出箱消息，必须在 CAP 注册完成后显式包装底层 ORM 工厂：

```csharp
services.AddScoped<EfCoreOrderedEventTransactionFactory<BusinessDbContext>>();
services.AddCapEventBus(options => { /* 数据库与 RabbitMQ */ });
services.AddBingCapOrderedEventTransaction<
    EfCoreOrderedEventTransactionFactory<BusinessDbContext>>();
services.AddBingOrderedConsumer<InventoryHandler>("inventory", maxConcurrency: 4,
    maxPagesPerScan: 16);
```

`maxPagesPerScan` 默认值为 16。连续扫描达到该页数后会回到首个流键，避免持续满页时新出现的较小流键一直等待；它只控制扫描游标回绕，不会终止正在执行的处理器，也不替代数据库游标、业务幂等和排空等待。

CAP 有序事务适配器在物理事务提交后会等待 dispatcher 返回的发布、执行和调度 `ValueTask` 完成；这只表示入队调用完成，不代表 RabbitMQ broker 已确认。入队失败仍保留 `Committed` 结果，调用方必须按事件 ID 查询和补偿。

`maxConcurrency` 只限制不同业务流的并行处理数，默认值为 `1`。同一业务流仍由持久化游标和事务锁保证串行；当前扫描页面内某个流完成后会立即补充下一个流，慢处理器不会占住整个页面的空闲槽位；页面全部完成后才按原扫描顺序推进游标，避免异步完成顺序造成跳过。提高该值前应确认数据库连接、事务锁和处理器外部副作用具备相应容量。停止宿主时会等待当前页面已启动的所有流任务完成或响应取消，不会把仍在执行的任务标记为已清理。若处理器忽略停止令牌，`StopAsync` 在等待令牌取消后会抛出带有 `Bing.OrderedEventConsumerDrainIncomplete` 标记的取消异常，并保留在途作用域；调用方应先让处理器结束，再使用未取消的令牌再次等待停止，不能在超时路径强制释放这些资源。
入箱端可为 `DbOrderedEventStore` 配置 `maxPendingPerConsumer`，限制同一逻辑消费者跨业务流的待处理总量。该参数为空时保持兼容行为；启用时要先执行容量表迁移，入箱与消费在同一数据库事务锁定消费者容量行并重新计数。容量满时重复事件仍按幂等规则返回，新的事件会被拒绝；失败事件继续占用容量，成功处理提交后才释放容量。该配置只对当前数据库存储生效，不等同于跨数据库或 Redis 集群配额。

FreeSQL 使用 `FreeSqlOrderedEventTransactionFactory`，并保持 `IFreeSql` 由宿主拥有。包装器只拥有 CAP 缓冲，数据库事务仍由底层工厂释放；不能同时再注册未包装的 `IOrderedEventTransactionFactory`。同一服务集合只能注册一个 CAP 有序事务适配器，重复注册同一底层工厂是幂等操作，尝试注册另一底层工厂会在注册阶段失败，避免 DI 最后注册覆盖前一个 ORM 事务适配器。异步开启或保存点回滚完成后，适配器会在当前执行上下文重新激活 CAP ambient，避免 `AsyncLocal` 值只停留在工厂内部。

`ReceiveAsync` 仅对内置存储的原生 MySQL 1213、确认回滚且清理成功的入箱事务做最多三次重试（共四次尝试，20/40/80 ms 可取消等待）。业务处理、1205、未知提交及自定义存储不自动重试；CAP 重投预算应与此配置一起评估。新增七项真实驱动错误形状测试在 net6/net8 均已通过；它们不能代替实际 InnoDB 死锁或提交断连验证，完整回归结果另见执行报告。

处理完成后的收件箱记录可通过 `OrderedEventCoordinator.DeleteProcessedBeforeAsync` 按消费者和截止时间分批清理；该入口只针对已处理记录，待处理和失败记录不会被删除。内置数据库存储会在同一事务中先写入 `bing_event_inbox_archive` 再删除收件箱，底层自定义存储未实现 `IOrderedEventStoreRetention` 时返回未支持结果。历史表可在确认重放窗口后通过 `OrderedEventCoordinator.DeleteArchivedBeforeAsync` 单独按批次清理，底层存储需实现 `IOrderedEventStoreArchiveRetention`。`AddBingOrderedEventRetentionScheduler` 提供默认关闭的周期调度；默认只清理收件箱，显式同时配置 `ArchiveRetentionPeriod` 和 `ArchiveConsumers` 后才会自动清理历史表。宿主必须先保证游标、审计和业务幂等依据的保留，再启用任一清理。

需要由应用统一调度时，可显式启用默认关闭的 `AddBingOrderedEventRetentionScheduler`，并配置稳定消费者名称：

```csharp
services.AddBingOrderedEventRetentionScheduler(options =>
{
    options.Enabled = true;
    options.RetentionPeriod = TimeSpan.FromDays(30);
    options.BatchSize = 500;
    options.Consumers.Add("inventory");
    options.UseCoordinationLease = true;
});
```

如需自动清理已经进入历史表的记录，必须同时配置 `ArchiveRetentionPeriod` 和 `ArchiveConsumers`；两项默认关闭，待处理和失败记录不会被调度器删除。

调度器每轮只执行有限批次，并为每个消费者使用独立事务；启用协调租约时需要先注册 `ILockLeaseProvider`。清理不会删除待处理或失败记录，停止时会等待当前事务结束。内置存储的已处理记录会先进入历史表；历史表的保留期限、审计和幂等依据由宿主负责配置，未显式配置归档期限时框架不会删除历史或无限重试。

重放窗口结束后可由受信运维任务显式调用 `OrderedEventCoordinator.DeleteArchivedBeforeAsync`，按固定截止时间和有限批次循环清理历史表；也可在确认相同前置条件后通过归档调度配置自动执行。删除前必须确认业务幂等与审计留存仍覆盖允许重放范围，取消或失败批次应保留并在后续任务中继续处理。

最近一次验收记录包含 Admin EFCore 有序 CAP 16/16、Admin FreeSQL 有序 CAP 15/15、串行 FreeSQL MySQL 有序集成 68/68，以及 Sample Lazy 冒烟 1/1。`OrderedEventPublisher` 的 EFCore `IDbContextTransaction` 包装事务兼容行为有 4 项 net6 定向测试；这些数字不代表未运行的其它框架、锁定 SDK 或蓝绿部署通过。

有序消费者注册后可从 `IEnumerable<IOrderedEventMetrics>` 获取按逻辑消费者隔离的快照，包含待处理流数、待处理事件数、最老积压年龄、成功、缺口、失败、重试、在途数、最近处理耗时和扫描失败次数。`ScanFailureCount` 记录主扫描、全局流/事件统计或最老年龄统计失败的次数；主扫描失败时保留上一份积压快照，单项统计失败时只保留对应的全局数量或年龄，调用方必须同时检查该计数，不能把旧快照误认为当前零积压。内置数据库存储通过可选的 `IOrderedEventStoreStatistics` 提供全局待处理事件数，通过 `IOrderedEventStorePendingStreamStatistics` 提供全局待处理业务流数，并通过 `IOrderedEventStorePendingAgeStatistics` 提供最老未完成事件的年龄；旧的自定义存储没有这些契约时分别回退为最近一页流数或空年龄。指标只使用固定消费者名称，不把事件 ID 或实体键写入标签。`BingOrderedEventMetrics` 同时发布标准 `Meter`（`Bing.Events.Ordering`），可由 OpenTelemetry、Prometheus 或 `MeterListener` 采集；除固定 `bing.ordered.scan.failures` 外，缺少最老积压时不发布伪造的负年龄测量。Meter 随指标实例释放，不使用静态注册表。`BingOrderedEventMetricsPrometheusExporter.Export` 可将快照导出为稳定的 Prometheus 文本格式，只输出固定消费者标签；导出器不创建 HTTP 端点，也不负责认证、授权、告警或长期归档，宿主接入监控端点时必须自行保护访问并保持低基数输出。`BingOrderedEventMetricsAlertEvaluator` 可在进程内按待处理事件数、最老积压年龄和扫描失败增量评估规则，并按消费者与规则执行冷却；它只返回告警结果，不发送通知、不做跨节点去重，通知、鉴权和集群聚合由宿主或外部监控负责。`maxPagesPerScan` 控制跨页回绕，默认 16 页；达到预算后回到首个流键，避免持续满页时较小的新流长期等待。该预算不会终止慢处理器，排空仍以实际处理任务退出为准。生产环境仍需补充容量预算和归档策略。

需要在应用内汇总多个消费者时，可显式注册只读快照提供程序：

```csharp
services.AddBingOrderedEventMetricsDiagnostics();
var diagnostics = serviceProvider.GetRequiredService<IOrderedEventMetricsSnapshotProvider>();
var snapshots = diagnostics.GetSnapshots();
```

该入口按消费者名称使用 `Ordinal` 排序，并在注册时拒绝重复名称；每次调用返回新的只读集合，不包含消息正文、事件标识或实体键。它只提供进程内数据，不负责 HTTP 暴露、身份认证、授权、告警节流或归档；宿主接入监控端点时必须自行保护访问并保持低基数输出。未注册消费者时返回空集合，调用方预先注册的自定义提供程序不会被覆盖。

先注册 `IOrderedEventStore` 及对应 ORM 的 `IOrderedEventTransactionFactory`，部署五张有序事件表。新版收件箱表包含 `CreatedAtUtc`，用于计算最老积压年龄；启用 `maxPendingPerConsumer` 时还需要部署 `bing_event_consumer_capacity`。已处理记录清理前会写入 `bing_event_inbox_archive`，继续保留事件 ID 去重依据。已有部署必须通过受控数据库迁移补充收件箱时间列、容量表和历史表，`GetCreateStatements` 不会自动修改现有表。回调必须使用传入事务；基础事务工厂不会自动绑定派生 CAP 发布。业务异常会在保持游标锁的同一事务中回滚业务保存点、提交失败标记；取消不记录失败。`TransactionOutcome.Committed` 仅表示外层事务已提交，异常上的 `Bing.OrderedEventBusinessRolledBack` 与 `Bing.OrderedEventFailureRecorded` 区分业务回滚和标记提交；结果为 `Unknown` 时必须查询持久状态。原始异常保留，清理错误附于异常 `Data`。缺号与失败阻塞当前实体，不自动跳过。详细注册、驱动保存点边界、只读状态查询、恢复和容量约束见[有序事件指南](../../../docs/guides/ordered-events.md)。

人工调用 `RetryAsync` 时可注册 `IOrderedEventRecoveryAuthorizer`；协调器会在开启恢复事务前依次执行授权器，任一授权器拒绝就不会写数据库。授权器应同时校验租户、消费者和业务流，并记录关键审计；受控恢复服务没有授权器时会拒绝操作，生产宿主应显式注册授权器。

管理端查询和人工恢复可注册 `AddBingOrderedEventRecovery()`，通过 `IOrderedEventRecoveryService` 统一执行受授权状态查询、重试和 `IOrderedEventRecoveryAudit` 安全审计。授权器和至少一个审计记录器都必须由宿主提供；缺少任一项时操作会在读取状态或开启恢复事务前拒绝。恢复已经提交后审计失败会保留 `Committed` 结果，不会伪装成事务回滚。

Admin 宿主的适配器默认关闭；启用 `OrderedEvents:Recovery:Enabled` 后必须提供恢复角色和消费者白名单，并按当前认证用户校验租户。Admin Web 宿主会同时提供受认证保护的状态查询和重试端点；通用宿主仍只提供服务契约，不自动暴露 HTTP 端点。

Admin 有序指标诊断默认关闭。只有在 `BING_HOST_ROLE=consumer`、`OrderedEvents:Enabled=true` 且显式设置
`OrderedEvents:Metrics:Enabled=true` 时，宿主才注册快照提供程序和
`/internal/metrics/ordered-events` 端点；同时必须配置 `OrderedEvents:Metrics:Role`，当前认证用户需具备该角色。
端点返回只包含固定 `consumer` 标签的 Prometheus 文本，不包含消息正文、事件标识或实体键。Web 角色和 Task 角色启用该配置会在注册阶段失败，避免预热实例意外暴露诊断数据。

## 消费取消的显式失败重试

CAP 8.0.1 的原生执行器会将逃出的 `OperationCanceledException` 当作已处理，而不是持久化失败。需要将业务取消交给 CAP 失败重试的宿主显式启用：

```csharp
services.AddCapEventBus(options =>
{
    // 配置当前数据库与消息传输。
});
services.EnableBingCapConsumerCancellationRetry();
```

本入口注册 `BingSubscribeInvoker`，仅把原生调用器逃出的取消包装为普通失败，原取消异常保留在 `InnerException`，并设置 `Data["Bing.CapConsumerCancellation"]`。重试由 CAP 自身持久化路径执行，不自行重发消息，不保证 RabbitMQ broker 重新入队。启用前后业务都需要同事务消费收据；取消发生在提交之后时，重试必须查询原收据以避免重复修改。

已有消费过滤器先沿原生路径执行：过滤器明确处理异常后，调用器不会重新将其判为失败；未处理的取消才转换。所有逃出的取消均采用此策略，包括调用令牌引发的取消。宿主停止、业务超时和主动取消需按业务定义可重试性；不能以调用方停止等待推断数据库已回滚或资源已释放。

重复启用不改变已适配的类型或实例注册。未知自定义执行器/工厂不会被探测或覆盖，并会明确报错；自定义执行器应继承 `BingSubscribeInvoker` 保留 `GetInstance` 扩展点，或直接注册返回该执行器的工厂。CAP 必须通过 `ISubscribeInvoker` 调用；直接调用基类公开 `InvokeAsync` 保持原生行为。两套 Admin 的 `CapModule` 已显式启用此策略，外部使用 `AddCapEventBus` 的应用仍需自行选择。

## CAP 消费排空

需要在蓝绿切换或宿主排停前等待 CAP 业务回调退出时，在启用取消重试适配的同时解析
`IBingCapConsumerRuntime`：

```csharp
services.AddCapEventBus(options => { /* 数据库与 RabbitMQ */ });
services.EnableBingCapConsumerCancellationRetry();

var runtime = serviceProvider.GetRequiredService<IBingCapConsumerRuntime>();
runtime.BeginDrain();
var drained = await runtime.WaitForDrainAsync(TimeSpan.FromSeconds(30), cancellationToken);
```

排空后新的 `ISubscribeInvoker` 回调会进入 CAP 失败路径，在途回调继续使用原作用域运行，
直到业务结束；等待超时或取消只结束等待，不释放活动作用域。`InFlightCount` 归零且
`WaitForDrainAsync` 返回 `true` 后，才可继续关闭消费者或切换版本。该契约只覆盖
`BingSubscribeInvoker`；运行时注册跟随调用器的生命周期，作用域调用器不会被根容器捕获。
Singleton 调用器会由 `CapHostedLifecycleService` 在宿主停止时自动排空；Scoped 调用器只提供
作用域内的运行时，宿主不会从根容器聚合它。它不自动排空直接使用 CAP 原生调用器、Hangfire、
定时任务或其他后台任务。

宿主自动排空默认使用无限等待。需要让停止流程在有限时间内报告未完成时，可在启用取消重试适配时配置预算：

```csharp
services.EnableBingCapConsumerCancellationRetry(options =>
{
    options.DrainTimeout = TimeSpan.FromSeconds(30);
});
```

超时只结束宿主关闭任务的等待并附带 `Bing.CapConsumerDrainIncomplete` 诊断，仍在执行的消费回调、作用域和事务不会被强制释放。调用方应在业务结束后使用未取消令牌重新等待关闭完成；配置为 `Timeout.InfiniteTimeSpan` 可保持原有行为。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [横切能力文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/横切能力文档.md) —— 邮件 / 模板 / 锁 / 本地化 / 事件总线的详解与选型
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Events

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
