namespace Bing.Locks;

/// <summary>
/// 使用持有者标识保护一次性释放操作。
/// </summary>
internal sealed class LockLease : ILockLease
{
    /// <summary>
    /// 同步释放回调。
    /// </summary>
    private readonly Func<bool> _release;

    /// <summary>
    /// 异步释放回调。
    /// </summary>
    private readonly Func<Task<bool>> _releaseAsync;

    /// <summary>
    /// 释放状态。
    /// </summary>
    private int _released;

    /// <summary>
    /// 初始化一个 <see cref="LockLease" /> 类型的实例。
    /// </summary>
    /// <param name="key">对象在容器内的键。</param>
    /// <param name="owner">拥有当前实例或资源的对象。</param>
    /// <param name="release">用于释放当前锁租约的回调。</param>
    /// <param name="releaseAsync">用于异步释放当前锁租约的回调。</param>
    internal LockLease(string key, string owner, Func<bool> release, Func<Task<bool>> releaseAsync)
    {
        Key = key;
        Owner = owner;
        _release = release;
        _releaseAsync = releaseAsync;
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
            return _release();
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
            return await _releaseAsync().ConfigureAwait(false);
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
