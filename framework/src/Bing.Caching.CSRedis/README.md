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

`CSRedisCacheManager` 实现 `ICache` / `IRedisCache`。因为依赖 CSRedis 的**静态初始化**，需要两步注册。`modules/admin` 使用的就是它。

## 安装

```bash
dotnet add package Bing.Caching.CSRedis
```

## 快速上手

```csharp
// ① CSRedis 静态初始化（必须）
RedisHelper.Initialization(new CSRedisClient("127.0.0.1:6379,password=,defaultDatabase=0"));

// ② 注册 ICache
services.AddScoped<ICache, CSRedisCacheManager>();

// 使用
var user = await cache.GetAsync("user:1", async () => await repo.FindByIdAsync(1));
await cache.SetAsync(new CacheKey("user:{0}", 1), user, TimeSpan.FromMinutes(10));
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `CSRedisCacheManager` | `Bing.Caching.CSRedis` | `ICache` / `IRedisCache` 实现 |

## 与其它包的关系

**本包依赖**：`Bing.Caching`

**三方 NuGet**：`CSRedisCore`

## 注意事项

⚠ **必须先 `RedisHelper.Initialization(...)`**，否则运行时取不到 Redis 客户端。本包也没有 `AddXxx` 入口。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [横切能力文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/横切能力文档.md) —— 邮件 / 模板 / 锁 / 本地化 / 事件总线的详解与选型
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Caching.CSRedis

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
