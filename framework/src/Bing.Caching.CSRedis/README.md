# Bing.Caching.CSRedis
[![NuGet](https://img.shields.io/nuget/v/Bing.Caching.CSRedis.svg)](https://www.nuget.org/packages/Bing.Caching.CSRedis/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Caching.CSRedis.svg)](https://www.nuget.org/packages/Bing.Caching.CSRedis/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 基于 CSRedisCore 的 `ICache` 实现（当前推荐）。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：1
>
> **上游 Bing 包**：`Bing.Caching`
>
> **三方依赖**：`CSRedisCore`

---

## 这是什么

`CSRedisCacheManager` 实现 `ICache` / `IRedisCache`，通过依赖注入获取独立的 `CSRedisClient` 和必填的缓存命名空间。

## 安装

```bash
dotnet add package Bing.Caching.CSRedis
```

## 快速上手

```csharp
services.AddSingleton(new CSRedisClient("127.0.0.1:6379,password=,defaultDatabase=0"));
services.AddSingleton(new CacheConfiguration("my-app", "csredis"));
services.AddScoped<ICache, CSRedisCacheManager>();

// 使用
var user = await cache.GetAsync("user:1", async () => await repo.FindByIdAsync(1));
await cache.SetAsync(new CacheKey("user:{0}", 1), user,
    new CacheOptions { Expiration = TimeSpan.FromMinutes(10) });
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `CSRedisCacheManager` | `Bing.Caching.CSRedis` | `ICache` / `IRedisCache` 实现 |

## 与其它包的关系

**本包依赖**：`Bing.Caching`

**三方 NuGet**：`CSRedisCore`

## 注意事项

`Clear` 只扫描并删除当前命名空间的键，不执行 `FLUSHDB`。升级后旧物理键不会回读，需为冷缓存回源预留容量。不要在 CSRedis 连接串中配置 `prefix`，否则构造时会报错。客户端生命周期由 DI 管理。

删除批次同时受 `CacheConfiguration.BatchSize` 与 `CacheResourceOptions.MaxBatchCommandBytes` 限制，后者按数据键和 `:lease` 键参数的 UTF-8 字节数计算，不包括 Redis 协议帧。原生 `SCAN` 路径还支持 `CacheResourceOptions.MaxScanPages`；页预算耗尽后不会继续发送下一条 `SCAN` 命令，默认值 `0` 不限制。

多节点部署必须设置 `CacheTopology.RedisCluster` 或 `CacheTopology.ClientSharded`，并提供主节点扫描客户端。客户端分片须另设 `expectedShardCount`，数量不符或 Redis 实例重复时构造失败；Cluster 扫描前会按当前主节点身份核对客户端实际发现的节点，遗漏、重复或节点失效时操作抛出异常。主节点列表须随拓扑变化更新，不能将副本或生产外的节点混入。普通按键读写仍由主客户端路由。客户端分片的批量读取直连对应扫描节点；若 CSRedisClient 使用自定义 `NodeRule`，构造 `CSRedisCacheManager` 时必须传入作用于物理键的 `shardNodeIndex`，返回与 `scanClients` 顺序一致的节点索引。默认 `NodeRule` 可省略该参数。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [横切能力文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/横切能力文档.md) —— 邮件 / 模板 / 锁 / 本地化 / 事件总线的详解与选型
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Caching.CSRedis

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
