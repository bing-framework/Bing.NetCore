# 本地事件、消息事件与领域事件

## 功能说明

当前事件体系包含进程内总线、基于 CAP 的消息发布，以及聚合根领域事件记录与分发器。它们的生命周期与一致性保证不同，不能互换。

## 核心实现逻辑与关键文件

| 路径 | 职责 |
| --- | --- |
| `framework/src/Bing.Core/Bing/Events/` | 核心事件契约与进程内总线基础设施。 |
| `framework/src/Bing.Events/Default/` | 默认事件处理器管理与本地发布实现。 |
| `framework/src/Bing.Events/Cap/` | CAP 注册、消息发布、Headers 与追踪。 |
| `framework/src/Bing.Ddd.Domain/Bing/Domain/Entities/Events/` | 聚合根事件及领域事件分发器。 |
| `framework/src/Bing.EventBus/`、`framework/src/Bing.EventBus.Abstractions/`、`framework/src/Bing.Events.Cap.MySql/` | 遗留事件包与旧 CAP 镜像；不是当前 CAP 注册链的默认依赖。 |

本地处理发生在进程内；CAP 消息经持久化和代理发送。业务写入与消息是否共用事务取决于具体 ORM/CAP 装配，单独调用发布 API 不自动建立业务事务。

## 对外接口与用法

```csharp
using Bing.Events.Default;

services.AddDefaultEventBus();
```

跨服务使用 `Bing.Events.Cap` 的 `AddCapEventBus`，按宿主选择持久化 Provider 与代理；订阅类实现 `ICapSubscribe`，处理方法使用框架 `EventHandlerAttribute`。完整例子见[分布式事件使用说明](../guides/分布式事件使用说明.md)。

## 依赖与配置项

`Bing.Events` 依赖 DotNetCore.CAP；CAP Options 决定数据库、代理、消息版本与重试。EF Core 与 FreeSQL 的事务接入不同，需使用对应宿主模块。处理器扫描和 DI 注册必须覆盖业务程序集。

## 已知限制与注意事项

- 消息处理应支持重复投递；发布成功不表示全部消费者已处理。
- 聚合根 `AddDomainEvent` 不会自动触发当前工作单元中的事件排空；自动接入：待实现/待确认。
- `send:false` 等延迟发送路径需遵守事务/出箱上下文，见现有事件指南。
- 遗留包与现行总线存在同名接口；旧镜像的现行接入和升级兼容性：待实现/待确认。
