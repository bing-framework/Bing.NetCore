# Bing.Events.EventBusAdapter

[![Bing.Events.EventBusAdapter](https://img.shields.io/nuget/v/Bing.Events.EventBusAdapter.svg)](https://www.nuget.org/packages/Bing.Events.EventBusAdapter/) [![Bing.Events.EventBusAdapter](https://img.shields.io/nuget/vpre/Bing.Events.EventBusAdapter.svg)](https://www.nuget.org/packages/Bing.Events.EventBusAdapter/) [![Bing.Events.EventBusAdapter](https://img.shields.io/nuget/dt/Bing.Events.EventBusAdapter.svg)](https://www.nuget.org/packages/Bing.Events.EventBusAdapter/)

该包在旧 `Bing.EventBus.IMessageEventBus` 契约与当前 CAP 事件总线之间提供显式适配，便于现有调用方逐步迁移。

## 使用

```csharp
services.AddLegacyCapMessageEventBusAdapter();
```

注册入口以 Scoped 生命周期提供 `LegacyCapMessageEventBusAdapter`。宿主仍需按所用传输和存储配置 CAP；本包不会自动注册 CAP，也不会把本地事件转换为分布式事件。

## 依赖与限制

- 依赖 `Bing.EventBus` 与 `Bing.Events`。
- 目标框架为 `net6.0`。
- 适配器保持旧消息契约的调用入口，但可靠投递、事务出箱和消费者幂等仍由 CAP 配置及业务处理负责。
- 同一业务流程应选择一个发布入口，避免新旧入口同时发布造成重复消息。
