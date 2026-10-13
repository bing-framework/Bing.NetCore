using Bing.Caching;
using EasyCaching.Core;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 验证 Redis 租约协调器在工厂和释放均失败时保留主异常。
/// </summary>
public class RedisLeaseCleanupTest
{
    /// <summary>
    /// 工厂失败时释放租约失败不会替换同步或异步主异常。
    /// </summary>
    [Fact]
    public async Task FactoryFault_ReleaseFailureDoesNotReplacePrimaryAsyncOrSync()
    {
        var database = new Mock<IDatabase>();
        var provider = new Mock<IEasyCachingProvider>();
        provider.SetupGet(value => value.Database).Returns(database.Object);
        provider.Setup(value => value.Get<int>("item")).Returns(new CacheValue<int>(0, false));
        provider.Setup(value => value.GetAsync<int>("item", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CacheValue<int>(0, false));
        database.Setup(value => value.StringSet(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(), When.NotExists, CommandFlags.None)).Returns(true);
        database.Setup(value => value.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(), When.NotExists, CommandFlags.None)).ReturnsAsync(true);
        var release = new IOException("release unavailable");
        database.Setup(value => value.ScriptEvaluate(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(), CommandFlags.None)).Throws(release);
        database.Setup(value => value.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(), CommandFlags.None)).ThrowsAsync(release);
        var coordinator = new RedisLeaseCoordinator(provider.Object, new CacheConfiguration("app", "redis"));
        var primary = new InvalidOperationException("factory failed");
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.GetOrCreateAsync<int>("item",
            _ => Task.FromException<int>(primary), TimeSpan.FromMinutes(1), CancellationToken.None));
        Assert.Same(primary, actual);
        Assert.Same(release, actual.Data["CacheCleanupException"]);
        actual = Assert.Throws<InvalidOperationException>(() => coordinator.GetOrCreate<int>("item",
            () => throw primary, TimeSpan.FromMinutes(1)));
        Assert.Same(primary, actual);
        Assert.Same(release, actual.Data["CacheCleanupException"]);
    }
}
