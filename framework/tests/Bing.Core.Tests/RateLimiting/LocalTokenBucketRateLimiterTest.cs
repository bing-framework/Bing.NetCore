using Bing.RateLimiting;
using Bing.Timing;

namespace Bing.Tests.RateLimiting;

/// <summary>
/// 验证进程内令牌桶的额度、并发及键生命周期。
/// </summary>
public class LocalTokenBucketRateLimiterTest
{
    /// <summary>
    /// 取消的异步请求不消耗额度；成功请求与同步入口共用额度。
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_CancelledRequest_ShouldNotConsumePermit()
    {
        ITokenBucketRateLimiter limiter = Create(new FakeClock(), capacity: 1);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            limiter.TryAcquireAsync("shared", cancellationToken: cancellation.Token));
        (await limiter.TryAcquireAsync("shared")).IsAllowed.ShouldBeTrue();
        limiter.TryAcquire("shared").IsAllowed.ShouldBeFalse();
    }

    /// <summary>
    /// 验证突发额度及令牌补充后的重试时间。
    /// </summary>
    [Fact]
    public void TryAcquire_BurstAndRefill_ReturnsPreciseRetryAfter()
    {
        var clock = new FakeClock();
        ITokenBucketRateLimiter limiter = Create(clock, capacity: 3, refill: 2);

        limiter.TryAcquire("tenant-a", 3).IsAllowed.ShouldBeTrue();
        var denied = limiter.TryAcquire("tenant-a");
        denied.IsAllowed.ShouldBeFalse();
        denied.RetryAfter.ShouldBe(TimeSpan.FromMilliseconds(500));

        clock.Advance(TimeSpan.FromMilliseconds(250));
        limiter.TryAcquire("tenant-a").RetryAfter.ShouldBe(TimeSpan.FromMilliseconds(250));
        clock.Advance(TimeSpan.FromMilliseconds(250));
        limiter.TryAcquire("tenant-a").IsAllowed.ShouldBeTrue();
        limiter.TryAcquire("tenant-a").IsAllowed.ShouldBeFalse();
    }

    /// <summary>
    /// 闲置但尚未补满的桶不能通过回收提前恢复额度。
    /// </summary>
    [Fact]
    public void PruneIdle_ExhaustedBucket_ShouldRetainQuotaUntilRefilled()
    {
        var clock = new FakeClock();
        var limiter = new LocalTokenBucketRateLimiter(1, 0.01, TimeSpan.FromSeconds(1), 1, clock);
        limiter.TryAcquire("same").IsAllowed.ShouldBeTrue();

        clock.Advance(TimeSpan.FromSeconds(2));
        limiter.PruneIdle().ShouldBe(0);
        limiter.TryAcquire("same").IsAllowed.ShouldBeFalse();
        limiter.TryAcquire("other").IsAllowed.ShouldBeFalse();

        clock.Advance(TimeSpan.FromSeconds(100));
        limiter.PruneIdle().ShouldBe(1);
        limiter.TryAcquire("same").IsAllowed.ShouldBeTrue();
    }

    /// <summary>
    /// 验证并发请求不会超过可用额度。
    /// </summary>
    [Fact]
    public void TryAcquire_ConcurrentCalls_GrantOnlyAvailableTokens()
    {
        var limiter = Create(new FakeClock(), capacity: 20);
        var allowed = 0;

        Parallel.For(0, 200, _ =>
        {
            if (limiter.TryAcquire("same-key").IsAllowed)
                Interlocked.Increment(ref allowed);
        });

        allowed.ShouldBe(20);
    }

    /// <summary>
    /// 验证不同限流键互相独立且键数受限。
    /// </summary>
    [Fact]
    public void TryAcquire_KeysAreIndependent_AndMaximumKeysIsBounded()
    {
        var clock = new FakeClock();
        var limiter = Create(clock, capacity: 1, maxKeys: 2);

        limiter.TryAcquire("tenant-a").IsAllowed.ShouldBeTrue();
        limiter.TryAcquire("tenant-b").IsAllowed.ShouldBeTrue();
        limiter.TryAcquire("tenant-a").IsAllowed.ShouldBeFalse();
        var overflow = limiter.TryAcquire("tenant-c");
        overflow.IsAllowed.ShouldBeFalse();
        overflow.RetryAfter.ShouldBe(TimeSpan.FromMinutes(1));
        limiter.KeyCount.ShouldBe(2);

        clock.Advance(TimeSpan.FromMinutes(1));
        limiter.TryAcquire("tenant-c").IsAllowed.ShouldBeTrue();
        limiter.KeyCount.ShouldBe(1);
    }

    /// <summary>
    /// 验证最近访问的键不会被闲置回收。
    /// </summary>
    [Fact]
    public void PruneIdle_DoesNotRemoveRecentlyUsedKey()
    {
        var clock = new FakeClock();
        var limiter = Create(clock, capacity: 1);
        limiter.TryAcquire("old");
        limiter.TryAcquire("recent");

        clock.Advance(TimeSpan.FromSeconds(50));
        limiter.TryAcquire("recent");
        clock.Advance(TimeSpan.FromSeconds(10));

        limiter.PruneIdle().ShouldBe(1);
        limiter.KeyCount.ShouldBe(1);
        limiter.TryAcquire("recent").IsAllowed.ShouldBeTrue();
    }

    /// <summary>
    /// 验证时钟回拨不会生成额外令牌。
    /// </summary>
    [Fact]
    public void TryAcquire_ClockMovesBack_DoesNotMintTokens()
    {
        var clock = new FakeClock();
        var limiter = Create(clock, capacity: 1);
        limiter.TryAcquire("key").IsAllowed.ShouldBeTrue();

        clock.Advance(TimeSpan.FromSeconds(-10));
        limiter.TryAcquire("key").IsAllowed.ShouldBeFalse();
        clock.Advance(TimeSpan.FromSeconds(10));
        limiter.TryAcquire("key").IsAllowed.ShouldBeFalse();
    }

    /// <summary>
    /// 验证无效配置和请求额度会立即失败。
    /// </summary>
    [Fact]
    public void TryAcquire_InvalidConfigurationAndPermits_FailFast()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Create(new FakeClock(), capacity: 0));
        Should.Throw<ArgumentOutOfRangeException>(() => Create(new FakeClock(), refill: double.NaN));
        Should.Throw<ArgumentOutOfRangeException>(() => Create(new FakeClock(), maxKeys: 0));
        var limiter = Create(new FakeClock(), capacity: 1);
        Should.Throw<ArgumentException>(() => limiter.TryAcquire(" "));
        Should.Throw<ArgumentOutOfRangeException>(() => limiter.TryAcquire("key", 2));
    }

    /// <summary>
    /// 验证极低补充速率会安全限制重试时间。
    /// </summary>
    [Fact]
    public void TryAcquire_VeryLowRefillRate_SaturatesRetryAfter()
    {
        var limiter = Create(new FakeClock(), refill: double.Epsilon);
        limiter.TryAcquire("key").IsAllowed.ShouldBeTrue();
        limiter.TryAcquire("key").RetryAfter.ShouldBe(TimeSpan.MaxValue);
    }

    /// <summary>
    /// 创建使用可控时钟的本地限流器。
    /// </summary>
    /// <param name="clock">可控时钟。</param>
    /// <param name="capacity">令牌容量。</param>
    /// <param name="refill">每秒补充令牌数。</param>
    /// <param name="maxKeys">限流键数上限。</param>
    /// <returns>配置完成的限流器。</returns>
    private static LocalTokenBucketRateLimiter Create(FakeClock clock, int capacity = 1,
        double refill = 1, int maxKeys = 100) =>
        new(capacity, refill, TimeSpan.FromMinutes(1), maxKeys, clock);

    /// <summary>
    /// 为限流器测试提供可控时钟。
    /// </summary>
    private sealed class FakeClock : IClock
    {
        /// <summary>
        /// 可控的 UTC 当前时间。
        /// </summary>
        private DateTime _utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <inheritdoc />
        public DateTime Now => _utcNow.ToLocalTime();
        /// <inheritdoc />
        public DateTime UtcNow => _utcNow;
        /// <inheritdoc />
        public DateTimeOffset NowOffset => new(_utcNow);

        /// <summary>
        /// 推进可控时钟。
        /// </summary>
        /// <param name="interval">时间变化量。</param>
        public void Advance(TimeSpan interval) => _utcNow = _utcNow.Add(interval);
    }
}
