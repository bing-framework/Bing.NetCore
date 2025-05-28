namespace Bing.Threading;

/// <summary>
/// 异步任务取消令牌提供程序(<see cref="ICancellationTokenProvider"/>) 扩展
/// </summary>
public static class CancellationTokenProviderExtensions
{
    /// <summary>
    /// 后备提供程序
    /// </summary>
    /// <param name="provider">异步任务取消令牌提供程序</param>
    /// <param name="preferredValue">优先值</param>
    public static CancellationToken FallbackToProvider(this ICancellationTokenProvider provider, CancellationToken preferredValue = default)
    {
        return preferredValue == default || preferredValue == CancellationToken.None
            ? provider.Token
            : preferredValue;
    }
}
