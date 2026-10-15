# 按实体有序事件

> **适用版本**：框架 `7.0.0`｜源码核对：2026-10-13

有序处理以“租户 + 业务流 + 实体键”为边界，保证业务事务按连续序号推进。RabbitMQ 到达顺序不等于业务提交顺序；该能力显式启用，不改变原有消息总线或本地事件。

## 部署与注册

部署时执行 `OrderedEventSchema.GetCreateStatements(OrderedEventDatabase.MySql)` 的五张表脚本：序号表、游标表、消费者容量表、收件箱和已处理收件历史表。收件箱包含 `CreatedAtUtc`，用于计算最老未完成事件年龄；已有环境必须通过受控迁移补充该非空列并为历史记录填充明确的 UTC 值，同时执行 `deploy/migrations/ordered-events/002-inbox-archive.mysql.sql` 和（需要全局积压闸门时）`003-consumer-capacity.mysql.sql`，不能依靠 `CREATE TABLE IF NOT EXISTS` 修改旧表，也不能在生产启动时自动改表。仓库提供 `deploy/migrations/ordered-events/001-created-at-utc.mysql.sql`、`002-inbox-archive.mysql.sql` 和 `003-consumer-capacity.mysql.sql` 作为一次性 MySQL 迁移模板，执行前仍需按部署流程备份、停写和核对。SQLite 仅供本地验证。

Admin 的 EFCore 和 FreeSQL 宿主可通过 `OrderedEvents:Enabled=true`（环境变量为
`OrderedEvents__Enabled=true`）注册生产有序事件基础设施。EFCore 适配器复用 Admin 工作单元；FreeSQL
适配器借用 `FreeSqlWrapper.Orm`，不接管根 ORM。通用入口只注册 `IOrderedEventStore` 和 CAP 事务适配器，
不会为未知业务流自动创建处理器、恢复授权器或后台消费者；消费者必须在拥有明确业务流、租户校验和幂等策略后显式调用
`AddBingOrderedConsumer<THandler>`，且只能在允许消费的宿主角色启用。Admin 的库存业务是一个明确的例外：
同时设置 `Inventory:Enabled=true`、`OrderedEvents:Enabled=true`、`BING_HOST_ROLE=consumer` 和
`BING_CAP_CONSUMER_ENABLED=true` 时，两个 Admin 宿主会注册固定主题、固定 CAP 组和 `admin-inventory-v1`
有序消费者；Web/Task 只发布消息，不启动该后台消费者。

管理端人工恢复默认关闭。启用时必须同时配置恢复角色和逻辑消费者白名单；配置不完整会拒绝恢复，不会默认放行：

```json
{
  "OrderedEvents": {
    "Enabled": true,
    "Recovery": {
      "Enabled": true,
      "Role": "ordered-events-operator",
      "RequireTenantMatch": true,
      "Consumers": [ "inventory" ]
    }
  }
}
```

两个 Admin 宿主会注册 `IOrderedEventRecoveryService`、基于 `ICurrentUser` 的角色/消费者/租户授权器以及审计适配器。消费者白名单按 `Ordinal` 精确匹配大小写，并在启动配置阶段拒绝超过 200 个字符或包含首尾空白的名称。
授权器只信任认证上下文，不信任请求头中的租户值；恢复事务提交后才保存安全审计摘要，审计失败会保留已提交诊断。
Admin Web 宿主在恢复开关启用时提供受认证保护的 `GET /api/ordered-events/recovery/state` 和
`POST /api/ordered-events/recovery/retry`。恢复关闭时控制器返回 404；请求中的租户、消费者和业务流
仍由授权器校验，空租户只表示全局业务流。通用宿主仍只提供服务契约，不自动暴露 HTTP 端点。

请求只包含 `consumer`、`tenantId`、`stream` 和 `entityKey`，不会接收事件正文。恢复审计使用宿主注册的
`IAuditingStore`；生产环境应配置可靠的数据库审计存储或等价持久化实现，不能把默认日志输出当作恢复操作的
长期审计凭据。

```csharp
services.AddLogging();
services.AddSingleton<IOrderedEventStore>(
    new DbOrderedEventStore(OrderedEventDatabase.MySql));
services.AddScoped<IOrderedEventTransactionFactory,
    EfCoreOrderedEventTransactionFactory<BusinessDbContext>>();
// FreeSQL 改用：
// services.AddScoped<IOrderedEventTransactionFactory,
//     FreeSqlOrderedEventTransactionFactory>();
services.AddBingOrderedConsumer<InventoryHandler>("inventory", maxConcurrency: 4);
```

Admin 宿主还可以配置 `OrderedEvents:MaxPendingPerConsumer`（环境变量写法为
`OrderedEvents__MaxPendingPerConsumer`）限制同一逻辑消费者跨业务流的未完成总量；未配置时保持兼容行为。
启用前必须先执行 `003-consumer-capacity` 数据库迁移，容量行和收件箱必须位于同一个数据库中。
消费者注册还可以通过 `maxPagesPerScan` 设置连续分页预算，默认值为 16；达到预算后游标回到首页，避免持续满页时排序较小的新流长期等待。该设置只影响扫描公平，不会终止慢处理器，也不改变业务流顺序和幂等语义：

```csharp
services.AddBingOrderedConsumer<InventoryHandler>("inventory",
    maxConcurrency: 4, maxPagesPerScan: 16);
```

Admin 库存消费者使用以下稳定标识，蓝绿版本必须保持不变：主题
`bing.admin.inventory.completed.ordered.v1`，CAP 组 `bing-admin-inventory-ordered-ingress`，逻辑消费者
`admin-inventory-v1`，业务流 `inventory-order`。处理器只在有序事务中写入完成投影，不会再次扣减库存；投影表
`bing_inventory_ordered_projection` 必须与 `InventoryOrderSchema` 的其它表一起受控部署。

`maxConcurrency` 默认值为 `1`，只限制不同业务流的并行处理数。同一业务流仍依赖游标锁和业务事务串行推进；提高并发前应确认数据库连接池、锁等待、处理器幂等和外部副作用容量。内置数据库存储实现 `IOrderedEventStorePendingStreamStatistics`，指标中的待处理流数是全局数量；未实现该可选契约的自定义存储仍以当前分页数量作为兼容回退。宿主停止时会等待当前批次的处理任务完成或响应取消，超时不能作为已清理的证明。处理器若忽略停止令牌，消费者会以 `Bing.OrderedEventConsumerDrainIncomplete` 标记报告排空未完成并保留活动作用域；应在业务处理结束后再次用未取消令牌等待停止，不能把超时当成资源已释放。

部署切换需要显式使用 `IOrderedEventConsumerRuntime` 协调有序消费者。通过 `IEnumerable<IOrderedEventConsumerRuntime>` 按 `ConsumerName` 找到逻辑消费者后，先调用 `BeginDrain()` 停止新的扫描，再调用 `WaitForDrainAsync(timeout, cancellationToken)` 等待后台循环和在途处理真实退出。返回 `false` 或抛出带有 `Bing.OrderedEventConsumerDrainIncomplete` 的停止异常时，只表示等待未完成；必须保留原进程、作用域和数据库恢复证据，不能提前释放根容器或切换为新的业务身份。该接口只覆盖 Bing 有序消费者，不会替代 CAP 原生回调、Hangfire、SSE/WebSocket 或其他脱离请求的任务排空。

如果处理器需要在同一个业务事务中发布派生 CAP 消息，先注册 CAP，再使用显式适配器包装底层工厂：

```csharp
services.AddScoped<EfCoreOrderedEventTransactionFactory<BusinessDbContext>>();
services.AddCapEventBus(options => { /* 数据库与 RabbitMQ */ });
services.AddBingCapOrderedEventTransaction<
    EfCoreOrderedEventTransactionFactory<BusinessDbContext>>();
services.AddBingOrderedConsumer<InventoryHandler>("inventory", maxConcurrency: 4);
```

适配器只管理当前作用域的 CAP 缓冲，物理数据库事务仍由 EFCore 或 FreeSQL 工厂单独拥有。不要再同时注册未包装的 `IOrderedEventTransactionFactory`。同一服务集合只能选择一个底层 ORM 工厂；重复注册同一工厂不会增加适配器，切换到另一工厂会在注册阶段失败，避免最后注册覆盖前一个事务适配器。`BeginAsync` 和保存点重置跨越异步边界后，会通过 `IOrderedEventTransactionState.ActivateAmbient()` 在调用方上下文重新绑定 CAP 事务；这一步不能依赖工厂内部对 `AsyncLocal` 的赋值。适配器会等待 CAP dispatcher 返回的发布、执行和调度 `ValueTask` 完成，再结束本次提交任务；这只表示入队调用完成，不代表 RabbitMQ broker 已确认。入队失败仍保留已提交结果，调用方必须按事件 ID 查询和补偿。

类型位于 `Bing.Data.Events.Ordering`、`Bing.Events.Ordering` 和对应 ORM 的 `Events` 命名空间。业务 DbContext 注册为 Scoped；IFreeSql 由宿主拥有。CAP 存储、RabbitMQ 和订阅者沿用原有注册。消费者名称跨蓝绿版本保持稳定。仅发布的进程可独立注册 `OrderedEventPublisher`，不用启动后台消费者。

## 发布与消费

在已有 CAP 工作单元中登记发布回调：

```csharp
var message = new Bing.Events.Messages.MessageEvent
{
    Id = Guid.NewGuid().ToString("N"),
    Name = "inventory.changed", TenantId = tenantId,
    Stream = "inventory", EntityKey = sku,
    ContractVersion = "1", Data = payload
};
actions.Register(transaction => orderedPublisher.PublishAsync(
    (DbTransaction)transaction, message, cancellationToken));
await unitOfWork.SaveChangesAsync(cancellationToken);
```

`actions` 是当前作用域的 `ITransactionActionManager`。不要预设 Sequence；传入事务必须与 CAP 发布器中的事务是同一对象。发布器在该业务事务内分配连续序号并写 CAP 出箱，回滚不留序号洞。失败后丢弃消息和工作单元，不能沿用已赋值的序号。

CAP 订阅者等待有序收件箱提交后才返回。可以直接使用 `CapOrderedEventIngress` 统一执行头部校验和入箱：

```csharp
services.AddBingCapOrderedEventIngress();

public sealed class InventoryOrderedSubscriber : ICapSubscribe
{
    private readonly CapOrderedEventIngress _ingress;

    public InventoryOrderedSubscriber(CapOrderedEventIngress ingress) => _ingress = ingress;

    [CapSubscribe("inventory.changed", Group = "inventory-ordered")]
    public Task ReceiveAsync(string payload, [FromCap] CapHeader headers) =>
        _ingress.ReceiveAsync("inventory", headers, payload);
}
```

主题和 CAP 消费者组仍由宿主使用固定值显式声明，消费者名称必须跨蓝绿版本稳定；框架不会把所有 CAP 消息自动接入有序队列。适配器完成持久化接收后订阅方法才返回确认。重投必须保留相同事件 ID、序号、正文及版本，不能更换 JSON 序列化方式。

接收入箱有窄范围的有界重试：仅内置 `DbOrderedEventStore` 遇到原生 MySQL 1213 死锁、外层事务已确认 `RolledBack` 且没有 `Bing.TransactionCleanupException` 时，重做整个入箱事务。首次加最多三次重试，共最多四次尝试；每次回滚和释放完成后依次等待 20、40、80 ms，等待可由调用方取消。业务 `ProcessNextAsync` 的 handler 不参与此重试。MySQL 1205 锁等待超时、自定义存储、提交结果 `Unknown` 或 `Committed`、回滚或释放失败都不会自动重试。CAP 自身还可能重投，部署时应一并配置重试次数、超时和幂等保留期限，避免两层重试把单条故障消息放大为长时间占用。

后台业务处理器实现 `IOrderedEventHandler.HandleAsync`，所有数据库修改使用传入 DbTransaction，不提前提交或另开事务。EFCore 工厂拒绝已有外部事务；FreeSQL 工厂只释放本次创建的工作单元，不释放宿主 ORM。基础事务工厂不会自动为回调的新 CAP 发布绑定当前事务；需要出箱时必须另外使用兼容的 CAP 事务接入，不能把未绑定发布当成原子出箱。保存点只回滚数据库修改，不恢复 ORM 已跟踪对象、事务动作回调或 CAP 发送缓冲；业务处理失败后应丢弃当前作用域及工作单元，不在其中继续处理下一事件。

## 失败与恢复

- 3、1、2 到达时先保存 3 并显示 Gap，待 1、2 补齐后按序处理。默认游标从 0 开始，订阅者必须完整可见全部序号。
- 协调器在取得游标锁后建立保存点。非取消业务异常时，先回滚保存点内的业务写入，再在同一外层事务中记录当前序号失败并提交；整个过程保持游标锁，失败前序阻塞该实体，不自动跳过。保存点回滚或失败标记写入失败时，回滚外层事务并保留原始业务异常。执行中取消直接回滚外层事务，不记录失败标记。
- `GetStateAsync` 在存储层执行单条普通 `SELECT`，不隐式创建游标，也不加 `FOR UPDATE`；同一语句内部的数据取决于调用方事务的隔离级别和快照。未知流返回空状态。`RetryAsync` 仅允许当前失败序号再次处理；生产宿主应注册 `IOrderedEventRecoveryAuthorizer`，在恢复事务开始前授权、验证租户并记录关键审计。通过 `IOrderedEventRecoveryService` 查询或恢复时未注册授权器会直接拒绝；底层协调器仍保留旧的显式调用契约，业务应优先使用受控恢复服务。
- 有序消费者发现失败流后会在当前消费者进程内按轮询间隔退避，退避期间只读取状态，不重复构造处理器或执行业务回调；处理器构造、依赖解析和业务处理的持续异常也进入同一退避路径。状态被 `RetryAsync` 显式恢复后，下一次探测会立即重新处理；退避缓存有固定上限，失败流过多时使用短暂全局探测窗口，避免无界内存增长和失败风暴。该退避不跨节点共享，节点重启或其他节点仍可能再次探测，正确性仍由失败状态、业务幂等和人工恢复保证。

需要向管理端或运维任务暴露恢复能力时，注册 `AddBingOrderedEventRecovery()` 并使用 `IOrderedEventRecoveryService`。该入口会在状态查询和人工重试开启数据库事务前执行授权器；授权通过后再调用协调器的无授权事务路径，避免同一授权器重复执行。生产环境应同时注册 `IOrderedEventRecoveryAudit`，只保存操作、消费者、业务流和异常类型等安全摘要，不记录消息正文或 SQL 参数。恢复事务已经提交后审计保存失败会返回 `Committed` 诊断，不能把它解释为恢复回滚。
- `exception.GetTransactionOutcome() == Committed` 只表示外层物理事务提交成功，不表示业务成功：业务保存点已回滚时，异常 `Data["Bing.OrderedEventBusinessRolledBack"]` 为 `true`，失败标记提交成功时 `Data["Bing.OrderedEventFailureRecorded"]` 为 `true`。`Unknown` 表示提交已开始但结果不能确定，即使异常上没有 `FailureRecorded` 标志，标记仍可能已落库；先查询业务幂等结果、失败状态和游标，不直接重放 HTTP/邮件。外部副作用不会随数据库回滚。
- 对外继续抛原始业务异常；失败标记写入或提交错误保留在 `Bing.OrderedEventFailureRecordingException`，回滚与释放错误分别保留在 `Bing.TransactionRollbackException`、`Bing.TransactionCleanupException`。复用同一异常对象时，本轮标记不会继承上一轮诊断。
- 正常停止等待在途操作及作用域清理；不响应取消时不能提前释放活动资源。强杀恢复依靠事务、收件箱与游标，不依靠退出回调。

内置 MySQL/SQLite 存储在旧驱动未实现 `DbTransaction.SupportsSavepoints` 时使用当前事务上的 SQL 保存点；自定义存储没有此回落，必须提供原生保存点能力。保存点失败不能退化为另开事务记录失败。SQLite 的默认事务工厂可能先取得写锁；纯只读诊断若使用 `PRAGMA query_only=ON`，应由调用方建立 deferred 只读事务再调用存储查询，不能把工厂争锁当作 `GetStateAsync` 写入。

## 容量与所有权

事件 ID 在同一逻辑消费者范围内跨租户唯一。同 ID 或同序号正文不同会拒绝。实体删除重建时实体键应含新流代次；半途订阅、过滤及历史重放需要先设计起点。

默认单条正文上限 65,536 UTF-8 字节，单消费者实体上限 10,000 条未完成事件。`DbOrderedEventStore` 的 `maxPendingPerConsumer` 可选参数用于限制一个逻辑消费者跨所有业务流的未完成事件总数；为空时保持历史行为，不启用容量表闸门。启用后，入箱、消费、失败记录和重试会在同一事务锁定该消费者容量行并重新统计收件箱，重复事件先完成幂等判断，失败记录继续占用容量，成功处理提交后才减少计数；容量已满时应暂停接收并处理积压，不能依赖应用进程内计数。已有环境必须先执行 `003-consumer-capacity.mysql.sql`（SQLite 本地环境执行对应脚本），并将容量表与收件箱、业务幂等记录放在同一数据库中。该闸门只覆盖当前数据库存储，不提供 Redis 或跨数据库的全局配额。完成正文在清理前会转入 `bing_event_inbox_archive`，继续用于重复事件 ID 校验。历史表默认不会由留存调度器自动清理；确认允许重放窗口结束后，可通过 `OrderedEventCoordinator.DeleteArchivedBeforeAsync` 按消费者和有界批次清理，也可以在同时配置 `ArchiveRetentionPeriod` 与 `ArchiveConsumers` 后启用归档调度。宿主必须规划归档、重放期限和容量预算，不能按年龄删除未完成消息或在重放期内删除历史去重记录。

完成记录的留存可以通过 `OrderedEventCoordinator.DeleteProcessedBeforeAsync` 显式清理，也可以注册默认关闭的 `AddBingOrderedEventRetentionScheduler`：

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

该调度器默认只清理指定消费者的已处理收件记录，待处理和失败记录始终保留；内置存储在同一事务中先写入历史表再删除收件箱，每个消费者在协调器自己的事务中按有界批次执行。若明确配置 `ArchiveRetentionPeriod` 和 `ArchiveConsumers`，同一轮调度会按归档期限调用 `DeleteArchivedBeforeAsync` 清理历史表；两项必须同时配置，缺少任一项会在启动阶段拒绝，默认不会自动删除历史表。启用后会在 `StartAsync` 预检 `OrderedEventCoordinator` 及其依赖，缺少有序事件存储、事务工厂或协调器会立即拒绝启动，不会先启动后台循环再持续记录错误。多节点启用时应配置有限租期的 `ILockLeaseProvider`，并保证游标、业务幂等、审计和归档依据的保留期覆盖允许重放范围。调度器不删除待处理或失败记录，也不提供跨节点告警，停止时等待当前清理真实完成，不能因超时强制释放作用域。

确认重放窗口已结束后，宿主也可以显式清理历史表。未配置归档调度时，该调用应使用独立的运维授权和有界循环；归档调度同样必须由宿主确认重放窗口和业务幂等保留策略后显式开启：

```csharp
var cutoffUtc = DateTime.UtcNow.AddDays(-90);
while (true)
{
    var deleted = await coordinator.DeleteArchivedBeforeAsync(
        "inventory", cutoffUtc, batchSize: 500, CancellationToken.None);
    if (deleted is null || deleted == 0)
        break;
}
```

删除历史记录会释放对应事件 ID 的重复接收保护。业务幂等记录、审计记录和恢复依据必须至少保留到同一重放窗口之后；取消或事务失败时应保留未完成批次，下一轮从相同截止时间继续。

后台每轮分页、每实体处理一条。缺号/失败不阻塞下一页；默认 `maxConcurrency=1`，需要时可按数据库和外部副作用容量提高并发上限。并发只跨不同业务流，同一流仍串行，不承诺特定 QPS。

消费者指标快照中的 `PendingStreams` 表示最近一次分页发现的流数；内置数据库存储实现可选的 `IOrderedEventStoreStatistics` 后，同时提供当前消费者的全局 `PendingEvents`，实现 `IOrderedEventStorePendingAgeStatistics` 后还会提供 `OldestPendingEventAge`。旧自定义存储不实现这些可选契约时分别回退为当前分页流数或空年龄。年龄按收件箱 `CreatedAtUtc` 与当前 UTC 时间计算，查询失败只记录诊断并清空本轮年龄，不阻断业务消费。`BingOrderedEventMetrics` 还发布标准 `Meter` `Bing.Events.Ordering`，只使用固定 `consumer` 标签；指标实例释放时会释放 Meter，不使用静态注册表。需要汇总多个消费者时可注册 `AddBingOrderedEventMetricsDiagnostics()`，通过 `IOrderedEventMetricsSnapshotProvider` 获取按 `Ordinal` 排序的只读快照；该入口不包含 HTTP 授权或消息正文。`BingOrderedEventMetricsPrometheusExporter.Export` 可将快照导出为稳定的 Prometheus 文本格式，只输出固定消费者标签；导出器不创建 HTTP 端点，也不负责认证、授权、告警或归档，宿主接入监控端点时必须自行保护访问并保持低基数输出。`BingOrderedEventMetricsAlertEvaluator` 可在进程内按待处理事件数、最老积压年龄和扫描失败增量评估规则，并按消费者与规则执行冷却；它只返回告警结果，不发送通知、不做跨节点去重，通知、鉴权和集群聚合由宿主或外部监控负责。每轮会增加相应的统计查询，生产环境仍需补充容量预算和归档策略。

重复异步释放共享清理任务并传播失败。事务操作不能与提交/释放并发。FreeSQL Commit/Rollback 使用原生同步 API，Task 签名不等于非阻塞 I/O。

## 验证

EventBus 测试直接覆盖存储、协调器、发布器和后台宿主。FreeSQL 测试的 OrderedEventMySqlProcessTest 使用两个独立进程及提交前后中断，需显式启用 RUN_MYSQL_INTEGRATION_TESTS 并配置专用 MySQL 测试库。真实 CAP/RabbitMQ 测试还需 RUN_MYSQL_CAP_INTEGRATION_TESTS。实际结果见任务 execution.md；测试文件存在不代表环境已验收。

历史有序 CAP 批次结果：Admin EFCore 16/16、Admin FreeSQL 15/15；FreeSQL MySQL 有序集成在串行化后 68/68；Sample Lazy 冒烟 1/1。`OrderedEventPublisher` 对 EFCore `IDbContextTransaction` 包装事务的兼容行为另有 4 项 net6 定向测试。以上是对应运行批次的事实，不替代锁定 SDK、其它目标框架或真实蓝绿部署验收。

`OrderedEventReceiveRetryMySqlTest` 在真实 MySqlConnector 事务中使用 `SIGNAL` 生成原生 1213/1205 错误，验证上述重试判断、资源释放和调用次数。这是原生错误形状注入，不是实际 InnoDB 死锁或提交断连；两 Admin 新增的库存真实死锁用例使用相反行锁顺序，详情见库存指南；有序接收入箱的 `MySql.Data` 驱动分支尚无直接测试。该七项新增测试在 net6/net8 均为 7/7、无跳过；实际 TRX 位于 `artifacts/ordered-receive-retry-regressions`。完整回归仍单独验收，不能沿用此前回归的通过数量。

逻辑消费者名称必须是非空、长度不超过 200 个字符且不含首尾空白。框架在消费者注册、CAP 入箱、协调器、恢复服务、指标和数据库存储入口统一拒绝这类名称；不要依赖数据库排序规则或隐式 `Trim` 推断消费者身份。名称应在蓝绿版本和所有节点之间保持完全一致。

### 门控环境验收

专用测试库设置 `RUN_MYSQL_INTEGRATION_TESTS=true`、`RUN_MYSQL_CAP_INTEGRATION_TESTS=true`、`ALLOW_DATABASE_RESET_FOR_TESTS=true`，并配置 `ConnectionStrings__MySqlConnection` 与 `CAP_RABBITMQ_HOST/PORT/USERNAME/PASSWORD`。凭据通过环境注入，不写入源码或日志。

`eng/ci/Invoke-AdminMySqlCapIntegrationTests.ps1` 顺序运行两套 Admin 的门控集成，逐方法要求库存、有序消息及故障恢复关键用例全部通过且无跳过，禁止通过无关测试补足数量。必需方法和最低数量以脚本当前声明为准；最新真实批次见任务 execution.md。链路覆盖同事务发布/回滚、重复投递和真实 CAP 3→1→2 到达。`OrderedEventMySqlProcessTest` 则分别验证两个独立进程及消费提交前后中断；它不等同于真实网络断连或发布进程中断验收。
