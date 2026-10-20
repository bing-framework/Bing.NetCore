using Bing.Caching.FreeRedis;
using FreeRedis;
using Newtonsoft.Json;

namespace Bing.Caching.FreeRedis.Tests;

/// <summary>
/// 验证 FreeRedis 的命名空间隔离、输入限制和回源协调。
/// </summary>
public class NamespaceAndCoordinationTest
{
    /// <summary>
    /// 用于命名空间隔离和回源协调测试的 Redis 客户端。
    /// </summary>
    private readonly RedisClient _client;

    /// <summary>
    /// 初始化一个 <see cref="NamespaceAndCoordinationTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">Redis 测试客户端。</param>
    public NamespaceAndCoordinationTest(RedisClient client) => _client = client;

    /// <summary>
    /// 检测到 FreeRedis 客户端内置前缀时拒绝创建缓存管理器。
    /// </summary>
    [Fact]
    public void HiddenClientPrefix_IsRejected()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        using var prefixed = new RedisClient(connection + ",prefix=hidden:");
        prefixed.Serialize = JsonConvert.SerializeObject;
        prefixed.Deserialize = JsonConvert.DeserializeObject;
        Assert.Throws<ArgumentException>(() => new FreeRedisCacheManager(prefixed,
            new CacheConfiguration("prefix-test", "redis")));
    }

    /// <summary>
    /// 输入键、前缀和模式超限时，在发送 Redis 命令前拒绝请求。
    /// </summary>
    [Fact]
    public async Task InputLimits_RejectOverlongKeyPrefixAndPatternBeforeRedisCommands()
    {
        var configuration = new CacheConfiguration("limits-" + Guid.NewGuid().ToString("N"), "redis")
            .WithInputLimits(new CacheInputLimits(3, 3, 3));
        var cache = new FreeRedisCacheManager(_client, configuration);

        Assert.Throws<ArgumentOutOfRangeException>(() => cache.Get<int>("four"));
        Assert.Throws<ArgumentOutOfRangeException>(() => cache.GetByPrefix<int>("four"));
        Assert.Throws<ArgumentOutOfRangeException>(() => cache.RemoveByPattern("four"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cache.GetByPrefixAsync<int>("four"));

        await using var stream = cache.GetByPrefixStreamAsync<int>("four").GetAsyncEnumerator();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => stream.MoveNextAsync().AsTask());
    }

    /// <summary>
    /// 前缀删除、宽泛模式和清理均不会影响其他命名空间。
    /// </summary>
    [Fact]
    public async Task ClearAndBroadPattern_KeepOtherNamespace()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var left = new FreeRedisCacheManager(_client, new CacheConfiguration("cache-left-" + suffix, "redis"));
        var right = new FreeRedisCacheManager(_client, new CacheConfiguration("cache-right-" + suffix, "redis"));
        left.Set("lease", 1);
        left.Set("prefix:item", 2);
        right.Set("prefix:item", 3);

        await left.RemoveByPrefixAsync("prefix:");
        Assert.False(left.Exists("prefix:item"));
        Assert.Equal(3, right.Get<int>("prefix:item"));
        Assert.Throws<ArgumentException>(() => left.RemoveByPattern("**"));
        await left.ClearAsync();
        Assert.False(left.Exists("lease"));
        Assert.Equal(3, right.Get<int>("prefix:item"));
        right.Clear();
    }

    /// <summary>
    /// 批量写入覆盖已有值并保留毫秒级 TTL。
    /// </summary>
    [Fact]
    public async Task SetAllAsync_OverwritesAndKeepsMillisecondTtl()
    {
        var cache = new FreeRedisCacheManager(_client,
            new CacheConfiguration("cache-write-" + Guid.NewGuid().ToString("N"), "redis"));
        cache.Set("item", 1);
        await cache.SetAllAsync(new Dictionary<string, int> { ["item"] = 2 });
        Assert.Equal(2, cache.Get<int>("item"));
        await cache.SetAsync("short", 4, new CacheOptions { Expiration = TimeSpan.FromMilliseconds(1500) });
        Assert.True(cache.Exists("short"));
        await Task.Delay(1700);
        Assert.False(cache.Exists("short"));
        cache.Clear();
    }

    /// <summary>
    /// 验证两个管理器对同一缓存未命中只执行一次回源。
    /// </summary>
    [Fact]
    public async Task TwoManagers_CoordinateSameMiss()
    {
        var configuration = new CacheConfiguration("cache-lease-" + Guid.NewGuid().ToString("N"), "redis");
        var left = new FreeRedisCacheManager(_client, configuration);
        var right = new FreeRedisCacheManager(_client, configuration);
        var calls = 0;
        async Task<int> Retrieve(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(150, cancellationToken);
            return 42;
        }

        var results = await Task.WhenAll(
            left.GetAsync("same-key", Retrieve),
            right.GetAsync("same-key", Retrieve));
        Assert.Equal(new[] { 42, 42 }, results);
        Assert.Equal(1, calls);
        left.Clear();
    }

    /// <summary>
    /// 两个管理器对同一缓存未命中只执行一次回源。
    /// </summary>
    [Fact]
    public async Task LostLease_CannotOverwriteNewOwner()
    {
        var configuration = new CacheConfiguration("cache-lost-" + Guid.NewGuid().ToString("N"), "redis");
        var first = new FreeRedisCacheManager(_client, configuration);
        var second = new FreeRedisCacheManager(_client, configuration);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = first.GetAsync("item", async cancellationToken =>
        {
            entered.SetResult(true);
            await release.Task;
            return 1;
        });
        await entered.Task;
        _client.Del(CacheKeyFormat.GetLeaseKey(configuration, "item"));
        var current = await second.GetAsync("item", _ => Task.FromResult(2));
        release.SetResult(true);

        Assert.Equal(2, current);
        await Assert.ThrowsAsync<InvalidOperationException>(() => old);
        Assert.Equal(2, first.Get<int>("item"));
        first.Clear();
    }

    /// <summary>
    /// 验证缓存命中 null，并拒绝同键递归回源。
    /// </summary>
    [Fact]
    public async Task NullValue_IsCacheHit_AndRecursiveFetchIsRejected()
    {
        var cache = new FreeRedisCacheManager(_client,
            new CacheConfiguration("cache-null-" + Guid.NewGuid().ToString("N"), "redis"));
        var calls = 0;
        Assert.Null(await cache.GetAsync<string>("null", _ =>
        {
            calls++;
            return Task.FromResult<string>(null);
        }));
        Assert.Null(await cache.GetAsync<string>("null", _ =>
        {
            calls++;
            return Task.FromResult("wrong");
        }));
        Assert.Equal(1, calls);
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync<int>("recurse",
            async cancellationToken => await cache.GetAsync<int>("recurse", _ => Task.FromResult(1),
                cancellationToken: cancellationToken)));
        cache.Clear();
    }

    /// <summary>
    /// 忽略取消令牌的回源不会永久占用本地协调门。
    /// </summary>
    [Fact]
    public async Task IgnoringCancellation_CannotHoldLocalGateForever()
    {
        var configuration = new CacheConfiguration("freeredis-timeout-" + Guid.NewGuid(), "redis",
            coordination: new CacheCoordinationOptions(
                leaseDuration: TimeSpan.FromSeconds(2), renewalInterval: TimeSpan.FromMilliseconds(200),
                waitTimeout: TimeSpan.FromSeconds(1), retrievalTimeout: TimeSpan.FromMilliseconds(100)));
        var cache = new FreeRedisCacheManager(_client, configuration);
        await Assert.ThrowsAsync<TimeoutException>(() => cache.GetAsync<int>("item", async _ =>
        {
            await Task.Delay(500);
            return 1;
        }));
        Assert.Equal(2, await cache.GetAsync<int>("item", _ => Task.FromResult(2)));
        cache.Clear();
    }
}
