namespace Bing.RateLimiting;

/// <summary>
/// 按键消耗令牌的限流器。调用立即返回，不等待令牌补充。
/// </summary>
public interface ITokenBucketRateLimiter
{
    /// <summary>
    /// 尝试为指定键取得令牌。
    /// </summary>
    /// <param name="key">由调用方按策略、租户、用户和路由组合的可信限流键。</param>
    /// <param name="permits">本次请求消耗的令牌数。</param>
    /// <returns>是否允许请求及拒绝时建议的等待时间。</returns>
    RateLimitResult TryAcquire(string key, int permits = 1);

    /// <summary>
    /// 异步尝试为指定键取得令牌。
    /// </summary>
    /// <param name="key">限流键。</param>
    /// <param name="permits">消耗的令牌数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>限流结果。</returns>
    Task<RateLimitResult> TryAcquireAsync(string key, int permits = 1, CancellationToken cancellationToken = default);
}
