# Bing.Events
[![NuGet](https://img.shields.io/nuget/v/Bing.Events.svg)](https://www.nuget.org/packages/Bing.Events/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Events.svg)](https://www.nuget.org/packages/Bing.Events/) ![TFM](https://img.shields.io/badge/TFM-net6.0-blue.svg)

> 事件总线：本地事件 + 基于 DotNetCore.CAP 的分布式事件。
>
> **目标框架**：`net6.0` ｜ **分层**：横切 / 事件 ｜ **当前版本**：7.0.0 ｜ **源文件**：10
>
> **上游 Bing 包**：`Bing.Aop.AspectCore`、`Bing.Data`
>
> **三方依赖**：`DotNetCore.CAP`

---

## 这是什么

统一的事件总线抽象，两层发布：

- `ISimpleEventBus` / `IEventBus` —— 本地事件（进程内）；
- `IMessageEventBus` —— 分布式集成事件（基于 **DotNetCore.CAP 8.0.1**），支持 Outbox 事务一致性。

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

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `IEventBus` / `ISimpleEventBus` / `IMessageEventBus` | `Bing.Events` / `.Messages` | 总线契约 |
| `[EventHandler]` | `Bing.Events` | 订阅特性（继承自 CAP `TopicAttribute`） |

## 与其它包的关系

**本包依赖**：`Bing.Aop.AspectCore`、`Bing.Data`

**三方 NuGet**：`DotNetCore.CAP`

## 注意事项

⚠ 仓库里还有一套遗留的 `Bing.EventBus`（命名空间 `Bing.EventBus`），它与本包**都有** `IEventBus` / `IMessageEventBus`，同时 using 会歧义。新代码请只用 `Bing.Events`。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [横切能力文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/横切能力文档.md) —— 邮件 / 模板 / 锁 / 本地化 / 事件总线的详解与选型
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Events

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
