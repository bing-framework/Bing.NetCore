using System.Net;
using Bing.Caching;
using Bing.EasyCaching;
using EasyCaching.Core;
using EasyCaching.Core.Serialization;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 客户端分片 Redis 节点身份变化测试。
/// </summary>
public sealed class ShardedIdentityUnitTest
{
    /// <summary>
    /// 路由分片的运行标识在构造后变化时读取失败。
    /// </summary>
    [Fact]
    public void Get_FailsWhenRoutedShardRunIdChangesAfterConstruction()
    {
        var configuration = CreateConfiguration();
        var firstId = "first-initial";
        var secondId = "second-initial";
        var firstInfo = new MutableInfo(() => firstId);
        var secondInfo = new MutableInfo(() => secondId);
        var providers = CreateProviders(firstInfo, secondInfo);
        var cache = new ShardedRedisCacheManager(providers, configuration);

        firstId = "first-replaced";
        secondId = "second-replaced";

        var exception = Assert.Throws<InvalidOperationException>(() => cache.Get<int>("identity-change"));

        Assert.Contains("身份已变化", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 任一分片的运行标识在构造后变化时批量读取失败。
    /// </summary>
    [Fact]
    public async Task GetAllAsync_FailsWhenAnyShardRunIdChangesAfterConstruction()
    {
        var configuration = CreateConfiguration();
        var firstId = "first-initial";
        var secondId = "second-initial";
        var firstInfo = new MutableInfo(() => firstId);
        var secondInfo = new MutableInfo(() => secondId);
        var providers = CreateProviders(firstInfo, secondInfo);
        var cache = new ShardedRedisCacheManager(providers, configuration);

        firstId = "first-replaced";
        secondId = "second-replaced";

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.GetAllAsync<int>(new[] { "identity-change-one", "identity-change-two" }));

        Assert.Contains("身份", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 批量读取期间任一分片运行标识变化都会失败。
    /// </summary>
    [Fact]
    public async Task GetAllAsync_FailsWhenShardRunIdChangesDuringRead()
    {
        var configuration = CreateConfiguration();
        var firstId = "first-initial";
        var secondId = "second-initial";
        var firstInfo = new MutableInfo(() => firstId);
        var secondInfo = new MutableInfo(() => secondId);
        var readCalls = 0;
        var providers = CreateProviders(firstInfo, secondInfo, () =>
        {
            Interlocked.Increment(ref readCalls);
            firstId = "first-replaced-during-read";
            secondId = "second-replaced-during-read";
        });
        var cache = new ShardedRedisCacheManager(providers, configuration);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.GetAllAsync<int>(new[] { "identity-read-one", "identity-read-two" }));

        Assert.True(readCalls > 0);
        Assert.Contains("身份", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 读取期间路由分片的运行标识变化会失败。
    /// </summary>
    [Fact]
    public void Get_FailsWhenShardRunIdChangesDuringRead()
    {
        var configuration = CreateConfiguration();
        var firstId = "first-initial";
        var secondId = "second-initial";
        var reads = 0;
        var cache = new ShardedRedisCacheManager(CreateProviders(
            new MutableInfo(() => firstId), new MutableInfo(() => secondId), () =>
            {
                reads++;
                firstId = "first-replaced-during-read";
                secondId = "second-replaced-during-read";
            }), configuration);

        var error = Assert.Throws<InvalidOperationException>(() => cache.Get<int>("single-read"));

        Assert.Equal(1, reads);
        Assert.Contains("身份已变化", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 异步读取期间路由分片的运行标识变化会失败。
    /// </summary>
    [Fact]
    public async Task GetAsync_FailsWhenShardRunIdChangesDuringRead()
    {
        var configuration = CreateConfiguration();
        var firstId = "first-initial";
        var secondId = "second-initial";
        var reads = 0;
        var cache = new ShardedRedisCacheManager(CreateProviders(
            new MutableInfo(() => firstId), new MutableInfo(() => secondId), () =>
            {
                reads++;
                firstId = "first-replaced-during-read";
                secondId = "second-replaced-during-read";
            }), configuration);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.GetAsync<int>("single-read-async"));

        Assert.Equal(1, reads);
        Assert.Contains("身份已变化", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 写入期间路由分片的运行标识变化会失败。
    /// </summary>
    [Fact]
    public void Set_FailsWhenShardRunIdChangesDuringWrite()
    {
        var configuration = CreateConfiguration();
        var firstId = "first-initial";
        var secondId = "second-initial";
        var writes = 0;
        var cache = new ShardedRedisCacheManager(CreateProviders(
            new MutableInfo(() => firstId), new MutableInfo(() => secondId), () =>
            {
                writes++;
                firstId = "first-replaced-during-write";
                secondId = "second-replaced-during-write";
            }), configuration);

        var error = Assert.Throws<InvalidOperationException>(() => cache.Set("single-write", 1));

        Assert.Equal(1, writes);
        Assert.Contains("身份已变化", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 删除期间路由分片的运行标识变化会失败。
    /// </summary>
    [Fact]
    public async Task RemoveAsync_FailsWhenShardRunIdChangesDuringDelete()
    {
        var configuration = CreateConfiguration();
        var firstId = "first-initial";
        var secondId = "second-initial";
        var deletes = 0;
        var cache = new ShardedRedisCacheManager(CreateProviders(
            new MutableInfo(() => firstId), new MutableInfo(() => secondId), () =>
            {
                deletes++;
                firstId = "first-replaced-during-delete";
                secondId = "second-replaced-during-delete";
            }), configuration);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.RemoveAsync("single-delete"));

        Assert.Equal(1, deletes);
        Assert.Contains("身份已变化", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 包含通配符的字面键只路由到其所属分片。
    /// </summary>
    [Fact]
    public void RemoveByPattern_ExactLiteralRoutesToOnlyItsShard()
    {
        var configuration = CreateConfiguration();
        var firstDeletes = 0;
        var secondDeletes = 0;
        var cache = new ShardedRedisCacheManager(new[]
        {
            CreateProvider(new MutableInfo(() => "first"), () => firstDeletes++),
            CreateProvider(new MutableInfo(() => "second"), () => secondDeletes++)
        }, configuration);

        cache.RemoveByPattern("single-literal");

        Assert.Equal(1, firstDeletes + secondDeletes);
    }

    /// <summary>
    /// 异步删除包含通配符的字面键只路由到其所属分片。
    /// </summary>
    [Fact]
    public async Task RemoveByPatternAsync_ExactLiteralRoutesToOnlyItsShard()
    {
        var configuration = CreateConfiguration();
        var firstDeletes = 0;
        var secondDeletes = 0;
        var cache = new ShardedRedisCacheManager(new[]
        {
            CreateProvider(new MutableInfo(() => "first"), () => firstDeletes++),
            CreateProvider(new MutableInfo(() => "second"), () => secondDeletes++)
        }, configuration);

        await cache.RemoveByPatternAsync(@"single\*");

        Assert.Equal(1, firstDeletes + secondDeletes);
    }

    /// <summary>
    /// 创建客户端分片身份测试使用的缓存配置。
    /// </summary>
    /// <returns>返回配置了两个客户端分片的缓存配置。</returns>
    private static CacheConfiguration CreateConfiguration() =>
        new("easy-shard-identity", "redis", batchSize: 2,
            topology: CacheTopology.ClientSharded, expectedShardCount: 2);

    /// <summary>
    /// 创建使用可变节点身份信息的分片 Provider 集合。
    /// </summary>
    /// <param name="firstInfo">第一个节点信息。</param>
    /// <param name="secondInfo">第二个节点信息。</param>
    /// <param name="onCommand">命令回调。</param>
    /// <returns>返回按分片顺序排列的 Provider 集合。</returns>
    private static IReadOnlyList<IEasyCachingProvider> CreateProviders(
        MutableInfo firstInfo, MutableInfo secondInfo, Action onCommand = null)
    {
        return new[]
        {
            CreateProvider(firstInfo, onCommand),
            CreateProvider(secondInfo, onCommand)
        };
    }

    /// <summary>
    /// 创建按命令回调返回节点信息的 Redis Provider 模拟对象。
    /// </summary>
    /// <param name="info">节点信息。</param>
    /// <param name="onCommand">命令回调。</param>
    /// <returns>返回配置了节点信息回调的 Provider 模拟对象。</returns>
    private static IEasyCachingProvider CreateProvider(MutableInfo info, Action onCommand = null)
    {
        var endpoint = new DnsEndPoint("localhost", 6379);
        var server = new Mock<IServer>();
        server.SetupGet(value => value.IsConnected).Returns(true);
        server.SetupGet(value => value.IsReplica).Returns(false);

        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(value => value.GetEndPoints(It.IsAny<bool>()))
            .Returns(new EndPoint[] { endpoint });
        multiplexer.Setup(value => value.GetServer(endpoint, It.IsAny<object>()))
            .Returns(server.Object);

        var database = new Mock<IDatabase>();
        database.SetupGet(value => value.Multiplexer).Returns(multiplexer.Object);
        database.Setup(value => value.Execute(
                "INFO",
                It.Is<object[]>(arguments => arguments.Length == 1 &&
                    string.Equals(arguments[0] == null ? null : arguments[0].ToString(),
                        "SERVER", StringComparison.Ordinal))))
            .Returns(() => RedisResult.Create((RedisValue)info.GetServerInfo()));
        database.Setup(value => value.ExecuteAsync(
                "INFO",
                It.Is<object[]>(arguments => arguments.Length == 1 &&
                    string.Equals(arguments[0] == null ? null : arguments[0].ToString(),
                        "SERVER", StringComparison.Ordinal))))
            .Returns(() => Task.FromResult(RedisResult.Create((RedisValue)info.GetServerInfo())));

        var provider = new Mock<IEasyCachingProvider>();
        var serializer = new Mock<IEasyCachingSerializer>();
        serializer.Setup(value => value.Serialize(It.IsAny<int>())).Returns(new byte[] { (byte)'1' });
        serializer.Setup(value => value.Deserialize<int>(It.IsAny<byte[]>())).Returns(1);
        provider.Setup(value => value.GetProviderInfo()).Returns(new ProviderInfo { Serializer = serializer.Object });
        database.Setup(value => value.StringGetAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey[] keys, CommandFlags _) =>
            {
                onCommand?.Invoke();
                return Task.FromResult(keys.Select(_ => (RedisValue)"1").ToArray());
            });
        database.Setup(value => value.ScriptEvaluate(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .Returns(() => { onCommand?.Invoke(); return RedisResult.Create((RedisValue)"OK"); });
        database.Setup(value => value.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .Returns(() => { onCommand?.Invoke(); return Task.FromResult(1L); });
        database.Setup(value => value.KeyDelete(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .Returns(() => { onCommand?.Invoke(); return 1L; });
        provider.SetupGet(value => value.Database).Returns(database.Object);
        provider.Setup(value => value.Get<int>(It.IsAny<string>()))
            .Returns(() =>
            {
                onCommand?.Invoke();
                return new CacheValue<int>(1, true);
            });
        provider.Setup(value => value.GetAsync<int>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                onCommand?.Invoke();
                return Task.FromResult(new CacheValue<int>(1, true));
            });
        provider.Setup(value => value.GetAllAsync<int>(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> keys, CancellationToken _) =>
            {
                onCommand?.Invoke();
                IDictionary<string, CacheValue<int>> values = keys.ToDictionary(
                    key => key,
                    _ => new CacheValue<int>(1, true));
                return Task.FromResult(values);
            });
        provider.Setup(value => value.Set(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>()))
            .Callback(() => onCommand?.Invoke());
        provider.Setup(value => value.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                onCommand?.Invoke();
                return Task.CompletedTask;
            });
        provider.Setup(value => value.Remove(It.IsAny<string>()))
            .Callback(() => onCommand?.Invoke());
        return provider.Object;
    }

    /// <summary>
    /// 保存可变的分片节点运行标识信息。
    /// </summary>
    private sealed class MutableInfo
    {
        /// <summary>
        /// 生成当前节点运行标识的委托。
        /// </summary>
        private readonly Func<string> _id;

        /// <summary>
        /// 初始化一个 <see cref="MutableInfo"/> 类型的实例。
        /// </summary>
        /// <param name="id">生成节点运行标识的委托。</param>
        public MutableInfo(Func<string> id) => _id = id;

        /// <summary>
        /// 返回包含当前运行标识的 Redis INFO SERVER 响应。
        /// </summary>
        /// <returns>包含当前运行标识的 SERVER 信息文本。</returns>
        public string GetServerInfo() => "# Server\r\nrun_id:" + _id() + "\r\n";
    }
}
