# 锁与限流

> **适用版本**：框架 `7.0.0`｜源码核对：2026-10-13

## 功能说明

框架提供按键本地异步互斥、Redis 租约锁、本地令牌桶、Redis 令牌桶和 ASP.NET Core 限流中间件。HTTP 限流按策略、路由以及租户/用户或客户端地址分桶，超额返回 `429` 和 `Retry-After`。

## 核心实现

- 锁契约与本地实现：`framework/src/Bing.Core/Bing/Locks/`
- Redis 锁：`framework/src/Bing.Locks.CSRedis/Bing/Locks/CSRedisDistributedLock.cs`
- 令牌桶契约与本地实现：`framework/src/Bing.Core/Bing/RateLimiting/`
- Redis 令牌桶：`framework/src/Bing.Locks.CSRedis/Bing/RateLimiting/`
- HTTP 适配：`framework/src/Bing.AspNetCore/Bing/AspNetCore/RateLimiting/`

## 用法

```csharp
services.AddBingLocalRateLimiting(100, 20, TimeSpan.FromMinutes(10), 10_000);
app.UseBingRateLimiting();
```

Redis 限流通过 `Bing.Locks.CSRedis` 注册。锁的新代码优先使用 `ILockLeaseProvider`，把等待时间、租期、所有者和取消令牌分开表达。

## 依赖与配置

- 本地实现只协调单进程；集群配额或互斥需 Redis 实现。
- 未认证请求使用 `RemoteIpAddress`，反向代理场景必须先配置可信代理和真实 IP 解析。
- 限流存储异常默认返回 `503`；只有明确配置 `FailOpenOnInfrastructureError` 才放行。

## 限制与注意事项

- Redis 租约可能过期，主从切换也可能破坏独占假设；库存正确性不能只依赖分布式锁。
- 数据库条件更新和唯一约束才是防超卖及幂等的最终保障。
- 本地限流额度会随实例数增加，集群统一额度应使用 Redis。
- 框架未提供通用无锁队列；进程内原子状态应使用运行库的 `Interlocked` 和并发集合。
