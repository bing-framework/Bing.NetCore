using System.Collections.Concurrent;
using Bing.Caching.FusionCache.Probe;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Locking.Distributed.Redis;

namespace Bing.Caching.FusionCache.Tests.Integration;

/// <summary>
/// 验证 Redis Backplane 通道身份隔离和锁连接的生命周期。
/// </summary>
public class FusionCacheChannelLifetimeTest
{
    /// <summary>
    /// 验证独立缓存实例只接收自身通道消息，并在释放后退订对应通道。
    /// </summary>
    [RedisFact]
    public async Task SeparateChannelsReceiveOnlyOwnIdentityMessages_AndSubscriptionsDisappearOnDispose()
    {
        var ns = RedisTestSupport.NewNamespace();
        using var admin = RedisTestHost.Create(ns, "admin");
        var database = admin.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var a = RedisTestHost.Create(ns, "a");
        var b = RedisTestHost.Create(ns, "b");
        try
        {
            using var publisherA = RedisTestHost.Create(ns, "a");
            using var publisherB = RedisTestHost.Create(ns, "b");
            var receivedA = new ConcurrentQueue<string>();
            var receivedB = new ConcurrentQueue<string>();
            a.GetRequiredService<IFusionCacheProvider>().GetCache("a").Events.Backplane.MessageReceived += (_, e) => receivedA.Enqueue(e.Message.CacheKey!);
            b.GetRequiredService<IFusionCacheProvider>().GetCache("b").Events.Backplane.MessageReceived += (_, e) => receivedB.Enqueue(e.Message.CacheKey!);
            var pattern = $"bing:fc:v1:{Uri.EscapeDataString(ns)}:*";
            var channels = (RedisResult[])(await database.ExecuteAsync("PUBSUB", "CHANNELS", pattern))!;
            Assert.Equal(3, channels.Length);
            foreach (var (provider, name) in new[] { (publisherA, "a"), (publisherB, "b") })
            {
                var cache = provider.GetRequiredService<IHybridCache>();
                await cache.SetAsync("payload-" + name, 1, tags: ["tag-" + name]);
                await cache.RemoveAsync("payload-" + name);
                await cache.InvalidateTagAsync("tag-" + name);
                await cache.ClearAsync();
                await cache.SetAsync("barrier-" + name, 2);
            }
            await RedisTestSupport.Eventually(() => Task.FromResult(receivedA.Any(x => x.Contains("barrier-a")) && receivedB.Any(x => x.Contains("barrier-b"))));
            Assert.True(receivedA.Count >= 5);
            Assert.True(receivedB.Count >= 5);
            var prefixA = $"bing:fc:v1:{Uri.EscapeDataString(ns)}:a:";
            var prefixB = $"bing:fc:v1:{Uri.EscapeDataString(ns)}:b:";
            Assert.All(receivedA, key => Assert.StartsWith(prefixA, key));
            Assert.All(receivedB, key => Assert.StartsWith(prefixB, key));
        }
        finally { a.Dispose(); b.Dispose(); }
        await RedisTestSupport.Eventually(async () =>
        {
            var remaining = (RedisResult[])(await database.ExecuteAsync("PUBSUB", "CHANNELS", $"bing:fc:v1:{Uri.EscapeDataString(ns)}:*"))!;
            return remaining.Length == 1; // 仅 inspector/admin 仍存活。
        });
    }

    /// <summary>
    /// 验证依赖注入所有的分布式锁连接会随容器释放，而外部检查连接保持可用。
    /// </summary>
    [RedisFact]
    public async Task DiOwnedLockerDisposesItsDedicatedConnection_ExternalInspectorRemainsUsable()
    {
        using var external = await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("BING_TEST_FUSIONCACHE_REDIS")!);
        var lockerConnection = await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("BING_TEST_FUSIONCACHE_REDIS")!);
        using (var provider = RedisTestHost.Create(RedisTestSupport.NewNamespace(), distributedLock: true,
            configureLocker: o => o.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(lockerConnection)))
        {
            Assert.Equal(1, await provider.GetRequiredService<IHybridCache>().GetOrCreateAsync("key", _ => Task.FromResult(1)));
            Assert.NotNull(provider.GetRequiredService<RedisDistributedLocker>());
            Assert.True(lockerConnection.IsConnected);
        }
        Assert.False(lockerConnection.IsConnected);
        await external.GetDatabase().PingAsync();
        Assert.True(external.IsConnected);
    }
}
