# Bing.Caching.FreeRedis
[![NuGet](https://img.shields.io/nuget/v/Bing.Caching.FreeRedis.svg)](https://www.nuget.org/packages/Bing.Caching.FreeRedis/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Caching.FreeRedis.svg)](https://www.nuget.org/packages/Bing.Caching.FreeRedis/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 基于 FreeRedis 的 Redis 缓存实现（`FreeRedisCacheManager`）。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0
>
> **上游 Bing 包**：`Bing.Caching`
>
> **三方依赖**：`FreeRedis`

---

## 这是什么

实现 `IRedisCache`。**没有注册入口**，需要自己先注册 FreeRedis 的 `RedisClient`，再注册管理器。

## 安装

```bash
dotnet add package Bing.Caching.FreeRedis
```

## 快速上手

```csharp
var client = new RedisClient("127.0.0.1:6379,password=,defaultDatabase=0");
client.Serialize = Newtonsoft.Json.JsonConvert.SerializeObject;
client.Deserialize = Newtonsoft.Json.JsonConvert.DeserializeObject;
services.AddSingleton(client);
services.AddSingleton(new CacheConfiguration("my-app", "freeredis"));
services.AddSingleton<IRedisCache, FreeRedisCacheManager>();
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `FreeRedisCacheManager` | `Bing.Caching.FreeRedis` | `IRedisCache` 实现，构造需要 `RedisClient` 和 `CacheConfiguration` |

## 与其它包的关系

**本包依赖**：`Bing.Caching`

**三方 NuGet**：`FreeRedis`

## 注意事项

`Clear` 仅删除本实例命名空间内的键；旧物理键不会回读，也不会被新 `Clear` 清理。不要在 FreeRedis 连接串中配置 `prefix`，构造时会拒绝隐藏前缀。缓存回源委托必须幂等，租约仅提供缓存级协调保证。

删除批次同时受 `CacheConfiguration.BatchSize` 与 `CacheResourceOptions.MaxBatchCommandBytes` 限制，后者按数据键和 `:lease` 键参数的 UTF-8 字节数计算，不包括 Redis 协议帧。原生 `SCAN` 路径还支持 `CacheResourceOptions.MaxScanPages`；页预算耗尽后不会继续发送下一条 `SCAN` 命令，默认值 `0` 不限制。

Cluster 和客户端分片必须在 `CacheConfiguration` 指定拓扑，并向构造函数提供全部主节点的单节点扫描客户端列表。客户端分片还须设置 `expectedShardCount`；数量不符或 Redis 实例重复时构造失败。客户端分片按固定列表顺序路由，变更顺序会导致旧缓存键不可见；Cluster 的普通命令由主客户端路由。Cluster 扫描前会核对当前主节点身份，列表遗漏、重复或节点失效时操作抛出异常。前缀查询返回列表，内存仍与匹配值数量成正比。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [横切能力文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/横切能力文档.md) —— 邮件 / 模板 / 锁 / 本地化 / 事件总线的详解与选型
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Caching.FreeRedis

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
