# Bing.Caching.FusionCache

[![NuGet](https://img.shields.io/nuget/v/Bing.Caching.FusionCache.svg)](https://www.nuget.org/packages/Bing.Caching.FusionCache/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Caching.FusionCache.svg)](https://www.nuget.org/packages/Bing.Caching.FusionCache/) ![TFM](https://img.shields.io/badge/TFM-net8.0-blue.svg)

**Bing.Caching.FusionCache** 将官方 FusionCache 2.9.0 接入 Bing 的轻量异步混合缓存契约 IHybridCache。它是可选 Provider，不替换或重注册旧 ICache、ILocalCache、IRedisCache 或 EasyCaching 服务。新包目标为 net8.0；现有 Bing.Caching 与旧 Provider 的目标框架和行为不变。

## 安装与注册

```csharp
using Bing.Caching;
using Bing.Caching.FusionCache;
using ZiggyCreatures.Caching.Fusion;

services.AddBingFusionCache(
    "catalog",
    options =>
    {
        options.Namespace = "store:production";
        options.UseAsDefault = true;
    },
    fusion => fusion.WithDefaultEntryOptions(options =>
    {
        options.Duration = TimeSpan.FromMinutes(5);
        options.IsFailSafeEnabled = true;
        options.FailSafeMaxDuration = TimeSpan.FromMinutes(10);
        options.FailSafeThrottleDuration = TimeSpan.FromSeconds(10);
        options.FactorySoftTimeout = TimeSpan.FromMilliseconds(200);
        options.FactoryHardTimeout = TimeSpan.FromSeconds(2);
        options.AllowTimedOutFactoryBackgroundCompletion = true;
    }));
```

默认策略为 TTL 5 分钟、工厂硬超时 2 分钟、最多 8 个活跃工厂和 128 个排队工厂、排队 30 秒、自动恢复队列最多 1000 项。Fail-Safe、提前刷新及超时后后台完成默认关闭；上例展示按业务显式开启这些能力的方式。

通过 IHybridCacheProvider.GetCache("catalog") 选择命名实例；只有一个实例配置 UseAsDefault = true 时，才可直接注入无名 IHybridCache。Namespace 必须包含应用和环境等受信身份，不能由请求参数控制。

业务条目的 TTL 与内部标签/Clear 元数据策略相互独立。注册器保留 FusionCache 原生较长的 `TagsDefaultEntryOptions` 保留期；标签元数据的 L2/Backplane 操作默认前台执行（`AllowBackground... = false`），相关异常重新抛出（`ReThrow... = true`）。业务 Fail-Safe 只配置在普通 `DefaultEntryOptions`，不要把业务 TTL 或容错策略覆盖到标签元数据上。

## 有界 L1 内存

需要限制 L1 时，为一个命名缓存单独注册 `MemoryCache` singleton，并由 FusionCache builder 使用；不要将有大小限制的实例共享给无关缓存或其他业务：

```csharp
const long l1Capacity = 1_000;
services.AddSingleton<IMemoryCache>(_ => new MemoryCache(new MemoryCacheOptions
{
    SizeLimit = l1Capacity
}));

services.AddBingFusionCache(
    "catalog",
    options => options.Namespace = "store:development",
    fusion => fusion
        .WithMemoryCache(serviceProvider => serviceProvider.GetRequiredService<IMemoryCache>())
        .WithDefaultEntryOptions(options =>
        {
            options.Duration = TimeSpan.FromMinutes(5);
            options.Size = 1;
        })
        .WithOptions(options => options.TagsDefaultEntryOptions.Size = 0));
```

`SizeLimit` 使用应用自定义单位，不代表字节数；上例按业务条目计 1 单位，标签元数据计 0 单位且继续使用原生长保留期。FusionCache/MemoryCache 的估算单位和 `1_000` 都只是演示配置，不能直接作为生产容量承诺。

## Redis L2 与多实例失效

核心包不强制 Redis、serializer、Backplane、locker、日志 provider 或 OpenTelemetry。宿主需要显式安装并配置官方 FusionCache 2.9.0 组件；Redis L2 使用 Microsoft.Extensions.Caching.StackExchangeRedis 10.0.1：

```csharp
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnection;
    options.InstanceName = string.Empty;
});

services.AddBingFusionCache(
    "catalog",
    options => options.Namespace = "store:production",
    fusion => fusion
        .WithDefaultEntryOptions(options => options.Duration = TimeSpan.FromMinutes(5))
        .WithSerializer(new FusionCacheSystemTextJsonSerializer())
        .WithRegisteredDistributedCache()
        .WithBackplane(new RedisBackplane(new RedisBackplaneOptions
        {
            Configuration = redisConnection
        })));
```

官方组件包包括 ZiggyCreatures.FusionCache.Serialization.SystemTextJson、ZiggyCreatures.FusionCache.Backplane.StackExchangeRedis、ZiggyCreatures.FusionCache.Locking.Distributed.Redis 和 ZiggyCreatures.FusionCache.OpenTelemetry，统一锁定 2.9.0。Backplane 负责多实例 L1 失效通知；L2 本身不会使另一进程的 L1 立即失效。若不同命名实例共用同一个 IDistributedCache，应由集成层保证 CacheKeyPrefix 和 BackplaneChannelPrefix 隔离，宿主不要再叠加隐式 Redis InstanceName 前缀。

注册器强制等待首次 Backplane 订阅并在订阅失败时使宿主启动失败；不允许覆盖为异步就绪。默认保留期及每次 TTL 覆盖都会检查数据最长物理窗口（含 L1/L2、Fail-Safe、抖动）不超过元数据最短物理窗口，不安全配置在写入/回源前失败。

可选 distributed locker 通过原生 builder WithDistributedLocker(...) 接线。它只协助跨节点回源合并，不提供业务严格一次执行、原子 TrySet 或旧缓存租约 fencing。日志走宿主 ILogger；遥测按需安装官方 OpenTelemetry 包并调用 AddFusionCacheInstrumentation()。如需采集 Bing 回源准入指标，还要订阅 `Meter` / `ActivitySource` 名称 `Bing.Caching.FusionCache`；指标为 `bing.cache.factory.waiting`、`bing.cache.factory.active`、`bing.cache.factory.rejected` 和 `bing.cache.factory.wait.duration`（毫秒）。

## 工厂、TTL 与失效

```csharp
var cache = cacheProvider.GetCache("catalog");
var item = await cache.GetOrCreateAsync<ItemDto>(
    $"tenant:{tenantId}:item:{itemId}",
    async cancellationToken =>
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<ItemReader>();
        return await reader.FindAsync(tenantId, itemId, cancellationToken);
    },
    new CacheOptions { Expiration = TimeSpan.FromMinutes(5) },
    new[] { $"tenant:{tenantId}:catalog" },
    cancellationToken);

await cache.InvalidateTagAsync($"tenant:{tenantId}:catalog", cancellationToken);
```

工厂可能因软超时、Eager Refresh 或允许后台完成而在调用者返回后继续运行，必须自行创建并释放异步 DI scope。CacheOptions.Expiration 只表达本次基础 TTL；Fail-Safe、L1/L2 TTL、刷新、超时和自动恢复通过 FusionCache 原生 builder 设置。只读查询不返回过期值；带工厂的查询可在 Fail-Safe 策略允许时返回旧值。显式删除、标签失效和 Clear 用于改变缓存可见性，不构成数据库事务或并发工厂的线性一致性屏障。

Redis L2 的物理 TTL 精度取决于 Provider；固定的 StackExchangeRedis 10.0.1 按整秒设置过期，小于 1 秒的 L2 保留期可能立即失效。命名实例的长 TTL、Fail-Safe 和抖动窗口不能超过标签/Clear 元数据保留期，详见迁移指南。

## 能力边界

- 本包只实现 IHybridCache 单键异步契约，不完整实现旧 ICache。TrySet、任意前缀/模式扫描、批量与流式 API 继续使用原 Provider。
- 首次切换使用新的物理键空间，不反序列化旧 Provider 的 Redis payload。
- Backplane、L2 和 L1 传播具最终一致性；应用需容忍传播窗口和 Redis 故障时按 TTL 收敛。
- in-flight 旧工厂可能晚于 Set/Remove 完成并写回；本包不承诺旧 Provider 的租约 fencing，正确性依赖及时撤销或原子条件写入的路径不应直接迁移。
- 推荐先读[缓存模块](../../../docs/modules/caching.md)和[迁移指南](../../../docs/migrations/fusioncache-adoption.md)，再按[样例](../../../samples/Bing.Samples.FusionCache/README.md)运行。
