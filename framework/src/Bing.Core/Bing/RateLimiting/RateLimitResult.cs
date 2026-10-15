namespace Bing.RateLimiting;

/// <summary>
/// 一次令牌申请的结果。
/// </summary>
public readonly struct RateLimitResult
{
    /// <summary>
    /// 初始化一个 <see cref="RateLimitResult" /> 类型的实例。
    /// </summary>
    /// <param name="isAllowed">是否取得令牌。</param>
    /// <param name="retryAfter">拒绝后建议等待的时长；允许时为零。</param>
    public RateLimitResult(bool isAllowed, TimeSpan retryAfter)
    {
        if (retryAfter < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(retryAfter));

        IsAllowed = isAllowed;
        RetryAfter = retryAfter;
    }

    /// <summary>
    /// 是否取得令牌。
    /// </summary>
    public bool IsAllowed { get; }

    /// <summary>
    /// 拒绝后建议等待的时长。
    /// </summary>
    public TimeSpan RetryAfter { get; }
}
