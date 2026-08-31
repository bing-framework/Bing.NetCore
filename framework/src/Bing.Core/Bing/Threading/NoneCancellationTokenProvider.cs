using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Threading;

/// <summary>
/// 始终提供不可取消令牌的回退实现。
/// </summary>
[Dependency(ServiceLifetime.Singleton, TryAdd = true)]
public class NoneCancellationTokenProvider : ICancellationTokenProvider
{
    /// <summary>
    /// 获取可全局复用的不可取消令牌提供程序实例。
    /// </summary>
    public static readonly ICancellationTokenProvider Instance = new NoneCancellationTokenProvider();

    /// <inheritdoc />
    /// <remarks>始终返回 <see cref="CancellationToken.None"/>，不会请求取消。</remarks>
    public CancellationToken Token { get; } = CancellationToken.None;
}
