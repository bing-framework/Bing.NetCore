using Bing.Locks;
using Shouldly;
using System.Reflection;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// 验证 Redis 分布式锁的租约与持有者规则。
/// </summary>
public class RedisDistributedLockTest
{
    /// <summary>
    /// 并发获取同一锁时只能有一个持有者。
    /// </summary>
    [Fact]
    public async Task LockTakeAsync_ConcurrentOwners_ShouldHaveOneWinner()
    {
        var key = $"bing-lock-test:{Guid.NewGuid():N}";
        var locks = Enumerable.Range(0, 8).Select(_ => new CSRedisDistributedLock()).ToArray();
        var results = await Task.WhenAll(locks.Select((item, index) => item.LockTakeAsync(key, $"owner-{index}", TimeSpan.FromSeconds(5))));

        results.Count(result => result).ShouldBe(1);
        var winner = Array.IndexOf(results, true);
        (await locks[winner].LockReleaseAsync(key, $"owner-{winner}")).ShouldBeTrue();
    }

    /// <summary>
    /// 错误持有者以及过期租约均不能释放新锁。
    /// </summary>
    [Fact]
    public async Task LockReleaseAsync_ShouldCheckOwnerAfterExpiry()
    {
        var key = $"bing-lock-test:{Guid.NewGuid():N}";
        var first = new CSRedisDistributedLock();
        var second = new CSRedisDistributedLock();

        (await first.LockTakeAsync(key, "old", TimeSpan.FromMilliseconds(80))).ShouldBeTrue();
        (await second.LockReleaseAsync(key, "wrong")).ShouldBeFalse();
        await Task.Delay(160);
        (await second.LockTakeAsync(key, "new", TimeSpan.FromSeconds(5))).ShouldBeTrue();
        (await first.LockReleaseAsync(key, "old")).ShouldBeFalse();
        (await second.LockTakeAsync(key, "third", TimeSpan.FromSeconds(5))).ShouldBeFalse();
        (await second.LockReleaseAsync(key, "new")).ShouldBeTrue();
    }

    /// <summary>
    /// 租期使用完整时长而不是秒分量。
    /// </summary>
    [Fact]
    public async Task LockTakeAsync_LeaseOverMinute_ShouldRemainHeld()
    {
        var key = $"bing-lock-test:{Guid.NewGuid():N}";
        var @lock = new CSRedisDistributedLock();

        (await @lock.LockTakeAsync(key, "long", TimeSpan.FromSeconds(65))).ShouldBeTrue();
        (await @lock.LockTakeAsync(key, "other", TimeSpan.FromSeconds(2))).ShouldBeFalse();
        (await @lock.LockReleaseAsync(key, "long")).ShouldBeTrue();
    }

    /// <summary>
    /// 同步及异步执行失败后仍应释放对应租约。
    /// </summary>
    [Fact]
    public async Task ExecuteWithLock_WhenCallbackFails_ShouldReleaseLease()
    {
        var key = $"bing-lock-test:{Guid.NewGuid():N}";
        var @lock = new CSRedisDistributedLock();

        Should.Throw<InvalidOperationException>(() =>
            @lock.ExecuteWithLock(key, "ignored", TimeSpan.FromSeconds(5), () => throw new InvalidOperationException("sync")));
        @lock.LockTake(key, "next", TimeSpan.FromSeconds(5)).ShouldBeTrue();
        @lock.LockRelease(key, "next").ShouldBeTrue();

        await Should.ThrowAsync<InvalidOperationException>(() =>
            @lock.ExecuteWithLockAsync(key, "ignored", TimeSpan.FromSeconds(5), () => Task.FromException(new InvalidOperationException("async"))));
        (await @lock.LockTakeAsync(key, "last", TimeSpan.FromSeconds(5))).ShouldBeTrue();
        (await @lock.LockReleaseAsync(key, "last")).ShouldBeTrue();

        Should.Throw<InvalidOperationException>(() =>
            @lock.ExecuteWithLock<int>(key, "ignored", TimeSpan.FromSeconds(5), () => throw new InvalidOperationException("sync result")));
        @lock.LockTake(key, "result-next", TimeSpan.FromSeconds(5)).ShouldBeTrue();
        @lock.LockRelease(key, "result-next").ShouldBeTrue();

        await Should.ThrowAsync<InvalidOperationException>(() =>
            @lock.ExecuteWithLockAsync<int>(key, "ignored", TimeSpan.FromSeconds(5), () => Task.FromException<int>(new InvalidOperationException("async result"))));
        (await @lock.LockTakeAsync(key, "result-last", TimeSpan.FromSeconds(5))).ShouldBeTrue();
        (await @lock.LockReleaseAsync(key, "result-last")).ShouldBeTrue();
    }

    /// <summary>
    /// 执行锁回调使用调用方提供的持有者值释放锁。
    /// </summary>
    [Fact]
    public async Task ExecuteWithLock_ShouldHonorProvidedOwner()
    {
        var key = $"bing-lock-test:{Guid.NewGuid():N}";
        var @lock = new CSRedisDistributedLock();
        var owner = $"owner-{Guid.NewGuid():N}";

        @lock.ExecuteWithLock(key, owner, TimeSpan.FromSeconds(5), () => { });
        @lock.LockTake(key, "next", TimeSpan.FromSeconds(1)).ShouldBeTrue();
        @lock.LockRelease(key, "next").ShouldBeTrue();

        await @lock.ExecuteWithLockAsync(key, owner, TimeSpan.FromSeconds(5), () => Task.CompletedTask);
        (await @lock.LockTakeAsync(key, "last", TimeSpan.FromSeconds(1))).ShouldBeTrue();
        (await @lock.LockReleaseAsync(key, "last")).ShouldBeTrue();
    }

    /// <summary>
    /// 同步释放失败应附在回调异常中，供调用方同时定位两次失败。
    /// </summary>
    [Xunit.Fact]
    public void ExecuteWithLock_WhenCallbackAndReleaseFail_ShouldKeepCallbackError()
    {
        var callbackError = new InvalidOperationException("callback");
        var releaseError = new ApplicationException("release");
        var method = typeof(CSRedisDistributedLock).GetMethod("ReleaseAfterCallbackError", BindingFlags.NonPublic | BindingFlags.Static);
        method.ShouldNotBeNull();

        method.Invoke(null, new object[] { callbackError, (Action)(() => throw releaseError) });

        callbackError.Data["Bing.LockReleaseError"].ShouldBeSameAs(releaseError);
    }

    /// <summary>
    /// 异步释放失败应附在回调异常中，供调用方同时定位两次失败。
    /// </summary>
    [Xunit.Fact]
    public async Task ExecuteWithLockAsync_WhenCallbackAndReleaseFail_ShouldKeepCallbackError()
    {
        var callbackError = new InvalidOperationException("callback");
        var releaseError = new ApplicationException("release");
        var method = typeof(CSRedisDistributedLock).GetMethod("ReleaseAfterCallbackErrorAsync", BindingFlags.NonPublic | BindingFlags.Static);
        method.ShouldNotBeNull();

        var task = (Task)method.Invoke(null, new object[] { callbackError, (Func<Task<bool>>)(() => Task.FromException<bool>(releaseError)) });
        await task;

        callbackError.Data["Bing.LockReleaseError"].ShouldBeSameAs(releaseError);
    }

    /// <summary>
    /// 真实 Redis 释放失败时保留同步回调异常。
    /// </summary>
    [Fact]
    public void ExecuteWithLock_WhenRedisReleaseFails_ShouldThrowOriginalError()
    {
        var key = $"bing-lock-test:{Guid.NewGuid():N}";
        var callbackError = new InvalidOperationException("business failed");
        try
        {
            var thrown = Should.Throw<InvalidOperationException>(() => new CSRedisDistributedLock().ExecuteWithLock(
                key, "ignored", TimeSpan.FromSeconds(5), () =>
                {
                    RedisHelper.Eval("redis.call('DEL',KEYS[1]);redis.call('HSET',KEYS[1],'field','value')", key);
                    throw callbackError;
                }));

            thrown.ShouldBeSameAs(callbackError);
            (thrown.Data["Bing.LockReleaseError"] is Exception).ShouldBeTrue();
        }
        finally
        {
            RedisHelper.Del(key);
        }
    }

    /// <summary>
    /// 真实 Redis 释放失败时保留异步回调异常。
    /// </summary>
    [Fact]
    public async Task ExecuteWithLockAsync_WhenRedisReleaseFails_ShouldThrowOriginalError()
    {
        var key = $"bing-lock-test:{Guid.NewGuid():N}";
        var callbackError = new InvalidOperationException("business failed");
        try
        {
            var thrown = await Should.ThrowAsync<InvalidOperationException>(() => new CSRedisDistributedLock().ExecuteWithLockAsync(
                key, "ignored", TimeSpan.FromSeconds(5), async () =>
                {
                    await RedisHelper.EvalAsync("redis.call('DEL',KEYS[1]);redis.call('HSET',KEYS[1],'field','value')", key);
                    throw callbackError;
                }));

            thrown.ShouldBeSameAs(callbackError);
            (thrown.Data["Bing.LockReleaseError"] is Exception).ShouldBeTrue();
        }
        finally
        {
            RedisHelper.Del(key);
        }
    }

    /// <summary>
    /// Redis 租约区分等待期限与锁租期并支持取消。
    /// </summary>
    [Fact]
    public async Task TryAcquireLeaseAsync_ShouldWaitAndCancel()
    {
        var key = $"bing-lock-test:{Guid.NewGuid():N}";
        var @lock = new CSRedisDistributedLock();
        var first = await @lock.TryAcquireLeaseAsync(key, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        first.ShouldNotBeNull();
        (await @lock.TryAcquireLeaseAsync(key, TimeSpan.Zero, TimeSpan.FromSeconds(5))).ShouldBeNull();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() =>
            @lock.TryAcquireLeaseAsync(key, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), cancellation.Token));

        (await first.ReleaseAsync()).ShouldBeTrue();
        first.Release().ShouldBeFalse();
        var second = await @lock.TryAcquireLeaseAsync(key, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
        second.ShouldNotBeNull();
        await second.DisposeAsync();
    }

}
