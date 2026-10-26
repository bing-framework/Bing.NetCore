# 缓存模块

> 内容核对：2026-10-24。对应 `Bing.Caching`、`Bing.Caching.CSRedis`、`Bing.Caching.FreeRedis`、`Bing.EasyCaching`、`Bing.Caching.FusionCache`。

## 功能说明

缓存模块提供统一的 `ICache` / `ILocalCache` / `IRedisCache` 抽象、逻辑键到物理键的命名空间规则，以及 CSRedis、FreeRedis、EasyCaching 三类实现。当前代码还覆盖批量操作、扫描删除、资源预算、单机/Cluster/客户端分片拓扑和 Hybrid 本地加分布式缓存协调。新增的 `Bing.Caching.FusionCache` 是可选的 net8.0+ Provider，通过 `IHybridCache` 提供异步单键缓存、同键回源合并、Fail-Safe 和基于标签的失效；它不替换旧 `ICache` 契约。

## 核心实现逻辑与关键文件

| 层次 | 关键路径 | 行为 |
| --- | --- | --- |
| 契约与边界 | `framework/src/Bing.Caching/Bing/Caching/` | 缓存接口、`CacheConfiguration`、键格式、拓扑、输入限制、扫描/批量/失效消息预算。 |
| CSRedis | `framework/src/Bing.Caching.CSRedis/Bing/Caching/CSRedis/` | Redis 键值、批量、流式扫描、清理与协调实现。 |
| FreeRedis | `framework/src/Bing.Caching.FreeRedis/Bing/Caching/FreeRedis/` | Redis 适配、批量、流式扫描与协调实现。 |
| EasyCaching | `framework/src/Bing.EasyCaching/Bing/EasyCaching/` | Memory、Redis、Hybrid 注册，Cluster/分片路由、租约、失效消息、outbox 与 dead-letter 管理。 |
| FusionCache | `framework/src/Bing.Caching.FusionCache/Bing/Caching/FusionCache/` | 官方 FusionCache 2.9.0 适配、命名隔离、输入校验、TTL 映射和回源准入。 |
| 验证 | `framework/tests/Bing.Caching.Tests/`、`framework/tests/Bing.EasyCaching.Tests/`、两个 Redis 集成测试工程 | 契约、预算、命名空间、拓扑、故障和取消行为。 |

现有 `ICache` Provider 的物理键由受信任的 `CacheConfiguration` 统一生成；FusionCache 的独立身份前缀由 `AddBingFusionCache` 生成。扫描与清理先限定命名空间，再按页数、键数、批量命令字节数等预算执行；Hybrid 写入还需要向其他节点发布失效消息。

### FusionCache 集成

`IHybridCacheProvider.GetCache(name)` 显式选择 FusionCache 命名实例；`UseAsDefault` 仅在唯一实例明确启用时注册无名别名。实例身份和键/标签限制由 Bing 配置，L1/L2、Fail-Safe、刷新与超时通过官方 builder 配置。Redis L2、System.Text.Json serializer、Redis Backplane、分布式 locker、日志 provider 和 OpenTelemetry 均由宿主按需安装，不作为核心集成包的强制依赖。

Redis L2 负责跨进程共享条目，Backplane 负责通知其他实例失效其 L1；只有 L2 而没有 Backplane 时，其他实例的旧 L1 可能保留到 TTL。传播最终一致，不能作为事务提交、授权撤销或原子条件写入屏障。首版只承诺异步单键契约，不提供 TrySet、任意前缀/模式扫描、批量或流式读取。详细注册方式见 [FusionCache 包 README](../../framework/src/Bing.Caching.FusionCache/README.md)，迁移步骤见 [FusionCache 迁移指南](../migrations/fusioncache-adoption.md)，可运行样例见 [Bing.Samples.FusionCache](../../samples/Bing.Samples.FusionCache/README.md)。

FusionCache 的业务条目 TTL 不应覆盖内部标签/Clear 元数据的 `TagsDefaultEntryOptions` 长保留期。标签元数据默认使用前台 L2/Backplane 操作并重新抛出相关异常；业务条目的 Fail-Safe 需单独配置。启用 OpenTelemetry 工厂准入指标时，订阅 `Bing.Caching.FusionCache` meter；指标名包括 `bing.cache.factory.waiting`、`bing.cache.factory.active`、`bing.cache.factory.rejected` 与 `bing.cache.factory.wait.duration`。

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

CSRedis 与 FreeRedis 没有统一 `AddCache` 入口，应按各包 README 注册底层客户端和对应管理器。FusionCache 使用 `AddBingFusionCache` 注册入口。命名空间 v2 升级步骤见[缓存命名空间 v2 迁移](../migrations/cache-namespace-v2.md)；从旧 Provider 迁移到 FusionCache 见 [FusionCache 迁移指南](../migrations/fusioncache-adoption.md)。

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
- FusionCache 使用独立物理空间，不回读旧 Provider 的载荷；灰度期间业务更新必须同时失效仍在服务的旧、新缓存空间。
- FusionCache Backplane 与并发回源不是线性一致性协议；旧工厂可能在 Set/Remove 之后迟到写回，严格撤销或原子条件写入路径应保留旧 Provider。
- 外部 Redis 实机验证受环境门控；未运行对应集成测试时，发布结论标注为“待实现/待确认”。
