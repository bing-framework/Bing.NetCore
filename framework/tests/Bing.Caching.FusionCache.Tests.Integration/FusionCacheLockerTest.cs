using System.Diagnostics;
using System.Collections.Concurrent;
using Bing.Caching.FusionCache.Probe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Locking.Distributed;

namespace Bing.Caching.FusionCache.Tests.Integration;

/// <summary>
/// 验证 Redis 分布式锁在竞争超时和权限失败下的回源与恢复行为。
/// </summary>
public class FusionCacheLockerTest
{
    /// <summary>
    /// 验证锁等待超时后回退到回源工厂，且释放后的锁可再次获取。
    /// </summary>
    [RedisFact]
    public async Task LockTimeoutFallsBackToFactory_EvenWithRethrow_AndReleasedLockCanBeAcquiredAgain()
    {
        var ns = RedisTestSupport.NewNamespace();
        var lockAbandonTimeout = TimeSpan.Zero;
        var locker = new RecordingDistributedLocker();
        using var first = RedisTestHost.Create(ns, distributedLock: true,
            configureLocker: o => lockAbandonTimeout = o.AbandonTimeout,
            decorateLocker: locker.Wrap);
        // 2.9.0 的 AbandonTimeout 为只读配置；从实际实例读取，重获必须在其一半时间内完成。
        var maximumReacquireAge = lockAbandonTimeout / 2;
        Assert.True(maximumReacquireAge > TimeSpan.FromSeconds(5));
        using var second = RedisTestHost.Create(ns, distributedLock: true, configure: o =>
        {
            o.DefaultEntryOptions.DistributedLockTimeout = TimeSpan.FromMilliseconds(80);
            o.DefaultEntryOptions.ReThrowDistributedLockerExceptions = true;
        });
        var a = first.GetRequiredService<IHybridCache>();
        var b = second.GetRequiredService<IHybridCache>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstCalls = 0;
        var secondCalls = 0;
        var held = a.GetOrCreateAsync("contended", async _ =>
        {
            Interlocked.Increment(ref firstCalls);
            entered.SetResult();
            await release.Task;
            return 1;
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var watch = Stopwatch.StartNew();
            Assert.Equal(2, await b.GetOrCreateAsync("contended", _ => { secondCalls++; return Task.FromResult(2); }));
            Assert.InRange(watch.ElapsedMilliseconds, 50, 5000);
            Assert.Equal(1, firstCalls);
            Assert.Equal(1, secondCalls);

            var heldLock = Assert.Single(locker.Acquisitions, acquisition => acquisition.Handle is not null);
            Assert.False(string.IsNullOrWhiteSpace(heldLock.LockName));
            Assert.DoesNotContain(locker.Releases, releaseEntry => ReferenceEquals(releaseEntry.Handle, heldLock.Handle));
        }
        finally { release.TrySetResult(); await held.WaitAsync(TimeSpan.FromSeconds(5)); }

        var acquiredLock = Assert.Single(locker.Acquisitions, acquisition => acquisition.Handle is not null);
        var releaseEntry = Assert.Single(locker.Releases, entry => ReferenceEquals(entry.Handle, acquiredLock.Handle));
        Assert.Equal(acquiredLock.LockName, releaseEntry.LockName);
        Assert.True(Stopwatch.GetElapsedTime(acquiredLock.Timestamp) < maximumReacquireAge,
            "The same lock must be reacquired before its Redis abandonment expiry.");

        // 直接读取官方获取结果，避免缓存命中或拿锁超时后的回源降级造成假通过。
        using var reacquireTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var reacquiredHandle = await locker.AcquireLockAsync(acquiredLock.CacheName, acquiredLock.CacheInstanceId,
            "post-release-reacquire", acquiredLock.Key, acquiredLock.LockName, TimeSpan.FromSeconds(5), null,
            reacquireTimeout.Token);
        Assert.NotNull(reacquiredHandle);
        try
        {
            Assert.True(Stopwatch.GetElapsedTime(acquiredLock.Timestamp) < maximumReacquireAge,
                "Reacquisition must complete before the original lock can expire naturally.");
            var reacquiredLock = Assert.Single(locker.Acquisitions, entry => entry.OperationId == "post-release-reacquire");
            Assert.Equal(acquiredLock.LockName, reacquiredLock.LockName);
            Assert.Same(reacquiredHandle, reacquiredLock.Handle);
        }
        finally
        {
            await locker.ReleaseLockAsync(acquiredLock.CacheName, acquiredLock.CacheInstanceId,
                "post-release-reacquire", acquiredLock.Key, acquiredLock.LockName, reacquiredHandle, null,
                CancellationToken.None);
        }

        await a.RemoveAsync("contended");
    }

    /// <summary>
    /// 验证锁权限失败时按严格或容错配置分别抛出或降级，并在恢复权限后继续工作。
    /// </summary>
    [RedisFact]
    public async Task OfficialLockerPermissionFailure_ThrowsOrDegradesAsConfigured_ThenRecovers()
    {
        var ns = RedisTestSupport.NewNamespace();
        using var inspector = RedisTestHost.Create(ns + ":admin");
        var admin = inspector.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var user = "fc-lock-" + Guid.NewGuid().ToString("N");
        var password = Guid.NewGuid().ToString("N");
        await admin.ExecuteAsync("ACL", "SETUSER", user, "on", ">" + password, "~*", "+@all");
        try
        {
            foreach (var strict in new[] { false, true })
            {
                using var provider = RedisTestHost.Create(ns + strict, distributedLock: true, failSafe: true,
                    configure: o => o.DefaultEntryOptions.ReThrowDistributedLockerExceptions = strict,
                    configureLocker: o =>
                    {
                        var options = ConfigurationOptions.Parse(o.Configuration!);
                        options.User = user;
                        options.Password = password;
                        o.ConfigurationOptions = options;
                    });
                var cache = provider.GetRequiredService<IHybridCache>();
                Assert.Equal(1, await cache.GetOrCreateAsync("warm", _ => Task.FromResult(1)));
                await cache.SetAsync("stale", 7);
                await cache.ExpireAsync("stale");
                // 仅撤销 locker 专属连接权限；L2/Backplane 使用不同连接，继续工作。
                await admin.ExecuteAsync("ACL", "SETUSER", user, "-@all");
                var calls = 0;
                try
                {
                    if (strict)
                    {
                        var error = await Assert.ThrowsAsync<FusionCacheDistributedLockerException>(() =>
                            cache.GetOrCreateAsync("stale", _ => { calls++; return Task.FromResult(9); }));
                        Assert.IsType<RedisServerException>(error.InnerException);
                        Assert.Equal(0, calls);
                    }
                    else
                    {
                        Assert.Equal(9, await cache.GetOrCreateAsync("cold", _ => { calls++; return Task.FromResult(9); }));
                        Assert.Equal(7, await cache.GetOrCreateAsync<int>("stale", _ => { calls++; throw new FormatException("origin failed"); }));
                        Assert.Equal(2, calls);
                    }
                }
                finally { await admin.ExecuteAsync("ACL", "SETUSER", user, "+@all"); }
                Assert.Equal(11, await cache.GetOrCreateAsync("recovered", _ => Task.FromResult(11)));
            }
        }
        finally { await admin.ExecuteAsync("ACL", "DELUSER", user); }
    }

    /// <summary>
    /// 透明记录官方 locker 的实际获取与释放结果，不改变锁行为，也不取得组件所有权。
    /// </summary>
    private sealed class RecordingDistributedLocker : IFusionCacheDistributedLocker
    {
        private IFusionCacheDistributedLocker? _inner;

        public ConcurrentQueue<LockObservation> Acquisitions { get; } = new();

        public ConcurrentQueue<LockObservation> Releases { get; } = new();

        public IFusionCacheDistributedLocker Wrap(IFusionCacheDistributedLocker inner)
        {
            _inner = inner;
            return this;
        }

        public async ValueTask<object?> AcquireLockAsync(string cacheName, string cacheInstanceId, string operationId,
            string key, string lockName, TimeSpan timeout, ILogger? logger, CancellationToken token)
        {
            var startedAt = Stopwatch.GetTimestamp();
            var handle = await Inner.AcquireLockAsync(cacheName, cacheInstanceId, operationId, key, lockName,
                timeout, logger, token);
            Acquisitions.Enqueue(new LockObservation(cacheName, cacheInstanceId, operationId, key, lockName, handle,
                startedAt));
            return handle;
        }

        public object? AcquireLock(string cacheName, string cacheInstanceId, string operationId, string key,
            string lockName, TimeSpan timeout, ILogger? logger, CancellationToken token)
        {
            var startedAt = Stopwatch.GetTimestamp();
            var handle = Inner.AcquireLock(cacheName, cacheInstanceId, operationId, key, lockName, timeout, logger,
                token);
            Acquisitions.Enqueue(new LockObservation(cacheName, cacheInstanceId, operationId, key, lockName, handle,
                startedAt));
            return handle;
        }

        public async ValueTask ReleaseLockAsync(string cacheName, string cacheInstanceId, string operationId,
            string key, string lockName, object? lockObj, ILogger? logger, CancellationToken token)
        {
            await Inner.ReleaseLockAsync(cacheName, cacheInstanceId, operationId, key, lockName, lockObj, logger,
                token);
            Releases.Enqueue(new LockObservation(cacheName, cacheInstanceId, operationId, key, lockName, lockObj,
                Stopwatch.GetTimestamp()));
        }

        public void ReleaseLock(string cacheName, string cacheInstanceId, string operationId, string key,
            string lockName, object? lockObj, ILogger? logger, CancellationToken token)
        {
            Inner.ReleaseLock(cacheName, cacheInstanceId, operationId, key, lockName, lockObj, logger, token);
            Releases.Enqueue(new LockObservation(cacheName, cacheInstanceId, operationId, key, lockName, lockObj,
                Stopwatch.GetTimestamp()));
        }

        private IFusionCacheDistributedLocker Inner => _inner
            ?? throw new InvalidOperationException("The Redis locker has not been attached.");
    }

    private sealed record LockObservation(string CacheName, string CacheInstanceId, string OperationId, string Key,
        string LockName, object? Handle, long Timestamp);
}
