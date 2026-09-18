# Bing.Locks.CSRedis
[![NuGet](https://img.shields.io/nuget/v/Bing.Locks.CSRedis.svg)](https://www.nuget.org/packages/Bing.Locks.CSRedis/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Locks.CSRedis.svg)](https://www.nuget.org/packages/Bing.Locks.CSRedis/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 基于 CSRedis 的分布式锁（`IDistributedLock`）。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：横切 / 工具 ｜ **当前版本**：7.0.0 ｜ **源文件**：2
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

`CSRedisDistributedLock` 实现 `IDistributedLock`，提供 `ExecuteWithLockAsync` 临界区语义。

## 安装

```bash
dotnet add package Bing.Locks.CSRedis
```

## 快速上手

```csharp
services.AddRedisDistributedLock();   // ⚠ 需先 RedisHelper.Initialization(...)

await distributedLock.ExecuteWithLockAsync(
    "order:1", Guid.NewGuid().ToString(), TimeSpan.FromSeconds(30),
    async () => { /* 临界区 */ });
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddRedisDistributedLock()` / `<TDistributedLock>()` | `Bing.Locks` | 注册单例 `IDistributedLock` |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `IDistributedLock` | `Bing.Locks` | 分布式锁契约 |
| `CSRedisDistributedLock` | `Bing.Locks` | CSRedis 实现 |

## 与其它包的关系

## 注意事项

⚠ **必须先 `RedisHelper.Initialization(...)`** 完成 CSRedis 静态初始化，否则运行时拿不到 Redis 客户端。进程内锁请用 `Bing.Core` 的 `AddLocalLock()`。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [横切能力文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/横切能力文档.md) —— 邮件 / 模板 / 锁 / 本地化 / 事件总线的详解与选型
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Locks.CSRedis

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
