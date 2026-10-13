using Bing.Caching;
using Bing.Caching.ContractTests;
using EasyCaching.Core;
using EasyCaching.Core.Configurations;
using EasyCaching.Core.Bus;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 仅在启用集成测试并配置隔离 EasyCaching Redis 环境时运行。
/// </summary>
public sealed class RedisIntegrationFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="RedisIntegrationFactAttribute"/> 类型的实例。
    /// </summary>
    public RedisIntegrationFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS")))
            Skip = "需要隔离的 EasyCaching Redis 集成环境。";
    }
}

/// <summary>
/// 验证 Redis 和 Hybrid 协调器的批量、回源及失效语义。
/// </summary>
public class RedisCoordinatorIntegrationTest
{
    /// <summary>
    /// Redis 和 Hybrid Provider 在不同键工厂下共享资源准入和载荷预算。
    /// </summary>
    [RedisIntegrationFact]
    public async Task RedisAndHybrid_DifferentKeyFactoriesAndBatchPayloadRespectResourceLimits()
    {
        foreach (var hybrid in new[] { false, true })
        {
            var configuration = new CacheConfiguration("resource-easy-" + Guid.NewGuid().ToString("N"),
                "easy-resource", batchSize: 2, resources: new CacheResourceOptions(1, 8, 32));
            using var container = hybrid ? BuildHybrid(configuration) : BuildRedis(configuration);
            var provider = container.GetRequiredService<IEasyCachingProviderFactory>()
                .GetCachingProvider(hybrid ? configuration.ProviderName + ":distributed" : configuration.ProviderName);
            await CacheCoordinationAssertions.VerifyResourceLimitsAsync(container.GetRequiredService<IRedisCache>(),
                values => provider.SetAllAsync(values.ToDictionary(item => CacheKeyFormat.GetPhysicalKey(configuration, item.Key),
                    item => item.Value), TimeSpan.FromMinutes(1)));
        }
    }

    /// <summary>
    /// 批量载荷拆分后保持 List 和流式读取语义一致。
    /// </summary>
    [RedisIntegrationFact]
    public async Task RedisAndHybrid_BatchPayloadSplittingPreservesListAndStreamSemantics()
    {
        foreach (var hybrid in new[] { false, true })
        {
            var configuration = new CacheConfiguration("adaptive-easy-" + Guid.NewGuid().ToString("N"),
                "easy-adaptive", batchSize: 2,
                resources: new CacheResourceOptions(maxBatchPayloadBytes: 32));
            using var container = hybrid ? BuildHybrid(configuration) : BuildRedis(configuration);
            await CacheCoordinationAssertions.VerifyAdaptivePayloadSplittingAsync(
                container.GetRequiredService<IRedisCache>());
        }
    }

    /// <summary>
    /// 相同键在零队列和租约丢失时合并回源并取消工厂。
    /// </summary>
    [RedisIntegrationFact]
    public async Task RedisAndHybrid_SameKeyMergeWithZeroQueueAndLeaseLossCancelsFactory()
    {
        foreach (var hybrid in new[] { false, true })
        {
            var configuration = new CacheConfiguration("merge-easy-" + Guid.NewGuid().ToString("N"), "easy-merge",
                resources: new CacheResourceOptions(1, 0),
                coordination: new CacheCoordinationOptions(renewalInterval: TimeSpan.FromMilliseconds(50)));
            using var container = hybrid ? BuildHybrid(configuration) : BuildRedis(configuration);
            await CacheCoordinationAssertions.VerifySameKeyMergeAndLeaseLossAsync(container.GetRequiredService<IRedisCache>());
        }
    }

    /// <summary>
    /// 发布失效消息时抛出异常的测试总线。
    /// </summary>
    private sealed class FailingBus : IEasyCachingBus
    {
        /// <inheritdoc />
        /// <remarks>
        /// 返回固定为 failing-bus 的测试总线名称。
        /// </remarks>
        public string Name => "failing-bus";

        /// <inheritdoc />
        /// <param name="topic">发布主题。</param>
        /// <param name="message">消息内容。</param>
        public void Publish(string topic, EasyCachingMessage message) =>
            throw new InvalidOperationException("bus failed");

        /// <inheritdoc />
        /// <param name="topic">发布主题。</param>
        /// <param name="message">消息内容。</param>
        /// <param name="token">令牌。</param>
        public Task PublishAsync(string topic, EasyCachingMessage message, CancellationToken token = default) =>
            Task.FromException(new InvalidOperationException("bus failed"));

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
    /// Provider 满足共享缓存契约。
    /// </summary>
    [RedisIntegrationFact]
    public async Task Provider_SatisfiesSharedContract()
    {
        var configuration = new CacheConfiguration("contract-easy-" + Guid.NewGuid().ToString("N"), "easy-redis");
        using var container = BuildRedis(configuration);
        await CacheContractAssertions.VerifyAsync(container.GetRequiredService<IRedisCache>());
    }

    /// <summary>
    /// Redis 批量读取和流式读取保持顺序、重复键和缺失值一致。
    /// </summary>
    [RedisIntegrationFact]
    public async Task RedisBatchReadAndStream_KeepOrderDuplicatesAndMissingConsistent()
    {
        var configuration = new CacheConfiguration("easy-batch-" + Guid.NewGuid().ToString("N"), "easy-redis",
            batchSize: 2);
        using var container = BuildRedis(configuration);
        var cache = container.GetRequiredService<IRedisCache>();
        var prefix = "batch:";
        var keys = new[] { prefix + "one", prefix + "two", prefix + "three" };
        var requested = new[] { keys[2], keys[0], prefix + "missing", keys[1], keys[0] };

        try
        {
            await cache.SetAllAsync(new Dictionary<string, int>
            {
                [keys[0]] = 11,
                [keys[1]] = 22,
                [keys[2]] = 33
            });

            var list = await cache.GetAllAsync<int?>(requested);
            var stream = new List<CacheEntry<int?>>();
            await foreach (var entry in cache.GetAllStreamAsync<int?>(requested))
                stream.Add(entry);

            Assert.Equal(requested, stream.Select(entry => entry.Key));
            Assert.Equal(list, stream.Select(entry => entry.Value));
            Assert.Equal(new[] { true, true, false, true, true }, stream.Select(entry => entry.Hit));

            var prefixList = await cache.GetByPrefixAsync<int?>(prefix);
            var prefixStream = new List<CacheEntry<int?>>();
            await foreach (var entry in cache.GetByPrefixStreamAsync<int?>(prefix))
                prefixStream.Add(entry);
            Assert.Equal(prefixList.OrderBy(value => value), prefixStream.Select(entry => entry.Value).OrderBy(value => value));
            Assert.All(prefixStream, entry => Assert.True(entry.Hit));
        }
        finally
        {
            await cache.ClearAsync();
        }
    }

    /// <summary>
    /// Redis 10,000 条数据的列表读取与流式读取结果一致。
    /// </summary>
    [RedisIntegrationFact]
    public async Task RedisLargeBatch_ListAndStreamAgreeAcrossTenThousandKeys()
    {
        var configuration = new CacheConfiguration("easy-large-" + Guid.NewGuid().ToString("N"),
            "easy-redis", batchSize: 500);
        using var container = BuildRedis(configuration);
        var cache = container.GetRequiredService<IRedisCache>();
        const string prefix = "large:";
        var values = Enumerable.Range(0, 10_000)
            .ToDictionary(index => prefix + index, index => index + 1);
        var requested = values.Keys.Reverse()
            .Concat(new[] { prefix + "missing", prefix + "0", prefix + "missing" }).ToArray();

        try
        {
            await cache.SetAllAsync(values);
            var list = await cache.GetAllAsync<int>(requested);
            var stream = new List<CacheEntry<int>>(requested.Length);
            await foreach (var entry in cache.GetAllStreamAsync<int>(requested))
                stream.Add(entry);

            Assert.Equal(requested, stream.Select(entry => entry.Key));
            Assert.Equal(list, stream.Select(entry => entry.Value));
            Assert.Equal(requested.Select(values.ContainsKey), stream.Select(entry => entry.Hit));

            var prefixList = await cache.GetByPrefixAsync<int>(prefix);
            var prefixStream = new List<CacheEntry<int>>(values.Count);
            await foreach (var entry in cache.GetByPrefixStreamAsync<int>(prefix))
                prefixStream.Add(entry);
            Assert.Equal(values.Count, prefixStream.Count);
            Assert.All(prefixStream, entry => Assert.True(entry.Hit));
            Assert.Equal(prefixList.OrderBy(value => value),
                prefixStream.Select(entry => entry.Value).OrderBy(value => value));
        }
        finally
        {
            await cache.ClearAsync();
        }
    }

    /// <summary>
    /// Hybrid 批量读取和流式读取保持分布式层与本地层值一致。
    /// </summary>
    [RedisIntegrationFact]
    public async Task HybridBatchReadAndStream_KeepDistributedAndLocalValuesConsistent()
    {
        var configuration = new CacheConfiguration("easy-hybrid-batch-" + Guid.NewGuid().ToString("N"), "easy-hybrid",
            batchSize: 2);
        using var container = BuildHybrid(configuration);
        var cache = container.GetRequiredService<IRedisCache>();
        var factory = container.GetRequiredService<IEasyCachingProviderFactory>();
        var distributed = factory.GetCachingProvider(configuration.ProviderName + ":distributed");
        var local = factory.GetCachingProvider(configuration.ProviderName + ":local");
        var prefix = "hybrid-batch:";
        var keys = new[] { prefix + "one", prefix + "two", prefix + "three" };
        var requested = new[] { keys[1], keys[2], prefix + "missing", keys[0], keys[1] };
        var physicalKeys = keys.Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key)).ToArray();
        var missingPhysicalKey = CacheKeyFormat.GetPhysicalKey(configuration, prefix + "missing");

        try
        {
            await local.RemoveAllAsync(physicalKeys.Concat(new[] { missingPhysicalKey }), CancellationToken.None);
            await distributed.SetAllAsync(new Dictionary<string, int>
            {
                [physicalKeys[0]] = 101,
                [physicalKeys[1]] = 202,
                [physicalKeys[2]] = 303
            }, configuration.DefaultExpiration, CancellationToken.None);

            Assert.All(physicalKeys, physicalKey => Assert.True(distributed.Get<int>(physicalKey).HasValue));
            Assert.All(physicalKeys.Append(missingPhysicalKey),
                physicalKey => Assert.False(local.Get<int>(physicalKey).HasValue));

            var list = await cache.GetAllAsync<int?>(requested);
            Assert.Equal(101, local.Get<int>(physicalKeys[0]).Value);
            Assert.Equal(202, local.Get<int>(physicalKeys[1]).Value);
            Assert.Equal(303, local.Get<int>(physicalKeys[2]).Value);
            Assert.False(local.Get<int>(missingPhysicalKey).HasValue);

            await local.RemoveAllAsync(physicalKeys, CancellationToken.None);
            var stream = new List<CacheEntry<int?>>();
            await foreach (var entry in cache.GetAllStreamAsync<int?>(requested))
                stream.Add(entry);

            Assert.Equal(requested, stream.Select(entry => entry.Key));
            Assert.Equal(list, stream.Select(entry => entry.Value));
            Assert.Equal(new[] { true, true, false, true, true }, stream.Select(entry => entry.Hit));
            Assert.Equal(101, local.Get<int>(CacheKeyFormat.GetPhysicalKey(configuration, keys[0])).Value);
            Assert.Equal(202, local.Get<int>(CacheKeyFormat.GetPhysicalKey(configuration, keys[1])).Value);
            Assert.Equal(303, local.Get<int>(CacheKeyFormat.GetPhysicalKey(configuration, keys[2])).Value);
            Assert.False(local.Get<int>(missingPhysicalKey).HasValue);

            var prefixList = await cache.GetByPrefixAsync<int?>(prefix);
            var prefixStream = new List<CacheEntry<int?>>();
            await foreach (var entry in cache.GetByPrefixStreamAsync<int?>(prefix))
                prefixStream.Add(entry);
            Assert.Equal(prefixList.OrderBy(value => value), prefixStream.Select(entry => entry.Value).OrderBy(value => value));
            Assert.All(prefixStream, entry => Assert.True(entry.Hit));
        }
        finally
        {
            await cache.ClearAsync();
        }
    }

    /// <summary>
    /// Redis 前缀扫描支持特殊键且不触碰其他命名空间。
    /// </summary>
    [RedisIntegrationFact]
    public async Task RedisScan_HandlesSpecialKeysWithoutTouchingAnotherNamespace()
    {
        var configuration = new CacheConfiguration("easy-scan-" + Guid.NewGuid().ToString("N"), "easy-redis");
        var otherConfiguration = new CacheConfiguration(configuration.Namespace + "-other", "easy-redis");
        using var container = BuildRedis(configuration);
        var cache = container.GetRequiredService<IRedisCache>();
        var database = (IDatabase)container.GetRequiredService<IEasyCachingProviderFactory>()
            .GetCachingProvider(configuration.ProviderName).Database;
        var sentinel = CacheKeyFormat.GetPhysicalKey(otherConfiguration, "订单{a}sentinel");
        try
        {
            await database.StringSetAsync(sentinel, "other");
            cache.Set("订单{a}*\\?", 1);
            cache.Set("订单{a}2", 2);
            Assert.Equal(2, (await cache.GetByPrefixAsync<int>("订单{a}")).Count);
            var streamed = new List<CacheEntry<int>>();
            await foreach (var entry in cache.GetByPrefixStreamAsync<int>("订单{a}"))
                streamed.Add(entry);
            Assert.Equal(new[] { 1, 2 }, streamed.Select(entry => entry.Value).OrderBy(value => value));
            Assert.All(streamed, entry => Assert.True(entry.Hit));
            Assert.DoesNotContain(streamed, entry => entry.Key == "订单{a}sentinel");
            await cache.RemoveByPatternAsync("订单{a}\\*\\\\\\?");
            Assert.False(cache.Exists("订单{a}*\\?"));
            Assert.True(cache.Exists("订单{a}2"));
            await cache.ClearAsync();
            Assert.True(await database.KeyExistsAsync(sentinel));
        }
        finally { await database.KeyDeleteAsync(sentinel); }
    }

    /// <summary>
    /// Hybrid 前缀扫描支持特殊键且不触碰其他命名空间。
    /// </summary>
    [RedisIntegrationFact]
    public async Task HybridScan_HandlesSpecialKeysWithoutTouchingAnotherNamespace()
    {
        var configuration = new CacheConfiguration("easy-hybrid-scan-" + Guid.NewGuid().ToString("N"),
            "easy-hybrid");
        var otherConfiguration = new CacheConfiguration(configuration.Namespace + "-other", "easy-hybrid");
        using var container = BuildHybrid(configuration);
        var cache = container.GetRequiredService<IRedisCache>();
        var distributed = container.GetRequiredService<IEasyCachingProviderFactory>()
            .GetCachingProvider(configuration.ProviderName + ":distributed");
        var database = Assert.IsAssignableFrom<IDatabase>(distributed.Database);
        var sentinel = CacheKeyFormat.GetPhysicalKey(otherConfiguration, "订单{a}sentinel");
        const string prefix = "订单{a}";
        const string wildcardKey = "订单{a}*\\?";
        const string ordinaryKey = "订单{a}2";
        try
        {
            await database.StringSetAsync(sentinel, "other");
            await cache.SetAsync(wildcardKey, 1);
            await cache.SetAsync(ordinaryKey, 2);

            var streamed = new List<CacheEntry<int>>();
            await foreach (var entry in cache.GetByPrefixStreamAsync<int>(prefix))
                streamed.Add(entry);

            Assert.Equal(new[] { 1, 2 }, streamed.Select(entry => entry.Value).OrderBy(value => value));
            Assert.All(streamed, entry => Assert.True(entry.Hit));
            Assert.Contains(streamed, entry => entry.Key == wildcardKey);
            Assert.DoesNotContain(streamed, entry => entry.Key == "订单{a}sentinel");
            Assert.False(cache.Exists("订单{a}sentinel"));

            await cache.RemoveByPatternAsync("订单{a}\\*\\\\\\?");
            Assert.False(cache.Exists(wildcardKey));
            Assert.True(cache.Exists(ordinaryKey));

            await cache.ClearAsync();
            Assert.True(await database.KeyExistsAsync(sentinel));
        }
        finally
        {
            await database.KeyDeleteAsync(sentinel);
            await cache.RemoveAllAsync(new[] { ordinaryKey, wildcardKey });
        }
    }

    /// <summary>
    /// Redis 回源在多个管理器实例间合并相同键的请求。
    /// </summary>
    [RedisIntegrationFact]
    public async Task RedisRetriever_CoalescesAcrossManagerInstances()
    {
        var configuration = new CacheConfiguration("easy-coalesce-" + Guid.NewGuid().ToString("N"), "easy-redis");
        using var container = BuildRedis(configuration);
        var factory = container.GetRequiredService<IEasyCachingProviderFactory>();
        var left = new RedisCacheManager(factory, configuration);
        var right = new RedisCacheManager(factory, configuration);
        var calls = 0;
        async Task<int> Retrieve(CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(150, token);
            return 42;
        }

        var results = await Task.WhenAll(left.GetAsync("entry", Retrieve), right.GetAsync("entry", Retrieve));

        Assert.Equal(new[] { 42, 42 }, results);
        Assert.Equal(1, calls);
        await left.ClearAsync();
    }

    /// <summary>
    /// Redis 回源拒绝已过期持有者的提交。
    /// </summary>
    [RedisIntegrationFact]
    public async Task RedisRetriever_RejectsExpiredOwnerCommit()
    {
        var coordination = new CacheCoordinationOptions(TimeSpan.FromMilliseconds(300),
            TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(150));
        var configuration = new CacheConfiguration("easy-expired-" + Guid.NewGuid().ToString("N"),
            "easy-redis", coordination: coordination);
        using var container = BuildRedis(configuration);
        var cache = container.GetRequiredService<IRedisCache>();
        var key = CacheKeyFormat.GetPhysicalKey(configuration, "entry");
        var database = (IDatabase)container.GetRequiredService<IEasyCachingProviderFactory>()
            .GetCachingProvider(configuration.ProviderName).Database;

        await Assert.ThrowsAsync<TimeoutException>(() => cache.GetAsync("entry", async _ =>
        {
            await Task.Delay(300);
            return 7;
        }));
        Assert.False(await database.KeyExistsAsync(key));
        Assert.Equal(8, await cache.GetAsync("entry", _ => Task.FromResult(8)));
        await cache.ClearAsync();
    }

    /// <summary>
    /// Redis 回源在工厂失败和取消后释放租约。
    /// </summary>
    [RedisIntegrationFact]
    public async Task RedisRetriever_ReleasesLeaseAfterFactoryErrorAndCancellation()
    {
        var configuration = new CacheConfiguration("easy-recovery-" + Guid.NewGuid().ToString("N"), "easy-redis");
        using var container = BuildRedis(configuration);
        var cache = container.GetRequiredService<IRedisCache>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync<int>("entry",
            _ => throw new InvalidOperationException("factory failed")));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.GetAsync("entry",
            _ => Task.FromResult(1), cancellationToken: cancelled.Token));
        Assert.Equal(7, await cache.GetAsync("entry", _ => Task.FromResult(7)));
        await cache.ClearAsync();
    }

    /// <summary>
    /// Hybrid 原子提交后使本地值失效。
    /// </summary>
    [RedisIntegrationFact]
    public async Task HybridRetriever_InvalidatesLocalValueAfterAtomicCommit()
    {
        var configuration = new CacheConfiguration("easy-hybrid-" + Guid.NewGuid().ToString("N"), "easy-hybrid");
        using var container = BuildHybrid(configuration);
        var cache = container.GetRequiredService<IRedisCache>();

        Assert.Equal(42, await cache.GetAsync("entry", _ => Task.FromResult(42)));
        Assert.Equal(42, cache.Get<int>("entry"));
        await cache.ClearAsync();
    }

    /// <summary>
    /// Hybrid 批量操作发布跨实例失效并最终收敛。
    /// </summary>
    [RedisIntegrationFact]
    public async Task HybridBatchOperations_PublishCrossInstanceInvalidationAndConverge()
    {
        var configuration = new CacheConfiguration("easy-hybrid-batch-bus-" + Guid.NewGuid().ToString("N"),
            "easy-hybrid-batch-bus", batchSize: 2);
        using var firstContainer = BuildHybrid(configuration);
        using var secondContainer = BuildHybrid(configuration);
        var first = firstContainer.GetRequiredService<IRedisCache>();
        var second = secondContainer.GetRequiredService<IRedisCache>();
        var firstFactory = firstContainer.GetRequiredService<IEasyCachingProviderFactory>();
        var secondFactory = secondContainer.GetRequiredService<IEasyCachingProviderFactory>();
        var firstDistributed = firstFactory.GetCachingProvider(configuration.ProviderName + ":distributed");
        var secondDistributed = secondFactory.GetCachingProvider(configuration.ProviderName + ":distributed");
        var firstLocal = firstFactory.GetCachingProvider(configuration.ProviderName + ":local");
        var secondLocal = secondFactory.GetCachingProvider(configuration.ProviderName + ":local");
        var prefix = "hybrid-cross-instance:";
        var keys = new[] { prefix + "one", prefix + "two", prefix + "three" };
        var physicalKeys = keys.Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key)).ToArray();
        var initial = new Dictionary<string, int>
        {
            [keys[0]] = 101,
            [keys[1]] = 202,
            [keys[2]] = 303
        };
        var updated = new Dictionary<string, int>
        {
            [keys[0]] = 1101,
            [keys[1]] = 1202,
            [keys[2]] = 1303
        };
        var initialValues = new[] { 101, 202, 303 };
        var updatedValues = new[] { 1101, 1202, 1303 };

        try
        {
            await firstDistributed.RemoveAllAsync(physicalKeys, CancellationToken.None);
            await secondDistributed.RemoveAllAsync(physicalKeys, CancellationToken.None);
            await firstLocal.RemoveAllAsync(physicalKeys, CancellationToken.None);
            await secondLocal.RemoveAllAsync(physicalKeys, CancellationToken.None);

            await first.SetAllAsync(initial);
            Assert.Equal(initialValues.Select(value => (int?)value),
                await ReadDistributedAsync(firstDistributed, physicalKeys));
            Assert.Equal(initialValues, await first.GetAllAsync<int>(keys));
            Assert.Equal(initialValues, await second.GetAllAsync<int>(keys));
            AssertLocalValues(firstLocal, physicalKeys, initialValues.Select(value => (int?)value));
            AssertLocalValues(secondLocal, physicalKeys, initialValues.Select(value => (int?)value));

            await second.SetAllAsync(updated);
            Assert.Equal(updatedValues.Select(value => (int?)value),
                await ReadDistributedAsync(firstDistributed, physicalKeys));
            Assert.Equal(updatedValues.Select(value => (int?)value),
                await ReadDistributedAsync(secondDistributed, physicalKeys));
            await WaitForConditionAsync(
                () => Task.FromResult(physicalKeys.All(key => !firstLocal.Get<int>(key).HasValue)),
                "第一实例未收到批量覆盖的失效通知。");
            Assert.Equal(updatedValues, await second.GetAllAsync<int>(keys));
            AssertLocalValues(secondLocal, physicalKeys, updatedValues.Select(value => (int?)value));
            Assert.Equal(updatedValues, await first.GetAllAsync<int>(keys));
            AssertLocalValues(firstLocal, physicalKeys, updatedValues.Select(value => (int?)value));

            await first.RemoveAllAsync(keys);
            Assert.Equal(new[] { false, false, false },
                await ReadDistributedPresenceAsync(firstDistributed, physicalKeys));
            Assert.Equal(new[] { false, false, false },
                await ReadDistributedPresenceAsync(secondDistributed, physicalKeys));
            await WaitForConditionAsync(
                () => Task.FromResult(physicalKeys.All(key => !firstLocal.Get<int>(key).HasValue &&
                    !secondLocal.Get<int>(key).HasValue)),
                "批量删除后两个实例的本地层未完成失效。");
            Assert.Equal(new[] { 0, 0, 0 }, await second.GetAllAsync<int>(keys));
            AssertLocalValues(firstLocal, physicalKeys, new int?[3]);
            AssertLocalValues(secondLocal, physicalKeys, new int?[3]);
        }
        finally
        {
            await firstDistributed.RemoveAllAsync(physicalKeys, CancellationToken.None);
            await secondDistributed.RemoveAllAsync(physicalKeys, CancellationToken.None);
            await firstLocal.RemoveAllAsync(physicalKeys, CancellationToken.None);
            await secondLocal.RemoveAllAsync(physicalKeys, CancellationToken.None);
        }
    }

    /// <summary>
    /// Hybrid 回源向其他实例发布本地失效通知。
    /// </summary>
    [RedisIntegrationFact]
    public async Task HybridRetriever_PublishesInvalidationToOtherInstance()
    {
        var configuration = new CacheConfiguration("easy-hybrid-bus-" + Guid.NewGuid().ToString("N"), "easy-hybrid");
        using var firstContainer = BuildHybrid(configuration);
        using var secondContainer = BuildHybrid(configuration);
        var first = firstContainer.GetRequiredService<IRedisCache>();
        var second = secondContainer.GetRequiredService<IRedisCache>();
        var key = CacheKeyFormat.GetPhysicalKey(configuration, "entry");
        var database = (IDatabase)firstContainer.GetRequiredService<IEasyCachingProviderFactory>()
            .GetCachingProvider(configuration.ProviderName + ":distributed").Database;
        first.Set("entry", 1);
        Assert.Equal(1, first.Get<int>("entry"));
        await database.KeyDeleteAsync(key);

        Assert.Equal(2, await second.GetAsync("entry", _ => Task.FromResult(2)));
        await Task.Delay(100);
        Assert.Equal(2, first.Get<int>("entry"));
        await first.ClearAsync();
    }

    /// <summary>
    /// Hybrid 总线失败报告已提交值，并清除本地值。
    /// </summary>
    [RedisIntegrationFact]
    public async Task HybridRetriever_BusFailureReportsCommittedValueAndClearsLocal()
    {
        var configuration = new CacheConfiguration("easy-hybrid-fault-" + Guid.NewGuid().ToString("N"), "easy-hybrid");
        using var ownerContainer = BuildHybrid(configuration);
        using var peerContainer = BuildHybrid(configuration);
        var ownerFactory = ownerContainer.GetRequiredService<IEasyCachingProviderFactory>();
        var owner = new HybridCacheManager(ownerFactory,
            ownerContainer.GetRequiredService<IHybridProviderFactory>(), configuration,
            configuration.ProviderName + ":distributed", configuration.ProviderName + ":local",
            new HybridBusFailureTracker(new FailingBus()), configuration.ProviderName + ":invalidations");
        var peer = peerContainer.GetRequiredService<IRedisCache>();
        var local = ownerFactory.GetCachingProvider(configuration.ProviderName + ":local");
        var database = (IDatabase)ownerFactory.GetCachingProvider(configuration.ProviderName + ":distributed").Database;
        var key = CacheKeyFormat.GetPhysicalKey(configuration, "entry");
        try
        {
            peer.Set("entry", 1);
            Assert.Equal(1, peer.Get<int>("entry"));
            await database.KeyDeleteAsync(key);

            await Assert.ThrowsAsync<InvalidOperationException>(() => owner.GetAsync("entry", _ => Task.FromResult(2)));
            Assert.True(await database.KeyExistsAsync(key));
            Assert.False(local.Get<int>(key).HasValue);
            Assert.Equal(1, peer.Get<int>("entry"));
            Assert.Equal(2, await owner.GetAsync("entry", _ => Task.FromResult(3)));

            await database.KeyDeleteAsync(key);
            local.Remove(key);
            Assert.Throws<InvalidOperationException>(() => owner.Get("entry", () => 4));
            Assert.True(database.KeyExists(key));
            Assert.False(local.Get<int>(key).HasValue);
            Assert.Equal(4, owner.Get<int>("entry"));
        }
        finally { await peer.ClearAsync(); }
    }

    /// <summary>
    /// Hybrid 批量回填使用剩余 TTL，并移除缺失的本地值。
    /// </summary>
    [RedisIntegrationFact]
    public async Task HybridBatchRefill_UsesRemainingTtlAndRemovesMissingLocalValue()
    {
        var configuration = new CacheConfiguration("ttl-refill-" + Guid.NewGuid().ToString("N"), "hybrid-ttl");
        using var container = BuildHybrid(configuration);
        var cache = container.GetRequiredService<IRedisCache>();
        var factory = container.GetRequiredService<IEasyCachingProviderFactory>();
        var distributed = factory.GetCachingProvider(configuration.ProviderName + ":distributed");
        var local = factory.GetCachingProvider(configuration.ProviderName + ":local");
        var database = (IDatabase)distributed.Database;
        var physical = CacheKeyFormat.GetPhysicalKey(configuration, "item");
        try
        {
            // 绕过 Hybrid 写入，在分布式层建立短 TTL，批量读取必须只回填剩余时间。
            await database.StringSetAsync(physical, distributed.GetProviderInfo().Serializer.Serialize(42),
                TimeSpan.FromMilliseconds(500));
            Assert.Equal(new[] { 42 }, cache.GetAll<int>(new[] { "item" }));
            Assert.True(local.Get<int>(physical).HasValue);
            Assert.InRange(local.GetExpiration(physical).TotalMilliseconds, 1, 500);
            await Task.Delay(650);
            Assert.False(local.Get<int>(physical).HasValue);
            Assert.Equal(0, await cache.GetAsync<int>("item"));

            await database.StringSetAsync(physical, distributed.GetProviderInfo().Serializer.Serialize(43),
                TimeSpan.FromMilliseconds(1500));
            await Task.Delay(250);
            await foreach (var entry in cache.GetByPrefixStreamAsync<int>("item"))
                Assert.Equal(43, entry.Value);
            Assert.InRange(local.GetExpiration(physical).TotalMilliseconds, 1, 1250);
            await database.KeyDeleteAsync(physical);
            await cache.GetAllAsync<int>(new[] { "item" });
            Assert.False(local.Get<int>(physical).HasValue);
        }
        finally { await database.KeyDeleteAsync(physical); local.Remove(physical); }
    }

    /// <summary>
    /// Redis 和 Hybrid 显式变更拒绝旧回源提交覆盖新值。
    /// </summary>
    [RedisIntegrationFact]
    public async Task ExplicitMutations_RejectOlderFactoryCommitForRedisAndHybrid()
    {
        var configuration = new CacheConfiguration("race-easy-" + Guid.NewGuid().ToString("N"), "race-easy");
        using var redis = BuildRedis(configuration);
        using var hybrid = BuildHybrid(configuration);
        await CacheCoordinationAssertions.VerifyMutationsWinAsync(redis.GetRequiredService<IRedisCache>());
        await CacheCoordinationAssertions.VerifyMutationsWinAsync(hybrid.GetRequiredService<IRedisCache>());
    }

    /// <summary>
    /// 工厂超时后释放键门控并拒绝迟到提交。
    /// </summary>
    [RedisIntegrationFact]
    public async Task FactoryTimeout_ReleasesGateAndRejectsLateCommit()
    {
        var configuration = new CacheConfiguration("timeout-easy-" + Guid.NewGuid().ToString("N"), "timeout-easy",
            coordination: new CacheCoordinationOptions(retrievalTimeout: TimeSpan.FromMilliseconds(100)));
        using var redis = BuildRedis(configuration);
        using var hybrid = BuildHybrid(configuration);
        await CacheCoordinationAssertions.VerifyTimeoutAsync(redis.GetRequiredService<IRedisCache>());
        await CacheCoordinationAssertions.VerifyTimeoutAsync(hybrid.GetRequiredService<IRedisCache>());
    }

    /// <summary>
    /// Redis 和 Hybrid 流保留首批结果，并传播第二批解码失败。
    /// </summary>
    [RedisIntegrationFact]
    public async Task RedisAndHybridStreams_PreserveFirstBatchAndPropagateSecondBatchDecodeFailure()
    {
        var configuration = new CacheConfiguration("decode-fault-" + Guid.NewGuid().ToString("N"), "decode-fault", batchSize: 2);
        using var redis = BuildRedis(configuration);
        using var hybrid = BuildHybrid(configuration);
        foreach (var container in new[] { redis, hybrid })
        {
            var cache = container.GetRequiredService<IRedisCache>();
            var name = container == redis ? configuration.ProviderName : configuration.ProviderName + ":distributed";
            var database = (IDatabase)container.GetRequiredService<IEasyCachingProviderFactory>().GetCachingProvider(name).Database;
            var keys = new[] { "one", "two", "broken", "four" };
            try
            {
                await cache.SetAllAsync(new Dictionary<string, int> { [keys[0]] = 1, [keys[1]] = 2, [keys[3]] = 4 });
                await database.StringSetAsync(CacheKeyFormat.GetPhysicalKey(configuration, "broken"), "{invalid-json");
                var yielded = new List<int>();
                var error = await Record.ExceptionAsync(async () =>
                {
                    await foreach (var entry in cache.GetAllStreamAsync<int>(keys)) yielded.Add(entry.Value);
                });
                Assert.IsType<System.Text.Json.JsonException>(error);
                Assert.Equal(new[] { 1, 2 }, yielded);
                await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => cache.GetAllAsync<int>(keys));
            }
            finally { await cache.RemoveAllAsync(keys); }
        }
    }

    /// <summary>
    /// 创建配置了 Redis 分布式层和 Hybrid 本地层的服务容器。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <returns>返回已注册 Hybrid Provider 的服务容器。</returns>
    private static ServiceProvider BuildHybrid(CacheConfiguration configuration)
    {
        var endpoint = Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS").Split(':');
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCaching(configuration, configuration.ProviderName + ":distributed",
            redis => redis.DBConfig.Endpoints.Add(new ServerEndPoint(endpoint[0], int.Parse(endpoint[1]))),
            bus => bus.ConfigurationOptions = new ConfigurationOptions
            {
                EndPoints = { { endpoint[0], int.Parse(endpoint[1]) } }
            });
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Provider 序列化器与 Redis 原子写入使用的载荷格式兼容。
    /// </summary>
    [RedisIntegrationFact]
    public async Task ProviderSerializer_IsCompatibleWithAtomicRedisWrite()
    {
        var endpoint = Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS").Split(':');
        var configuration = new CacheConfiguration("easy-wire-" + Guid.NewGuid().ToString("N"), "easy-wire");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRedisCaching(configuration, options =>
            options.DBConfig.Endpoints.Add(new ServerEndPoint(endpoint[0], int.Parse(endpoint[1]))));
        using var container = services.BuildServiceProvider();
        var factory = container.GetRequiredService<IEasyCachingProviderFactory>();
        var provider = factory.GetCachingProvider(configuration.ProviderName);
        var database = Assert.IsAssignableFrom<IDatabase>(provider.Database);
        var key = CacheKeyFormat.GetPhysicalKey(configuration, "item");
        try
        {
            var payload = provider.GetProviderInfo().Serializer.Serialize(42);
            await database.StringSetAsync(key, payload, TimeSpan.FromSeconds(30));
            Assert.Equal(42, provider.Get<int>(key).Value);
        }
        finally { await database.KeyDeleteAsync(key); }
    }

    /// <summary>
    /// 创建配置了 Redis Provider 的服务容器。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <returns>返回已注册 Redis Provider 的服务容器。</returns>
    private static ServiceProvider BuildRedis(CacheConfiguration configuration)
    {
        var endpoint = Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS").Split(':');
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRedisCaching(configuration, options =>
            options.DBConfig.Endpoints.Add(new ServerEndPoint(endpoint[0], int.Parse(endpoint[1]))));
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 按物理键读取分布式层中的可空值。
    /// </summary>
    /// <param name="provider">缓存 Provider。</param>
    /// <param name="physicalKeys">物理缓存键集合。</param>
    /// <returns>按输入顺序返回分布式层值的异步任务。</returns>
    private static async Task<IReadOnlyList<int?>> ReadDistributedAsync(IEasyCachingProvider provider,
        IReadOnlyList<string> physicalKeys)
    {
        var values = await provider.GetAllAsync<int>(physicalKeys, CancellationToken.None);
        return physicalKeys.Select(key => values.TryGetValue(key, out var value) && value.HasValue
            ? (int?)value.Value
            : null).ToArray();
    }

    /// <summary>
    /// 按物理键读取分布式层中的命中状态。
    /// </summary>
    /// <param name="provider">缓存 Provider。</param>
    /// <param name="physicalKeys">物理缓存键集合。</param>
    /// <returns>按输入顺序返回缓存命中状态的异步任务。</returns>
    private static async Task<IReadOnlyList<bool>> ReadDistributedPresenceAsync(IEasyCachingProvider provider,
        IReadOnlyList<string> physicalKeys)
    {
        var values = await provider.GetAllAsync<int>(physicalKeys, CancellationToken.None);
        return physicalKeys.Select(key => values.TryGetValue(key, out var value) && value.HasValue).ToArray();
    }

    /// <summary>
    /// 断言本地层中的物理键值与预期一致。
    /// </summary>
    /// <param name="provider">缓存 Provider。</param>
    /// <param name="physicalKeys">物理缓存键集合。</param>
    /// <param name="expected">预期值。</param>
    private static void AssertLocalValues(IEasyCachingProvider provider, IReadOnlyList<string> physicalKeys,
        IEnumerable<int?> expected)
    {
        Assert.Equal(expected, physicalKeys.Select(key =>
        {
            var value = provider.Get<int>(key);
            return value.HasValue ? (int?)value.Value : null;
        }));
    }

    /// <summary>
    /// 等待异步条件满足或超时。
    /// </summary>
    /// <param name="condition">等待条件。</param>
    /// <param name="failureMessage">条件未满足时抛出的失败提示。</param>
    private static async Task WaitForConditionAsync(Func<Task<bool>> condition, string failureMessage)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!await condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException(failureMessage);
            await Task.Delay(50);
        }
    }
}
