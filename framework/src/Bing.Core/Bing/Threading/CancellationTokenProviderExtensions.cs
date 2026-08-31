namespace Bing.Threading;

/// <summary>
/// 提供 <see cref="ICancellationTokenProvider"/> 的取消令牌选择扩展。
/// </summary>
public static class CancellationTokenProviderExtensions
{
    /// <summary>
    /// 选择显式令牌或提供程序令牌。
    /// </summary>
    /// <param name="provider">在显式令牌不可取消时提供回退值的令牌提供程序。</param>
    /// <param name="preferredValue">优先使用的取消令牌。</param>
    /// <returns>优先令牌可取消时返回该令牌；否则返回 <paramref name="provider"/> 的当前令牌。</returns>
    /// <remarks><c>default</c> 和 <see cref="CancellationToken.None"/> 均被视为未提供可取消令牌，无法区分显式传入 <c>None</c> 与未提供令牌。</remarks>
    public static CancellationToken FallbackToProvider(this ICancellationTokenProvider provider, CancellationToken preferredValue = default)
    {
        return preferredValue == default || preferredValue == CancellationToken.None
            ? provider.Token
            : preferredValue;
    }
}
