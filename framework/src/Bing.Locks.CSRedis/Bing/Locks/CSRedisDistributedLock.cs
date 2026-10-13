using Bing.Helpers;

namespace Bing.Locks;

/// <summary>
/// 基于CSRedis实现的分布式锁
/// </summary>
// ReSharper disable once InconsistentNaming
public class CSRedisDistributedLock : IDistributedLock
{
    /// <inheritdoc />
    public bool LockTake(string key, string value, TimeSpan expiration)
    {
        Check.NotNull(key, nameof(key));
        Check.NotNull(value, nameof(value));
        Check.NotNull(expiration, nameof(expiration));
        if (RedisHelper.Exists(key))
            return false;
        return RedisHelper.Set(key, value, expiration);
    }

    /// <inheritdoc />
    public async Task<bool> LockTakeAsync(string key, string value, TimeSpan expiration)
    {
        Check.NotNull(key, nameof(key));
        Check.NotNull(value, nameof(value));
        Check.NotNull(expiration, nameof(expiration));
        if (await RedisHelper.ExistsAsync(key))
            return false;
        return await RedisHelper.SetAsync(key, value, expiration);
    }

    /// <inheritdoc />
    public bool LockRelease(string key, string value)
    {
        Check.NotNull(key, nameof(key));
        Check.NotNull(value, nameof(value));
        if(RedisHelper.Exists(key))
            RedisHelper.Del(key);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> LockReleaseAsync(string key, string value)
    {
        Check.NotNull(key, nameof(key));
        Check.NotNull(value, nameof(value));
        if(await RedisHelper.ExistsAsync(key))
            await RedisHelper.DelAsync(key);
        return true;
    }

    /// <inheritdoc />
    public void ExecuteWithLock(string key, string value, TimeSpan expiration, Action executeAction)
    {
        if (executeAction == null)
            return;
        using (RedisHelper.Lock(key, expiration.Seconds))
            executeAction();
    }

    /// <inheritdoc />
    public async Task ExecuteWithLockAsync(string key, string value, TimeSpan expiration, Func<Task> executeAction)
    {
        if (executeAction == null)
            return;
        using (RedisHelper.Lock(key, expiration.Seconds))
            await executeAction();
    }

    /// <inheritdoc />
    public T ExecuteWithLock<T>(string key, string value, TimeSpan expiration, Func<T> executeAction, T defaultValue = default)
    {
        if (executeAction == null)
            return defaultValue;
        using (RedisHelper.Lock(key, expiration.Seconds))
            return executeAction();
    }

    /// <inheritdoc />
    public async Task<T> ExecuteWithLockAsync<T>(string key, string value, TimeSpan expiration, Func<Task<T>> executeAction,
        T defaultValue = default)
    {
        if (executeAction == null)
            return defaultValue;
        using (RedisHelper.Lock(key, expiration.Seconds))
            return await executeAction();
    }
}