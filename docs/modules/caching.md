# 缓存模块

> 内容核对：2026-10-13。对应 `Bing.Caching`、`Bing.Caching.CSRedis`、`Bing.Caching.FreeRedis`、`Bing.EasyCaching`。

## 功能说明

缓存模块提供统一的 `ICache` / `ILocalCache` / `IRedisCache` 抽象、逻辑键到物理键的命名空间规则，以及 CSRedis、FreeRedis、EasyCaching 三类实现。当前代码还覆盖批量操作、扫描删除、资源预算、单机/Cluster/客户端分片拓扑和 Hybrid 本地加分布式缓存协调。

## 核心实现逻辑与关键文件

| 层次 | 关键路径 | 行为 |
| --- | --- | --- |
| 契约与边界 | `framework/src/Bing.Caching/Bing/Caching/` | 缓存接口、`CacheConfiguration`、键格式、拓扑、输入限制、扫描/批量/失效消息预算。 |
| CSRedis | `framework/src/Bing.Caching.CSRedis/Bing/Caching/CSRedis/` | Redis 键值、批量、流式扫描、清理与协调实现。 |
| FreeRedis | `framework/src/Bing.Caching.FreeRedis/Bing/Caching/FreeRedis/` | Redis 适配、批量、流式扫描与协调实现。 |
| EasyCaching | `framework/src/Bing.EasyCaching/Bing/EasyCaching/` | Memory、Redis、Hybrid 注册，Cluster/分片路由、租约、失效消息、outbox 与 dead-letter 管理。 |
| 验证 | `framework/tests/Bing.Caching.Tests/`、`framework/tests/Bing.EasyCaching.Tests/`、两个 Redis 集成测试工程 | 契约、预算、命名空间、拓扑、故障和取消行为。 |

物理键由受信任的 `CacheConfiguration` 统一生成。扫描与清理先限定命名空间，再按页数、键数、批量命令字节数等预算执行；Hybrid 写入还需要向其他节点发布失效消息。

## 对外接口与用法

EasyCaching 注册入口：

```csharp
using Bing.Caching;
using Bing.EasyCaching;
using EasyCaching.Core.Configurations;

var memory = new CacheConfiguration("admin", "admin-memory");
services.AddMemoryCaching(memory);
```

Redis 模式在另一个宿主中注册：

```csharp
var redis = new CacheConfiguration("admin", "admin-redis");
services.AddRedisCaching(redis, options =>
    options.DBConfig.Endpoints.Add(new ServerEndPoint("localhost", 6379)));
```

启用输入边界：

```csharp
var configuration = new CacheConfiguration("admin", "admin-redis")
    .WithInputLimits(new CacheInputLimits(
        maxLogicalKeyLength: 256,
        maxPrefixLength: 128,
        maxPatternLength: 256));
```

CSRedis 与 FreeRedis 没有统一 `AddCache` 入口，应按各包 README 注册底层客户端和对应管理器。命名空间 v2 升级步骤见[缓存命名空间 v2 迁移](../migrations/cache-namespace-v2.md)。

## 依赖与配置项

| 项 | 说明 |
| --- | --- |
| 命名空间 | 所有模式都要求非空 `Namespace` 与 `ProviderName`，底层客户端不得再叠加隐藏前缀。 |
| 拓扑 | `CacheTopology` 区分单节点、Redis Cluster 和客户端分片；分片要求稳定节点清单和一致的预期分片数。 |
| 资源预算 | `CacheResourceOptions` 控制回源、批量响应、扫描、删除命令与失效消息；各字段的零值语义见下表。 |
| 协调 | `CacheCoordinationOptions` 仅控制租约、续租、等待和回源超时。 |
| 失效投递 | `HybridInvalidationDeliveryOptions` 控制发布重试、Redis outbox 与死信；持久出箱要求原生 Redis 数据库。 |
| Provider | CSRedisCore、FreeRedis 或 EasyCaching；Cluster/分片/Hybrid 的部分路径要求 Provider 暴露 StackExchange.Redis 数据库。 |

默认值来自 `CacheConfiguration`、`CacheResourceOptions` 和 `CacheCoordinationOptions`：

| 配置项 | 默认值与约束 |
| --- | --- |
| `DefaultExpiration` / `BatchSize` / `MaxConcurrency` | 8 小时 / 500 / 8；批量和并发必须大于零。 |
| `MaxConcurrentFactories` / `MaxQueuedFactories` | 8 / 128；回源并发必须大于零，队列为零表示不排队。 |
| `MaxBatchPayloadBytes` / `MaxBatchCommandBytes` | 各 8 MiB；必须大于零。 |
| `MaxScanKeys` / `MaxScanPages` / `MaxInvalidationMessageBytes` | 均为 0；仅这三项的零值表示不限制。 |
| `LeaseDuration` / `RenewalInterval` | 30 秒 / 10 秒。 |
| `WaitTimeout` / `RetrievalTimeout` | 30 秒 / 2 分钟；协调时长均需大于零。 |

失效投递默认只发布一次（`MaxPublishAttempts = 1`），无重试间隔；`MaxPendingMessages = 0` 关闭 Redis outbox，正数设置待发消息上限。`MaxDeliveryAttempts` 与 `DeadLetterMaxMessages` 必须同时为正数才启用有界死信，默认同时为零。`DeadLetterMaxBytes` 默认零表示不限制字节，`DeadLetterRetention` 默认零表示不自动过期；清理间隔默认 1 分钟、批次 100，轮询间隔 1 秒。完整校验与配置入口见 `framework/src/Bing.EasyCaching/Bing/EasyCaching/HybridInvalidationDeliveryOptions.cs` 和包 README。

## 已知限制与注意事项

- 不存在跨实现通用的 `services.AddCache()`；注册入口因 Provider 而异。
- EasyCaching 的 `ICache` 默认注册使用 `TryAddScoped`；同一容器同时注册多个模式时不会自动选择最后一种，需按 `ILocalCache` / `IRedisCache` 或自定义装配区分。
- 扫描删除不是跨批次事务。命令字节预算按 UTF-8 参数字节估算，不包含 Redis 协议帧。
- 未暴露页边界的 Provider 无法精确执行页数预算，但仍受键数预算约束。
- 客户端分片不提供跨分片事务；失败时只保证已启动任务被观察，不能回滚已成功分片。
- EasyCaching 的 Cluster、分片、租约和 Redis outbox 能力依赖底层 Provider 暴露原生数据库；不满足时会明确抛出 `NotSupportedException`。
- Hybrid 失效消息预算不等同于消息代理帧上限；是否具备持久 outbox 和 dead-letter 管理取决于注册方式。
- 外部 Redis 实机验证受环境门控；未运行对应集成测试时，发布结论标注为“待实现/待确认”。
