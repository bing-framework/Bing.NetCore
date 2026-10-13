using System.Collections;
using Bing.Test.Shared;
using FreeRedis;
using Newtonsoft.Json;

namespace Bing.Caching.FreeRedis.Tests;

/// <summary>
/// Redis 大规模批量契约测试专用 Fact 特性。
/// </summary>
public sealed class LargeCacheDatasetFactAttribute : Xunit.FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="LargeCacheDatasetFactAttribute"/> 类型的实例。
    /// </summary>
    public LargeCacheDatasetFactAttribute()
    {
        if (!IntegrationTestGate.IsProviderEnabled() ||
            !string.Equals(Environment.GetEnvironmentVariable("BING_TEST_LARGE_CACHE_DATASET"), "true",
                StringComparison.OrdinalIgnoreCase))
            Skip = "需启用隔离 Redis 集成环境和 BING_TEST_LARGE_CACHE_DATASET=true。";
    }
}

/// <summary>
/// FreeRedis 批量读取契约测试。
/// </summary>
public sealed class BatchContractTest
{
    /// <summary>
    /// 保存批量契约测试使用的缓存实例。
    /// </summary>
    private readonly ICache _cache;
    /// <summary>
    /// 保存批量契约测试使用的 Redis 客户端。
    /// </summary>
    private readonly RedisClient _client;

    /// <summary>
    /// 初始化一个 <see cref="BatchContractTest"/> 类型的实例。
    /// </summary>
    /// <param name="cache">待测试的缓存实例。</param>
    /// <param name="client">Redis 测试客户端。</param>
    public BatchContractTest(ICache cache, RedisClient client)
    {
        _cache = cache;
        _client = client;
    }

    /// <summary>
    /// 批量读取保留输入顺序、重复键和缺失项位置，并且只枚举输入一次。
    /// </summary>
    [Fact]
    public void GetAll_PreservesOrderDuplicatesAndMissingDefault_AndEnumeratesOnce()
    {
        var keys = CreateKeys();
        _cache.Set(keys[0], 11);
        _cache.Set(keys[1], 22);

        try
        {
            var actual = _cache.GetAll<int?>(new SingleUseEnumerable<string>(new[]
            {
                keys[2], keys[1], keys[0], keys[1]
            }));

            Assert.Equal(new int?[] { null, 22, 11, 22 }, actual);
            Assert.Empty(_cache.GetAll<int?>(Array.Empty<string>()));
        }
        finally
        {
            _cache.RemoveAll(keys.Take(2));
        }
    }

    /// <summary>
    /// 批量读取保留输入顺序、重复键和缺失项位置，并且只枚举输入一次。
    /// </summary>
    [Fact]
    public async Task GetAllAsync_PreservesOrderDuplicatesAndMissingDefault_AndEnumeratesOnce()
    {
        var keys = CreateKeys();
        await _cache.SetAsync(keys[0], 11);
        await _cache.SetAsync(keys[1], 22);

        try
        {
            var actual = await _cache.GetAllAsync<int?>(new SingleUseEnumerable<string>(new[]
            {
                keys[2], keys[1], keys[0], keys[1]
            }));

            Assert.Equal(new int?[] { null, 22, 11, 22 }, actual);
            Assert.Empty(await _cache.GetAllAsync<int?>(Array.Empty<string>()));
        }
        finally
        {
            await _cache.RemoveAllAsync(keys.Take(2));
        }
    }

    /// <summary>
    /// 批量读取将已命中的 null 标量值映射为默认值且保留命中状态。
    /// </summary>
    [Fact]
    public async Task GetAllAsync_ScalarBatchNullUsesDefaultWithoutLosingHit()
    {
        var intKey = "scalar-null-int:" + Guid.NewGuid().ToString("N");
        var longKey = "scalar-null-long:" + Guid.NewGuid().ToString("N");
        var decimalKey = "scalar-null-decimal:" + Guid.NewGuid().ToString("N");
        var missingKey = "scalar-null-missing:" + Guid.NewGuid().ToString("N");
        await _cache.SetAsync<int?>(intKey, null);
        await _cache.SetAsync<long?>(longKey, null);
        await _cache.SetAsync<decimal?>(decimalKey, null);

        try
        {
            Assert.Equal(new[] { 0 }, await _cache.GetAllAsync<int>(new[] { intKey }));
            Assert.Equal(new[] { 0L }, await _cache.GetAllAsync<long>(new[] { longKey }));
            Assert.Equal(new[] { 0m }, await _cache.GetAllAsync<decimal>(new[] { decimalKey }));

            var entries = new List<CacheEntry<int>>();
            await foreach (var entry in _cache.GetAllStreamAsync<int>(new[] { intKey, missingKey }))
                entries.Add(entry);
            Assert.True(entries[0].Hit);
            Assert.Equal(0, entries[0].Value);
            Assert.False(entries[1].Hit);
            Assert.Equal(0, entries[1].Value);
        }
        finally
        {
            await _cache.RemoveAllAsync(new[] { intKey, longKey, decimalKey, missingKey });
        }
    }

    /// <summary>
    /// 流式批量读取按输入顺序返回键、值和命中状态。
    /// </summary>
    [Fact]
    public async Task GetAllStream_ReturnsKeyHitAndValue_InInputOrder()
    {
        var keys = CreateKeys();
        await _cache.SetAsync(keys[0], 11);
        await _cache.SetAsync<int?>(keys[1], null);

        try
        {
            var actual = new List<CacheEntry<int?>>();
            await foreach (var entry in _cache.GetAllStreamAsync<int?>(new SingleUseEnumerable<string>(new[]
                           { keys[2], keys[1], keys[0], keys[1] })))
                actual.Add(entry);

            Assert.Equal(new[] { keys[2], keys[1], keys[0], keys[1] }, actual.Select(item => item.Key));
            Assert.False(actual[0].Hit);
            Assert.True(actual[1].Hit);
            Assert.True(actual[2].Hit);
            Assert.True(actual[3].Hit);
            Assert.Equal(new int?[] { null, null, 11, null }, actual.Select(item => item.Value));
        }
        finally
        {
            await _cache.RemoveAllAsync(keys);
        }
    }

    /// <summary>
    /// 流式批量读取使用客户端自定义序列化器逐项解码结果。
    /// </summary>
    [Fact]
    public async Task GetAllStream_CustomSerializerPreservesPerItemProtocol()
    {
        using var client = new RedisClient(Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection"));
        client.Serialize = value => "custom:" + JsonConvert.SerializeObject(value);
        client.Deserialize = (value, type) => JsonConvert.DeserializeObject(value.Substring("custom:".Length), type);
        var configuration = new CacheConfiguration("custom-batch-" + Guid.NewGuid().ToString("N"), "freeredis");
        var cache = new FreeRedisCacheManager(client, configuration);
        var keys = CreateKeys();
        await cache.SetAsync(keys[0], new CustomPayload { Value = 11 });
        await cache.SetAsync(keys[1], new CustomPayload { Value = 22 });

        try
        {
            var entries = new List<CacheEntry<CustomPayload>>();
            await foreach (var entry in cache.GetAllStreamAsync<CustomPayload>(new[] { keys[1], keys[2], keys[0] }))
                entries.Add(entry);

            Assert.Equal(new[] { keys[1], keys[2], keys[0] }, entries.Select(entry => entry.Key));
            Assert.Equal(new int?[] { 22, null, 11 }, entries.Select(entry => entry.Value?.Value));
            Assert.Equal(new[] { true, false, true }, entries.Select(entry => entry.Hit));
        }
        finally
        {
            await cache.RemoveAllAsync(keys.Take(2));
        }
    }

    /// <summary>
    /// 在首次读取前取消时不开始流式批量读取。
    /// </summary>
    [Fact]
    public async Task GetAllStream_HonorsCancellationBeforeDispatch()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var enumerator = _cache.GetAllStreamAsync<int>(new[] { "cancelled:" + Guid.NewGuid().ToString("N") },
            cancellation.Token).GetAsyncEnumerator();
        try
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() => enumerator.MoveNextAsync().AsTask());
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
    }

    /// <summary>
    /// 消费首批条目后取消时不继续枚举后续输入批次。
    /// </summary>
    [Fact]
    public async Task GetAllStream_CancellationDuringConsumption_StopsNextBatch()
    {
        using var cancellation = new CancellationTokenSource();
        var enumerated = 0;
        IEnumerable<string> Keys()
        {
            for (var index = 0; index < 1000; index++)
            {
                enumerated++;
                yield return "stream-cancel:" + index;
            }
        }

        var enumerator = _cache.GetAllStreamAsync<int>(Keys(), cancellation.Token).GetAsyncEnumerator();
        try
        {
            Assert.True(await enumerator.MoveNextAsync());
            Assert.True(await enumerator.MoveNextAsync());
            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => enumerator.MoveNextAsync().AsTask());
            Assert.Equal(500, enumerated);
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
    }

    /// <summary>
    /// 消费首批条目后取消时不派发下一条 MGET 命令。
    /// </summary>
    [Fact]
    public async Task GetAllStream_CancellationAfterFirstBatch_DoesNotDispatchNextMGet()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Redis 集成测试要求设置 ConnectionStrings__RedisConnection。");

        var configuration = new CacheConfiguration(
            "freeredis-stream-command-cancel-" + Guid.NewGuid().ToString("N"),
            "batch",
            batchSize: 2);
        var cache = new FreeRedisCacheManager(_client, configuration);
        var keys = Enumerable.Range(0, 4)
            .Select(index => "stream-command-cancel-item:" + index)
            .ToArray();

        await cache.SetAllAsync(keys.ToDictionary(key => key, _ => 1));
        await using var monitor = await RedisCommandMonitor.ConnectAsync(connection);
        using var cancellation = new CancellationTokenSource();
        var enumerator = cache.GetAllStreamAsync<int>(keys, cancellation.Token).GetAsyncEnumerator();
        var namespaceMarker = "bing:v2:" + Uri.EscapeDataString(configuration.Namespace) + ":";
        var isNamespaceMGet = new Func<string, bool>(line =>
            line.Contains("\"mget\"", StringComparison.OrdinalIgnoreCase) &&
            line.Contains(namespaceMarker, StringComparison.Ordinal));
        try
        {
            // 首个 MoveNextAsync 会成功读取第一批；先确认真实 MGET 已被 Redis 执行。
            Assert.True(await enumerator.MoveNextAsync());
            await monitor.ReadUntilAsync(isNamespaceMGet, TimeSpan.FromSeconds(5));

            Assert.True(await enumerator.MoveNextAsync());
            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => enumerator.MoveNextAsync().AsTask());

            // 用独立命令排空 MONITOR，确保后续 MGET 已有机会被观察到。
            var pingMarker = "stream-cancel-observed-" + Guid.NewGuid().ToString("N");
            await _client.CallAsync(new CommandPacket("PING", pingMarker));
            await monitor.ReadUntilAsync(
                line => line.Contains("\"ping\"", StringComparison.OrdinalIgnoreCase) &&
                        line.Contains(pingMarker, StringComparison.Ordinal),
                TimeSpan.FromSeconds(5));

            Assert.Equal(1, monitor.ObservedLines.Count(isNamespaceMGet));
        }
        finally
        {
            await enumerator.DisposeAsync();
            await cache.RemoveAllAsync(keys);
        }
    }

    /// <summary>
    /// 消费前缀流首批后取消时停止消费后续扫描结果。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_CancellationAfterFirstBatch_StopsFurtherConsumption()
    {
        var prefix = "stream-prefix-cancel:" + Guid.NewGuid().ToString("N") + ":";
        var keys = Enumerable.Range(0, 501)
            .Select(index => prefix + index)
            .ToArray();
        await _cache.SetAllAsync(keys.ToDictionary(key => key, _ => 1));
        using var cancellation = new CancellationTokenSource();
        var enumerator = _cache.GetByPrefixStreamAsync<int>(prefix, cancellation.Token).GetAsyncEnumerator();

        try
        {
            Assert.True(await enumerator.MoveNextAsync());
            Assert.True(await enumerator.MoveNextAsync());
            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => enumerator.MoveNextAsync().AsTask());
        }
        finally
        {
            await enumerator.DisposeAsync();
            await _cache.RemoveAllAsync(keys);
        }
    }

    /// <summary>
    /// 前缀流首批读取后取消时不再派发下一条 MGET 命令。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_CancellationAfterFirstBatch_DoesNotDispatchNextMGet()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Redis 集成测试要求设置 ConnectionStrings__RedisConnection。");

        var configuration = new CacheConfiguration(
            "freeredis-prefix-stream-command-cancel-" + Guid.NewGuid().ToString("N"),
            "batch",
            batchSize: 2);
        var cache = new FreeRedisCacheManager(_client, configuration);
        var prefix = "stream-prefix-command-cancel:" + Guid.NewGuid().ToString("N") + ":";
        var keys = Enumerable.Range(0, 4)
            .Select(index => prefix + index)
            .ToArray();

        await cache.SetAllAsync(keys.ToDictionary(key => key, _ => 1));
        await using var monitor = await RedisCommandMonitor.ConnectAsync(connection);
        using var cancellation = new CancellationTokenSource();
        var enumerator = cache.GetByPrefixStreamAsync<int>(prefix, cancellation.Token).GetAsyncEnumerator();
        var namespaceMarker = "bing:v2:" + Uri.EscapeDataString(configuration.Namespace) + ":";
        var isNamespaceMGet = new Func<string, bool>(line =>
            line.Contains("\"mget\"", StringComparison.OrdinalIgnoreCase) &&
            line.Contains(namespaceMarker, StringComparison.Ordinal));
        try
        {
            Assert.True(await enumerator.MoveNextAsync());
            await monitor.ReadUntilAsync(isNamespaceMGet, TimeSpan.FromSeconds(5));

            Assert.True(await enumerator.MoveNextAsync());
            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => enumerator.MoveNextAsync().AsTask());

            var pingMarker = "stream-prefix-cancel-observed-" + Guid.NewGuid().ToString("N");
            await _client.CallAsync(new CommandPacket("PING", pingMarker));
            await monitor.ReadUntilAsync(
                line => line.Contains("\"ping\"", StringComparison.OrdinalIgnoreCase) &&
                        line.Contains(pingMarker, StringComparison.Ordinal),
                TimeSpan.FromSeconds(5));

            Assert.Equal(1, monitor.ObservedLines.Count(isNamespaceMGet));
        }
        finally
        {
            await enumerator.DisposeAsync();
            await cache.RemoveAllAsync(keys);
        }
    }

    /// <summary>
    /// 一千项输入下流式读取与列表读取保持键、值、命中状态一致。
    /// </summary>
    [Fact]
    public async Task GetAllStream_LargeInputMatchesListIncludingDuplicatesAndMissingKeys()
    {
        var prefix = "stream-large:" + Guid.NewGuid().ToString("N") + ":";
        var existingKeys = Enumerable.Range(0, 1000)
            .Select(index => prefix + "item:" + index)
            .ToArray();
        var nullKey = prefix + "null";
        var missingKey = prefix + "missing";
        await _cache.SetAllAsync(existingKeys.ToDictionary(key => key, key => Array.IndexOf(existingKeys, key) + 1));
        await _cache.SetAsync<int?>(nullKey, null);

        var input = existingKeys.ToList();
        input.Insert(125, nullKey);
        input.Insert(750, missingKey);
        input.Add(existingKeys[42]);
        input.Add(missingKey);

        try
        {
            var listValues = await _cache.GetAllAsync<int?>(input);
            var streamed = new List<CacheEntry<int?>>();
            await foreach (var entry in _cache.GetAllStreamAsync<int?>(input))
                streamed.Add(entry);

            Assert.Equal(input, streamed.Select(entry => entry.Key));
            Assert.Equal(listValues, streamed.Select(entry => entry.Value));
            Assert.True(streamed.Single(entry => entry.Key == nullKey).Hit);
            Assert.False(streamed.First(entry => entry.Key == missingKey).Hit);
            Assert.False(streamed.Last(entry => entry.Key == missingKey).Hit);
        }
        finally
        {
            await _cache.RemoveAllAsync(existingKeys.Append(nullKey).Append(missingKey));
        }
    }

    /// <summary>
    /// 一万项输入下流式读取与列表读取保持顺序、重复项和缺失状态一致。
    /// </summary>
    [Fact]
    public async Task GetAllStream_10000ItemsMatchesListIncludingDuplicatesMissingOrderAndHits()
    {
        var prefix = "stream-10000:" + Guid.NewGuid().ToString("N") + ":";
        var existingKeys = Enumerable.Range(0, 9997)
            .Select(index => prefix + "item:" + index)
            .ToArray();
        var nullKey = prefix + "null";
        var missingKey = prefix + "missing";
        var input = existingKeys.ToList();
        input.Insert(250, nullKey);
        input.Insert(5000, missingKey);
        input.Add(existingKeys[42]);
        var cleanupKeys = existingKeys.Append(nullKey).Append(missingKey).ToArray();

        try
        {
            await _cache.SetAllAsync(existingKeys
                .Select((key, index) => new KeyValuePair<string, int>(key, index + 1))
                .ToDictionary(item => item.Key, item => item.Value));
            await _cache.SetAsync<int?>(nullKey, null);

            var listValues = await _cache.GetAllAsync<int?>(input);
            var streamed = new List<CacheEntry<int?>>(input.Count);
            await foreach (var entry in _cache.GetAllStreamAsync<int?>(input))
                streamed.Add(entry);

            Assert.Equal(10_000, listValues.Count);
            Assert.Equal(input, streamed.Select(entry => entry.Key));
            Assert.Equal(listValues, streamed.Select(entry => entry.Value));
            Assert.True(streamed[250].Hit);
            Assert.Null(streamed[250].Value);
            Assert.False(streamed[5000].Hit);
            Assert.Null(streamed[5000].Value);
            Assert.True(streamed[^1].Hit);
            Assert.Equal(43, streamed[^1].Value);
        }
        finally
        {
            await _cache.RemoveAllAsync(cleanupKeys);
        }
    }

    /// <summary>
    /// 第二批反序列化失败时，流式读取在首批结果产出后传播错误。
    /// </summary>
    [Fact]
    public async Task GetAllStream_PropagatesSecondBatchDeserializationFailureAfterFirstBatch()
    {
        var prefix = "stream-fault:" + Guid.NewGuid().ToString("N") + ":";
        var firstBatch = Enumerable.Range(0, 500)
            .Select(index => prefix + index)
            .ToArray();
        var invalidKey = prefix + "invalid";
        await _cache.SetAllAsync(firstBatch.ToDictionary(key => key, _ => 1));
        await _cache.SetAsync(invalidKey, "not-an-integer");
        var keys = firstBatch.Append(invalidKey).ToArray();
        var enumerator = _cache.GetAllStreamAsync<int>(keys).GetAsyncEnumerator();
        var emitted = 0;

        try
        {
            for (var index = 0; index < firstBatch.Length; index++)
            {
                Assert.True(await enumerator.MoveNextAsync());
                emitted++;
            }
            Assert.Equal(500, emitted);
            await Assert.ThrowsAnyAsync<Exception>(() => enumerator.MoveNextAsync().AsTask());
        }
        finally
        {
            await enumerator.DisposeAsync();
            await _cache.RemoveAllAsync(keys);
        }
    }

    /// <summary>
    /// 前缀流式读取返回逻辑键、值和命中状态。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_ReturnsLogicalKeysAndHits()
    {
        var prefix = "stream-prefix:" + Guid.NewGuid().ToString("N") + ":";
        var keys = new[] { prefix + "first", prefix + "second", prefix + "outside" };
        await _cache.SetAsync(keys[0], 11);
        await _cache.SetAsync<int?>(keys[1], null);
        await _cache.SetAsync(keys[2], 33);

        try
        {
            var actual = new List<CacheEntry<int?>>();
            await foreach (var entry in _cache.GetByPrefixStreamAsync<int?>(prefix))
                actual.Add(entry);

            Assert.Equal(keys.OrderBy(key => key), actual.Select(item => item.Key).OrderBy(key => key));
            Assert.All(actual, item => Assert.True(item.Hit));
            Assert.Contains(actual, item => item.Key == keys[0] && item.Value == 11);
            Assert.Contains(actual, item => item.Key == keys[1] && item.Value == null);
            Assert.Contains(actual, item => item.Key == keys[2] && item.Value == 33);
        }
        finally
        {
            await _cache.RemoveAllAsync(keys);
        }
    }

    /// <summary>
    /// 前缀扫描第二批读取失败时，在首批结果产出后传播错误。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_PropagatesSecondBatchFailureAfterFirstBatch()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var configuration = new CacheConfiguration("freeredis-prefix-fault-" + suffix, "batch", batchSize: 2);
        var cache = new FreeRedisCacheManager(_client, configuration);
        var prefix = "stream-prefix-fault:" + suffix + ":";
        var keys = Enumerable.Range(0, 8).Select(index => prefix + index).ToArray();
        var enumerator = cache.GetByPrefixStreamAsync<int>(prefix).GetAsyncEnumerator();

        try
        {
            await cache.SetAllAsync(keys.ToDictionary(key => key, _ => 1));
            Assert.True(await enumerator.MoveNextAsync());
            var firstKey = enumerator.Current.Key;

            foreach (var key in keys.Where(key => !string.Equals(key, firstKey, StringComparison.Ordinal)))
                await cache.SetAsync(key, "invalid-json");

            var emitted = 1;
            var error = await Record.ExceptionAsync(async () =>
            {
                while (await enumerator.MoveNextAsync())
                    emitted++;
            });

            Assert.NotNull(error);
            Assert.True(emitted >= 1);
        }
        finally
        {
            await enumerator.DisposeAsync();
            await cache.RemoveAllAsync(keys);
        }
    }

    /// <summary>
    /// 一万项特殊键前缀扫描保持列表与流式结果一致且不越过命名空间。
    /// </summary>
    [Fact]
    public async Task GetByPrefix_10000Items_ListAndStreamRemainConsistentAcrossSpecialKeysAndNamespaces()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var left = new FreeRedisCacheManager(_client,
            new CacheConfiguration("freeredis-prefix-10000-left-" + suffix, "batch"));
        var sibling = new FreeRedisCacheManager(_client,
            new CacheConfiguration("freeredis-prefix-10000-left-" + suffix + "-sibling", "batch"));
        var prefix = "prefix-10000:" + suffix + ":";
        var ordinaryKeys = Enumerable.Range(0, 9_995)
            .Select(index => prefix + "item:" + index)
            .ToArray();
        var specialKeys = new[]
        {
            prefix + "Unicode:中文-😀",
            prefix + "花括号:{slot}",
            prefix + "星号:*",
            prefix + "问号:?",
            prefix + "反斜线:\\"
        };
        var allKeys = ordinaryKeys.Concat(specialKeys).ToArray();
        var sentinelKey = prefix + "哨兵:{other}*?\\";
        var values = allKeys.Select((key, index) =>
            new KeyValuePair<string, int>(key, index + 1)).ToDictionary(item => item.Key, item => item.Value);

        try
        {
            await left.SetAllAsync(values);
            await sibling.SetAsync(sentinelKey, 99_999);

            var listValues = await left.GetByPrefixAsync<int>(prefix);
            var streamed = new List<CacheEntry<int>>(allKeys.Length);
            await foreach (var entry in left.GetByPrefixStreamAsync<int>(prefix))
                streamed.Add(entry);

            Assert.Equal(allKeys.Length, listValues.Count);
            Assert.Equal(allKeys.Length, streamed.Count);
            Assert.Equal(values.Values.OrderBy(value => value), listValues.OrderBy(value => value));
            Assert.Equal(values.Values.OrderBy(value => value), streamed.Select(entry => entry.Value).OrderBy(value => value));
            Assert.Equal(allKeys.OrderBy(key => key), streamed.Select(entry => entry.Key).OrderBy(key => key));
            Assert.All(streamed, entry => Assert.True(entry.Hit));
            Assert.All(streamed, entry => Assert.Equal(values[entry.Key], entry.Value));
            Assert.DoesNotContain(streamed, entry => entry.Key == sentinelKey);
            Assert.Equal(99_999, await sibling.GetAsync<int>(sentinelKey));
        }
        finally
        {
            left.Clear();
            sibling.Clear();
        }
    }

    /// <summary>
    /// 十万项数据下列表与流式批量读取及前缀扫描结果保持一致。
    /// </summary>
    [LargeCacheDatasetFact]
    public async Task GetAllAndGetByPrefix_100000Items_ListAndStreamRemainConsistent()
    {
        var itemCount = GetLargeCacheItemCount();
        var suffix = Guid.NewGuid().ToString("N");
        var left = new FreeRedisCacheManager(_client,
            new CacheConfiguration("freeredis-large-" + suffix, "batch"));
        var sibling = new FreeRedisCacheManager(_client,
            new CacheConfiguration("freeredis-large-sibling-" + suffix, "batch"));
        var prefix = "prefix-100000:" + suffix + ":";
        var ordinaryKeys = Enumerable.Range(0, itemCount - 5)
            .Select(index => prefix + "item:" + index)
            .ToArray();
        var specialKeys = new[]
        {
            prefix + "Unicode:中文-😀",
            prefix + "花括号:{slot}",
            prefix + "星号:*",
            prefix + "问号:?",
            prefix + "反斜线:\\"
        };
        var allKeys = ordinaryKeys.Concat(specialKeys).ToArray();
        var values = allKeys.Select((key, index) =>
            new KeyValuePair<string, int>(key, index + 1))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var missingKeys = new[] { prefix + "missing:0", prefix + "missing:1" };
        var input = allKeys.ToList();
        var duplicateKey = allKeys[Math.Min(42, allKeys.Length - 1)];
        input.Insert(Math.Min(50_000, input.Count), duplicateKey);
        input.Insert(Math.Min(1_234, input.Count), missingKeys[0]);
        input.Add(missingKeys[1]);
        input.Add(specialKeys[^1]);
        var siblingKey = prefix + "sibling-only";
        var missingIndex = input.IndexOf(missingKeys[0]);
        var duplicateIndex = input.FindLastIndex(key => key == duplicateKey);

        try
        {
            await left.SetAllAsync(values);
            await sibling.SetAsync(siblingKey, 999_999);

            var expectedValues = input.Select(key =>
                values.TryGetValue(key, out var value) ? (int?)value : null).ToArray();
            var expectedHits = input.Select(values.ContainsKey).ToArray();
            var listValues = await left.GetAllAsync<int?>(input);
            var streamed = new List<CacheEntry<int?>>(input.Count);
            await foreach (var entry in left.GetAllStreamAsync<int?>(input))
                streamed.Add(entry);

            Assert.Equal(input, streamed.Select(entry => entry.Key));
            Assert.Equal(expectedValues, listValues);
            Assert.Equal(expectedValues, streamed.Select(entry => entry.Value));
            Assert.Equal(expectedHits, streamed.Select(entry => entry.Hit));
            Assert.False(streamed[missingIndex].Hit);
            Assert.Equal(duplicateKey, streamed[duplicateIndex].Key);
            Assert.True(streamed[^1].Hit);
            Assert.Equal(specialKeys[^1], streamed[^1].Key);

            var prefixValues = await left.GetByPrefixAsync<int>(prefix);
            var prefixStreamed = new List<CacheEntry<int>>(allKeys.Length);
            await foreach (var entry in left.GetByPrefixStreamAsync<int>(prefix))
                prefixStreamed.Add(entry);

            Assert.Equal(allKeys.Length, prefixValues.Count);
            Assert.Equal(allKeys.Length, prefixStreamed.Count);
            Assert.Equal(values.Values.OrderBy(value => value), prefixValues.OrderBy(value => value));

            var scannedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in prefixStreamed)
            {
                Assert.True(entry.Hit);
                Assert.True(values.TryGetValue(entry.Key, out var expected));
                Assert.Equal(expected, entry.Value);
                Assert.True(scannedKeys.Add(entry.Key), "前缀扫描返回了重复逻辑键。");
            }

            Assert.True(scannedKeys.SetEquals(allKeys));
            Assert.DoesNotContain(prefixStreamed, entry => entry.Key == siblingKey);
            Assert.Equal(999_999, await sibling.GetAsync<int>(siblingKey));
        }
        finally
        {
            await left.ClearAsync();
            await sibling.ClearAsync();
        }
    }

    /// <summary>
    /// 创建批量读取契约测试使用的逻辑键。
    /// </summary>
    /// <returns>包含两个待读取键和一个缺失键的逻辑键数组。</returns>
    private static string[] CreateKeys()
    {
        var prefix = "batch-contract:" + Guid.NewGuid().ToString("N") + ":";
        return new[] { prefix + "first", prefix + "second", prefix + "missing" };
    }

    /// <summary>
    /// 获取大批量集成测试使用的数据项数量。
    /// </summary>
    /// <returns>环境变量指定的数量，或默认数量。</returns>
    private static int GetLargeCacheItemCount()
    {
        var configured = Environment.GetEnvironmentVariable("BING_TEST_CACHE_LARGE_COUNT");
        if (string.IsNullOrWhiteSpace(configured))
            return 100_000;
        if (!int.TryParse(configured, out var count) || count < 5)
            throw new InvalidOperationException("BING_TEST_CACHE_LARGE_COUNT 必须是大于等于 5 的整数。");
        return count;
    }

    /// <summary>
    /// 表示用于验证序列化边界的自定义载荷。
    /// </summary>
    private sealed class CustomPayload
    {
        /// <summary>
        /// 获取或设置序列化载荷中的数值。
        /// </summary>
        public int Value { get; set; }
    }

    /// <summary>
    /// 限制源序列只能枚举一次的包装器。
    /// </summary>
    /// <typeparam name="T">序列元素类型。</typeparam>
    private sealed class SingleUseEnumerable<T> : IEnumerable<T>
    {
        /// <summary>
        /// 保存被包装的源序列。
        /// </summary>
        private readonly IEnumerable<T> _source;
        /// <summary>
        /// 指示源序列是否已经开始枚举。
        /// </summary>
        private bool _enumerated;

        /// <summary>
        /// 初始化一个 <see cref="SingleUseEnumerable{T}"/> 类型的实例。
        /// </summary>
        /// <param name="source">要包装的源序列。</param>
        public SingleUseEnumerable(IEnumerable<T> source) => _source = source;

        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator()
        {
            if (_enumerated)
                throw new InvalidOperationException("测试输入被重复枚举。");
            _enumerated = true;
            return _source.GetEnumerator();
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
