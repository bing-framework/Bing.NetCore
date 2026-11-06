using Bing.Caching.FusionCache.Probe;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Bing.Caching.FusionCache.Tests.Integration;

/// <summary>
/// 验证滚动重启、业务分类失效及数据版本键的常用 Redis 接入场景。
/// </summary>
public class FusionCacheCommonRedisScenarioTest
{
    /// <summary>
    /// 旧进程退出后，新进程从 L2 恢复数据，不再次访问数据源。
    /// </summary>
    [RedisFact]
    public async Task RestartedProcess_ReadsL2WithoutCallingOriginAgain()
    {
        var ns = RedisTestSupport.NewNamespace();
        const string key = "tenant:42:product:1001";
        await using (var original = await CacheNode.Start(ns))
            Assert.Equal(1, (await original.Send(new("create", key))).Value);

        await using var restarted = await CacheNode.Start(ns);
        Assert.Equal(1, (await restarted.Send(new("create", key))).Value);
        Assert.True((await restarted.Send(new("get", key))).Hit);
        using var inspector = RedisTestHost.Create(ns, "inspector");
        var database = inspector.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        Assert.Equal("1", (await database.StringGetAsync(RedisTestHost.CounterKey(ns, key))).ToString());
    }

    /// <summary>
    /// 分类变更只使相同租户的相关商品失效；其他分类和租户的暖缓存继续命中。
    /// </summary>
    [RedisFact]
    public async Task CategoryInvalidation_ConvergesAcrossProcessesWithoutEvictingOtherTenants()
    {
        var ns = RedisTestSupport.NewNamespace();
        await using var writer = await CacheNode.Start(ns);
        await using var reader = await CacheNode.Start(ns);
        var products = new[]
        {
            (Key: "tenant:42:product:1", Tag: "tenant:42:category:books", Value: 101),
            (Key: "tenant:42:product:2", Tag: "tenant:42:category:books", Value: 102),
            (Key: "tenant:42:product:3", Tag: "tenant:42:category:music", Value: 103),
            (Key: "tenant:99:product:1", Tag: "tenant:99:category:books", Value: 201)
        };
        foreach (var product in products)
        {
            await writer.Send(new("set", product.Key, Value: product.Value, Tag: product.Tag));
            Assert.Equal(product.Value, (await reader.Send(new("get", product.Key))).Value);
        }

        await writer.Send(new("tag", Tag: products[0].Tag));
        foreach (var affected in products.Take(2))
            await RedisTestSupport.Eventually(async () => !(await reader.Send(new("get", affected.Key))).Hit);
        foreach (var unaffected in products.Skip(2))
        {
            var cached = await reader.Send(new("get", unaffected.Key));
            Assert.True(cached.Hit);
            Assert.Equal(unaffected.Value, cached.Value);
        }

        // 失效后可发布新快照；后来启动的节点也必须看到新值和仍有效的其他分类。
        await writer.Send(new("set", products[0].Key, Value: 301, Tag: products[0].Tag));
        await RedisTestSupport.Eventually(async () => (await reader.Send(new("get", products[0].Key))).Value == 301);
        await using var newcomer = await CacheNode.Start(ns);
        Assert.Equal(301, (await newcomer.Send(new("get", products[0].Key))).Value);
        Assert.False((await newcomer.Send(new("get", products[1].Key))).Hit);
        Assert.Equal(103, (await newcomer.Send(new("get", products[2].Key))).Value);
        Assert.Equal(201, (await newcomer.Send(new("get", products[3].Key))).Value);
    }

    /// <summary>
    /// 调用方从权威数据获得新版本并切换键后，迟到的旧工厂只能写入旧版本空间。
    /// </summary>
    [RedisFact]
    public async Task VersionedKeys_KeepLateFactoryAwayFromCurrentRevisionInL2()
    {
        var ns = RedisTestSupport.NewNamespace();
        using var first = RedisTestHost.Create(ns);
        using var second = RedisTestHost.Create(ns);
        var a = first.GetRequiredService<IHybridCache>();
        var b = second.GetRequiredService<IHybridCache>();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        const string oldKey = "tenant:42:product:1001:revision:1";
        const string currentKey = "tenant:42:product:1001:revision:2";
        var currentOriginCalls = 0;
        var oldRead = a.GetOrCreateAsync(oldKey, async token =>
        {
            entered.TrySetResult(true);
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
            return new ProductSnapshot(1001, 1, "旧名称");
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // 模拟数据库已经提交 revision=2；版本来自权威源，不能从同一旧缓存推断。
            var updated = await b.GetOrCreateAsync(currentKey, _ =>
            {
                Interlocked.Increment(ref currentOriginCalls);
                return Task.FromResult(new ProductSnapshot(1001, 2, "新名称"));
            });
            Assert.Equal(2, updated.Revision);
        }
        finally
        {
            release.TrySetResult(true);
            await oldRead.WaitAsync(TimeSpan.FromSeconds(10));
        }

        using var coldReader = RedisTestHost.Create(ns, onlyL2: true);
        var cache = coldReader.GetRequiredService<IHybridCache>();
        var current = await cache.GetOrCreateAsync<ProductSnapshot>(currentKey,
            _ => throw new InvalidOperationException("当前版本应从 L2 命中。"));
        Assert.Equal(new ProductSnapshot(1001, 2, "新名称"), current);
        Assert.Equal(1, currentOriginCalls);
        // 旧数据并未被物理擦除；安全性来自调用方只选择当前版本键。
        Assert.Equal(1, (await cache.TryGetAsync<ProductSnapshot>(oldKey)).Value.Revision);
    }

    /// <summary>
    /// 可序列化的不可变商品快照；版本号由数据源提供。
    /// </summary>
    public sealed record ProductSnapshot(int Id, int Revision, string Name);
}
