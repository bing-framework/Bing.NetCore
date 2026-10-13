namespace Bing.Caching;

/// <summary>
/// Redis 回源租约的时间边界。
/// </summary>
public sealed class CacheCoordinationOptions
{
    /// <summary>
    /// 初始化一个 <see cref="CacheCoordinationOptions"/> 类型的实例。
    /// </summary>
    /// <param name="leaseDuration">租约持有时长；未指定时为 30 秒。</param>
    /// <param name="renewalInterval">租约续期间隔；未指定时为 10 秒。</param>
    /// <param name="waitTimeout">等待其他回源执行者的最长时长；未指定时为 30 秒。</param>
    /// <param name="retrievalTimeout">单次回源的最长时长；未指定时为 2 分钟。</param>
    public CacheCoordinationOptions(TimeSpan? leaseDuration = null, TimeSpan? renewalInterval = null,
        TimeSpan? waitTimeout = null, TimeSpan? retrievalTimeout = null)
    {
        LeaseDuration = Normalize(leaseDuration ?? TimeSpan.FromSeconds(30));
        RenewalInterval = Normalize(renewalInterval ?? TimeSpan.FromSeconds(10));
        WaitTimeout = Normalize(waitTimeout ?? TimeSpan.FromSeconds(30));
        RetrievalTimeout = Normalize(retrievalTimeout ?? TimeSpan.FromMinutes(2));
        if (LeaseDuration <= TimeSpan.Zero || RenewalInterval <= TimeSpan.Zero ||
            RenewalInterval >= LeaseDuration || WaitTimeout <= TimeSpan.Zero ||
            RetrievalTimeout <= TimeSpan.Zero || LeaseDuration.TotalMilliseconds > int.MaxValue ||
            RenewalInterval.TotalMilliseconds > int.MaxValue || WaitTimeout.TotalMilliseconds > int.MaxValue ||
            RetrievalTimeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "回源协调时间参数无效。");
    }

    // Timer/CTS 使用毫秒计时，正的不足一毫秒值不得被截断成零或形成忙循环。
    /// <summary>
    /// 将正时长向上取整到整数毫秒。
    /// </summary>
    /// <param name="value">待规范化的时长。</param>
    /// <returns>适用于毫秒计时器的时长；无法安全取整时保留原值。</returns>
    private static TimeSpan Normalize(TimeSpan value) => value > TimeSpan.Zero && value.TotalMilliseconds <= int.MaxValue
        ? TimeSpan.FromMilliseconds(Math.Ceiling(value.TotalMilliseconds)) : value;

    /// <summary>
    /// 获取租约持有时长。
    /// </summary>
    public TimeSpan LeaseDuration { get; }

    /// <summary>
    /// 获取租约续期间隔。
    /// </summary>
    public TimeSpan RenewalInterval { get; }

    /// <summary>
    /// 获取等待其他回源执行者的最长时长。
    /// </summary>
    public TimeSpan WaitTimeout { get; }

    /// <summary>
    /// 获取单次回源的最长时长。
    /// </summary>
    public TimeSpan RetrievalTimeout { get; }
}
