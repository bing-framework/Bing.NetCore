namespace Bing.Locks;

/// <summary>
/// 表示一次需要主动释放的锁持有权。
/// </summary>
public interface ILockLease : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 锁键。
    /// </summary>
    string Key { get; }

    /// <summary>
    /// 本次获取的唯一持有者标识。
    /// </summary>
    string Owner { get; }

    /// <summary>
    /// 释放锁持有权。
    /// </summary>
    /// <returns>仅首次成功释放时返回 true。</returns>
    bool Release();

    /// <summary>
    /// 异步释放锁持有权。
    /// </summary>
    /// <returns>仅首次成功释放时返回 true。</returns>
    Task<bool> ReleaseAsync();
}
