using Bing.Caching;
using EasyCaching.Core;
using EasyCaching.Core.Serialization;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Text;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 验证 CacheManager 的注册、批量读写、扫描和取消语义。
/// </summary>
public class CacheManagerTests
{
    /// <summary>
    /// 批量和 Hybrid 测试使用的缓存配置。
    /// </summary>
    private readonly CacheConfiguration _configuration = new("app-a", "hybrid", batchSize: 2);

    /// <summary>
    /// 客户端分片拓扑无效时在 Provider 注册前失败。
    /// </summary>
    [Fact]
    public void ClientShardedTopology_FailsBeforeProviderRegistration()
    {
        var configuration = new CacheConfiguration("app", "easy", topology: CacheTopology.ClientSharded,
            expectedShardCount: 2);
        var services = new ServiceCollection();

        Assert.Throws<NotSupportedException>(() => services.AddRedisCaching(configuration, _ => { }));
        Assert.Throws<NotSupportedException>(() => services.AddHybridCaching(configuration, "distributed",
            _ => { }, _ => { }));
        Assert.Empty(services);
        Assert.Throws<NotSupportedException>(() =>
            new CacheManager(new Mock<IEasyCachingProvider>().Object, configuration));
    }

    /// <summary>
    /// 缺少分片节点时在注册服务前拒绝配置。
    /// </summary>
    [Fact]
    public void ShardedRegistration_RejectsMissingNodeBeforeRegisteringServices()
    {
        var configuration = new CacheConfiguration("app", "easy-sharded",
            topology: CacheTopology.ClientSharded, expectedShardCount: 2);
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddShardedRedisCaching(configuration,
            new Action<global::EasyCaching.Redis.RedisOptions>[] { _ => { } }));
        Assert.Empty(services);
    }

    /// <summary>
    /// Hybrid 注册同时暴露死信管理和诊断服务。
    /// </summary>
    [Fact]
    public void HybridRegistration_ExposesDeadLetterAdminAlongsideDiagnostics()
    {
        var configuration = new CacheConfiguration("app", "easy-hybrid");
        var services = new ServiceCollection();

        services.AddHybridCaching(configuration, "distributed", _ => { }, _ => { });

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IHybridInvalidationDeliveryDiagnostics));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IHybridInvalidationDeadLetterAdmin));
    }

    /// <summary>
    /// 客户端分片 Hybrid 为每个主题注册一个死信管理器。
    /// </summary>
    [Fact]
    public void ShardedHybridRegistration_ExposesOneDeadLetterAdminPerTopic()
    {
        var configuration = new CacheConfiguration("app", "easy-sharded-hybrid",
            topology: CacheTopology.ClientSharded, expectedShardCount: 2);
        var services = new ServiceCollection();

        services.AddShardedHybridCaching(configuration,
            new Action<global::EasyCaching.Redis.RedisOptions>[] { _ => { }, _ => { } },
            _ => { });

        Assert.Equal(2, services.Count(descriptor =>
            descriptor.ServiceType == typeof(IHybridInvalidationDeadLetterAdmin)));
    }

    /// <summary>
    /// Hybrid 批量写入使用命名空间物理键并调用 Hybrid Provider。
    /// </summary>
    [Fact]
    public void Hybrid_SetAll_UsesHybridProviderWithNamespacedKeys()
    {
        var distributed = new Mock<IEasyCachingProvider>();
        var hybrid = new Mock<IHybridCachingProvider>();
        var manager = new CacheManager(distributed.Object, _configuration, hybrid.Object);

        manager.SetAll(new Dictionary<string, int> { ["one"] = 1, ["two"] = 2 });

        hybrid.Verify(provider => provider.SetAll(It.Is<IDictionary<string, int>>(items =>
            items.ContainsKey(CacheKeyFormat.GetPhysicalKey(_configuration, "one")) &&
            items.ContainsKey(CacheKeyFormat.GetPhysicalKey(_configuration, "two"))),
            TimeSpan.FromHours(8)), Times.Once);
        distributed.Verify(provider => provider.SetAll(It.IsAny<IDictionary<string, int>>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    /// <summary>
    /// Redis 原始批量响应解码保留顺序、缺失值和命中状态。
    /// </summary>
    [Fact]
    public async Task RawRedisBatchDecode_PreservesOrderAndMissingValues()
    {
        var provider = new Mock<IEasyCachingProvider>();
        var database = new Mock<IDatabase>();
        var serializer = new Mock<IEasyCachingSerializer>();
        serializer.Setup(value => value.Deserialize<int>(It.IsAny<byte[]>()))
            .Returns<byte[]>(payload => int.Parse(Encoding.UTF8.GetString(payload)));
        provider.SetupGet(value => value.Database).Returns(database.Object);
        provider.Setup(value => value.GetProviderInfo())
            .Returns(new ProviderInfo { Serializer = serializer.Object });
        database.Setup(value => value.StringGetAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey[] keys, CommandFlags _) => Task.FromResult(new[]
            {
                (RedisValue)"1",
                RedisValue.Null,
                (RedisValue)"3"
            }));

        var configuration = new CacheConfiguration("raw-batch", "redis", batchSize: 3);
        var manager = new CacheManager(provider.Object, configuration);
        var list = await manager.GetAllAsync<int>(new[] { "one", "missing", "three" });
        Assert.Equal(new[] { 1, 0, 3 }, list);

        var entries = new List<CacheEntry<int>>();

        await foreach (var entry in manager.GetAllStreamAsync<int>(new[] { "one", "missing", "three" }))
            entries.Add(entry);

        Assert.Equal(new[] { "one", "missing", "three" }, entries.Select(entry => entry.Key));
        Assert.Equal(new[] { 1, 0, 3 }, entries.Select(entry => entry.Value));
        Assert.Equal(new[] { true, false, true }, entries.Select(entry => entry.Hit));
        serializer.Verify(value => value.Deserialize<int>(It.IsAny<byte[]>()), Times.Exactly(4));
    }

    /// <summary>
    /// 清理只删除当前命名空间，且不会执行 FLUSH。
    /// </summary>
    [Fact]
    public void Clear_RemovesOnlyCurrentNamespaceAndNeverFlushes()
    {
        var own = CacheKeyFormat.GetPhysicalKey(_configuration, "one");
        var other = CacheKeyFormat.GetPhysicalKey(new CacheConfiguration("app-b", "other"), "one");
        var distributed = new Mock<IEasyCachingProvider>();
        var hybrid = new Mock<IHybridCachingProvider>();
        distributed.Setup(provider => provider.GetAllKeysByPrefix(It.IsAny<string>()))
            .Returns(new[] { own, other });
        var manager = new CacheManager(distributed.Object, _configuration, hybrid.Object);

        manager.Clear();

        hybrid.Verify(provider => provider.RemoveAll(It.Is<IEnumerable<string>>(keys =>
            keys.SequenceEqual(new[] { own }))), Times.Once);
        distributed.Verify(provider => provider.Flush(), Times.Never);
    }

    /// <summary>
    /// 前缀删除将双星号视为字面文本。
    /// </summary>
    [Fact]
    public void RemoveByPrefix_TreatsDoubleStarAsLiteralText()
    {
        var match = CacheKeyFormat.GetPhysicalKey(_configuration, "**task");
        var other = CacheKeyFormat.GetPhysicalKey(_configuration, "ordinary");
        var distributed = new Mock<IEasyCachingProvider>();
        var hybrid = new Mock<IHybridCachingProvider>();
        distributed.Setup(provider => provider.GetAllKeysByPrefix(It.IsAny<string>()))
            .Returns(new[] { match, other });
        var manager = new CacheManager(distributed.Object, _configuration, hybrid.Object);

        manager.RemoveByPrefix("**");

        hybrid.Verify(provider => provider.RemoveAll(It.Is<IEnumerable<string>>(keys =>
            keys.SequenceEqual(new[] { match }))), Times.Once);
    }

    /// <summary>
    /// 模式删除将转义通配符匹配为字面键。
    /// </summary>
    [Fact]
    public void RemoveByPattern_EscapedWildcardMatchesLiteralKeyOnly()
    {
        var match = CacheKeyFormat.GetPhysicalKey(_configuration, "report*");
        var other = CacheKeyFormat.GetPhysicalKey(_configuration, "report1");
        var distributed = new Mock<IEasyCachingProvider>();
        var hybrid = new Mock<IHybridCachingProvider>();
        distributed.Setup(provider => provider.GetAllKeysByPrefix(It.IsAny<string>()))
            .Returns(new[] { match, other });
        var manager = new CacheManager(distributed.Object, _configuration, hybrid.Object);

        manager.RemoveByPattern("report\\*");

        hybrid.Verify(provider => provider.Remove(match), Times.Once);
        distributed.Verify(provider => provider.GetAllKeysByPrefix(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// 模式删除与共享通配符和转义规则一致。
    /// </summary>
    [Fact]
    public async Task RemoveByPattern_UsesSharedWildcardAndEscapeSemantics()
    {
        var logicalKeys = new[] { "item-", "item-1", "item-12", "literal*", "literal?", @"path\" };
        var physicalKeys = logicalKeys.Select(key => CacheKeyFormat.GetPhysicalKey(_configuration, key)).ToArray();
        var distributed = new Mock<IEasyCachingProvider>();
        var hybrid = new Mock<IHybridCachingProvider>();
        distributed.Setup(provider => provider.GetAllKeysByPrefix(It.IsAny<string>()))
            .Returns(physicalKeys);
        distributed.Setup(provider => provider.GetAllKeysByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(physicalKeys);
        var manager = new CacheManager(distributed.Object, _configuration, hybrid.Object);

        manager.RemoveByPattern("item-?");
        manager.RemoveByPattern(@"literal\*");
        await manager.RemoveByPatternAsync(@"path\\");

        hybrid.Verify(provider => provider.RemoveAll(It.Is<IEnumerable<string>>(keys =>
            keys.SequenceEqual(new[] { physicalKeys[1] }))), Times.Once);
        hybrid.Verify(provider => provider.Remove(physicalKeys[3]), Times.Once);
        hybrid.Verify(provider => provider.RemoveAsync(physicalKeys[5], It.IsAny<CancellationToken>()), Times.Once);
        distributed.Verify(provider => provider.GetAllKeysByPrefix(It.IsAny<string>()), Times.Once);
        distributed.Verify(provider => provider.GetAllKeysByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Throws<ArgumentException>(() => manager.RemoveByPattern("**"));
    }

    /// <summary>
    /// CacheManager 各入口拒绝过长前缀和模式。
    /// </summary>
    [Fact]
    public async Task InputLimits_RejectOverlongPrefixAndPatternAcrossCacheManagerEntrypoints()
    {
        var configuration = new CacheConfiguration("input-limits", "easy")
            .WithInputLimits(new CacheInputLimits(maxPrefixLength: 2, maxPatternLength: 2));
        var distributed = new Mock<IEasyCachingProvider>();
        var manager = new CacheManager(distributed.Object, configuration);

        Assert.Equal("prefix", Assert.Throws<ArgumentOutOfRangeException>(() =>
            manager.GetByPrefix<int>("abc")).ParamName);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.GetByPrefixAsync<int>("abc"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await foreach (var _ in manager.GetByPrefixStreamAsync<int>("abc"))
            {
            }
        });

        Assert.Equal("prefix", Assert.Throws<ArgumentOutOfRangeException>(() =>
            manager.RemoveByPrefix("abc")).ParamName);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.RemoveByPrefixAsync("abc"));

        Assert.Equal("pattern", Assert.Throws<ArgumentOutOfRangeException>(() =>
            manager.RemoveByPattern("abc")).ParamName);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.RemoveByPatternAsync("abc"));
    }

    /// <summary>
    /// Hybrid 批量读取保持顺序和重复键。
    /// </summary>
    [Fact]
    public void GetAll_PreservesOrderAndDuplicateKeysThroughHybrid()
    {
        var one = CacheKeyFormat.GetPhysicalKey(_configuration, "one");
        var two = CacheKeyFormat.GetPhysicalKey(_configuration, "two");
        var distributed = new Mock<IEasyCachingProvider>();
        var hybrid = new Mock<IHybridCachingProvider>();
        hybrid.Setup(provider => provider.Get<string>(one)).Returns(new CacheValue<string>("first", true));
        hybrid.Setup(provider => provider.Get<string>(two)).Returns(new CacheValue<string>("second", true));
        var manager = new CacheManager(distributed.Object, _configuration, hybrid.Object);

        Assert.Equal(new[] { "second", "first", "second" }, manager.GetAll<string>(new[] { "two", "one", "two" }));
        distributed.Verify(provider => provider.Get<string>(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// 批量流读取保持顺序、重复键、缺失值和 null 值。
    /// </summary>
    [Fact]
    public async Task GetAllStream_PreservesOrderDuplicatesMissingAndNullValues()
    {
        var configuration = new CacheConfiguration("stream", "redis", batchSize: 2);
        var one = CacheKeyFormat.GetPhysicalKey(configuration, "one");
        var nullValue = CacheKeyFormat.GetPhysicalKey(configuration, "null");
        var distributed = new Mock<IEasyCachingProvider>();
        distributed.Setup(provider => provider.GetAllAsync<string>(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
            {
                var values = keys
                    .Where(key => key == one || key == nullValue)
                    .ToDictionary(key => key, key => key == one
                        ? new CacheValue<string>("first", true)
                        : new CacheValue<string>(null, true));
                return Task.FromResult<IDictionary<string, CacheValue<string>>>(values);
            });
        var manager = new CacheManager(distributed.Object, configuration);

        var actual = new List<CacheEntry<string>>();
        await foreach (var entry in manager.GetAllStreamAsync<string>(new[] { "one", "missing", "null", "one" }))
            actual.Add(entry);

        Assert.Equal(new[] { "one", "missing", "null", "one" }, actual.Select(entry => entry.Key));
        Assert.Equal(new[] { "first", null, null, "first" }, actual.Select(entry => entry.Value));
        Assert.Equal(new[] { true, false, true, true }, actual.Select(entry => entry.Hit));
        distributed.Verify(provider => provider.GetAllAsync<string>(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>
    /// 批量流读取首批取消后不派发下一批。
    /// </summary>
    [Fact]
    public async Task GetAllStream_CancellationAfterFirstBatch_DoesNotDispatchNextRead()
    {
        var configuration = new CacheConfiguration("stream-cancel-after-batch", "redis", batchSize: 2);
        var first = CacheKeyFormat.GetPhysicalKey(configuration, "one");
        var second = CacheKeyFormat.GetPhysicalKey(configuration, "two");
        var distributed = new Mock<IEasyCachingProvider>();
        var batches = new List<string[]>();
        distributed.Setup(provider => provider.GetAllAsync<int>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
            {
                var batch = keys.ToArray();
                batches.Add(batch);
                return Task.FromResult<IDictionary<string, CacheValue<int>>>(batch.ToDictionary(
                    key => key,
                    key => new CacheValue<int>(key == first ? 11 : key == second ? 22 : 0, true)));
            });
        var manager = new CacheManager(distributed.Object, configuration);
        using var cancellation = new CancellationTokenSource();
        await using var enumerator = manager.GetAllStreamAsync<int>(
                new[] { "one", "two", "three", "four" }, cancellation.Token)
            .GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("one", enumerator.Current.Key);
        Assert.Equal(11, enumerator.Current.Value);
        Assert.True(enumerator.Current.Hit);
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("two", enumerator.Current.Key);
        Assert.Equal(22, enumerator.Current.Value);
        Assert.True(enumerator.Current.Hit);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => enumerator.MoveNextAsync().AsTask());

        Assert.Single(batches);
        Assert.Equal(new[] { first, second }, batches[0]);
        distributed.Verify(provider => provider.GetAllAsync<int>(
            It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 列表批量读取与流式读取在重复键、缺失值和 null 值上保持一致。
    /// </summary>
    [Fact]
    public async Task GetAllAsync_ListMatchesStream_ForDuplicatesMissingAndNullValues()
    {
        var configuration = new CacheConfiguration("stream-list-consistency", "redis", batchSize: 2);
        var one = CacheKeyFormat.GetPhysicalKey(configuration, "one");
        var nullValue = CacheKeyFormat.GetPhysicalKey(configuration, "null");
        var distributed = new Mock<IEasyCachingProvider>();
        distributed.Setup(provider => provider.GetAllAsync<string>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
            {
                var values = keys
                    .Where(key => key == one || key == nullValue)
                    .ToDictionary(key => key, key => key == one
                        ? new CacheValue<string>("first", true)
                        : new CacheValue<string>(null, true));
                return Task.FromResult<IDictionary<string, CacheValue<string>>>(values);
            });
        var manager = new CacheManager(distributed.Object, configuration);
        var requested = new[] { "one", "missing", "null", "one" };

        var list = await manager.GetAllAsync<string>(requested);
        var stream = new List<CacheEntry<string>>();
        await foreach (var entry in manager.GetAllStreamAsync<string>(requested))
            stream.Add(entry);

        Assert.Equal(list, stream.Select(entry => entry.Value));
        Assert.Equal(requested, stream.Select(entry => entry.Key));
        Assert.Equal(new[] { true, false, true, true }, stream.Select(entry => entry.Hit));
        distributed.Verify(provider => provider.GetAllAsync<string>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    /// <summary>
    /// 前缀流读取按批次返回逻辑键。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_UsesBatchesAndReturnsLogicalKeys()
    {
        var configuration = new CacheConfiguration("stream-prefix", "redis", batchSize: 2);
        var first = CacheKeyFormat.GetPhysicalKey(configuration, "item:1");
        var second = CacheKeyFormat.GetPhysicalKey(configuration, "item:2");
        var ignored = CacheKeyFormat.GetPhysicalKey(configuration, "other");
        var distributed = new Mock<IEasyCachingProvider>();
        distributed.Setup(provider => provider.GetAllKeysByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, second, ignored });
        distributed.Setup(provider => provider.GetAllAsync<int>(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
                Task.FromResult<IDictionary<string, CacheValue<int>>>(keys.ToDictionary(
                    key => key,
                    key => key == first ? new CacheValue<int>(1, true) : new CacheValue<int>(2, true))));
        var manager = new CacheManager(distributed.Object, configuration);

        var actual = new List<CacheEntry<int>>();
        await foreach (var entry in manager.GetByPrefixStreamAsync<int>("item:"))
            actual.Add(entry);

        Assert.Equal(new[] { "item:1", "item:2" }, actual.Select(entry => entry.Key));
        Assert.Equal(new[] { 1, 2 }, actual.Select(entry => entry.Value));
        Assert.All(actual, entry => Assert.True(entry.Hit));
        distributed.Verify(provider => provider.GetAllAsync<int>(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 异步前缀读取直接使用物理前缀批量路径，并只返回匹配键的命中值。
    /// </summary>
    [Fact]
    public async Task GetByPrefixAsync_UsesPhysicalPrefixBatchPath()
    {
        var configuration = new CacheConfiguration("prefix-list", "memory", batchSize: 2);
        var first = CacheKeyFormat.GetPhysicalKey(configuration, "item:1");
        var second = CacheKeyFormat.GetPhysicalKey(configuration, "item:2");
        var ignored = CacheKeyFormat.GetPhysicalKey(configuration, "other");
        var distributed = new Mock<IEasyCachingProvider>();
        distributed.Setup(provider => provider.GetAllKeysByPrefixAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, second, ignored });
        distributed.Setup(provider => provider.GetAllAsync<int>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
                Task.FromResult<IDictionary<string, CacheValue<int>>>(keys.ToDictionary(
                    key => key,
                    key => key == first ? new CacheValue<int>(1, true) : new CacheValue<int>(2, true))));
        var manager = new CacheManager(distributed.Object, configuration);

        var values = await manager.GetByPrefixAsync<int>("item:");

        Assert.Equal(new[] { 1, 2 }, values);
        distributed.Verify(provider => provider.GetAllAsync<int>(
            It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 异步前缀读取保留 null 命中值并忽略缺失值。
    /// </summary>
    [Fact]
    public async Task GetByPrefixAsync_PreservesNullHitAndIgnoresMissingValue()
    {
        var configuration = new CacheConfiguration("prefix-list-null", "memory", batchSize: 2);
        var hit = CacheKeyFormat.GetPhysicalKey(configuration, "item:hit");
        var missing = CacheKeyFormat.GetPhysicalKey(configuration, "item:missing");
        var distributed = new Mock<IEasyCachingProvider>();
        distributed.Setup(provider => provider.GetAllKeysByPrefixAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { hit, missing });
        distributed.Setup(provider => provider.GetAllAsync<string>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
                Task.FromResult<IDictionary<string, CacheValue<string>>>(
                    new Dictionary<string, CacheValue<string>>
                    {
                        [hit] = new CacheValue<string>(null, true)
                    }));
        var manager = new CacheManager(distributed.Object, configuration);

        var values = await manager.GetByPrefixAsync<string>("item:");

        Assert.Single(values);
        Assert.Null(values[0]);
    }

    /// <summary>
    /// 前缀流读取区分 null 命中和缺失值。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_PreservesNullHitAndReportsMissingValue()
    {
        var configuration = new CacheConfiguration("prefix-stream-null", "memory", batchSize: 2);
        var hit = CacheKeyFormat.GetPhysicalKey(configuration, "item:hit");
        var missing = CacheKeyFormat.GetPhysicalKey(configuration, "item:missing");
        var distributed = new Mock<IEasyCachingProvider>();
        distributed.Setup(provider => provider.GetAllKeysByPrefixAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { hit, missing });
        distributed.Setup(provider => provider.GetAllAsync<string>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
                Task.FromResult<IDictionary<string, CacheValue<string>>>(
                    new Dictionary<string, CacheValue<string>>
                    {
                        [hit] = new CacheValue<string>(null, true)
                    }));
        var manager = new CacheManager(distributed.Object, configuration);

        var entries = new List<CacheEntry<string>>();
        await foreach (var entry in manager.GetByPrefixStreamAsync<string>("item:"))
            entries.Add(entry);

        Assert.Equal(new[] { "item:hit", "item:missing" }, entries.Select(entry => entry.Key));
        Assert.Equal(new[] { true, false }, entries.Select(entry => entry.Hit));
        Assert.Null(entries[0].Value);
        Assert.Null(entries[1].Value);
    }

    /// <summary>
    /// 前缀流回退路径报告 null 命中和缺失值。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_FallbackReportsNullHitAndMissingValue()
    {
        var configuration = new CacheConfiguration("prefix-stream-fallback", "memory", batchSize: 3);
        var hit = CacheKeyFormat.GetPhysicalKey(configuration, "item:hit");
        var nullHit = CacheKeyFormat.GetPhysicalKey(configuration, "item:null");
        var missing = CacheKeyFormat.GetPhysicalKey(configuration, "item:missing");
        var distributed = new Mock<IEasyCachingProvider>();
        distributed.Setup(provider => provider.GetAllKeysByPrefixAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { hit, nullHit, missing });
        distributed.Setup(provider => provider.GetAllAsync<string>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, CacheValue<string>>)null);
        distributed.Setup(provider => provider.GetAsync<string>(hit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CacheValue<string>("value", true));
        distributed.Setup(provider => provider.GetAsync<string>(nullHit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CacheValue<string>(null, true));
        distributed.Setup(provider => provider.GetAsync<string>(missing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CacheValue<string>(null, false));
        var manager = new CacheManager(distributed.Object, configuration);

        var entries = new List<CacheEntry<string>>();
        await foreach (var entry in manager.GetByPrefixStreamAsync<string>("item:"))
            entries.Add(entry);

        Assert.Equal(new[] { "item:hit", "item:null", "item:missing" }, entries.Select(entry => entry.Key));
        Assert.Equal(new[] { true, true, false }, entries.Select(entry => entry.Hit));
        Assert.Equal("value", entries[0].Value);
        Assert.Null(entries[1].Value);
        Assert.Null(entries[2].Value);
        distributed.Verify(provider => provider.GetAllAsync<string>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        distributed.Verify(provider => provider.GetAsync<string>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    /// <summary>
    /// 异步前缀读取首批取消后不派发下一批。
    /// </summary>
    [Fact]
    public async Task GetByPrefixAsync_CancellationAfterFirstBatch_DoesNotDispatchNextRead()
    {
        var configuration = new CacheConfiguration("prefix-list-cancel", "memory", batchSize: 1);
        var physicalKeys = new[] { "item:1", "item:2" }
            .Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key))
            .ToArray();
        var distributed = new Mock<IEasyCachingProvider>();
        distributed.Setup(provider => provider.GetAllAsync<int>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
                Task.FromResult<IDictionary<string, CacheValue<int>>>(keys.ToDictionary(
                    key => key, _ => new CacheValue<int>(1, true))));
        using var cancellation = new CancellationTokenSource();
        var enumerated = 0;

        async IAsyncEnumerable<string> KeyStream(string _,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            await Task.Yield();
            enumerated++;
            yield return physicalKeys[0];
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            enumerated++;
            yield return physicalKeys[1];
        }

        var manager = new CacheManager(distributed.Object, configuration, KeyStream);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => manager.GetByPrefixAsync<int>("item:", cancellation.Token));
        Assert.Equal(1, enumerated);
        distributed.Verify(provider => provider.GetAllAsync<int>(
            It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 前缀流读取使用注入的键流并遵守取消。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_UsesInjectedKeyStreamAndHonorsCancellation()
    {
        var configuration = new CacheConfiguration("memory-stream", "memory", batchSize: 1);
        var physicalKeys = new[] { "item:1", "item:2" }
            .Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key))
            .ToArray();
        var distributed = new Mock<IEasyCachingProvider>();
        distributed.Setup(provider => provider.GetAllAsync<int>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
                Task.FromResult<IDictionary<string, CacheValue<int>>>(keys.ToDictionary(
                    key => key, _ => new CacheValue<int>(1, true))));
        var enumerated = 0;

        async IAsyncEnumerable<string> KeyStream(string _,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            foreach (var key in physicalKeys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                enumerated++;
                yield return key;
            }
        }

        var manager = new CacheManager(distributed.Object, configuration, KeyStream);
        using var cancellation = new CancellationTokenSource();
        await using var enumerator = manager.GetByPrefixStreamAsync<int>("item:", cancellation.Token)
            .GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("item:1", enumerator.Current.Key);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => enumerator.MoveNextAsync().AsTask());
        Assert.Equal(1, enumerated);
        distributed.Verify(provider => provider.GetAllKeysByPrefixAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 前缀流读取首批取消后不派发下一次读取。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_CancellationAfterFirstBatch_DoesNotDispatchNextRead()
    {
        var configuration = new CacheConfiguration("prefix-stream-cancel", "memory", batchSize: 2);
        var physicalKeys = new[] { "item:1", "item:2", "item:3", "item:4" }
            .Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key))
            .ToArray();
        var distributed = new Mock<IEasyCachingProvider>();
        var batches = new List<string[]>();
        distributed.Setup(provider => provider.GetAllAsync<int>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
            {
                var batch = keys.ToArray();
                batches.Add(batch);
                return Task.FromResult<IDictionary<string, CacheValue<int>>>(batch.ToDictionary(
                    key => key, _ => new CacheValue<int>(1, true)));
            });

        var enumerated = 0;
        async IAsyncEnumerable<string> KeyStream(string _,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            foreach (var key in physicalKeys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                enumerated++;
                yield return key;
            }
        }

        var manager = new CacheManager(distributed.Object, configuration, KeyStream);
        using var cancellation = new CancellationTokenSource();
        await using var enumerator = manager.GetByPrefixStreamAsync<int>("item:", cancellation.Token)
            .GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(2, enumerated);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => enumerator.MoveNextAsync().AsTask());
        Assert.Single(batches);
        Assert.Equal(2, batches[0].Length);
        Assert.Equal(2, enumerated);
    }

    /// <summary>
    /// 前缀流优先使用 Provider 键流能力，再回退到旧路径。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_UsesProviderKeyStreamCapabilityBeforeLegacyFallback()
    {
        var configuration = new CacheConfiguration("provider-stream", "memory", batchSize: 1);
        var physicalKeys = new[] { "item:1", "item:2" }
            .Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key))
            .ToArray();
        var distributed = new Mock<IEasyCachingProvider>();
        distributed.Setup(provider => provider.GetAllAsync<int>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
                Task.FromResult<IDictionary<string, CacheValue<int>>>(keys.ToDictionary(
                    key => key, _ => new CacheValue<int>(1, true))));
        distributed.As<ICacheKeyStreamProvider>()
            .Setup(provider => provider.GetAllKeysByPrefixStreamAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken cancellationToken) => KeyStream(cancellationToken));

        async IAsyncEnumerable<string> KeyStream(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            foreach (var key in physicalKeys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return key;
            }
        }

        var manager = new CacheManager(distributed.Object, configuration);
        var actual = new List<CacheEntry<int>>();
        await foreach (var entry in manager.GetByPrefixStreamAsync<int>("item:"))
            actual.Add(entry);

        Assert.Equal(new[] { "item:1", "item:2" }, actual.Select(entry => entry.Key));
        distributed.As<ICacheKeyStreamProvider>().Verify(provider =>
            provider.GetAllKeysByPrefixStreamAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        distributed.Verify(provider => provider.GetAllKeysByPrefixAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 前缀流优先使用 Provider 分页键流能力，再回退到旧路径。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_UsesProviderKeyPageStreamCapabilityBeforeLegacyFallback()
    {
        var configuration = new CacheConfiguration("provider-page-stream", "memory", batchSize: 2);
        var logicalKeys = new[] { "item:1", "item:2" };
        var physicalKeys = logicalKeys
            .Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key))
            .ToArray();
        var distributed = new Mock<IEasyCachingProvider>();
        distributed.Setup(provider => provider.GetAllAsync<int>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
                Task.FromResult<IDictionary<string, CacheValue<int>>>(keys.ToDictionary(
                    key => key, key => new CacheValue<int>(Array.IndexOf(physicalKeys, key) + 1, true))));
        distributed.As<ICacheKeyPageStreamProvider>()
            .Setup(provider => provider.GetAllKeyPagesByPrefixStreamAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken cancellationToken) => KeyPages(cancellationToken));
        distributed.Setup(provider => provider.GetAllKeysByPrefixAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("不应回退到完整键集合。"));

        async IAsyncEnumerable<IReadOnlyList<string>> KeyPages(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new[] { physicalKeys[0] };
            yield return Array.Empty<string>();
            yield return new[] { physicalKeys[1] };
        }

        var manager = new CacheManager(distributed.Object, configuration);
        var actual = new List<CacheEntry<int>>();
        await foreach (var entry in manager.GetByPrefixStreamAsync<int>("item:"))
            actual.Add(entry);

        Assert.Equal(logicalKeys, actual.Select(entry => entry.Key));
        Assert.Equal(new[] { 1, 2 }, actual.Select(entry => entry.Value));
        Assert.All(actual, entry => Assert.True(entry.Hit));
        distributed.As<ICacheKeyPageStreamProvider>().Verify(provider =>
            provider.GetAllKeyPagesByPrefixStreamAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        distributed.Verify(provider => provider.GetAllKeysByPrefixAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 分页 Provider 的前缀流读取遵守扫描页预算。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_PageProvider_EnforcesScanPageBudget()
    {
        var configuration = new CacheConfiguration("provider-page-budget", "memory", batchSize: 1,
            resources: new CacheResourceOptions(maxScanPages: 1));
        var physicalKeys = new[] { "item:1", "item:2" }
            .Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key))
            .ToArray();
        var distributed = new Mock<IEasyCachingProvider>();
        distributed.Setup(provider => provider.GetAllAsync<int>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
                Task.FromResult<IDictionary<string, CacheValue<int>>>(keys.ToDictionary(
                    key => key, _ => new CacheValue<int>(1, true))));
        distributed.As<ICacheKeyPageStreamProvider>()
            .Setup(provider => provider.GetAllKeyPagesByPrefixStreamAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken cancellationToken) => KeyPages(cancellationToken));

        async IAsyncEnumerable<IReadOnlyList<string>> KeyPages(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new[] { physicalKeys[0] };
            yield return new[] { physicalKeys[1] };
        }

        var manager = new CacheManager(distributed.Object, configuration);
        await using var enumerator = manager.GetByPrefixStreamAsync<int>("item:").GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        var error = await Assert.ThrowsAsync<CacheScanLimitExceededException>(
            () => enumerator.MoveNextAsync().AsTask());

        Assert.Equal(CacheScanLimitKind.ScanPages, error.Kind);
        Assert.Equal(1, error.Limit);
        Assert.Equal(1, error.Consumed);
        distributed.Verify(provider => provider.GetAllAsync<int>(
            It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 分页 Provider 的前缀流读取在取消后停止。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_PageProvider_StopsOnCancellation()
    {
        var configuration = new CacheConfiguration("provider-page-cancel", "memory", batchSize: 1);
        var physicalKeys = new[] { "item:1", "item:2" }
            .Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key))
            .ToArray();
        var requestedPages = 0;
        var distributed = new Mock<IEasyCachingProvider>();
        distributed.Setup(provider => provider.GetAllAsync<int>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
                Task.FromResult<IDictionary<string, CacheValue<int>>>(keys.ToDictionary(
                    key => key, _ => new CacheValue<int>(1, true))));
        distributed.As<ICacheKeyPageStreamProvider>()
            .Setup(provider => provider.GetAllKeyPagesByPrefixStreamAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken cancellationToken) => KeyPages(cancellationToken));

        async IAsyncEnumerable<IReadOnlyList<string>> KeyPages(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            requestedPages++;
            yield return new[] { physicalKeys[0] };
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            requestedPages++;
            yield return new[] { physicalKeys[1] };
        }

        var manager = new CacheManager(distributed.Object, configuration);
        using var cancellation = new CancellationTokenSource();
        await using var enumerator = manager.GetByPrefixStreamAsync<int>("item:", cancellation.Token)
            .GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => enumerator.MoveNextAsync().AsTask());
        Assert.Equal(1, requestedPages);
    }

    /// <summary>
    /// MemoryCacheManager 的流读取使用同步键枚举。
    /// </summary>
    [Fact]
    public async Task MemoryCacheManager_UsesSynchronousKeyEnumerationForStream()
    {
        var configuration = new CacheConfiguration("memory-adapter", "memory", batchSize: 1);
        var physicalKey = CacheKeyFormat.GetPhysicalKey(configuration, "item:1");
        var provider = new Mock<IEasyCachingProvider>();
        provider.SetupGet(value => value.Database).Returns((IDatabase)null);
        provider.Setup(value => value.GetAllKeysByPrefix(It.IsAny<string>()))
            .Returns(new[] { physicalKey });
        provider.Setup(value => value.GetAllAsync<int>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
                Task.FromResult<IDictionary<string, CacheValue<int>>>(keys.ToDictionary(
                    key => key, _ => new CacheValue<int>(1, true))));
        provider.Setup(value => value.RemoveAllAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var factory = new Mock<IEasyCachingProviderFactory>();
        factory.Setup(value => value.GetCachingProvider(configuration.ProviderName))
            .Returns(provider.Object);
        var manager = new MemoryCacheManager(factory.Object, configuration);

        var entries = new List<CacheEntry<int>>();
        await foreach (var entry in manager.GetByPrefixStreamAsync<int>("item:"))
            entries.Add(entry);

        Assert.Single(entries);
        Assert.Equal("item:1", entries[0].Key);
        await manager.RemoveByPrefixAsync("item:");

        provider.Verify(value => value.GetAllKeysByPrefix(It.IsAny<string>()), Times.Exactly(2));
        provider.Verify(value => value.GetAllKeysByPrefixAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 前缀流读取遵守扫描键预算。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_EnforcesScanKeyBudget()
    {
        var configuration = new CacheConfiguration("stream-prefix-budget", "redis", batchSize: 2,
            resources: new CacheResourceOptions(maxScanKeys: 2));
        var logicalKeys = new[] { "item:1", "item:2", "item:3" };
        var physicalKeys = logicalKeys
            .Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key))
            .ToArray();
        var distributed = new Mock<IEasyCachingProvider>();
        distributed.Setup(provider => provider.GetAllKeysByPrefixAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(physicalKeys);
        distributed.Setup(provider => provider.GetAllAsync<int>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
                Task.FromResult<IDictionary<string, CacheValue<int>>>(keys.ToDictionary(
                    key => key, _ => new CacheValue<int>(1, true))));
        var manager = new CacheManager(distributed.Object, configuration);
        await using var enumerator = manager.GetByPrefixStreamAsync<int>("item:").GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.True(await enumerator.MoveNextAsync());
        var error = await Assert.ThrowsAsync<CacheScanLimitExceededException>(
            () => enumerator.MoveNextAsync().AsTask());

        Assert.Contains("MaxScanKeys", error.Message, StringComparison.Ordinal);
        Assert.Equal(2, error.Limit);
        Assert.Equal(2, error.Consumed);
        distributed.Verify(provider => provider.GetAllAsync<int>(
            It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 特殊键保留当前命名空间和逻辑文本。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_SpecialKeysStayInNamespaceAndPreserveLogicalText()
    {
        var configuration = new CacheConfiguration("stream-special", "redis", batchSize: 2);
        var similarConfiguration = new CacheConfiguration("stream-special-similar", "redis");
        var logicalKeys = new[] { "订单:{a}*?\\中文:one", "订单:{b}*?\\中文:two" };
        var physicalKeys = logicalKeys
            .Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key))
            .ToArray();
        var sentinel = CacheKeyFormat.GetPhysicalKey(similarConfiguration, "订单:{a}*?\\中文:sentinel");
        var requestedBatches = new List<string[]>();
        var distributed = new Mock<IEasyCachingProvider>();
        distributed.Setup(provider => provider.GetAllKeysByPrefixAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(physicalKeys.Concat(new[] { sentinel }).ToArray());
        distributed.Setup(provider => provider.GetAllAsync<int>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
            {
                var batch = keys.ToArray();
                requestedBatches.Add(batch);
                return Task.FromResult<IDictionary<string, CacheValue<int>>>(batch.ToDictionary(
                    key => key,
                    key => new CacheValue<int>(Array.IndexOf(physicalKeys, key) + 1, true)));
            });
        var manager = new CacheManager(distributed.Object, configuration);

        var actual = new List<CacheEntry<int>>();
        await foreach (var entry in manager.GetByPrefixStreamAsync<int>("订单:"))
            actual.Add(entry);

        Assert.Equal(logicalKeys, actual.Select(entry => entry.Key));
        Assert.Equal(new[] { 1, 2 }, actual.Select(entry => entry.Value));
        Assert.All(actual, entry => Assert.True(entry.Hit));
        Assert.Single(requestedBatches);
        Assert.Equal(physicalKeys, requestedBatches[0]);
        Assert.DoesNotContain(sentinel, requestedBatches[0]);
        Assert.DoesNotContain(actual, entry => entry.Key.Contains("sentinel", StringComparison.Ordinal));
    }

    /// <summary>
    /// 前缀流读取首批取消后不派发下一批。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_CancellationAfterFirstBatch_DoesNotDispatchNextBatch()
    {
        var configuration = new CacheConfiguration("stream-prefix-cancel", "redis", batchSize: 2);
        var logicalKeys = new[] { "item:1", "item:2", "item:3", "item:4" };
        var physicalKeys = logicalKeys
            .Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key))
            .ToArray();
        var distributed = new Mock<IEasyCachingProvider>();
        var batches = new List<string[]>();
        distributed.Setup(provider => provider.GetAllKeysByPrefixAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(physicalKeys);
        distributed.Setup(provider => provider.GetAllAsync<int>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
            {
                var batch = keys.ToArray();
                batches.Add(batch);
                return Task.FromResult<IDictionary<string, CacheValue<int>>>(batch.ToDictionary(
                    key => key,
                    _ => new CacheValue<int>(1, true)));
            });
        var manager = new CacheManager(distributed.Object, configuration);
        using var cancellation = new CancellationTokenSource();
        await using var enumerator = manager.GetByPrefixStreamAsync<int>("item:", cancellation.Token)
            .GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("item:1", enumerator.Current.Key);
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("item:2", enumerator.Current.Key);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enumerator.MoveNextAsync().AsTask());
        Assert.Single(batches);
        Assert.Equal(physicalKeys.Take(2), batches[0]);
        distributed.Verify(provider => provider.GetAllAsync<int>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 前缀流读取首批完成后传播第二批失败。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_PropagatesSecondBatchFailureAfterFirstBatch()
    {
        var configuration = new CacheConfiguration("stream-prefix-failure", "redis", batchSize: 2);
        var logicalKeys = new[] { "item:1", "item:2", "item:3" };
        var physicalKeys = logicalKeys
            .Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key))
            .ToArray();
        var distributed = new Mock<IEasyCachingProvider>();
        var failure = new InvalidOperationException("second batch failed");
        var batchCalls = 0;
        distributed.Setup(provider => provider.GetAllKeysByPrefixAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(physicalKeys);
        distributed.Setup(provider => provider.GetAllAsync<int>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
            {
                batchCalls++;
                if (batchCalls == 2)
                    return Task.FromException<IDictionary<string, CacheValue<int>>>(failure);

                var batch = keys.ToArray();
                return Task.FromResult<IDictionary<string, CacheValue<int>>>(batch.ToDictionary(
                    key => key,
                    key => new CacheValue<int>(Array.IndexOf(physicalKeys, key) + 1, true)));
            });
        var manager = new CacheManager(distributed.Object, configuration);
        await using var enumerator = manager.GetByPrefixStreamAsync<int>("item:").GetAsyncEnumerator();
        var actual = new List<CacheEntry<int>>();

        Assert.True(await enumerator.MoveNextAsync());
        actual.Add(enumerator.Current);
        Assert.True(await enumerator.MoveNextAsync());
        actual.Add(enumerator.Current);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => enumerator.MoveNextAsync().AsTask());

        Assert.Same(failure, exception);
        Assert.Equal(new[] { "item:1", "item:2" }, actual.Select(entry => entry.Key));
        Assert.Equal(new[] { 1, 2 }, actual.Select(entry => entry.Value));
        Assert.All(actual, entry => Assert.True(entry.Hit));
        Assert.Equal(2, batchCalls);
        distributed.Verify(provider => provider.GetAllAsync<int>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>
    /// 批量流读取仅在消费到对应批次时枚举输入键。
    /// </summary>
    [Fact]
    public async Task GetAllStream_CacheKeys_EnumeratesInputOnlyAsBatchesAreConsumed()
    {
        var configuration = new CacheConfiguration("stream-cache-keys", "redis", batchSize: 2);
        var distributed = new Mock<IEasyCachingProvider>();
        distributed.Setup(provider => provider.GetAllAsync<int>(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
                Task.FromResult<IDictionary<string, CacheValue<int>>>(keys.ToDictionary(
                    key => key, _ => new CacheValue<int>(1, true))));
        var manager = new CacheManager(distributed.Object, configuration);
        var enumerated = 0;

        IEnumerable<CacheKey> Keys()
        {
            foreach (var key in new[] { "one", "two", "three", "four" })
            {
                enumerated++;
                yield return new CacheKey(key);
            }
        }

        var stream = manager.GetAllStreamAsync<int>(Keys());
        Assert.Equal(0, enumerated);

        await using var enumerator = stream.GetAsyncEnumerator();
        Assert.Equal(0, enumerated);
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(2, enumerated);
        Assert.Equal("one", enumerator.Current.Key);
        distributed.Verify(provider => provider.GetAllAsync<int>(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// 批量流读取首批取消后不派发下一批。
    /// </summary>
    [Fact]
    public async Task GetAllStream_CacheKeys_CancellationAfterFirstBatch_DoesNotDispatchNextBatch()
    {
        var configuration = new CacheConfiguration("stream-cache-cancel", "redis", batchSize: 2);
        var distributed = new Mock<IEasyCachingProvider>();
        var batches = new List<string[]>();
        distributed.Setup(provider => provider.GetAllAsync<int>(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
            {
                batches.Add(keys.ToArray());
                return Task.FromResult<IDictionary<string, CacheValue<int>>>(keys.ToDictionary(
                    key => key, _ => new CacheValue<int>(1, true)));
            });
        var manager = new CacheManager(distributed.Object, configuration);
        using var cancellation = new CancellationTokenSource();
        var keys = new[] { new CacheKey("one"), new CacheKey("two"), new CacheKey("three"), new CacheKey("four") };
        await using var enumerator = manager.GetAllStreamAsync<int>(keys, cancellation.Token).GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.True(await enumerator.MoveNextAsync());
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enumerator.MoveNextAsync().AsTask());
        Assert.Single(batches);
        Assert.Equal(2, batches[0].Length);
        distributed.Verify(provider => provider.GetAllAsync<int>(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// 批量流读取传播第二批失败。
    /// </summary>
    [Fact]
    public async Task GetAllStream_PropagatesSecondBatchFailureAfterFirstBatch()
    {
        var configuration = new CacheConfiguration("stream-failure", "redis", batchSize: 2);
        var distributed = new Mock<IEasyCachingProvider>();
        var failure = new InvalidOperationException("second batch failed");
        var calls = 0;
        distributed.Setup(provider => provider.GetAllAsync<int>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
            {
                calls++;
                if (calls == 2)
                    return Task.FromException<IDictionary<string, CacheValue<int>>>(failure);
                var batch = keys.ToArray();
                return Task.FromResult<IDictionary<string, CacheValue<int>>>(batch.ToDictionary(
                    key => key, key => new CacheValue<int>(Array.IndexOf(batch, key) + 1, true)));
            });
        var manager = new CacheManager(distributed.Object, configuration);
        await using var enumerator = manager.GetAllStreamAsync<int>(
            new[] { "item:1", "item:2", "item:3", "item:4" }).GetAsyncEnumerator();
        var actual = new List<CacheEntry<int>>();

        Assert.True(await enumerator.MoveNextAsync());
        actual.Add(enumerator.Current);
        Assert.True(await enumerator.MoveNextAsync());
        actual.Add(enumerator.Current);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => enumerator.MoveNextAsync().AsTask());

        Assert.Same(failure, exception);
        Assert.Equal(new[] { "item:1", "item:2" }, actual.Select(entry => entry.Key));
        Assert.Equal(new[] { 1, 2 }, actual.Select(entry => entry.Value));
        Assert.All(actual, entry => Assert.True(entry.Hit));
        Assert.Equal(2, calls);
    }

    /// <summary>
    /// 批量流读取在取消后停止派发。
    /// </summary>
    [Fact]
    public async Task GetAllStream_StopsBeforeDispatchWhenCancelled()
    {
        var configuration = new CacheConfiguration("stream-cancel", "redis", batchSize: 2);
        var distributed = new Mock<IEasyCachingProvider>();
        using var source = new CancellationTokenSource();
        source.Cancel();
        var manager = new CacheManager(distributed.Object, configuration);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in manager.GetAllStreamAsync<int>(new[] { "one", "two" }, source.Token))
            {
            }
        });
        distributed.Verify(provider => provider.GetAllAsync<int>(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Cluster 批量读取在派发期间取消后停止后续读取，并等待已发出的任务。
    /// </summary>
    [Fact]
    public async Task ClusterBatch_CancellationDuringDispatchStopsFurtherReadsAndObservesIssuedTask()
    {
        var configuration = new CacheConfiguration("cluster-cancel", "redis", batchSize: 2,
            maxConcurrency: 2, topology: CacheTopology.RedisCluster);
        var first = CacheKeyFormat.GetPhysicalKey(configuration, "one");
        var second = CacheKeyFormat.GetPhysicalKey(configuration, "two");
        Assert.NotEqual(CacheClusterTopology.GetHashSlot(first), CacheClusterTopology.GetHashSlot(second));
        var provider = new Mock<IEasyCachingProvider>();
        var database = new Mock<IDatabase>();
        provider.Setup(cache => cache.Database).Returns(database.Object);
        using var cancellation = new CancellationTokenSource();
        var issued = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<RedisValue[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        database.Setup(cache => cache.StringGetAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .Returns(() =>
            {
                cancellation.Cancel();
                issued.SetResult(true);
                return pending.Task;
            });
        var manager = new CacheManager(provider.Object, configuration);
        await using var enumerator = manager.GetAllStreamAsync<int>(new[] { "one", "two" }, cancellation.Token)
            .GetAsyncEnumerator();

        var read = enumerator.MoveNextAsync().AsTask();
        await issued.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
        pending.SetException(new IOException("late command fault"));
        database.Verify(cache => cache.StringGetAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()),
            Times.Once);
    }

    /// <summary>
    /// Cluster 批量写入在派发期间取消后等待已发出的写入。
    /// </summary>
    [Fact]
    public async Task ClusterBatch_CancellationDuringDispatchWaitsForIssuedWrite()
    {
        var configuration = new CacheConfiguration("cluster-cancel-write", "redis", batchSize: 2,
            maxConcurrency: 2, topology: CacheTopology.RedisCluster);
        var first = CacheKeyFormat.GetPhysicalKey(configuration, "one");
        var second = CacheKeyFormat.GetPhysicalKey(configuration, "two");
        Assert.NotEqual(CacheClusterTopology.GetHashSlot(first), CacheClusterTopology.GetHashSlot(second));
        var provider = new Mock<IEasyCachingProvider>();
        provider.Setup(cache => cache.Database).Returns(new Mock<IDatabase>().Object);
        using var cancellation = new CancellationTokenSource();
        var issued = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.Setup(cache => cache.SetAllAsync(
                It.IsAny<IDictionary<string, int>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns((IDictionary<string, int> _, TimeSpan _, CancellationToken _) =>
            {
                cancellation.Cancel();
                issued.SetResult(true);
                return pending.Task;
            });
        var manager = new CacheManager(provider.Object, configuration);

        var write = manager.SetAllAsync(new Dictionary<string, int> { ["one"] = 1, ["two"] = 2 },
            cancellationToken: cancellation.Token);
        await issued.Task;
        Assert.False(write.IsCompleted);
        pending.SetResult(true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => write);
        provider.Verify(cache => cache.SetAllAsync(
                It.IsAny<IDictionary<string, int>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Cluster 批量删除在派发期间取消后等待已发出的删除。
    /// </summary>
    [Fact]
    public async Task ClusterBatch_CancellationDuringDispatchWaitsForIssuedRemove()
    {
        var configuration = new CacheConfiguration("cluster-cancel-remove", "redis", batchSize: 2,
            maxConcurrency: 2, topology: CacheTopology.RedisCluster);
        var first = CacheKeyFormat.GetPhysicalKey(configuration, "one");
        var second = CacheKeyFormat.GetPhysicalKey(configuration, "two");
        Assert.NotEqual(CacheClusterTopology.GetHashSlot(first), CacheClusterTopology.GetHashSlot(second));
        var provider = new Mock<IEasyCachingProvider>();
        provider.Setup(cache => cache.Database).Returns(new Mock<IDatabase>().Object);
        using var cancellation = new CancellationTokenSource();
        var issued = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.Setup(cache => cache.RemoveAllAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> _, CancellationToken _) =>
            {
                cancellation.Cancel();
                issued.SetResult(true);
                return pending.Task;
            });
        var manager = new CacheManager(provider.Object, configuration);

        var remove = manager.RemoveAllAsync(new[] { "one", "two" }, cancellation.Token);
        await issued.Task;
        Assert.False(remove.IsCompleted);
        pending.SetResult(true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => remove);
        provider.Verify(cache => cache.RemoveAllAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Memory Provider 注册支持命名 Provider 解析和读写往返。
    /// </summary>
    [Fact]
    public void MemoryRegistration_ResolvesNamedProviderAndRoundTrips()
    {
        var configuration = new CacheConfiguration("app-memory", "local-a");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCaching(configuration);
        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<ILocalCache>();

        cache.Set("item", 42);

        Assert.Equal(42, cache.Get<int>("item"));
        Assert.Equal(42, provider.GetRequiredService<ICache>().Get<int>("item"));
    }

    /// <summary>
    /// Memory 清理保留同一 Provider 下其他命名空间。
    /// </summary>
    [Fact]
    public void MemoryClear_PreservesOtherNamespaceOnSameProvider()
    {
        var firstConfig = new CacheConfiguration("tenant-one", "shared-memory");
        var secondConfig = new CacheConfiguration("tenant-two", "shared-memory");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCaching(firstConfig);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IEasyCachingProviderFactory>();
        var first = new MemoryCacheManager(factory, firstConfig);
        var second = new MemoryCacheManager(factory, secondConfig);
        first.Set("entry", 1);
        second.Set("entry", 2);

        first.Clear();

        Assert.False(first.Exists("entry"));
        Assert.Equal(2, second.Get<int>("entry"));
    }

    /// <summary>
    /// 多次 Memory 注册将默认缓存绑定到首个 Provider。
    /// </summary>
    [Fact]
    public void MultipleMemoryRegistrations_KeepDefaultCacheBoundToFirstProvider()
    {
        var firstConfig = new CacheConfiguration("first", "memory-first");
        var secondConfig = new CacheConfiguration("second", "memory-second");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCaching(firstConfig);
        services.AddMemoryCaching(secondConfig);
        using var provider = services.BuildServiceProvider();
        var defaultCache = provider.GetRequiredService<ICache>();
        var lastLocalCache = provider.GetRequiredService<ILocalCache>();
        defaultCache.Set("entry", 1);
        lastLocalCache.Set("entry", 2);

        Assert.Equal(1, defaultCache.Get<int>("entry"));
        Assert.Equal(2, lastLocalCache.Get<int>("entry"));
    }

    /// <summary>
    /// 异步回源收到与调用方关联的生命周期取消令牌。
    /// </summary>
    [Fact]
    public async Task AsyncRetriever_ReceivesLifetimeTokenLinkedToCallerCancellation()
    {
        var configuration = new CacheConfiguration("app-memory-async", "local-async");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCaching(configuration);
        using var provider = services.BuildServiceProvider();
        var cache = (MemoryCacheManager)provider.GetRequiredService<ILocalCache>();
        using var source = new CancellationTokenSource();
        var observed = CancellationToken.None;
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var result = cache.GetAsync<int>("missing", async token =>
        {
            observed = token;
            entered.SetResult(true);
            await Task.Delay(Timeout.Infinite, token);
            return 7;
        }, cancellationToken: source.Token);

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(observed.CanBeCanceled);
        source.Cancel();
        var error = await Assert.ThrowsAsync<OperationCanceledException>(() => result);
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.True(observed.IsCancellationRequested);
    }

    /// <summary>
    /// 多个管理器实例共享回源合并。
    /// </summary>
    [Fact]
    public async Task MemoryRetriever_CoalescesAcrossManagerInstances()
    {
        var configuration = new CacheConfiguration("app-memory-coalesce-" + Guid.NewGuid().ToString("N"), "local-coalesce");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCaching(configuration);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IEasyCachingProviderFactory>();
        var left = new MemoryCacheManager(factory, configuration);
        var right = new MemoryCacheManager(factory, configuration);
        var calls = 0;
        async Task<int> Retrieve(CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(100, token);
            return 42;
        }

        var results = await Task.WhenAll(left.GetAsync("item", Retrieve), right.GetAsync("item", Retrieve));

        Assert.Equal(new[] { 42, 42 }, results);
        Assert.Equal(1, calls);
        left.Clear();
    }

    /// <summary>
    /// 批量删除按配置批大小拆分命令。
    /// </summary>
    [Fact]
    public void RemoveAll_SplitsCommandsAtConfiguredBatchSize()
    {
        var distributed = new Mock<IEasyCachingProvider>();
        var hybrid = new Mock<IHybridCachingProvider>();
        var manager = new CacheManager(distributed.Object, _configuration, hybrid.Object);

        manager.RemoveAll(new[] { "one", "two", "three" });

        hybrid.Verify(provider => provider.RemoveAll(It.Is<IEnumerable<string>>(keys => keys.Count() == 2)), Times.Once);
        hybrid.Verify(provider => provider.RemoveAll(It.Is<IEnumerable<string>>(keys => keys.Count() == 1)), Times.Once);
        distributed.Verify(provider => provider.RemoveAll(It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    /// <summary>
    /// 批量删除按 UTF-8 命令载荷预算拆分物理批次。
    /// </summary>
    [Fact]
    public void RemoveAll_SplitsPhysicalBatchesByUtf8CommandBudget()
    {
        var physicalKeys = new List<string[]>();
        var distributed = new Mock<IEasyCachingProvider>();
        var hybrid = new Mock<IHybridCachingProvider>();
        hybrid.Setup(provider => provider.RemoveAll(It.IsAny<IEnumerable<string>>()))
            .Callback<IEnumerable<string>>(keys => physicalKeys.Add(keys.ToArray()));

        var probeConfiguration = new CacheConfiguration("delete-budget", "hybrid");
        var probeKeys = new[] { "a", "订单" }
            .Select(key => CacheKeyFormat.GetPhysicalKey(probeConfiguration, key))
            .ToArray();
        static long PairBytes(string key) => Encoding.UTF8.GetByteCount(key) * 2L +
                                             Encoding.UTF8.GetByteCount(":lease");
        var commandBudget = PairBytes(probeKeys[0]) + PairBytes(probeKeys[1]) - 1;
        var configuration = new CacheConfiguration("delete-budget", "hybrid", batchSize: 10,
            resources: new CacheResourceOptions(maxBatchCommandBytes: commandBudget));
        var manager = new CacheManager(distributed.Object, configuration, hybrid.Object);

        manager.RemoveAll(new[] { "a", "订单" });

        Assert.Equal(2, physicalKeys.Count);
        Assert.All(physicalKeys, batch => Assert.Single(batch));
        Assert.Equal(new[]
        {
            CacheKeyFormat.GetPhysicalKey(configuration, "a"),
            CacheKeyFormat.GetPhysicalKey(configuration, "订单")
        }, physicalKeys.SelectMany(batch => batch));
    }

    /// <summary>
    /// Redis 注册拒绝隐藏键前缀。
    /// </summary>
    [Fact]
    public void RedisRegistration_RejectsHiddenKeyPrefix()
    {
        var services = new ServiceCollection();
        var configuration = new CacheConfiguration("app-redis", "redis-a");

        services.AddLogging();
        services.AddRedisCaching(configuration, options => options.DBConfig.KeyPrefix = "hidden:");
        using var provider = services.BuildServiceProvider();

        Assert.Throws<ArgumentException>(() => provider.GetRequiredService<IRedisCache>());
    }
}
