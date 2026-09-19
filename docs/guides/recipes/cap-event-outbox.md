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

> 🟢 只在本地消费就用 `ISimpleEventBus`——走 `IMessageEventBus` 会额外产生一次数据库写入 + 一次投递。
> ⚠ 但**它不是"不用装包"**：接口在 `Bing.Core`，实现却在 `Bing.Events` 包里。只发本地事件时，仍要装 `Bing.Events` 并调 `services.AddDefaultEventBus()`；且**处理器必须手工注册** `services.AddTransient<IEventHandler<MyEvent>, MyHandler>()`——总线不扫描处理器，漏注册不报错、发布静默无操作。

## 3. 定义事件（继承 `MessageEvent`，不要塞实体）

泛型约束是 `where TEvent : IMessageEvent`，**普通 DTO 传不进去**。最省事的做法是继承 `MessageEvent`（它已实现 `IMessageEvent`，自带 `Id` / `Time` / `Name` / `Data` / `Callback` / `Send`）：

```csharp
using Bing.Events.Messages;

public class OrderCreatedEvent : MessageEvent
{
    public Guid OrderId { get; set; }
    public string OrderNo { get; set; }
    public decimal Amount { get; set; }
    public DateTime OccurredAt { get; set; }
}

// 或用强类型版本，载荷类单独定义（admin 的 UserLoginMessageEvent 就是这种写法）
public class OrderCreatedEvent : MessageEvent<OrderCreatedPayload>
{
    public OrderCreatedEvent(OrderCreatedPayload data, bool send = true) : base(data)
    {
        Send = send;                       // 基类 Send 默认 false，这里按需显式设 true
        Name = "order.created";            // Topic 取的就是这个 Name，不是类型名
    }
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

        // ⚠ 必须是 IMessageEvent 实现；普通 DTO 编译不过
        await _bus.PublishAsync(new OrderCreatedEvent(new OrderCreatedPayload
        {
            OrderId = entity.Id,
            OrderNo = entity.OrderNo,
            Amount = entity.Amount,
            OccurredAt = DateTime.UtcNow
        }));
        return entity.Id;
    }
}
```

> **只有 `Data` 会被序列化。** `PublishAsync<TEvent>(@event)` 内部转调 `PublishAsync(@event.Name, @event.Data, @event.Callback, @event.Send)`（`Cap/MessageEventBus.cs:52-54`）——Topic 取 `Name`，载荷取 `Data`，事件类上的其它属性**不会**进入消息。

**Outbox 怎么保证可靠**：消息与业务数据写进**同一数据库事务**，提交后由 CAP 异步投递。业务成功 ⇒ 消息必然存在；投递失败会重试。

⚠ 这条链路**三个前提缺一即静默丢消息**：① 当前必须有活动事务；② 工作单元必须在提交时调用 `TransactionActionManager.CommitAsync`；③ 该 UoW 要认识 CAP 的 ambient transaction。`AdminUnitOfWork` 三条都满足；而 `Bing.EntityFrameworkCore` 对 CAP **零引用**——基类 `UnitOfWorkBase` 确实会在事务内执行登记的动作（`Core/UnitOfWorkBase.cs:434-436`、`:558-560`），但用的是**自己的裸事务**，不与 CAP 共享，于是原子性丧失（**不报错**）。详见[分布式事件使用说明 §4](../分布式事件使用说明.md)。

## 5. 订阅

```csharp
// ICapSubscribe 可直接实现；admin 是让它由基类 MessageEventHandlerBase 实现
public class OrderCreatedHandler : ICapSubscribe
{
    private readonly IOrderStore _store;
    private readonly IUnitOfWork _unitOfWork;

    [EventHandler("order.created", Group = "order-service")]   // ⚠ 不是 [CapSubscribe]
    public async Task HandleAsync(OrderCreatedPayload payload) // ⚠ 收 Data 的反序列化结果，不是事件类
    {
        // 🔴 幂等：至少一次投递，重复消费一定会发生
        if (await _store.ExistsAsync(x => x.OrderNo == payload.OrderNo))
            return;

        // ... 业务处理
        await _unitOfWork.CommitAsync();
    }
}
```

> **订阅者要手工注册。** 框架不使用订阅者自动扫描，必须自己 `services.AddTransient<OrderCreatedHandler>()`（admin 就是在 `CapModule.cs:77-81` 逐个注册的——注意注册的是**具体处理器类型**，不是接口）。

## 注意事项

1. 🔴 **订阅用 `[EventHandler]`，不是 `[CapSubscribe]`**。`[EventHandler]` 继承 CAP 的 `TopicAttribute`（所以支持 `Group` 命名属性），第一个参数是 Topic；`ICapSubscribe` 标记接口则通常由基类实现。**订阅者不会自动扫描，必须手工注册**——漏注册订阅静默不生效。
2. 🔴 **消费端必须幂等**：消息是**至少一次**投递。不幂等就是重复下单、重复扣款。
3. 🔴 **别把普通 DTO 当事件传**：`PublishAsync` 的泛型约束是 `where TEvent : IMessageEvent`，且只有 `Data` 会被序列化。事件类继承 `MessageEvent` 最省事。
4. **别把本地事件发到消息总线**：`IMessageEventBus` 会带来一次落库 + 一次投递的额外成本，纯本地场景用 `ISimpleEventBus`（但它也需要 `AddDefaultEventBus()` + 手工注册处理器）。
5. ⚠ **`Bing.EventBus` / `Bing.EventBus.Abstractions` 是遗留包**（无人维护），里面的 `ILocalEventBus` / `IDistributedEventBus` **不要在新代码里用**——它们与 `Bing.Core` 的 `ISimpleEventBus` / `IMessageEventBus` **同名接口双重存在**，同时 using 会导致歧义编译失败。
6. ⚠ **`Bing.Events.Cap.MySql`** 是 CAP 的源码镜像（换 MySqlConnector 以支持 FreeSQL），目前**无任何工程引用**，与 `Bing.Events` 用的 CAP 8.0.1 相差数个版本。除非你明确知道在做什么，否则不要启用。
7. **事务里别做外部调用**：把 HTTP / 文件 / 长耗时操作挪出事务，否则会占住数据库连接。

## 相关

- [本地事件与消息事件使用说明](../本地事件与消息事件使用说明.md)（两套机制的定义、区别、对照表与防误用清单）
- [分布式事件使用说明](../分布式事件使用说明.md)（Outbox 三个前提、TraceId 贯通、11 条陷阱）
- [子系统深挖](../../architecture/子系统深挖.md)（两级发布与 Outbox 的代码级解析）
- [最佳实践 §5](../最佳实践.md)（事件与消息）
- [术语表 §5](../../getting-started/术语表.md)（两级发布、Outbox、`[EventHandler]`）
