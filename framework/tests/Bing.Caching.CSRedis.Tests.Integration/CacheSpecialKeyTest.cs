using Bing.Caching.CSRedis;
using CSRedis;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// CSRedis 特殊字符逻辑键隔离测试。
/// </summary>
/// <remarks>
/// 覆盖读写、扫描、删除和命名空间清理路径。
/// </remarks>
public sealed class CacheSpecialKeyTest
{
    /// <summary>
    /// 保存由测试夹具提供、用于创建多个隔离命名空间缓存管理器的 CSRedis 客户端。
    /// </summary>
    private readonly CSRedisClient _client;

    /// <summary>
    /// 初始化一个 <see cref="CacheSpecialKeyTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">供各测试创建缓存管理器并执行直接 Redis 校验的客户端。</param>
    public CacheSpecialKeyTest(CSRedisClient client) => _client = client;

    /// <summary>
    /// 验证特殊字符逻辑键的读写和清理。
    /// </summary>
    /// <remarks>
    /// 使用 Unicode、花括号、星号、问号和反斜线键，检查批量读取、按模式删除、按前缀删除、清理及命名空间隔离。
    /// </remarks>
    [Fact]
    public async Task SpecialLogicalKeys_RoundTrip_PrefixPatternAndClearStayIsolated()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var left = new CSRedisCacheManager(_client,
            new CacheConfiguration("csredis-special-left-" + suffix, "special"));
        var right = new CSRedisCacheManager(_client,
            new CacheConfiguration("csredis-special-right-" + suffix, "sentinel"));
        const string sentinelKey = "哨兵:{other}*?\\";
        var specialKeys = new[]
        {
            "特殊:Unicode:中文-😀",
            "特殊:花括号:{slot}",
            "特殊:星号:*",
            "特殊:问号:?",
            "特殊:反斜线:\\"
        };
        var readKeys = specialKeys.Select(key => "读取:" + key).ToArray();
        var removeKeys = specialKeys.Select(key => "删除:" + key).ToArray();

        try
        {
            right.Set(sentinelKey, 9001);
            for (var index = 0; index < specialKeys.Length; index++)
            {
                left.Set(readKeys[index], index + 1);
                await left.SetAsync(removeKeys[index], index + 101);
            }

            for (var index = 0; index < specialKeys.Length; index++)
            {
                Assert.Equal(index + 1, left.Get<int>(readKeys[index]));
                Assert.Equal(index + 1, await left.GetAsync<int>(readKeys[index]));
                Assert.Equal(index + 101, left.Get<int>(removeKeys[index]));
                Assert.Equal(index + 101, await left.GetAsync<int>(removeKeys[index]));
            }

            Assert.Equal(Enumerable.Range(1, specialKeys.Length),
                left.GetByPrefix<int>("读取:").OrderBy(value => value));
            Assert.Equal(Enumerable.Range(1, specialKeys.Length),
                (await left.GetByPrefixAsync<int>("读取:")).OrderBy(value => value));
            Assert.Equal(9001, right.Get<int>(sentinelKey));

            left.RemoveByPattern("删除:特殊:星号:*");
            left.RemoveByPattern("删除:特殊:问号:?");
            await left.RemoveByPatternAsync("删除:特殊:反斜线:\\");
            Assert.False(left.Exists(removeKeys[2]));
            Assert.False(left.Exists(removeKeys[3]));
            Assert.False(left.Exists(removeKeys[4]));
            Assert.Equal(102, left.Get<int>(removeKeys[1]));
            Assert.Equal(101, left.Get<int>(removeKeys[0]));

            left.RemoveByPrefix("删除:");
            Assert.Empty(left.GetByPrefix<int>("删除:"));
            Assert.Equal(9001, right.Get<int>(sentinelKey));

            left.Clear();
            Assert.Empty(await left.GetByPrefixAsync<int>("读取:"));
            Assert.Equal(9001, right.Get<int>(sentinelKey));
        }
        finally
        {
            left.Clear();
            right.Clear();
        }
    }

    /// <summary>
    /// 验证流式前缀扫描返回特殊字符逻辑键。
    /// </summary>
    /// <remarks>
    /// 检查扫描排除相邻命名空间的哨兵键，并保留每个条目的命中值。
    /// </remarks>
    [Fact]
    public async Task GetByPrefixStream_ReturnsSpecialLogicalKeys_AndExcludesSimilarNamespaceSentinel()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var left = new CSRedisCacheManager(_client,
            new CacheConfiguration("csredis-stream-special-left-" + suffix, "special"));
        var right = new CSRedisCacheManager(_client,
            new CacheConfiguration("csredis-stream-special-left-" + suffix + "-sibling", "special"));
        var prefix = "流式特殊:" + suffix + ":";
        var keys = new[]
        {
            prefix + "Unicode:中文-😀",
            prefix + "花括号:{slot}",
            prefix + "星号:*",
            prefix + "问号:?",
            prefix + "反斜线:\\"
        };
        var sentinelKey = prefix + "哨兵:{other}*?\\";

        try
        {
            for (var index = 0; index < keys.Length; index++)
                await left.SetAsync(keys[index], index + 1);
            await right.SetAsync(sentinelKey, 9001);

            var actual = new List<CacheEntry<int>>();
            await foreach (var entry in left.GetByPrefixStreamAsync<int>(prefix))
                actual.Add(entry);

            Assert.Equal(keys.OrderBy(key => key), actual.Select(entry => entry.Key).OrderBy(key => key));
            Assert.All(actual, entry => Assert.True(entry.Hit));
            foreach (var key in keys)
                Assert.Equal(Array.IndexOf(keys, key) + 1, actual.Single(entry => entry.Key == key).Value);
            Assert.DoesNotContain(actual, entry => entry.Key == sentinelKey);
            Assert.Equal(9001, await right.GetAsync<int>(sentinelKey));
        }
        finally
        {
            left.Clear();
            right.Clear();
        }
    }

    /// <summary>
    /// 验证特殊字符模式删除使用共享 glob 转义语义。
    /// </summary>
    /// <remarks>
    /// 检查仅匹配的键被删除，并拒绝无效模式。
    /// </remarks>
    [Fact]
    public async Task RemoveByPattern_UsesSharedGlobAndEscapeSemantics()
    {
        var configuration = new CacheConfiguration("csredis-pattern-" + Guid.NewGuid().ToString("N"), "pattern");
        var cache = new CSRedisCacheManager(_client, configuration);
        var prefix = "模式:" + Guid.NewGuid().ToString("N");
        var literalStar = prefix + "*";
        var literalQuestion = prefix + "?";
        var literalSlash = prefix + "\\";
        var oneCharacter = prefix + "-a";
        var twoCharacters = prefix + "-ab";

        try
        {
            cache.Set(literalStar, 1);
            cache.Set(literalQuestion, 2);
            cache.Set(literalSlash, 3);
            cache.Set(oneCharacter, 4);
            cache.Set(twoCharacters, 5);

            cache.RemoveByPattern(prefix + "-?");
            Assert.False(cache.Exists(oneCharacter));
            Assert.True(cache.Exists(twoCharacters));

            cache.RemoveByPattern(prefix + "\\*");
            cache.RemoveByPattern(prefix + "\\?");
            await cache.RemoveByPatternAsync(prefix + "\\\\");

            Assert.False(cache.Exists(literalStar));
            Assert.False(cache.Exists(literalQuestion));
            Assert.False(cache.Exists(literalSlash));
            Assert.True(cache.Exists(twoCharacters));
            Assert.Throws<ArgumentException>(() => cache.RemoveByPattern("**"));
        }
        finally
        {
            cache.Clear();
        }
    }

    /// <summary>
    /// 验证流式前缀扫描遵守键数量预算。
    /// </summary>
    /// <remarks>
    /// 达到 MaxScanKeys 上限后应抛出扫描预算异常，并报告已消费的键数量。
    /// </remarks>
    [Fact]
    public async Task GetByPrefixStream_EnforcesScanKeyBudget()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var configuration = new CacheConfiguration("csredis-scan-budget-" + suffix, "budget",
            batchSize: 2, resources: new CacheResourceOptions(maxScanKeys: 2));
        var cache = new CSRedisCacheManager(_client, configuration);
        var prefix = "预算:" + suffix + ":";
        var keys = Enumerable.Range(0, 3).Select(index => prefix + index).ToArray();

        try
        {
            await cache.SetAllAsync(keys.ToDictionary(key => key, _ => 1));
            await using var enumerator = cache.GetByPrefixStreamAsync<int>(prefix).GetAsyncEnumerator();
            Assert.True(await enumerator.MoveNextAsync());
            Assert.True(await enumerator.MoveNextAsync());

            var error = await Assert.ThrowsAsync<CacheScanLimitExceededException>(
                () => enumerator.MoveNextAsync().AsTask());
            Assert.Contains("MaxScanKeys", error.Message, StringComparison.Ordinal);
            Assert.Equal(2, error.Limit);
            Assert.Equal(2, error.Consumed);
        }
        finally
        {
            await cache.RemoveAllAsync(keys);
        }
    }
}
