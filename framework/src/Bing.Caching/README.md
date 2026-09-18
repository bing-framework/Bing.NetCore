# Bing.Caching
[![NuGet](https://img.shields.io/nuget/v/Bing.Caching.svg)](https://www.nuget.org/packages/Bing.Caching/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Caching.svg)](https://www.nuget.org/packages/Bing.Caching/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 缓存抽象 `ICache`（含 `ILocalCache` / `IRedisCache`）与 `CacheKey`。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：10
>
> **上游 Bing 包**：`Bing.Core`
>
> **三方依赖**：`Microsoft.Extensions.Caching.Memory`

---

## 这是什么

只定义缓存契约。**具体实现在** `Bing.Caching.CSRedis`（推荐）/ `Bing.Caching.FreeRedis` / `Bing.EasyCaching`（当前不可用）。

## 安装

```bash
dotnet add package Bing.Caching
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `ICache` | `Bing.Caching` | 缓存抽象 |
| `CacheKey` / `CacheOptions` | `Bing.Caching` | 缓存键与选项 |

## 与其它包的关系

**本包依赖**：`Bing.Core`

**依赖本包**：`Bing.AspNetCore.Authentication.JwtBearer`、`Bing.Caching.CSRedis`、`Bing.Caching.FreeRedis`、`Bing.EasyCaching`

**三方 NuGet**：`Microsoft.Extensions.Caching.Memory`

## 注意事项

⚠ 框架**没有** `AddCache` / `AddCaching` 注册入口，缓存必须手工注册，详见文档。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [横切能力文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/横切能力文档.md) —— 邮件 / 模板 / 锁 / 本地化 / 事件总线的详解与选型
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Caching

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
