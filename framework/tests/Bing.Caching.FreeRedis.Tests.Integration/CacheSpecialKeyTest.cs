using Bing.Caching.FreeRedis;
using FreeRedis;

namespace Bing.Caching.FreeRedis.Tests;

/// <summary>
/// 验证特殊逻辑键在 FreeRedis 的读写、扫描和命名空间清理路径中保持隔离。
/// </summary>
public sealed class CacheSpecialKeyTest
{
    /// <summary>
    /// 用于写入和读取特殊逻辑键的 Redis 客户端。
    /// </summary>
    private readonly RedisClient _client;

    /// <summary>
    /// 初始化一个 <see cref="CacheSpecialKeyTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">Redis 测试客户端。</param>
    public CacheSpecialKeyTest(RedisClient client) => _client = client;

    /// <summary>
    /// 特殊逻辑键的读写、模式删除和命名空间清理保持隔离。
    /// </summary>
    [Fact]
    public async Task SpecialLogicalKeys_RoundTrip_PrefixPatternAndClearStayIsolated()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var left = new FreeRedisCacheManager(_client,
            new CacheConfiguration("freeredis-special-left-" + suffix, "special"));
        var right = new FreeRedisCacheManager(_client,
            new CacheConfiguration("freeredis-special-right-" + suffix, "sentinel"));
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
    /// 前缀流式读取特殊逻辑键并排除相似命名空间哨兵键。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_ReturnsSpecialLogicalKeys_AndExcludesSimilarNamespaceSentinel()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var left = new FreeRedisCacheManager(_client,
            new CacheConfiguration("freeredis-stream-special-left-" + suffix, "special"));
        var right = new FreeRedisCacheManager(_client,
            new CacheConfiguration("freeredis-stream-special-left-" + suffix + "-sibling", "special"));
        var prefix = "流式特殊:" + suffix + ":";
        var keys = new[]
        {
            prefix + "Unicode:中文-😀",
            prefix + "花括号:{slot}",
            prefix + "圆括号:(round)",
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
            Assert.Equal(
                keys.Select((key, index) => new { Key = key, Value = index + 1 })
                    .OrderBy(item => item.Key),
                actual.Select(entry => new { entry.Key, entry.Value }).OrderBy(item => item.Key));
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
    /// 模式通配符按模式匹配，转义后按字面匹配特殊字符。
    /// </summary>
    [Fact]
    public async Task RemoveByPattern_UsesSharedGlobAndEscapeSemantics()
    {
        var configuration = new CacheConfiguration("freeredis-pattern-" + Guid.NewGuid().ToString("N"), "pattern");
        var cache = new FreeRedisCacheManager(_client, configuration);
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
    /// 前缀流式扫描超过键数上限时抛出扫描预算异常。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_EnforcesScanKeyBudget()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var configuration = new CacheConfiguration("freeredis-scan-budget-" + suffix, "budget",
            batchSize: 2, resources: new CacheResourceOptions(maxScanKeys: 2));
        var cache = new FreeRedisCacheManager(_client, configuration);
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
