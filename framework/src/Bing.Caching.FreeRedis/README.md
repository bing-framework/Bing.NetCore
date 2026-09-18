# Bing.Caching.FreeRedis
[![NuGet](https://img.shields.io/nuget/v/Bing.Caching.FreeRedis.svg)](https://www.nuget.org/packages/Bing.Caching.FreeRedis/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Caching.FreeRedis.svg)](https://www.nuget.org/packages/Bing.Caching.FreeRedis/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 基于 FreeRedis 的 Redis 缓存实现（`FreeRedisCacheManager`）。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：1
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
services.AddSingleton(new RedisClient("127.0.0.1:6379,password=,defaultDatabase=0"));
services.AddSingleton<IRedisCache, FreeRedisCacheManager>();
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `FreeRedisCacheManager` | `Bing.Caching.FreeRedis` | `IRedisCache` 实现，构造需要 `RedisClient` |

## 与其它包的关系

**本包依赖**：`Bing.Caching`

**三方 NuGet**：`FreeRedis`

## 注意事项

⚠ 无 `AddXxx` 入口；注册前确认你期望 `ICache` 解析到哪个实现。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [横切能力文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/横切能力文档.md) —— 邮件 / 模板 / 锁 / 本地化 / 事件总线的详解与选型
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Caching.FreeRedis

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
