using System.Collections.Concurrent;
using Bing.Helpers;

namespace Bing.Locks;

/// <summary>
/// 本地锁
/// </summary>
public class LocalLock : ILock
{
    /// <summary>
    /// 按锁键复用进程内同步对象的映射。
    /// </summary>
    // ReSharper disable once InconsistentNaming
    private static readonly ConcurrentDictionary<string, object> _lockCache = new ConcurrentDictionary<string, object>();

    /// <summary>
    /// 记录锁键对应的持有者值，用于校验释放请求的所有权。
    /// </summary>
    // ReSharper disable once InconsistentNaming
    private static readonly ConcurrentDictionary<string, string> _lockUserCache = new ConcurrentDictionary<string, string>();

    /// <inheritdoc />
    public bool LockTake(string key, string value, TimeSpan expiration)
    {
        Check.NotNull(key, nameof(key));
        Check.NotNull(value, nameof(value));

        var obj = _lockCache.GetOrAdd(key, k => new object());
        if (Monitor.TryEnter(obj, expiration))
        {
            _lockUserCache[key] = value;
            return true;
        }
        return false;
    }

    /// <inheritdoc />
    public Task<bool> LockTakeAsync(string key, string value, TimeSpan expiration) => Task.FromResult(LockTake(key, value, expiration));

    /// <inheritdoc />
    public bool LockRelease(string key, string value)
    {
        Check.NotNull(key, nameof(key));
        Check.NotNull(value, nameof(value));

        _lockCache.TryGetValue(key, out var obj);
        if (obj != null)
        {
            if (_lockUserCache[key] == value)
            {
                Monitor.Exit(obj);
                return true;
            }
            return false;
        }
        return true;
    }

    /// <inheritdoc />
    public Task<bool> LockReleaseAsync(string key, string value) => Task.FromResult(LockRelease(key, value));

    /// <inheritdoc />
    public void ExecuteWithLock(string key, string value, TimeSpan expiration, Action executeAction)
    {
        if (executeAction == null)
            return;
        if (LockTake(key, value, expiration))
        {
            try
            {
                executeAction();
            }
            finally
            {
                LockRelease(key, value);
            }
        }
    }

    /// <inheritdoc />
    public async Task ExecuteWithLockAsync(string key, string value, TimeSpan expiration, Func<Task> executeAction)
    {
        if (executeAction == null)
            return;
        if (await LockTakeAsync(key, value, expiration))
        {
            try
            {
                await executeAction();
            }
            finally
            {
                await LockReleaseAsync(key, value);
            }
        }
    }

    /// <inheritdoc />
    public T ExecuteWithLock<T>(string key, string value, TimeSpan expiration, Func<T> executeAction, T defaultValue = default)
    {
        if (executeAction == null)
            return defaultValue;
        if (LockTake(key, value, expiration))
        {
            try
            {
                return executeAction();
            }
            finally
            {
                LockRelease(key, value);
            }
        }
        return defaultValue;
    }

    /// <inheritdoc />
    public async Task<T> ExecuteWithLockAsync<T>(string key, string value, TimeSpan expiration, Func<Task<T>> executeAction, T defaultValue = default)
    {
        if (executeAction == null)
            return defaultValue;
        if (await LockTakeAsync(key, value, expiration))
        {
            try
            {
                return await executeAction();
            }
            finally
            {
                await LockReleaseAsync(key, value);
            }
        }
        return defaultValue;
    }
}
