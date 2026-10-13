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
var memory = new CacheConfiguration("admin", "admin-memory");
services.AddMemoryCaching(memory);

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
| 命名空间 | Redis 配置必须提供非空命名空间，底层客户端不得再叠加隐藏前缀。 |
| 拓扑 | `CacheTopology` 区分单节点、Redis Cluster 和客户端分片；分片要求稳定节点清单和一致的预期分片数。 |
| 资源预算 | `CacheResourceOptions` 控制回源、批量响应、扫描、删除命令与 Hybrid 失效消息边界；`0` 通常表示不限制。 |
| 协调 | `CacheCoordinationOptions` 控制租约、等待、失效投递与故障处理。 |
| Provider | CSRedisCore、FreeRedis 或 EasyCaching；Cluster/分片/Hybrid 的部分路径要求 Provider 暴露 StackExchange.Redis 数据库。 |

## 已知限制与注意事项

- 不存在跨实现通用的 `services.AddCache()`；注册入口因 Provider 而异。
- 扫描删除不是跨批次事务。命令字节预算按 UTF-8 参数字节估算，不包含 Redis 协议帧。
- 未暴露页边界的 Provider 无法精确执行页数预算，但仍受键数预算约束。
- 客户端分片不提供跨分片事务；失败时只保证已启动任务被观察，不能回滚已成功分片。
- EasyCaching 的 Cluster、分片、租约和 Redis outbox 能力依赖底层 Provider 暴露原生数据库；不满足时会明确抛出 `NotSupportedException`。
- Hybrid 失效消息预算不等同于消息代理帧上限；是否具备持久 outbox 和 dead-letter 管理取决于注册方式。
- 外部 Redis 实机验证受环境门控；未运行对应集成测试时，发布结论统一标注为待确认。
