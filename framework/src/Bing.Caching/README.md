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

定义缓存契约、实例配置和版本化物理键。具体实现位于 `Bing.Caching.CSRedis`、`Bing.Caching.FreeRedis` 和 `Bing.EasyCaching`。

## 安装

```bash
dotnet add package Bing.Caching
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `ICache` | `Bing.Caching` | 缓存抽象 |
| `CacheKey` / `CacheOptions` | `Bing.Caching` | 缓存键与选项 |
| `CacheConfiguration` / `CacheKeyFormat` | `Bing.Caching` | 命名空间、TTL、拓扑与物理键规则 |
| `CacheResourceOptions` | `Bing.Caching` | 回源、批量响应、扫描、删除命令及 Hybrid 失效消息资源预算 |
| `CacheInputLimits` | `Bing.Caching` | 可选的逻辑键、前缀和模式长度边界 |

## 与其它包的关系

**本包依赖**：`Bing.Core`

**依赖本包**：`Bing.AspNetCore.Authentication.JwtBearer`、`Bing.Caching.CSRedis`、`Bing.Caching.FreeRedis`、`Bing.EasyCaching`

**三方 NuGet**：`Microsoft.Extensions.Caching.Memory`

## 注意事项

Redis 适配器必须提供非空命名空间，且底层客户端不得再配置隐藏前缀。EasyCaching 提供 Memory、Redis 和 Hybrid 显式注册入口；其他适配器仍需注册客户端及缓存实例。

输入长度限制默认关闭，以保持兼容；生产接入可在可信配置中启用：

```csharp
var cacheConfig = new CacheConfiguration("admin", "admin-redis")
    .WithInputLimits(new CacheInputLimits(
        maxLogicalKeyLength: 256,
        maxPrefixLength: 128,
        maxPatternLength: 256));
```

限制按 .NET `string.Length` 计算。超限会在进入键生成、扫描或清理命令前抛出 `ArgumentOutOfRangeException`；`0` 表示对应输入不限制。该配置只限制逻辑输入，不限制 Redis 客户端已经接收的网络缓冲或最终 `List` 结果大小。

删除路径使用 `CacheResourceOptions.MaxBatchCommandBytes` 按 UTF-8 参数字节数切分 `DEL` 批次，同时删除数据键和对应的 `:lease` 键；该预算不包括 Redis 协议帧。单个键对超出预算时会在发送命令前抛出异常，批次之间不提供事务性。能够暴露原生 Redis `SCAN` 页或自定义页流的 Provider 还支持 `MaxScanPages`，用于限制已经返回并被适配器接受的扫描页数；默认值 `0` 不限制。未暴露页边界的逐键流和完整集合回退返回不可观测的页数 `-1`，仍由 `MaxScanKeys` 保护已接受候选。

非 Redis Provider 可以实现 `ICacheKeyPageStreamProvider`，按物理前缀异步返回 `IReadOnlyList<string>` 键页。适配器按页保持键顺序、执行取消和命名空间校验；页流未实现时仍兼容 `ICacheKeyStreamProvider` 和旧的完整键集合接口。页流提供器应在取消后停止产生新页，并接受达到页预算时当前页可能已由其内部读取的边界。

Hybrid 失效总线可通过 `CacheResourceOptions.MaxInvalidationMessageBytes` 限制单条消息的实际序列化载荷，并按物理键顺序拆分；默认值 `0` 不限制。单个键无法容纳时会在发布前抛出 `CacheInvalidationMessageLimitExceededException`。该预算不等价于消息代理网络帧上限，也不提供重试、背压或 outbox。

客户端分片同步清理默认保持分片顺序。配置 `CacheSyncCleanupMode.BoundedParallel` 后，EasyCaching 会按 `CacheConfiguration.MaxConcurrency` 分窗并发同步清理；窗口内任务全部观察结束，失败时停止派发后续窗口并按分片索引传播首个异常。该模式是显式选择，不提供跨分片事务性，也不接受取消令牌。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [横切能力文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/横切能力文档.md) —— 邮件 / 模板 / 锁 / 本地化 / 事件总线的详解与选型
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Caching

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
