namespace Bing.Locks;

/// <summary>
/// 提供带唯一持有权的锁租约。
/// </summary>
public interface ILockLeaseProvider
{
    /// <summary>
    /// 在指定等待时间内获取锁租约。
    /// </summary>
    /// <param name="key">锁键。</param>
    /// <param name="waitTimeout">最长等待时间。</param>
    /// <param name="leaseDuration">分布式锁租期；本地锁应传入 <see langword="null"/> 或无限时长。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功时返回唯一租约，超时时返回 <see langword="null"/>。</returns>
    /// <remarks>
    /// 本地锁不会因等待参数到期而自动释放；分布式实现的租期由实现负责校验和执行。
    /// </remarks>
    Task<ILockLease> TryAcquireLeaseAsync(string key, TimeSpan waitTimeout,
        TimeSpan? leaseDuration = null, CancellationToken cancellationToken = default);
}
