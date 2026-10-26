using System.Diagnostics;
using Bing.Caching.FusionCache.Probe;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using Xunit.Abstractions;

namespace Bing.Caching.FusionCache.Tests.Integration;

/// <summary>
/// 使用真实 Redis 验证混合缓存的 L2、标签、Backplane、锁和故障恢复行为。
/// </summary>
/// <param name="output">用于输出性能与故障采证信息的测试输出。</param>
public class FusionCacheRedisIntegrationTest(ITestOutputHelper output)
{
    /// <summary>
    /// 验证 L2 命中可还原 null 值且无需执行回源工厂。
    /// </summary>
    [RedisFact]
    public async Task L2Hit_PreservesNullAndAvoidsFactory()
    {
        var ns = RedisTestSupport.NewNamespace();
        using var first = RedisTestHost.Create(ns);
        using var second = RedisTestHost.Create(ns, onlyL2: true);
        var a = first.GetRequiredService<IHybridCache>();
        var b = second.GetRequiredService<IHybridCache>();
        await a.SetAsync<string?>("null", null);
        await a.SetAsync("product", new Product(1, "产品"), new CacheOptions { Expiration = TimeSpan.FromMinutes(2) });
        var empty = await b.TryGetAsync<string?>("null");
        Assert.True(empty.Hit);
        Assert.Null(empty.Value);
        var product = await b.GetOrCreateAsync<Product>("product", _ => throw new Exception("L2 should hit"));
        Assert.Equal(new Product(1, "产品"), product);
    }

    /// <summary>
    /// 验证共享 Redis 时不同名称和命名空间的数据及元数据彼此隔离。
    /// </summary>
    [RedisFact]
    public async Task SharedRedis_IsolatesNamesNamespacesAndMetadata()
    {
        var ns = RedisTestSupport.NewNamespace();
        using var first = RedisTestHost.Create(ns, "a:b");
        using var second = RedisTestHost.Create(ns, "a%3Ab");
        using var third = RedisTestHost.Create(ns + ":other", "a:b");
        var a = first.GetRequiredService<IHybridCache>();
        var b = second.GetRequiredService<IHybridCache>();
        var c = third.GetRequiredService<IHybridCache>();
        await a.SetAsync("key", 1, tags: ["tag"]);
        await b.SetAsync("key", 2, tags: ["tag"]);
        await c.SetAsync("key", 3, tags: ["tag"]);
        await a.InvalidateTagAsync("tag");
        Assert.False((await a.TryGetAsync<int>("key")).Hit);
        Assert.Equal(2, (await b.TryGetAsync<int>("key")).Value);
        Assert.Equal(3, (await c.TryGetAsync<int>("key")).Value);
        await a.ClearAsync();
        Assert.Equal(2, (await b.TryGetAsync<int>("key")).Value);
        Assert.Equal(3, (await c.TryGetAsync<int>("key")).Value);
        var mux = first.GetRequiredService<IConnectionMultiplexer>();
        var prefix = $"bing:fc:v1:{Uri.EscapeDataString(ns)}:";
        var keys = mux.GetServer(mux.GetEndPoints()[0]).Keys(pattern: "*" + prefix + "*").ToArray();
        Assert.NotEmpty(keys);
        Assert.All(keys, key => Assert.Equal(2, key.ToString().Split(prefix).Length));
        Assert.Contains(keys, key => key.ToString().Contains("a%3Ab:"));
        Assert.Contains(keys, key => key.ToString().Contains("a%253Ab:"));
    }

    /// <summary>
    /// 验证标签与清除标记的保留期独立于业务 TTL，不会先于数据消失。
    /// </summary>
    [RedisFact]
    public async Task TagAndClearMarkersKeepLongRetention_IndependentOfBusinessTtl()
    {
        var ns = RedisTestSupport.NewNamespace();
        using var first = RedisTestHost.Create(ns, configure: o => o.DefaultEntryOptions.Duration = TimeSpan.FromMilliseconds(30));
        var cache = first.GetRequiredService<IHybridCache>();
        await cache.SetAsync("long-lived", 1, new CacheOptions { Expiration = TimeSpan.FromHours(2) }, ["group"]);
        await cache.InvalidateTagAsync("group");
        await Task.Delay(100);
        using (var afterTag = RedisTestHost.Create(ns, onlyL2: true))
            Assert.False((await afterTag.GetRequiredService<IHybridCache>().TryGetAsync<int>("long-lived")).Hit);
        Assert.False((await cache.TryGetAsync<int>("long-lived")).Hit);
        await cache.SetAsync("long-lived", 2, new CacheOptions { Expiration = TimeSpan.FromHours(2) });
        await cache.ClearAsync();
        var mux = first.GetRequiredService<IConnectionMultiplexer>();
        var database = mux.GetDatabase();
        var prefix = $"bing:fc:v1:{Uri.EscapeDataString(ns)}:";
        var keys = mux.GetServer(mux.GetEndPoints()[0]).Keys(pattern: "*" + prefix + "*").ToArray();
        var markerKeys = keys.Where(key => !key.ToString().Contains(":d:")).ToArray();
        Assert.NotEmpty(markerKeys);
        foreach (var key in markerKeys)
            Assert.True(await database.KeyTimeToLiveAsync(key) > TimeSpan.FromHours(2), key.ToString());
        await Task.Delay(100);
        using var cold = RedisTestHost.Create(ns);
        Assert.False((await cold.GetRequiredService<IHybridCache>().TryGetAsync<int>("long-lived")).Hit);
    }

    /// <summary>
    /// 验证两个独立进程在写入、移除、标签失效和清除后最终一致。
    /// </summary>
    [RedisFact]
    public async Task TwoProcesses_ConvergeAfterUpdateRemoveTagAndClear()
    {
        var ns = RedisTestSupport.NewNamespace();
        await using var a = await CacheNode.Start(ns);
        await using var b = await CacheNode.Start(ns);
        await a.Send(new("set", Value: 1, Tag: "group"));
        Assert.Equal(1, (await b.Send(new("get"))).Value);
        await a.Send(new("set", Value: 2, Tag: "group"));
        await RedisTestSupport.Eventually(async () => (await b.Send(new("get"))).Value == 2);
        await a.Send(new("remove"));
        await RedisTestSupport.Eventually(async () => !(await b.Send(new("get"))).Hit);
        await a.Send(new("set", Value: 3, Tag: "group"));
        Assert.Equal(3, (await b.Send(new("get"))).Value);
        await a.Send(new("tag", Tag: "group"));
        await RedisTestSupport.Eventually(async () => !(await b.Send(new("get"))).Hit);
        await a.Send(new("set", Value: 4));
        Assert.Equal(4, (await b.Send(new("get"))).Value);
        await a.Send(new("clear"));
        await RedisTestSupport.Eventually(async () => !(await b.Send(new("get"))).Hit);
    }

    /// <summary>
    /// 验证可选的分布式锁能跨进程合并同键回源。
    /// </summary>
    [RedisFact]
    public async Task OptionalDistributedLocker_CoalescesAcrossProcesses()
    {
        var ns = RedisTestSupport.NewNamespace();
        await using var a = await CacheNode.Start(ns, distributedLock: true);
        await using var b = await CacheNode.Start(ns, distributedLock: true);
        using var inspector = RedisTestHost.Create(ns, "inspector");
        var database = inspector.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var first = a.Send(new("create", DelayMs: 600));
        await RedisTestSupport.Eventually(async () => await database.StringGetAsync(RedisTestHost.CounterKey(ns, "key")) == "1");
        var second = b.Send(new("create", DelayMs: 600));
        var results = await Task.WhenAll(first, second);
        Assert.All(results, result => Assert.Equal(1, result.Value));
        Assert.Equal("1", (await database.StringGetAsync(RedisTestHost.CounterKey(ns, "key"))).ToString());
    }

    /// <summary>
    /// 验证迟到的回源结果仍可能覆盖显式写入或移除后的 L2 值。
    /// </summary>
    [RedisFact]
    public async Task LateFactory_CanOverwriteExplicitSetOrRemoveInL2()
    {
        var ns = RedisTestSupport.NewNamespace();
        await using var a = await CacheNode.Start(ns);
        await using var b = await CacheNode.Start(ns);
        using var inspector = RedisTestHost.Create(ns, onlyL2: true);
        var database = inspector.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        foreach (var operation in new[] { "set", "remove" })
        {
            var key = "late-" + operation;
            var running = a.Send(new("create", key, DelayMs: 1000));
            await RedisTestSupport.Eventually(async () => await database.StringGetAsync(RedisTestHost.CounterKey(ns, key)) == "1");
            await b.Send(new(operation, key, Value: 99));
            await running;
            var result = await inspector.GetRequiredService<IHybridCache>().TryGetAsync<int?>(key);
            output.WriteLine($"Late factory after {operation}: L2 Hit={result.Hit}, Value={result.Value}");
            // 固定 2.9.0 的实测边界：该能力不等于旧 Provider 的写回 fencing。
            Assert.True(result.Hit);
            Assert.Equal(1, result.Value);
        }
    }

    /// <summary>
    /// 验证 Redis 故障时严格实例报告前台失败，容错实例使用旧值并自动恢复待处理写入。
    /// </summary>
    [RedisContainerFact]
    public async Task RedisFault_ReportsForegroundFailure_AndAutoRecoveryReplays()
    {
        var ns = RedisTestSupport.NewNamespace();
        using var strict = RedisTestHost.Create(ns);
        using var tolerant = RedisTestHost.Create(ns + ":resilient", failSafe: true, rethrow: false,
            configure: o => o.EnableSyncEventHandlersExecution = true);
        var strictCache = strict.GetRequiredService<IHybridCache>();
        var cache = tolerant.GetRequiredService<IHybridCache>();
        await strictCache.SetAsync("initial", 1);
        await cache.SetAsync("fallback", 7);
        using var observer = new AdmissionObserver("catalog");
        using var events = new NativeCacheEventObserver(tolerant.GetRequiredService<IFusionCacheProvider>().GetCache("catalog"));
        using var measurement = PerformanceMeasurement.Start();
        var factoryCalls = 0;
        try
        {
            await RedisTestSupport.Docker("stop");
            var failure = await Assert.ThrowsAsync<FusionCacheDistributedCacheException>(() => strictCache.SetAsync("write-failure", 2));
            Assert.IsAssignableFrom<RedisException>(failure.InnerException);
            // 读取失败时按显式容错配置使用本地旧值；回源失败仍可获得后备。
            await cache.ExpireAsync("fallback");
            Assert.Equal(7, await cache.GetOrCreateAsync<int>("fallback", _ => { factoryCalls++; throw new FormatException(); }));
            await cache.SetAsync("pending", 8);
        }
        finally { await RedisTestSupport.Docker("start"); }
        using var reader = RedisTestHost.Create(ns + ":resilient", onlyL2: true);
        await RedisTestSupport.Eventually(async () =>
        {
            try { return (await reader.GetRequiredService<IHybridCache>().TryGetAsync<int>("pending")).Value == 8; }
            catch (RedisException) { return false; }
        }, seconds: 25);
        var snapshot = measurement.Complete();
        Assert.Equal(1, factoryCalls);
        Assert.Equal(1, events.FailSafeCount);
        PerformanceEvidence.Create("redis-fault-recovery",
            [snapshot.ElapsedMilliseconds * 1000], factoryCalls, observer, snapshot, events).Save(output);
        await strictCache.SetAsync("after-recovery", 9);
        Assert.Equal(9, (await strictCache.TryGetAsync<int>("after-recovery")).Value);
    }

    /// <summary>
    /// 验证 L2 类型不匹配会报告序列化异常，且配置的 TTL 会按时过期。
    /// </summary>
    [RedisFact]
    public async Task L2SchemaTypeMismatchIsReported_AndConfiguredTtlExpires()
    {
        var ns = RedisTestSupport.NewNamespace();
        using var writer = RedisTestHost.Create(ns, configure: o =>
        {
            o.DefaultEntryOptions.Duration = TimeSpan.FromSeconds(2);
            o.DefaultEntryOptions.DistributedCacheDuration = TimeSpan.FromSeconds(3);
        });
        using var reader = RedisTestHost.Create(ns, onlyL2: true);
        var a = writer.GetRequiredService<IHybridCache>();
        var b = reader.GetRequiredService<IHybridCache>();
        using (var schemaWriter = RedisTestHost.Create(ns + ":schema"))
        using (var schemaReader = RedisTestHost.Create(ns + ":schema", onlyL2: true))
        {
            await schemaWriter.GetRequiredService<IHybridCache>().SetAsync("wrong-type", "not a product");
            var failure = await Assert.ThrowsAsync<FusionCacheSerializationException>(() => schemaReader.GetRequiredService<IHybridCache>().TryGetAsync<Product>("wrong-type"));
            Assert.IsType<System.Text.Json.JsonException>(failure.InnerException);
        }
        await a.SetAsync("ttl", 1);
        Assert.Equal(1, (await b.TryGetAsync<int>("ttl")).Value);
        await RedisTestSupport.Eventually(async () => !(await b.TryGetAsync<int>("ttl")).Hit);
        Assert.Equal(2, await b.GetOrCreateAsync("ttl", _ => Task.FromResult(2)));
    }

    /// <summary>
    /// 验证进程崩溃会丢失进程内待恢复写入，重启后读取已持久化的 L2 旧值。
    /// </summary>
    [RedisContainerFact]
    public async Task CrashedProcessLosesPendingRecovery_NewProcessReadsPreviousL2Value()
    {
        var ns = RedisTestSupport.NewNamespace();
        await using var node = await CacheNode.Start(ns, crashWindow: true);
        await node.Send(new("set", Value: 1));
        using var inspector = RedisTestHost.Create(ns, "snapshot");
        await inspector.GetRequiredService<IConnectionMultiplexer>().GetDatabase().ExecuteAsync("SAVE");
        try
        {
            // 先持久化已确认的值再断开服务，避免 pause 时 TCP 中的命令恢复后仍被执行。
            await RedisTestSupport.Docker("stop");
            await node.Send(new("set", Value: 2));
            Assert.Equal(2, (await node.Send(new("get"))).Value);
            await node.CrashAsync();
        }
        finally { await RedisTestSupport.Docker("start"); }
        await using var restarted = await CacheNode.Start(ns);
        Assert.Equal(1, (await restarted.Send(new("get"))).Value);
        await restarted.Send(new("set", Value: 3));
        Assert.Equal(3, (await restarted.Send(new("get"))).Value);
        output.WriteLine("Verified: process-local pending write 2 was lost; restart read durable L2 value 1; new write 3 succeeded.");
    }

    /// <summary>
    /// 验证 L2 与 Fail-Safe 的保留窗口不能超过标签与清除元数据窗口。
    /// </summary>
    [RedisFact]
    public void L2AndFailSafeRetentionCannotOutliveMetadata()
    {
        foreach (var failSafe in new[] { false, true })
            Assert.Throws<InvalidOperationException>(() => RedisTestHost.Create(RedisTestSupport.NewNamespace(), failSafe: failSafe,
                configure: o =>
                {
                    o.DefaultEntryOptions.DistributedCacheDuration = TimeSpan.FromDays(11);
                    o.DefaultEntryOptions.DistributedCacheFailSafeMaxDuration = TimeSpan.FromDays(12);
                }));
    }

    /// <summary>
    /// 集成测试使用的不可变产品值对象。
    /// </summary>
    /// <param name="Id">产品标识。</param>
    /// <param name="Name">产品名称。</param>
    public sealed record Product(int Id, string Name);
}
