using Bing.Helpers;

namespace Bing.Locks;

/// <summary>
/// 基于CSRedis实现的分布式锁
/// </summary>
// ReSharper disable once InconsistentNaming
public class CSRedisDistributedLock : IDistributedLock, ILockLeaseProvider
{
    /// <summary>
    /// 未显式指定时使用的默认 Redis 租期。
    /// </summary>
    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 原子获取具有到期时间的锁。
    /// </summary>
    private const string AcquireScript = "return redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[2], 'NX')";

    /// <summary>
    /// 仅由持有者释放锁。
    /// </summary>
    private const string ReleaseScript = "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('DEL', KEYS[1]) else return 0 end";

    /// <summary>
    /// 保存 Redis 租约并确保只释放一次。
    /// </summary>
    private sealed class RedisLockLease : ILockLease
    {
        /// <summary>
        /// 用于释放租约的锁实例。
        /// </summary>
        private readonly CSRedisDistributedLock _lock;

        /// <summary>
        /// 释放状态。
        /// </summary>
        private int _released;

        /// <summary>
        /// 初始化一个 <see cref="RedisLockLease" /> 类型的实例。
        /// </summary>
        internal RedisLockLease(CSRedisDistributedLock @lock, string key, string owner)
        {
            _lock = @lock;
            Key = key;
            Owner = owner;
        }

        /// <inheritdoc />
        public string Key { get; }

        /// <inheritdoc />
        public string Owner { get; }

        /// <inheritdoc />
        public bool Release()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0)
                return false;
            try
            {
                return _lock.LockRelease(Key, Owner);
            }
            catch
            {
                Interlocked.Exchange(ref _released, 0);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ReleaseAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0)
                return false;
            try
            {
                return await _lock.LockReleaseAsync(Key, Owner).ConfigureAwait(false);
            }
            catch
            {
                Interlocked.Exchange(ref _released, 0);
                throw;
            }
        }

        /// <inheritdoc />
        public void Dispose() => Release();

        /// <inheritdoc />
        public async ValueTask DisposeAsync() => await ReleaseAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 在指定等待时间内获取有期限的 Redis 锁租约。
    /// </summary>
    /// <param name="key">锁键。</param>
    /// <param name="waitTimeout">最长等待时间。</param>
    /// <param name="leaseDuration">锁在 Redis 中的租期。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功时返回租约，超时时返回 null。</returns>
    public async Task<ILockLease> TryAcquireLeaseAsync(string key, TimeSpan waitTimeout, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        Check.NotNull(key, nameof(key));
        GetLeaseMilliseconds(leaseDuration);
        if (waitTimeout < TimeSpan.Zero && waitTimeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(waitTimeout));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var owner = Guid.NewGuid().ToString("N");
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await LockTakeAsync(key, owner, leaseDuration).ConfigureAwait(false))
                return new RedisLockLease(this, key, owner);

            var remaining = waitTimeout == Timeout.InfiniteTimeSpan
                ? Timeout.InfiniteTimeSpan
                : waitTimeout - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
                return null;
            var delay = remaining == Timeout.InfiniteTimeSpan || remaining > TimeSpan.FromMilliseconds(50)
                ? TimeSpan.FromMilliseconds(50)
                : remaining;
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        } while (true);
    }

    /// <inheritdoc />
    Task<ILockLease> ILockLeaseProvider.TryAcquireLeaseAsync(string key, TimeSpan waitTimeout,
        TimeSpan? leaseDuration, CancellationToken cancellationToken) =>
        TryAcquireLeaseAsync(key, waitTimeout, leaseDuration ?? DefaultLeaseDuration, cancellationToken);

    /// <summary>
    /// 获取一个锁（需要自己释放）
    /// </summary>
    /// <param name="key">锁定标识</param>
    /// <param name="value">当前占用值</param>
    /// <param name="expiration">锁定时间间隔</param>
    /// <returns>true:成功锁定; false:之前已被锁定</returns>
    public bool LockTake(string key, string value, TimeSpan expiration)
    {
        Check.NotNull(key, nameof(key));
        Check.NotNull(value, nameof(value));
        return string.Equals(Convert.ToString(RedisHelper.Eval(AcquireScript, key, value, GetLeaseMilliseconds(expiration))), "OK", StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public async Task<bool> LockTakeAsync(string key, string value, TimeSpan expiration)
    {
        Check.NotNull(key, nameof(key));
        Check.NotNull(value, nameof(value));
        return string.Equals(Convert.ToString(await RedisHelper.EvalAsync(AcquireScript, key, value, GetLeaseMilliseconds(expiration)).ConfigureAwait(false)), "OK", StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public bool LockRelease(string key, string value)
    {
        Check.NotNull(key, nameof(key));
        Check.NotNull(value, nameof(value));
        return Convert.ToInt64(RedisHelper.Eval(ReleaseScript, key, value)) == 1;
    }

    /// <inheritdoc />
    public async Task<bool> LockReleaseAsync(string key, string value)
    {
        Check.NotNull(key, nameof(key));
        Check.NotNull(value, nameof(value));
        return Convert.ToInt64(await RedisHelper.EvalAsync(ReleaseScript, key, value).ConfigureAwait(false)) == 1;
    }

    /// <inheritdoc />
    public void ExecuteWithLock(string key, string value, TimeSpan expiration, Action executeAction)
    {
        if (executeAction == null)
            return;
        Check.NotNull(value, nameof(value));
        if (!LockTake(key, value, expiration))
            return;
        try
        {
            executeAction();
        }
        catch (Exception error)
        {
            ReleaseAfterCallbackError(error, () => LockRelease(key, value));
            throw;
        }
        LockRelease(key, value);
    }

    /// <inheritdoc />
    public async Task ExecuteWithLockAsync(string key, string value, TimeSpan expiration, Func<Task> executeAction)
    {
        if (executeAction == null)
            return;
        Check.NotNull(value, nameof(value));
        if (!await LockTakeAsync(key, value, expiration).ConfigureAwait(false))
            return;
        try
        {
            await executeAction().ConfigureAwait(false);
        }
        catch (Exception error)
        {
            await ReleaseAfterCallbackErrorAsync(error, () => LockReleaseAsync(key, value)).ConfigureAwait(false);
            throw;
        }
        await LockReleaseAsync(key, value).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public T ExecuteWithLock<T>(string key, string value, TimeSpan expiration, Func<T> executeAction, T defaultValue = default)
    {
        if (executeAction == null)
            return defaultValue;
        Check.NotNull(value, nameof(value));
        if (!LockTake(key, value, expiration))
            return defaultValue;
        T result;
        try
        {
            result = executeAction();
        }
        catch (Exception error)
        {
            ReleaseAfterCallbackError(error, () => LockRelease(key, value));
            throw;
        }
        LockRelease(key, value);
        return result;
    }

    /// <inheritdoc />
    public async Task<T> ExecuteWithLockAsync<T>(string key, string value, TimeSpan expiration, Func<Task<T>> executeAction,
        T defaultValue = default)
    {
        if (executeAction == null)
            return defaultValue;
        Check.NotNull(value, nameof(value));
        if (!await LockTakeAsync(key, value, expiration).ConfigureAwait(false))
            return defaultValue;
        T result;
        try
        {
            result = await executeAction().ConfigureAwait(false);
        }
        catch (Exception error)
        {
            await ReleaseAfterCallbackErrorAsync(error, () => LockReleaseAsync(key, value)).ConfigureAwait(false);
            throw;
        }
        await LockReleaseAsync(key, value).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// 回调失败时保留原异常，并记录锁释放失败。
    /// </summary>
    private static void ReleaseAfterCallbackError(Exception error, Action release)
    {
        try
        {
            release();
        }
        catch (Exception releaseError)
        {
            error.Data["Bing.LockReleaseError"] = releaseError;
        }
    }

    /// <summary>
    /// 异步回调失败时保留原异常，并记录锁释放失败。
    /// </summary>
    private static async Task ReleaseAfterCallbackErrorAsync(Exception error, Func<Task<bool>> release)
    {
        try
        {
            await release().ConfigureAwait(false);
        }
        catch (Exception releaseError)
        {
            error.Data["Bing.LockReleaseError"] = releaseError;
        }
    }

    /// <summary>
    /// 将有效租期转换为 Redis 毫秒数。
    /// </summary>
    private static long GetLeaseMilliseconds(TimeSpan expiration)
    {
        if (expiration <= TimeSpan.Zero || expiration.TotalMilliseconds > long.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(expiration));
        return Math.Max(1L, (long)Math.Ceiling(expiration.TotalMilliseconds));
    }
}
