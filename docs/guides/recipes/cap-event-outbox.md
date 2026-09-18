# 配方 2：CAP 分布式事件与 Outbox

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../../ReleaseNotes.md)

> 目标：业务提交后**可靠**地把消息发出去（不会「业务成功但消息丢了」），并且消费端**幂等**。
>
> 关键概念：本地事件 `ISimpleEventBus`（进程内）与消息事件 `IMessageEventBus`（跨服务，走 CAP + Outbox）是**两级**，别混用。

## 1. 装包

```bash
dotnet add package Bing.Events            # CAP 8.0.1 + Outbox
# 消息落库需要数据库支持；参考实现用 MySql
```

## 2. 注册

```csharp
services.AddCapEventBus(x =>
{
    // CAP 配置：存储、传输、重试等
});
```

> 若你只需要**进程内**事件，**不用装任何包**：`Bing.Core` 自带 `ISimpleEventBus`，直接注入即可。
> 🟢 只在本地消费就用 `ISimpleEventBus`——走 `IMessageEventBus` 会额外产生一次数据库写入 + 一次投递。

## 3. 定义事件（用 DTO，不要塞实体）

```csharp
public class OrderCreatedEvent
{
    public Guid OrderId { get; set; }
    public string OrderNo { get; set; }
    public decimal Amount { get; set; }
    public DateTime OccurredAt { get; set; }
}
```

> 消息会被序列化落库并长期保留。塞实体会导致结构一变历史消息就反序列化失败。

## 4. 发布（与业务同事务）

```csharp
public class OrderAppService : AppServiceBase
{
    private readonly IOrderStore _store;
    private readonly IMessageEventBus _bus;

    [UnitOfWork]
    public async Task<Guid> CreateAsync(OrderCreateDto dto)
    {
        var entity = dto.MapTo<Order>();
        await _store.AddAsync(entity);

        await _bus.PublishAsync(new OrderCreatedEvent
        {
            OrderId = entity.Id,
            OrderNo = entity.OrderNo,
            Amount = entity.Amount,
            OccurredAt = DateTime.UtcNow
        });
        return entity.Id;      // 消息与业务数据在同一事务里落库
    }
}
```

**Outbox 怎么保证可靠**：消息先与业务数据写进**同一数据库事务**（参考实现的 `AdminUnitOfWork.SaveChangesAsync` 就包装了这一步），提交后由 CAP 异步投递。业务成功 ⇒ 消息必然存在；投递失败会重试。

## 5. 订阅

```csharp
public class OrderCreatedHandler : ICapSubscribe
{
    private readonly IOrderStore _store;

    [EventHandler("order.created")]     // ⚠ 不是 [CapSubscribe]
    public async Task HandleAsync(OrderCreatedEvent e)
    {
        // 🔴 幂等：至少一次投递，重复消费一定会发生
        if (await _store.ExistsAsync(x => x.OrderNo == e.OrderNo))
            return;

        // ... 业务处理
    }
}
```

## 注意事项

1. 🔴 **订阅用 `[EventHandler]`，不是 `[CapSubscribe]`**。`[EventHandler]` 继承 CAP 的 `TopicAttribute`，框架按它扫描注册；用错特性订阅会**静默不生效**。
2. 🔴 **消费端必须幂等**：消息是**至少一次**投递。不幂等就是重复下单、重复扣款。
3. **别把本地事件发到消息总线**：`IMessageEventBus` 会带来一次落库 + 一次投递的额外成本，纯本地场景用 `ISimpleEventBus`。
4. ⚠ **`Bing.EventBus` / `Bing.EventBus.Abstractions` 是遗留包**（无人维护），里面的 `ILocalEventBus` / `IDistributedEventBus` **不要在新代码里用**——它们与 `Bing.Core` 的 `ISimpleEventBus` / `IMessageEventBus` **同名接口双重存在**，同时 using 会导致歧义编译失败。
5. ⚠ **`Bing.Events.Cap.MySql`** 是 CAP **2.6** 的源码镜像（换 MySqlConnector 以支持 FreeSQL），目前**无任何工程引用**，与 `Bing.Events` 用的 CAP 8.0.1 差几个大版本。除非你明确知道在做什么，否则不要启用。
6. **事务里别做外部调用**：把 HTTP / 文件 / 长耗时操作挪出事务，否则会占住数据库连接。

## 相关

- [子系统深挖](../../architecture/子系统深挖.md)（两级发布与 Outbox 的代码级解析）
- [最佳实践 §5](../最佳实践.md)（事件与消息）
- [术语表 §5](../../getting-started/术语表.md)（两级发布、Outbox、`[EventHandler]`）
