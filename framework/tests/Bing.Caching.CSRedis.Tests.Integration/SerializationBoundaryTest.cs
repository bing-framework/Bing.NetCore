using Bing.Caching;
using CSRedis;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// CSRedis 批量反序列化边界测试。
/// </summary>
/// <remarks>
/// 验证不同缓存值独立反序列化，避免共享 JSON 引用解析上下文。
/// </remarks>
public sealed class SerializationBoundaryTest
{
    /// <summary>
    /// 保存第一条测试 JSON 文本。
    /// </summary>
    private const string FirstJson =
        "{\"$id\":\"1\",\"Name\":\"first\",\"Optional\":null,\"Child\":{\"$id\":\"2\",\"Name\":\"first-child\",\"Optional\":7,\"Child\":null}}";

    /// <summary>
    /// 保存第二条测试 JSON 文本。
    /// </summary>
    private const string SecondJson =
        "{\"$id\":\"1\",\"Name\":\"second\",\"Optional\":null,\"Child\":{\"$id\":\"2\",\"Name\":\"second-child\",\"Optional\":8,\"Child\":null}}";

    /// <summary>
    /// 保存自引用测试 JSON 文本。
    /// </summary>
    private const string SelfJson =
        "{\"$id\":\"1\",\"Name\":\"self\",\"Optional\":null,\"Child\":{\"$ref\":\"1\"}}";

    /// <summary>
    /// 验证不同读取路径的 JSON 结果一致。
    /// </summary>
    /// <remarks>
    /// 覆盖单值、批量和流式读取，并检查对象引用、缺失项和 null 值语义。
    /// </remarks>
    [Bing.Test.Shared.IntegrationFact]
    public async Task IndependentJsonValuesRemainConsistentAcrossReadPaths()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        using var client = new CSRedisClient(connection);
        var configuration = new CacheConfiguration(
            "csredis-serialization-" + Guid.NewGuid().ToString("N"),
            "serialization",
            batchSize: 2);
        var cache = new CSRedisCacheManager(client, configuration);
        var suffix = Guid.NewGuid().ToString("N");
        var firstKey = "references:first:" + suffix;
        var secondKey = "references:second:" + suffix;
        var selfKey = "references:self:" + suffix;
        var nullableKey = "references:null:" + suffix;
        var missingKey = "references:missing:" + suffix;
        var missingNullableKey = "references:missing-nullable:" + suffix;
        var cleanupKeys = new[] { firstKey, secondKey, selfKey, nullableKey, missingKey, missingNullableKey };

        try
        {
            SetRaw(client, CacheKeyFormat.GetPhysicalKey(configuration, firstKey), FirstJson);
            SetRaw(client, CacheKeyFormat.GetPhysicalKey(configuration, secondKey), SecondJson);
            SetRaw(client, CacheKeyFormat.GetPhysicalKey(configuration, selfKey), SelfJson);
            SetRaw(client, CacheKeyFormat.GetPhysicalKey(configuration, nullableKey), "null");

            var single = cache.Get<ReferenceNode>(firstKey);
            AssertNode(single, "first", "first-child", 7);
            Assert.Null(cache.Get<ReferenceNode>(missingKey));
            Assert.Null(cache.Get<int?>(nullableKey));
            Assert.Null(cache.Get<int?>(missingNullableKey));

            var input = new[] { secondKey, firstKey, missingKey, selfKey, secondKey };
            var list = cache.GetAll<ReferenceNode>(input);
            AssertReferenceResults(input, list);

            var asyncList = await cache.GetAllAsync<ReferenceNode>(input);
            AssertReferenceResults(input, asyncList);

            var streamed = new List<CacheEntry<ReferenceNode>>();
            await foreach (var entry in cache.GetAllStreamAsync<ReferenceNode>(input))
                streamed.Add(entry);
            Assert.Equal(input, streamed.Select(entry => entry.Key));
            Assert.Equal(new[] { true, true, false, true, true },
                streamed.Select(entry => entry.Hit));
            AssertNode(streamed[0].Value, "second", "second-child", 8);
            AssertNode(streamed[1].Value, "first", "first-child", 7);
            Assert.Null(streamed[2].Value);
            AssertSelfNode(streamed[3].Value);
            AssertNode(streamed[4].Value, "second", "second-child", 8);
            Assert.NotSame(streamed[0].Value, streamed[4].Value);
            Assert.NotSame(streamed[0].Value.Child, streamed[4].Value.Child);

            var nullableInput = new[] { nullableKey, missingNullableKey, nullableKey };
            Assert.Equal(new int?[] { null, null, null }, cache.GetAll<int?>(nullableInput));
            Assert.Equal(new int?[] { null, null, null },
                await cache.GetAllAsync<int?>(nullableInput));

            var nullableStream = new List<CacheEntry<int?>>();
            await foreach (var entry in cache.GetAllStreamAsync<int?>(nullableInput))
                nullableStream.Add(entry);
            Assert.Equal(nullableInput, nullableStream.Select(entry => entry.Key));
            Assert.Equal(new[] { true, false, true }, nullableStream.Select(entry => entry.Hit));
            Assert.All(nullableStream, entry => Assert.Null(entry.Value));
        }
        finally
        {
            await cache.RemoveAllAsync(cleanupKeys);
        }
    }

    /// <summary>
    /// 将 JSON 文本直接写入 Redis 缓存键。
    /// </summary>
    /// <param name="client">Redis 客户端。</param>
    /// <param name="key">逻辑缓存键。</param>
    /// <param name="json">JSON 文本。</param>
    private static void SetRaw(CSRedisClient client, string key, string json) =>
        client.Set(key, json);

    /// <summary>
    /// 验证批量反序列化保留输入顺序、缺失项和对象隔离。
    /// </summary>
    /// <param name="keys">逻辑缓存键集合。</param>
    /// <param name="values">待处理值集合。</param>
    private static void AssertReferenceResults(IReadOnlyList<string> keys, IReadOnlyList<ReferenceNode> values)
    {
        Assert.Equal(keys.Count, values.Count);
        AssertNode(values[0], "second", "second-child", 8);
        AssertNode(values[1], "first", "first-child", 7);
        Assert.Null(values[2]);
        AssertSelfNode(values[3]);
        AssertNode(values[4], "second", "second-child", 8);
        Assert.NotSame(values[0], values[4]);
        Assert.NotSame(values[0].Child, values[4].Child);
    }

    /// <summary>
    /// 验证节点及其子节点的反序列化结果。
    /// </summary>
    /// <param name="node">Redis 节点。</param>
    /// <param name="name">名称。</param>
    /// <param name="childName">子节点名称。</param>
    /// <param name="childOptional">子节点的可选值。</param>
    private static void AssertNode(ReferenceNode node, string name, string childName, int childOptional)
    {
        Assert.NotNull(node);
        Assert.Equal(name, node.Name);
        Assert.Null(node.Optional);
        Assert.NotNull(node.Child);
        Assert.Equal(childName, node.Child.Name);
        Assert.Equal((int?)childOptional, node.Child.Optional);
        Assert.Null(node.Child.Child);
    }

    /// <summary>
    /// 验证自引用节点反序列化后仍指向自身。
    /// </summary>
    /// <param name="node">Redis 节点。</param>
    private static void AssertSelfNode(ReferenceNode node)
    {
        Assert.NotNull(node);
        Assert.Equal("self", node.Name);
        Assert.Null(node.Optional);
        Assert.Same(node, node.Child);
    }

    /// <summary>
    /// 表示具有可选值和子节点的序列化测试对象。
    /// </summary>
    private sealed class ReferenceNode
    {
        /// <summary>
        /// 获取或设置节点名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 获取或设置节点的可选整数值。
        /// </summary>
        public int? Optional { get; set; }

        /// <summary>
        /// 获取或设置节点的子节点。
        /// </summary>
        public ReferenceNode Child { get; set; }
    }
}
