using Bing.Caching;
using EasyCaching.Core;
using EasyCaching.Core.Bus;
using EasyCaching.Core.Configurations;
using EasyCaching.HybridCache;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Globalization;
using System.Reflection;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 验证客户端分片 Hybrid 在失效通知发布失败时仍传播错误，并保留已提交值。
/// </summary>
public sealed class EasyShardedHybridFailureIntegrationTest
{
    /// <summary>
    /// 发布失效消息时计数并抛出异常的测试总线。
    /// </summary>
    private sealed class FailingBus : IEasyCachingBus
    {
        /// <summary>
        /// 记录总线发布方法的调用次数。
        /// </summary>
        public int PublishCount;

        /// <inheritdoc />
        /// <remarks>
        /// 返回固定为 sharded-failing-bus 的测试总线名称。
        /// </remarks>
        public string Name => "sharded-failing-bus";

        /// <inheritdoc />
        /// <param name="topic">发布主题。</param>
        /// <param name="message">消息内容。</param>
        public void Publish(string topic, EasyCachingMessage message)
        {
            Interlocked.Increment(ref PublishCount);
            throw new InvalidOperationException("bus failed");
        }

        /// <inheritdoc />
        /// <param name="topic">发布主题。</param>
        /// <param name="message">消息内容。</param>
        /// <param name="token">令牌。</param>
        public Task PublishAsync(string topic, EasyCachingMessage message,
            CancellationToken token = default)
        {
            Interlocked.Increment(ref PublishCount);
            return Task.FromException(new InvalidOperationException("bus failed"));
        }

        /// <inheritdoc />
        /// <param name="topic">发布主题。</param>
        /// <param name="handler">处理器。</param>
        /// <param name="onConnect">连接回调。</param>
        public void Subscribe(string topic, Action<EasyCachingMessage> handler, Action onConnect) { }

        /// <inheritdoc />
        /// <param name="topic">发布主题。</param>
        /// <param name="handler">处理器。</param>
        /// <param name="onConnect">连接回调。</param>
        /// <param name="token">令牌。</param>
        public Task SubscribeAsync(string topic, Action<EasyCachingMessage> handler, Action onConnect,
            CancellationToken token = default) => Task.CompletedTask;
    }

    /// <summary>
    /// 验证 HybridCacheManager 拒绝未跟踪的总线。
    /// </summary>
    [Fact]
    public void HybridManager_RejectsUntrackedBus()
    {
        var configuration = new CacheConfiguration("hybrid-untracked-bus", "hybrid-untracked-bus");
        var provider = new Mock<IEasyCachingProvider>();
        var providerFactory = new Mock<IEasyCachingProviderFactory>();
        providerFactory.Setup(factory => factory.GetCachingProvider(It.IsAny<string>()))
            .Returns(provider.Object);
        var hybridFactory = new Mock<IHybridProviderFactory>();
        hybridFactory.Setup(factory => factory.GetHybridCachingProvider(configuration.ProviderName))
            .Returns(new Mock<IHybridCachingProvider>().Object);

        var error = Assert.Throws<ArgumentException>(() => new HybridCacheManager(
            providerFactory.Object, hybridFactory.Object, configuration, "distributed", "local",
            new FailingBus(), "invalidations"));

        Assert.Contains(nameof(HybridBusFailureTracker), error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 失效通知发布失败在分布式层和本地层提交完成后向调用方传播。
    /// </summary>
    [Fact]
    public async Task HybridBatchBusFailure_SetAllPropagatesAfterLayerOperations()
    {
        var fixture = CreateBatchFailureFixture();
        var values = new Dictionary<string, int>
        {
            ["key:one"] = 1,
            ["key:two"] = 2
        };

        var error = await Record.ExceptionAsync(() =>
            fixture.Manager.SetAllAsync(values, cancellationToken: CancellationToken.None));

        Assert.True(fixture.Bus.PublishCount > 0);
        Assert.IsType<InvalidOperationException>(error);
        var expected = values.ToDictionary(item =>
            CacheKeyFormat.GetPhysicalKey(fixture.Configuration, item.Key), item => item.Value);
        Assert.Equal(expected, fixture.DistributedValues);
        Assert.Equal(expected, fixture.LocalValues);
    }

    /// <summary>
    /// 批量删除完成两层清理后仍将失效通知发布失败向调用方传播。
    /// </summary>
    [Fact]
    public async Task HybridBatchBusFailure_RemoveAllPropagatesAfterLayerOperations()
    {
        var fixture = CreateBatchFailureFixture();
        var values = new Dictionary<string, int>
        {
            ["key:one"] = 1,
            ["key:two"] = 2
        };
        foreach (var item in values)
        {
            var physicalKey = CacheKeyFormat.GetPhysicalKey(fixture.Configuration, item.Key);
            fixture.DistributedValues[physicalKey] = item.Value;
            fixture.LocalValues[physicalKey] = item.Value;
        }

        var error = await Record.ExceptionAsync(() =>
            fixture.Manager.RemoveAllAsync(values.Keys, CancellationToken.None));

        Assert.True(fixture.Bus.PublishCount > 0);
        Assert.IsType<InvalidOperationException>(error);
        Assert.Empty(fixture.DistributedValues);
        Assert.Empty(fixture.LocalValues);
    }

    /// <summary>
    /// 创建包含两层数据字典和失败总线的批量故障夹具。
    /// </summary>
    /// <returns>返回用于验证批量失效通知失败传播的夹具。</returns>
    private static BatchFailureFixture CreateBatchFailureFixture()
    {
        var configuration = new CacheConfiguration("hybrid-batch-failure", "hybrid-batch-failure",
            batchSize: 2);
        var distributedValues = new Dictionary<string, int>(StringComparer.Ordinal);
        var localValues = new Dictionary<string, int>(StringComparer.Ordinal);
        var distributed = CreateStoreProvider(distributedValues);
        var local = CreateStoreProvider(localValues);
        distributed.SetupGet(item => item.IsDistributedCache).Returns(true);
        local.SetupGet(item => item.IsDistributedCache).Returns(false);
        var factory = new Mock<IEasyCachingProviderFactory>();
        factory.Setup(item => item.GetCachingProvider(It.IsAny<string>()))
            .Returns((string name) => string.Equals(name, "local", StringComparison.Ordinal)
                ? local.Object : distributed.Object);
        factory.Setup(item => item.GetRedisProvider(It.IsAny<string>()))
            .Returns(new Mock<IRedisCachingProvider>().Object);
        var bus = new FailingBus();
        var tracker = new HybridBusFailureTracker(bus);
        var hybrid = new HybridCachingProvider("hybrid-failure",
            new HybridCachingOptions
            {
                DistributedCacheProviderName = "distributed",
                LocalCacheProviderName = "local",
                TopicName = "hybrid-failure:invalidations",
                BusRetryCount = 0,
                ThrowIfDistributedCacheError = true
            }, factory.Object, tracker, NullLoggerFactory.Instance);
        var manager = new CacheManager(distributed.Object, configuration, hybrid, local.Object);
        manager.SetBusFailureTracker(tracker);
        return new BatchFailureFixture(manager, configuration, distributedValues, localValues, bus);
    }

    /// <summary>
    /// 保存批量 Hybrid 故障测试所需的管理器、层数据和总线。
    /// </summary>
    /// <param name="Manager">待测试的缓存管理器。</param>
    /// <param name="Configuration">缓存配置。</param>
    /// <param name="DistributedValues">模拟分布式层保存的物理键值。</param>
    /// <param name="LocalValues">模拟本地层保存的物理键值。</param>
    /// <param name="Bus">发布时失败的测试总线。</param>
    private sealed record BatchFailureFixture(
        CacheManager Manager,
        CacheConfiguration Configuration,
        Dictionary<string, int> DistributedValues,
        Dictionary<string, int> LocalValues,
        FailingBus Bus);

    /// <summary>
    /// 创建将批量写入和删除映射到指定字典的 Provider 模拟对象。
    /// </summary>
    /// <param name="values">待处理值集合。</param>
    /// <returns>返回绑定指定字典的 Provider 模拟对象。</returns>
    private static Mock<IEasyCachingProvider> CreateStoreProvider(IDictionary<string, int> values)
    {
        var provider = new Mock<IEasyCachingProvider>();
        provider.Setup(item => item.SetAllAsync(
                It.IsAny<IDictionary<string, int>>(), It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns((IDictionary<string, int> items, TimeSpan _, CancellationToken _) =>
            {
                foreach (var item in items)
                    values[item.Key] = item.Value;
                return Task.CompletedTask;
            });
        provider.Setup(item => item.RemoveAllAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
            {
                foreach (var key in keys)
                    values.Remove(key);
                return Task.CompletedTask;
            });
        return provider;
    }

    /// <summary>
    /// 分片 Hybrid 失效通知发布失败在提交完成后向调用方传播。
    /// </summary>
    [EasyShardedRedisFact]
    public async Task ShardedHybrid_BusPublishFailurePropagatesAfterCommit()
    {
        var configuration = new CacheConfiguration(
            "easy-sharded-hybrid-failure-" + Guid.NewGuid().ToString("N"),
            "easy-sharded-hybrid-failure-" + Guid.NewGuid().ToString("N"),
            batchSize: 2, maxConcurrency: 2, topology: CacheTopology.ClientSharded,
            expectedShardCount: 2);
        using var container = BuildHybrid(configuration);
        var factory = container.GetRequiredService<IEasyCachingProviderFactory>();
        var hybridFactory = container.GetRequiredService<IHybridProviderFactory>();
        var innerConfiguration = new CacheConfiguration(configuration.Namespace,
            configuration.ProviderName, configuration.DefaultExpiration, configuration.BatchSize,
            configuration.MaxConcurrency, configuration.Coordination);
        var managers = new CacheManager[2];
        var databases = new IDatabase[2];
        var distributedProviders = new IEasyCachingProvider[2];
        var localProviders = new IEasyCachingProvider[2];

        for (var index = 0; index < 2; index++)
        {
            var name = configuration.ProviderName + ":shard:" + index;
            var distributed = factory.GetCachingProvider(name + ":distributed");
            var local = factory.GetCachingProvider(name + ":local");
            var hybrid = hybridFactory.GetHybridCachingProvider(name);
            distributedProviders[index] = distributed;
            localProviders[index] = local;
            databases[index] = Assert.IsAssignableFrom<IDatabase>(distributed.Database);

            var perShardHybridFactory = new Mock<IHybridProviderFactory>();
            perShardHybridFactory.Setup(item => item.GetHybridCachingProvider(innerConfiguration.ProviderName))
                .Returns(hybrid);
            managers[index] = new HybridCacheManager(factory, perShardHybridFactory.Object,
                innerConfiguration, name + ":distributed", name + ":local",
                new HybridBusFailureTracker(new FailingBus()), name + ":invalidations");
        }

        var cache = CreateShardedManager(managers, databases, configuration);
        var key = FindKeyForShard(configuration, 0);
        var physicalKey = CacheKeyFormat.GetPhysicalKey(configuration, key);

        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                cache.GetAsync(key, _ => Task.FromResult(42)));

            Assert.Equal("bus failed", error.Message);
            Assert.Equal(42, distributedProviders[0].Get<int>(physicalKey).Value);
            Assert.False(localProviders[0].Get<int>(physicalKey).HasValue);
        }
        finally
        {
            await distributedProviders[0].RemoveAsync(physicalKey, CancellationToken.None);
            await localProviders[0].RemoveAsync(physicalKey, CancellationToken.None);
            await databases[0].KeyDeleteAsync(physicalKey + ":lease");
        }
    }

    /// <summary>
    /// 创建使用指定 Provider 和分片配置的缓存管理器。
    /// </summary>
    /// <param name="managers">各分片对应的缓存管理器。</param>
    /// <param name="databases">各分片对应的 Redis 数据库。</param>
    /// <param name="configuration">缓存配置。</param>
    /// <returns>返回绑定分片管理器和数据库的客户端分片缓存。</returns>
    private static ShardedRedisCacheManager CreateShardedManager(
        IReadOnlyList<CacheManager> managers, IReadOnlyList<IDatabase> databases,
        CacheConfiguration configuration)
    {
        var constructor = typeof(ShardedRedisCacheManager).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            new[] { typeof(IReadOnlyList<CacheManager>), typeof(IReadOnlyList<IDatabase>),
                typeof(CacheConfiguration) },
            modifiers: null);
        Assert.NotNull(constructor);
        return Assert.IsType<ShardedRedisCacheManager>(constructor.Invoke(new object[]
        {
            managers, databases, configuration
        }));
    }

    /// <summary>
    /// 查找由指定客户端分片负责的缓存键。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="expectedShard">预期分片。</param>
    /// <returns>返回映射到目标分片的逻辑键。</returns>
    private static string FindKeyForShard(CacheConfiguration configuration, int expectedShard)
    {
        for (var index = 0; index < 10000; index++)
        {
            var key = "entry:" + index;
            var physicalKey = CacheKeyFormat.GetPhysicalKey(configuration, key);
            var start = physicalKey.IndexOf('{') + 1;
            var hash = uint.Parse(physicalKey.Substring(start, 8), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
            if (hash % 2 == expectedShard)
                return key;
        }

        throw new InvalidOperationException("无法生成目标分片测试键。");
    }

    /// <summary>
    /// 创建配置了客户端分片和 Hybrid 总线的服务容器。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <returns>返回已注册客户端分片 Hybrid Provider 的服务容器。</returns>
    private static ServiceProvider BuildHybrid(CacheConfiguration configuration)
    {
        var first = ParseEndpoint(Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS"));
        var second = ParseEndpoint(Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION"));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddShardedHybridCaching(configuration,
            new[] { ConfigureNode(first), ConfigureNode(second) },
            bus => bus.ConfigurationOptions = CreateBusOptions(first));
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 为指定 Redis 节点创建连接配置回调。
    /// </summary>
    /// <param name="endpoint">Redis 端点。</param>
    /// <returns>返回应用 Redis 节点连接设置的配置回调。</returns>
    private static Action<global::EasyCaching.Redis.RedisOptions> ConfigureNode(
        (string Host, int Port) endpoint) => options =>
    {
        options.DBConfig.Endpoints.Add(new ServerEndPoint(endpoint.Host, endpoint.Port));
        options.DBConfig.AbortOnConnectFail = false;
    };

    /// <summary>
    /// 创建连接指定端点的总线 Redis 配置选项。
    /// </summary>
    /// <param name="endpoint">Redis 端点。</param>
    /// <returns>返回测试总线使用的 Redis 连接选项。</returns>
    private static ConfigurationOptions CreateBusOptions((string Host, int Port) endpoint)
    {
        var options = new ConfigurationOptions { AbortOnConnectFail = false };
        options.EndPoints.Add(endpoint.Host, endpoint.Port);
        return options;
    }

    /// <summary>
    /// 解析测试 Redis 连接字符串中的端点。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <returns>返回 Redis 主机名和端口。</returns>
    private static (string Host, int Port) ParseEndpoint(string connection)
    {
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("缺少 Redis 分片连接配置。");
        var endpoint = connection.Split(',', 2)[0].Trim();
        var separator = endpoint.LastIndexOf(':');
        if (separator <= 0 || separator == endpoint.Length - 1 ||
            !int.TryParse(endpoint[(separator + 1)..], out var port))
            throw new FormatException("Redis 连接串必须包含 host:port 端点。");
        var host = endpoint[..separator].Trim();
        if (host.StartsWith("[", StringComparison.Ordinal) && host.EndsWith("]", StringComparison.Ordinal))
            host = host[1..^1];
        return (host, port);
    }
}
