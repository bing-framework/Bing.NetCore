using System.Collections.Concurrent;
using Bing.Helpers;
using System.Collections.Generic;

namespace Bing.Locks;

/// <summary>
/// 本地锁
/// </summary>
public class LocalLock : ILock, ILockLeaseProvider
{
    /// <summary>
    /// 按键保存的锁状态。
    /// </summary>
    // ReSharper disable once InconsistentNaming
    private static readonly Dictionary<string, LockState> _lockCache = new Dictionary<string, LockState>();

    /// <summary>
    /// 保护锁状态及其引用计数。
    /// </summary>
    // ReSharper disable once InconsistentNaming
    private static readonly object _cacheGate = new object();

    /// <summary>
    /// 保存互斥量、持有者及使用者数量。
    /// </summary>
    private sealed class LockState
    {
        /// <summary>
        /// 异步互斥量。
        /// </summary>
        public readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 当前持有者。
        /// </summary>
        public string Owner;

        /// <summary>
        /// 持有者与等待者的总数。
        /// </summary>
        public int References;
    }

    /// <inheritdoc />
    public bool LockTake(string key, string value, TimeSpan expiration)
    {
        Check.NotNull(key, nameof(key));
        Check.NotNull(value, nameof(value));

        var state = AddReference(key);
        var acquired = false;
        try
        {
            acquired = state.Semaphore.Wait(expiration);
            if (acquired)
            {
                lock (_cacheGate)
                    state.Owner = value;
            }
            return acquired;
        }
        finally
        {
            if (!acquired)
                RemoveReference(key, state);
        }
    }

    /// <summary>
    /// 获取一个锁（需要自己释放）
    /// </summary>
    /// <param name="key">锁定标识</param>
    /// <param name="value">当前占用值</param>
    /// <param name="expiration">锁定时间间隔</param>
    /// <returns>表示尝试获取锁结果的异步操作；成功获取锁时结果为 <see langword="true"/>，否则为 <see langword="false"/>。</returns>
    public async Task<bool> LockTakeAsync(string key, string value, TimeSpan expiration)
    {
        Check.NotNull(key, nameof(key));
        Check.NotNull(value, nameof(value));

        var state = AddReference(key);
        var acquired = false;
        try
        {
            acquired = await state.Semaphore.WaitAsync(expiration).ConfigureAwait(false);
            if (acquired)
            {
                lock (_cacheGate)
                    state.Owner = value;
            }
            return acquired;
        }
        finally
        {
            if (!acquired)
                RemoveReference(key, state);
        }
    }

    /// <summary>
    /// 在指定等待时间内获取可取消的本地锁租约。
    /// </summary>
    /// <param name="key">锁键。</param>
    /// <param name="waitTimeout">最长等待时间。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功时返回租约，超时时返回 null。</returns>
    public async Task<ILockLease> TryAcquireLeaseAsync(string key, TimeSpan waitTimeout, CancellationToken cancellationToken = default)
    {
        Check.NotNull(key, nameof(key));
        var state = AddReference(key);
        var acquired = false;
        try
        {
            acquired = await state.Semaphore.WaitAsync(waitTimeout, cancellationToken).ConfigureAwait(false);
            if (!acquired)
                return null;
            var owner = Guid.NewGuid().ToString("N");
            lock (_cacheGate)
                state.Owner = owner;
            return new LockLease(key, owner, () => LockRelease(key, owner), () => LockReleaseAsync(key, owner));
        }
        finally
        {
            if (!acquired)
                RemoveReference(key, state);
        }
    }

    /// <inheritdoc />
    Task<ILockLease> ILockLeaseProvider.TryAcquireLeaseAsync(string key, TimeSpan waitTimeout,
        TimeSpan? leaseDuration, CancellationToken cancellationToken)
    {
        if (leaseDuration.HasValue && leaseDuration.Value != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "本地锁不支持自动租期，请传入 null 或无限时长。");
        return TryAcquireLeaseAsync(key, waitTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public bool LockRelease(string key, string value)
    {
        Check.NotNull(key, nameof(key));
        Check.NotNull(value, nameof(value));

        lock (_cacheGate)
        {
            if (!_lockCache.TryGetValue(key, out var state))
                return true;
            if (state.Owner != value)
                return false;

            state.Owner = null;
            state.Semaphore.Release();
            RemoveReferenceUnderGate(key, state);
            return true;
        }
    }

    /// <inheritdoc />
    public Task<bool> LockReleaseAsync(string key, string value) => Task.FromResult(LockRelease(key, value));

    /// <summary>
    /// 引用指定键的锁状态。
    /// </summary>
    private static LockState AddReference(string key)
    {
        lock (_cacheGate)
        {
            if (!_lockCache.TryGetValue(key, out var state))
            {
                state = new LockState();
                _lockCache.Add(key, state);
            }
            state.References++;
            return state;
        }
    }

    /// <summary>
    /// 释放等待者对锁状态的引用。
    /// </summary>
    private static void RemoveReference(string key, LockState state)
    {
        lock (_cacheGate)
            RemoveReferenceUnderGate(key, state);
    }

    /// <summary>
    /// 在持有缓存锁时回收无引用的锁状态。
    /// </summary>
    private static void RemoveReferenceUnderGate(string key, LockState state)
    {
        if (--state.References != 0)
            return;
        _lockCache.Remove(key);
        state.Semaphore.Dispose();
    }

    /// <summary>
    /// 使用锁执行一个方法
    /// </summary>
    /// <param name="key">锁定标识</param>
    /// <param name="value">当前占用值</param>
    /// <param name="expiration">锁定时间间隔</param>
    /// <param name="executeAction">执行的方法</param>
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
