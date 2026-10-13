using Bing.Caching;
using EasyCaching.Core;
using EasyCaching.Core.Configurations;
using EasyCaching.Redis;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 只在显式指定独立测试数据节点和总线节点时运行。
/// </summary>
public class EasyHybridBusFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="EasyHybridBusFactAttribute"/> 类型的实例。
    /// </summary>
    public EasyHybridBusFactAttribute()
    {
        var dataConnection = Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS");
        var busConnection = Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_BUS_REDIS");
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(dataConnection) || string.IsNullOrWhiteSpace(busConnection) ||
            string.Equals(dataConnection.Trim(), busConnection.Trim(), StringComparison.OrdinalIgnoreCase))
            Skip = "需要独立的非生产数据 Redis 与总线 Redis 端点。";
    }
}

/// <summary>
/// 仅对明确允许断开订阅连接的测试总线节点运行。
/// </summary>
public sealed class EasyHybridBusReconnectFactAttribute : EasyHybridBusFactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="EasyHybridBusReconnectFactAttribute"/> 类型的实例。
    /// </summary>
    public EasyHybridBusReconnectFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("BING_TEST_ALLOW_REDIS_CLIENT_KILL"), "true",
                StringComparison.OrdinalIgnoreCase))
            Skip = "需要显式设置 BING_TEST_ALLOW_REDIS_CLIENT_KILL=true。";
    }
}

/// <summary>
/// 仅对明确允许短时暂停的独立测试总线节点运行。
/// </summary>
public sealed class EasyHybridBusPauseFactAttribute : EasyHybridBusFactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="EasyHybridBusPauseFactAttribute"/> 类型的实例。
    /// </summary>
    public EasyHybridBusPauseFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("BING_TEST_ALLOW_REDIS_PAUSE"), "true",
                StringComparison.OrdinalIgnoreCase))
            Skip = "需要显式设置 BING_TEST_ALLOW_REDIS_PAUSE=true。";
    }
}

/// <summary>
/// 仅在明确允许断开订阅连接和暂停总线时运行组合恢复测试。
/// </summary>
public sealed class EasyHybridBusRecoveryFactAttribute : EasyHybridBusFactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="EasyHybridBusRecoveryFactAttribute"/> 类型的实例。
    /// </summary>
    public EasyHybridBusRecoveryFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("BING_TEST_ALLOW_REDIS_CLIENT_KILL"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("BING_TEST_ALLOW_REDIS_PAUSE"), "true",
                StringComparison.OrdinalIgnoreCase))
            Skip = "需要同时显式设置 BING_TEST_ALLOW_REDIS_CLIENT_KILL=true 和 BING_TEST_ALLOW_REDIS_PAUSE=true。";
    }
}

/// <summary>
/// 验证 Redis 总线订阅连接断开后可以恢复跨实例失效。
/// </summary>
public sealed class EasyHybridBusReconnectIntegrationTest
{
    /// <summary>
    /// 订阅连接重连后清除未重新发布的写入和删除通知。
    /// </summary>
    [EasyHybridBusReconnectFact]
    public async Task SubscriptionReconnect_ClearsMissedUpdateAndDeleteWithoutRepublish()
    {
        var configuration = new CacheConfiguration("bus-gap-" + Guid.NewGuid().ToString("N"),
            "bus-gap-" + Guid.NewGuid().ToString("N"));
        var data = ParseEndpoint(Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS"));
        var bus = ParseEndpoint(Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_BUS_REDIS"));
        using var container = BuildHybrid(configuration, data, bus);
        var cache = container.GetRequiredService<IRedisCache>();
        var factory = container.GetRequiredService<IEasyCachingProviderFactory>();
        var distributed = factory.GetCachingProvider(configuration.ProviderName + ":distributed");
        var local = factory.GetCachingProvider(configuration.ProviderName + ":local");
        var database = (IDatabase)distributed.Database;
        using var control = ConnectionMultiplexer.Connect(new ConfigurationOptions
        {
            EndPoints = { { bus.Host, bus.Port } }, AllowAdmin = true
        });
        var server = control.GetServer(bus.Host, bus.Port);
        var changed = CacheKeyFormat.GetPhysicalKey(configuration, "changed");
        var removed = CacheKeyFormat.GetPhysicalKey(configuration, "removed");
        try
        {
            await cache.SetAllAsync(new Dictionary<string, int> { ["changed"] = 1, ["removed"] = 1 });
            await cache.GetAllAsync<int>(new[] { "changed", "removed" });
            Assert.True(local.Get<int>(changed).HasValue);
            await WaitForSubscribersAsync(server, configuration.ProviderName + ":invalidations", 1);
            Assert.True((long)await server.ExecuteAsync("CLIENT", "KILL", "TYPE", "PUBSUB") >= 1);
            // 数据变更不发布通知，模拟断线窗口丢失的消息；重连后也不再额外写入。
            await database.StringSetAsync(changed, distributed.GetProviderInfo().Serializer.Serialize(2), TimeSpan.FromMinutes(1));
            await database.KeyDeleteAsync(removed);
            await WaitForSubscribersAsync(server, configuration.ProviderName + ":invalidations", 1);
            await WaitUntilAsync(() => !local.Get<int>(changed).HasValue && !local.Get<int>(removed).HasValue,
                "重连未清除可能错过失效通知的本地值。");
            Assert.Equal(2, await cache.GetAsync<int>("changed"));
            Assert.Equal(0, await cache.GetAsync<int>("removed"));
        }
        finally { await database.KeyDeleteAsync(new RedisKey[] { changed, removed }); }
    }

    /// <summary>
    /// 总线订阅断开并重连后使对端实例的本地缓存失效。
    /// </summary>
    [EasyHybridBusReconnectFact]
    public async Task HybridBus_SubscriptionDisconnect_ReconnectsAndInvalidatesPeer()
    {
        var configuration = new CacheConfiguration("bus-reconnect-" + Guid.NewGuid().ToString("N"),
            "bus-reconnect-" + Guid.NewGuid().ToString("N"));
        var dataEndpoint = ParseEndpoint(Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS"));
        var busEndpoint = ParseEndpoint(Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_BUS_REDIS"));
        using var firstContainer = BuildHybrid(configuration, dataEndpoint, busEndpoint);
        using var secondContainer = BuildHybrid(configuration, dataEndpoint, busEndpoint);
        using var busControl = ConnectionMultiplexer.Connect(new ConfigurationOptions
        {
            EndPoints = { { busEndpoint.Host, busEndpoint.Port } },
            AllowAdmin = true
        });
        var busServer = busControl.GetServer(busEndpoint.Host, busEndpoint.Port);
        var first = firstContainer.GetRequiredService<IRedisCache>();
        var second = secondContainer.GetRequiredService<IRedisCache>();
        var firstDistributed = firstContainer.GetRequiredService<IEasyCachingProviderFactory>()
            .GetCachingProvider(configuration.ProviderName + ":distributed");
        var dataDatabase = Assert.IsAssignableFrom<IDatabase>(firstDistributed.Database);
        var secondLocal = secondContainer.GetRequiredService<IEasyCachingProviderFactory>()
            .GetCachingProvider(configuration.ProviderName + ":local");
        var topic = configuration.ProviderName + ":invalidations";
        var physicalKey = CacheKeyFormat.GetPhysicalKey(configuration, "item");

        try
        {
            await first.SetAsync("item", 1);
            Assert.Equal(1, await second.GetAsync<int>("item"));
            Assert.Equal(1, secondLocal.Get<int>(physicalKey).Value);
            await WaitForSubscribersAsync(busServer, topic, 2);

            var killed = (long)await busServer.ExecuteAsync("CLIENT", "KILL", "TYPE", "PUBSUB");
            Assert.True(killed >= 2, $"预期断开两个 Hybrid 总线订阅，实际断开 {killed} 个。");
            await WaitForSubscribersAsync(busServer, topic, 2);

            await first.SetAsync("item", 2);
            await WaitUntilAsync(() => !secondLocal.Get<int>(physicalKey).HasValue,
                "总线重连后，第二实例未收到缓存失效通知。");
            Assert.Equal(2, await second.GetAsync<int>("item"));
        }
        finally
        {
            await dataDatabase.KeyDeleteAsync(physicalKey);
        }
    }

    /// <summary>
    /// 总线暂停期间的发布报告失败，并在恢复后继续工作。
    /// </summary>
    [EasyHybridBusPauseFact]
    public async Task HybridBus_PublishDuringPause_ReportsFailureAndRecovers()
    {
        var configuration = new CacheConfiguration("bus-pause-" + Guid.NewGuid().ToString("N"),
            "bus-pause-" + Guid.NewGuid().ToString("N"));
        var dataEndpoint = ParseEndpoint(Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS"));
        var busEndpoint = ParseEndpoint(Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_BUS_REDIS"));
        using var firstContainer = BuildHybrid(configuration, dataEndpoint, busEndpoint, shortBusTimeout: true);
        using var secondContainer = BuildHybrid(configuration, dataEndpoint, busEndpoint, shortBusTimeout: true);
        using var busControl = ConnectionMultiplexer.Connect(new ConfigurationOptions
        {
            EndPoints = { { busEndpoint.Host, busEndpoint.Port } },
            AllowAdmin = true
        });
        var busServer = busControl.GetServer(busEndpoint.Host, busEndpoint.Port);
        var first = firstContainer.GetRequiredService<IRedisCache>();
        var second = secondContainer.GetRequiredService<IRedisCache>();
        var firstDistributed = firstContainer.GetRequiredService<IEasyCachingProviderFactory>()
            .GetCachingProvider(configuration.ProviderName + ":distributed");
        var dataDatabase = Assert.IsAssignableFrom<IDatabase>(firstDistributed.Database);
        var secondLocal = secondContainer.GetRequiredService<IEasyCachingProviderFactory>()
            .GetCachingProvider(configuration.ProviderName + ":local");
        var physicalKey = CacheKeyFormat.GetPhysicalKey(configuration, "item");
        var topic = configuration.ProviderName + ":invalidations";

        try
        {
            await first.SetAsync("item", 1);
            Assert.Equal(1, await second.GetAsync<int>("item"));
            await WaitForSubscribersAsync(busServer, topic, 2);

            await busServer.ExecuteAsync("CLIENT", "PAUSE", "6000", "ALL");
            var error = await Record.ExceptionAsync(() => first.SetAsync("item", 2));
            Assert.NotNull(error);
            Assert.Equal(2, firstDistributed.Get<int>(physicalKey).Value);

            await WaitUntilAsync(async () =>
            {
                try { await busServer.PingAsync(); return true; }
                catch (RedisTimeoutException) { return false; }
                catch (RedisConnectionException) { return false; }
            }, "Redis 总线节点暂停后未恢复响应。");

            secondLocal.Remove(physicalKey);
            Assert.Equal(2, await second.GetAsync<int>("item"));
            await first.SetAsync("item", 3);
            await WaitUntilAsync(() => !secondLocal.Get<int>(physicalKey).HasValue,
                "总线恢复后的发布未使第二实例本地缓存失效。");
            Assert.Equal(3, await second.GetAsync<int>("item"));
        }
        finally
        {
            await dataDatabase.KeyDeleteAsync(physicalKey);
        }
    }

    /// <summary>
    /// 订阅和暂停故障恢复后重新发布通知，并使对端实例失效。
    /// </summary>
    [EasyHybridBusRecoveryFact]
    public async Task HybridBus_SubscriptionAndPauseFailures_ReconnectThenRepublishAndInvalidatePeer()
    {
        var configuration = new CacheConfiguration("bus-recovery-" + Guid.NewGuid().ToString("N"),
            "bus-recovery-" + Guid.NewGuid().ToString("N"));
        var dataEndpoint = ParseEndpoint(Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS"));
        var busEndpoint = ParseEndpoint(Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_BUS_REDIS"));
        using var firstContainer = BuildHybrid(configuration, dataEndpoint, busEndpoint, shortBusTimeout: true);
        using var secondContainer = BuildHybrid(configuration, dataEndpoint, busEndpoint, shortBusTimeout: true);
        using var busControl = ConnectionMultiplexer.Connect(new ConfigurationOptions
        {
            EndPoints = { { busEndpoint.Host, busEndpoint.Port } },
            AllowAdmin = true
        });
        var busServer = busControl.GetServer(busEndpoint.Host, busEndpoint.Port);
        var first = firstContainer.GetRequiredService<IRedisCache>();
        var second = secondContainer.GetRequiredService<IRedisCache>();
        var firstDistributed = firstContainer.GetRequiredService<IEasyCachingProviderFactory>()
            .GetCachingProvider(configuration.ProviderName + ":distributed");
        var dataDatabase = Assert.IsAssignableFrom<IDatabase>(firstDistributed.Database);
        var secondLocal = secondContainer.GetRequiredService<IEasyCachingProviderFactory>()
            .GetCachingProvider(configuration.ProviderName + ":local");
        var topic = configuration.ProviderName + ":invalidations";
        var physicalKey = CacheKeyFormat.GetPhysicalKey(configuration, "item");

        try
        {
            await first.SetAsync("item", 1);
            Assert.Equal(1, await second.GetAsync<int>("item"));
            Assert.Equal(1, secondLocal.Get<int>(physicalKey).Value);
            await WaitForSubscribersAsync(busServer, topic, 2);

            var killed = (long)await busServer.ExecuteAsync("CLIENT", "KILL", "TYPE", "PUBSUB");
            Assert.True(killed >= 2, $"预期断开两个 Hybrid 总线订阅，实际断开 {killed} 个。");
            await WaitForSubscribersAsync(busServer, topic, 2);

            await first.SetAsync("item", 2);
            await WaitUntilAsync(() => !secondLocal.Get<int>(physicalKey).HasValue,
                "订阅连接重建后，第二实例未收到缓存失效通知。");
            Assert.Equal(2, await second.GetAsync<int>("item"));
            Assert.Equal(2, secondLocal.Get<int>(physicalKey).Value);

            await busServer.ExecuteAsync("CLIENT", "PAUSE", "6000", "ALL");
            var publishError = await Record.ExceptionAsync(() => first.SetAsync("item", 3));
            Assert.NotNull(publishError);
            Assert.Equal(3, firstDistributed.Get<int>(physicalKey).Value);
            Assert.Equal(2, secondLocal.Get<int>(physicalKey).Value);

            await WaitUntilAsync(async () =>
            {
                try
                {
                    await busServer.PingAsync();
                    return true;
                }
                catch (RedisTimeoutException) { return false; }
                catch (RedisConnectionException) { return false; }
            }, "Redis 总线节点暂停后未恢复响应。");

            await first.SetAsync("item", 4);
            await WaitUntilAsync(() => !secondLocal.Get<int>(physicalKey).HasValue,
                "总线恢复后的重试发布未使第二实例本地缓存失效。");
            Assert.Equal(4, await second.GetAsync<int>("item"));
        }
        finally
        {
            await dataDatabase.KeyDeleteAsync(physicalKey);
        }
    }

    /// <summary>
    /// 创建配置了分布式层、本地层和测试总线的 Hybrid 服务容器。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="dataEndpoint">缓存数据 Redis 端点。</param>
    /// <param name="busEndpoint">失效消息总线 Redis 端点。</param>
    /// <param name="shortBusTimeout">是否使用较短的总线超时。</param>
    /// <returns>返回已注册 Hybrid Provider 和测试总线的服务容器。</returns>
    private static ServiceProvider BuildHybrid(CacheConfiguration configuration,
        (string Host, int Port) dataEndpoint, (string Host, int Port) busEndpoint,
        bool shortBusTimeout = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCaching(configuration, configuration.ProviderName + ":distributed",
            redis => redis.DBConfig.Endpoints.Add(new ServerEndPoint(dataEndpoint.Host, dataEndpoint.Port)),
            bus => bus.ConfigurationOptions = new ConfigurationOptions
            {
                EndPoints = { { busEndpoint.Host, busEndpoint.Port } },
                AbortOnConnectFail = false,
                SyncTimeout = shortBusTimeout ? 750 : 5000,
                AsyncTimeout = shortBusTimeout ? 750 : 5000
            },
            configureHybrid: hybrid => hybrid.BusRetryCount = shortBusTimeout ? 0 : hybrid.BusRetryCount);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 等待 Redis 订阅者数量达到预期值。
    /// </summary>
    /// <param name="server">Redis 服务端。</param>
    /// <param name="topic">发布主题。</param>
    /// <param name="expected">预期值。</param>
    private static async Task WaitForSubscribersAsync(IServer server, string topic, long expected)
    {
        await WaitUntilAsync(async () =>
        {
            var result = (RedisResult[])await server.ExecuteAsync("PUBSUB", "NUMSUB", topic);
            return (long)result[1] >= expected;
        }, $"Redis 总线主题 {topic} 未恢复 {expected} 个订阅。");
    }

    /// <summary>
    /// 等待条件满足或超时。
    /// </summary>
    /// <param name="condition">等待条件。</param>
    /// <param name="error">错误信息。</param>
    private static async Task WaitUntilAsync(Func<bool> condition, string error)
    {
        await WaitUntilAsync(() => Task.FromResult(condition()), error);
    }

    /// <summary>
    /// 等待条件满足或超时。
    /// </summary>
    /// <param name="condition">等待条件。</param>
    /// <param name="error">错误信息。</param>
    private static async Task WaitUntilAsync(Func<Task<bool>> condition, string error)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!await condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException(error);
            await Task.Delay(100);
        }
    }

    /// <summary>
    /// 解析测试总线连接字符串中的 Redis 端点。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <returns>返回 Redis 主机名和端口。</returns>
    private static (string Host, int Port) ParseEndpoint(string connection)
    {
        var endpoint = connection.Split(',', 2)[0].Trim();
        var separator = endpoint.LastIndexOf(':');
        if (separator <= 0 || !int.TryParse(endpoint[(separator + 1)..], out var port))
            throw new FormatException("Redis 测试连接串必须包含 host:port。");
        return (endpoint[..separator].Trim(), port);
    }
}
