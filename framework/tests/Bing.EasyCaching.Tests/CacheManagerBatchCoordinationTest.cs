using Bing.Caching;
using EasyCaching.Core;
using EasyCaching.Core.Serialization;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 验证 CacheManager 批量操作与协调器、本地缓存层之间的协作。
/// </summary>
public class CacheManagerBatchCoordinationTest
{
    /// <summary>
    /// 协调批次提交后将写入实例的本地层回填为全部值。
    /// </summary>
    [Fact]
    public void HybridSetAll_RefillsWritingInstanceLocalLayerAfterCoordinatorCommit()
    {
        var database = new Mock<IDatabase>();
        var serializer = CreateBatchSerializer();
        var provider = CreateRedisProvider(database, serializer);
        var local = new Mock<IEasyCachingProvider>();
        var hybrid = new Mock<IHybridCachingProvider>();
        database.Setup(value => value.ScriptEvaluate(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(), CommandFlags.None))
            .Returns((string script, RedisKey[] keys, RedisValue[] _, CommandFlags _) =>
                script.Contains("count=math.floor", StringComparison.Ordinal)
                    ? RedisResult.Create((RedisValue)"2")
                    : CreateRawBatchResult());

        var configuration = new CacheConfiguration("hybrid-batch-local", "hybrid", batchSize: 2);
        var manager = new CacheManager(provider.Object, configuration, hybrid.Object, local.Object);
        manager.SetCoordinator(new RedisLeaseCoordinator(provider.Object, configuration));

        manager.SetAll(new Dictionary<string, int> { ["one"] = 1, ["two"] = 2 });

        local.Verify(value => value.Set(
            CacheKeyFormat.GetPhysicalKey(configuration, "one"), 1, It.IsAny<TimeSpan>()), Times.Once);
        local.Verify(value => value.Set(
            CacheKeyFormat.GetPhysicalKey(configuration, "two"), 2, It.IsAny<TimeSpan>()), Times.Once);
    }

    /// <summary>
    /// 异步协调批次提交后将写入实例的本地层回填为全部值。
    /// </summary>
    [Fact]
    public async Task HybridSetAllAsync_RefillsWritingInstanceLocalLayerAfterCoordinatorCommit()
    {
        var database = new Mock<IDatabase>();
        var serializer = CreateBatchSerializer();
        var provider = CreateRedisProvider(database, serializer);
        var local = new Mock<IEasyCachingProvider>();
        var hybrid = new Mock<IHybridCachingProvider>();
        database.Setup(value => value.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .Returns((string script, RedisKey[] keys, RedisValue[] _, CommandFlags _) =>
                Task.FromResult(script.Contains("count=math.floor", StringComparison.Ordinal)
                    ? RedisResult.Create((RedisValue)"2")
                    : CreateRawBatchResult()));
        local.Setup(value => value.SetAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var configuration = new CacheConfiguration("hybrid-batch-local-async", "hybrid", batchSize: 2);
        var manager = new CacheManager(provider.Object, configuration, hybrid.Object, local.Object);
        manager.SetCoordinator(new RedisLeaseCoordinator(provider.Object, configuration));

        await manager.SetAllAsync(new Dictionary<string, int> { ["one"] = 1, ["two"] = 2 });

        local.Verify(value => value.SetAsync(
            CacheKeyFormat.GetPhysicalKey(configuration, "one"), 1, It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Once);
        local.Verify(value => value.SetAsync(
            CacheKeyFormat.GetPhysicalKey(configuration, "two"), 2, It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 批量写入和删除分别使用协调器提供的批量命令。
    /// </summary>
    [Fact]
    public async Task SetAllAndRemoveAll_UseCoordinatorBatchCommands()
    {
        var database = new Mock<IDatabase>();
        var serializer = new Mock<IEasyCachingSerializer>();
        serializer.Setup(value => value.Serialize(It.IsAny<int>()))
            .Returns(new byte[] { 1 });
        var provider = new Mock<IEasyCachingProvider>();
        provider.SetupGet(value => value.Database).Returns(database.Object);
        provider.Setup(value => value.GetProviderInfo())
            .Returns(new ProviderInfo { Serializer = serializer.Object });
        database.Setup(value => value.ScriptEvaluate(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(), CommandFlags.None))
            .Returns((string _, RedisKey[] keys, RedisValue[] _, CommandFlags _) =>
                RedisResult.Create((RedisValue)(keys.Length / 2).ToString()));
        database.Setup(value => value.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(), CommandFlags.None))
            .Returns((string _, RedisKey[] keys, RedisValue[] _, CommandFlags _) =>
                Task.FromResult(RedisResult.Create((RedisValue)(keys.Length / 2).ToString())));
        database.Setup(value => value.KeyDelete(It.IsAny<RedisKey[]>(), CommandFlags.None))
            .Returns(4L);
        database.Setup(value => value.KeyDeleteAsync(It.IsAny<RedisKey[]>(), CommandFlags.None))
            .ReturnsAsync(4L);
        var configuration = new CacheConfiguration("manager-batch", "redis", batchSize: 2);
        var manager = new CacheManager(provider.Object, configuration);
        manager.SetCoordinator(new RedisLeaseCoordinator(provider.Object, configuration));
        var values = new Dictionary<string, int> { ["one"] = 1, ["two"] = 2 };

        manager.SetAll(values);
        await manager.SetAllAsync(values);
        manager.RemoveAll(values.Keys);
        await manager.RemoveAllAsync(values.Keys);

        database.Verify(value => value.ScriptEvaluate(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(), CommandFlags.None), Times.Once);
        database.Verify(value => value.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(), CommandFlags.None), Times.Once);
        database.Verify(value => value.KeyDelete(It.IsAny<RedisKey[]>(), CommandFlags.None), Times.Once);
        database.Verify(value => value.KeyDeleteAsync(It.IsAny<RedisKey[]>(), CommandFlags.None), Times.Once);
    }

    /// <summary>
    /// 创建将整数映射为单字节载荷的模拟序列化器。
    /// </summary>
    /// <returns>返回配置了整数序列化和反序列化规则的模拟序列化器。</returns>
    private static Mock<IEasyCachingSerializer> CreateBatchSerializer()
    {
        var serializer = new Mock<IEasyCachingSerializer>();
        serializer.Setup(value => value.Serialize(It.IsAny<int>()))
            .Returns<int>(value => new[] { (byte)value });
        serializer.Setup(value => value.Deserialize<int>(It.IsAny<byte[]>()))
            .Returns<byte[]>(payload => payload[0]);
        return serializer;
    }

    /// <summary>
    /// 创建绑定指定数据库和序列化器的 EasyCaching Provider 模拟对象。
    /// </summary>
    /// <param name="database">Redis 数据库。</param>
    /// <param name="serializer">Provider 使用的序列化器模拟对象。</param>
    /// <returns>返回已绑定数据库和序列化器的 Provider 模拟对象。</returns>
    private static Mock<IEasyCachingProvider> CreateRedisProvider(Mock<IDatabase> database,
        Mock<IEasyCachingSerializer> serializer)
    {
        var provider = new Mock<IEasyCachingProvider>();
        provider.SetupGet(value => value.Database).Returns(database.Object);
        provider.Setup(value => value.GetProviderInfo())
            .Returns(new ProviderInfo { Serializer = serializer.Object });
        return provider;
    }

    /// <summary>
    /// 创建包含两个命中值及其过期时间的原始批量响应。
    /// </summary>
    /// <returns>返回供批量解码测试使用的 Redis 响应。</returns>
    private static RedisResult CreateRawBatchResult() => RedisResult.Create(new[]
    {
        RedisResult.Create((RedisValue)new byte[] { 1 }),
        RedisResult.Create((RedisValue)"60000"),
        RedisResult.Create((RedisValue)new byte[] { 2 }),
        RedisResult.Create((RedisValue)"60000")
    });
}
