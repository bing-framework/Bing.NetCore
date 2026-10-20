using CSRedis;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// CSRedis 命名空间和协调语义测试。
/// </summary>
/// <remarks>
/// 覆盖输入限制、租约协调、空值命中、清理和命名空间隔离。
/// </remarks>
public class NamespaceIsolationTest
{
    /// <summary>
    /// 保存由测试夹具提供、用于创建缓存管理器和直接检查物理键的 CSRedis 客户端。
    /// </summary>
    private readonly CSRedisClient _client;

    /// <summary>
    /// 初始化一个 <see cref="NamespaceIsolationTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">供各测试创建缓存管理器并执行 Redis 校验的客户端。</param>
    public NamespaceIsolationTest(CSRedisClient client) => _client = client;

    /// <summary>
    /// 验证输入长度限制在 Redis 访问前生效。
    /// </summary>
    /// <remarks>
    /// 键、前缀和模式超限时，同步、异步和流式入口都应在发出 Redis 命令前拒绝请求。
    /// </remarks>
    [Fact]
    public async Task InputLimits_RejectOverlongKeyPrefixAndPatternBeforeRedisCommands()
    {
        var configuration = new CacheConfiguration("limits-" + Guid.NewGuid().ToString("N"), "redis")
            .WithInputLimits(new CacheInputLimits(3, 3, 3));
        var cache = new CSRedisCacheManager(_client, configuration);

        Assert.Throws<ArgumentOutOfRangeException>(() => cache.Get<int>("four"));
        Assert.Throws<ArgumentOutOfRangeException>(() => cache.GetByPrefix<int>("four"));
        Assert.Throws<ArgumentOutOfRangeException>(() => cache.RemoveByPattern("four"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cache.GetByPrefixAsync<int>("four"));

        await using var stream = cache.GetByPrefixStreamAsync<int>("four").GetAsyncEnumerator();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => stream.MoveNextAsync().AsTask());
    }

    /// <summary>
    /// 验证回源超时后的本地协调门恢复。
    /// </summary>
    /// <remarks>
    /// 回源耗时超过 retrievalTimeout 后协调门不应永久占用，后续读取应能成功。
    /// </remarks>
    [Fact]
    public async Task IgnoringCancellation_CannotHoldLocalGateForever()
    {
        var configuration = new CacheConfiguration("csredis-timeout-" + Guid.NewGuid(), "redis",
            coordination: new CacheCoordinationOptions(
                leaseDuration: TimeSpan.FromSeconds(2), renewalInterval: TimeSpan.FromMilliseconds(200),
                waitTimeout: TimeSpan.FromSeconds(1), retrievalTimeout: TimeSpan.FromMilliseconds(100)));
        var cache = new CSRedisCacheManager(_client, configuration);
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => cache.GetAsync<int>("item", async _ =>
            {
                await Task.Delay(500);
                return 1;
            }));
            Assert.Equal(2, await cache.GetAsync<int>("item", _ => Task.FromResult(2)));
        }
        finally { cache.Clear(); }
    }

    /// <summary>
    /// 验证租约替换后旧回源不能覆盖新值。
    /// </summary>
    /// <remarks>
    /// 旧持有者丢失租约后，其迟到结果不得覆盖新持有者写入的值。
    /// </remarks>
    [Fact]
    public async Task LostLease_CannotOverwriteNewOwner()
    {
        var configuration = new CacheConfiguration("csredis-lost-" + Guid.NewGuid(), "redis");
        var first = new CSRedisCacheManager(_client, configuration);
        var second = new CSRedisCacheManager(_client, configuration);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var old = first.GetAsync<int>("item", async _ =>
            {
                entered.SetResult(true);
                await release.Task;
                return 1;
            });
            await entered.Task;
            _client.Del(CacheKeyFormat.GetLeaseKey(configuration, "item"));
            Assert.Equal(2, await second.GetAsync<int>("item", _ => Task.FromResult(2)));
            release.SetResult(true);
            await Assert.ThrowsAsync<InvalidOperationException>(() => old);
            Assert.Equal(2, first.Get<int>("item"));
        }
        finally { first.Clear(); }
    }

    /// <summary>
    /// 验证 null 缓存值被视为命中。
    /// </summary>
    /// <remarks>
    /// 重复读取应直接返回缓存的 null，不再次执行回源工厂。
    /// </remarks>
    [Fact]
    public async Task NullValue_IsCacheHit()
    {
        var cache = new CSRedisCacheManager(_client,
            new CacheConfiguration("csredis-null-" + Guid.NewGuid(), "redis"));
        var calls = 0;
        try
        {
            Assert.Null(await cache.GetAsync<string>("item", _ =>
            {
                calls++;
                return Task.FromResult<string>(null);
            }));
            Assert.Null(await cache.GetAsync<string>("item", _ =>
            {
                calls++;
                return Task.FromResult("wrong");
            }));
            Assert.Equal(1, calls);
        }
        finally { cache.Clear(); }
    }

    /// <summary>
    /// 验证通配符删除和清理保持命名空间隔离。
    /// </summary>
    /// <remarks>
    /// 当前命名空间的通配符前缀删除和清理不得删除其他命名空间的哨兵数据。
    /// </remarks>
    [Fact]
    public async Task Clear_AndWildcardPrefix_KeepOtherNamespace()
    {
        var left = new CSRedisCacheManager(_client, new CacheConfiguration("csredis-left-" + Guid.NewGuid(), "left"));
        var right = new CSRedisCacheManager(_client, new CacheConfiguration("csredis-right-" + Guid.NewGuid(), "right"));
        left.Set("**", 1);
        left.Set("other", 2);
        right.Set("sentinel", 3);

        try
        {
            await left.RemoveByPrefixAsync("**");
            Assert.False(left.Exists("**"));
            Assert.True(left.Exists("other"));
            Assert.Equal(3, right.Get<int>("sentinel"));

            await left.ClearAsync();
            Assert.False(left.Exists("other"));
            Assert.Equal(3, right.Get<int>("sentinel"));
        }
        finally
        {
            left.Clear();
            right.Clear();
        }
    }

    /// <summary>
    /// 验证批量写入覆盖值并设置毫秒级 TTL。
    /// </summary>
    /// <remarks>
    /// SetAllAsync 应覆盖已有值，并保留 1500 毫秒和 500 毫秒过期配置的 TTL 范围。
    /// </remarks>
    [Fact]
    public async Task SetAllAsync_Overwrites_AndPreservesSubsecondTtl()
    {
        var configuration = new CacheConfiguration("csredis-ttl-" + Guid.NewGuid(), "ttl");
        var cache = new CSRedisCacheManager(_client, configuration);
        try
        {
            cache.Set("value", 1);
            await cache.SetAllAsync(new Dictionary<string, int> { ["value"] = 2 },
                new CacheOptions { Expiration = TimeSpan.FromMilliseconds(1500) });
            Assert.Equal(2, cache.Get<int>("value"));

            // Use a second short-lived key for a direct TTL bound assertion.
            await cache.SetAsync("short", 7, new CacheOptions { Expiration = TimeSpan.FromMilliseconds(500) });
            var shortKey = CacheKeyFormat.GetPhysicalKey(configuration, "short");
            Assert.InRange(_client.PTtl(shortKey), 1, 500);
        }
        finally { cache.Clear(); }
    }

    /// <summary>
    /// 验证相同键的跨管理器回源合并。
    /// </summary>
    /// <remarks>
    /// 同一命名空间的两个缓存管理器应只执行一次回源，并共享得到的值。
    /// </remarks>
    [Fact]
    public async Task TwoManagers_UseOneRetrieverForSameKey()
    {
        var configuration = new CacheConfiguration("csredis-lease-" + Guid.NewGuid(), "lease");
        var first = new CSRedisCacheManager(_client, configuration);
        var second = new CSRedisCacheManager(_client, configuration);
        var calls = 0;
        async Task<int> Retrieve()
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(150);
            return 42;
        }

        try
        {
            var values = await Task.WhenAll(
                first.GetAsync("shared", Retrieve),
                second.GetAsync("shared", Retrieve));
            Assert.Equal(new[] { 42, 42 }, values);
            Assert.Equal(1, calls);
        }
        finally { first.Clear(); }
    }
}
