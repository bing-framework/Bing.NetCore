# 事件与一致性

> **适用版本**：框架 `7.0.0`｜源码核对：2026-10-13

## 功能说明

当前推荐事件入口是 `Bing.Events`：本地总线负责进程内通知，CAP 负责跨进程消息与事务出箱。EF Core 和 FreeSQL 工作单元会收集聚合根领域事件，并在事务动作阶段通过 `DomainEventDispatchSession` 有限轮次派发。消费收据、有序入箱、游标推进、恢复、指标和保留清理均已有生产实现。

## 核心实现

- 本地与 CAP 总线：`framework/src/Bing.Events/`
- 领域事件会话：`framework/src/Bing.Ddd.Domain/Bing/Domain/Entities/Events/DomainEventDispatchSession.cs`
- EF Core 接入：`framework/src/Bing.EntityFrameworkCore/Core/UnitOfWorkBase.cs`
- FreeSQL 接入：`framework/src/Bing.FreeSQL/Bing/Uow/UnitOfWorkBase.cs`
- 消费幂等：`framework/src/Bing.Events/Consumption/`
- 有序事件：`framework/src/Bing.Events/Ordering/`

## 用法

```csharp
services.AddDefaultEventBus();
services.AddCapEventBus(options => { /* CAP 存储与 RabbitMQ 配置 */ });
services.AddBingCapOrderedEventIngress();
services.AddBingOrderedConsumer<OrderHandler>("orders");
```

本地处理器需显式注册。跨节点消息应携带稳定事件 ID；需要顺序时还要提供业务流、实体键和连续序号。详细配置见[集群一致性指南](../guides/cluster-consistency.md)和[有序事件指南](../guides/ordered-events.md)。

## 依赖与配置

- CAP/RabbitMQ 由宿主配置，框架不内置生产凭据。
- 业务消费幂等记录必须与业务修改共用数据库事务。
- 有序事件需要部署对应入箱、游标和历史表。

## 限制与注意事项

- 本地事件不会跨节点广播；CAP 是至少一次投递，业务必须幂等。
- 仅设置 `Sequence` 消息头不会自动获得顺序保证，必须使用有序入箱与协调器。
- `MessageEvent.Send = false` 依赖工作单元提交动作；事务外使用可能没有发送时机。
- 领域事件派发已接入 EF Core 和 FreeSQL 工作单元，但派发器不等于可靠跨进程消息；需要可靠投递时仍应写入 CAP 出箱。
- RabbitMQ 网络重排、重投和并发消费不能仅靠单消费者参数解决。
