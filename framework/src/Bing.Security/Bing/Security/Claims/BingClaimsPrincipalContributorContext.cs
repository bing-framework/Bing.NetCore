using System.Security.Claims;
using Bing.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Security.Claims;

/// <summary>
/// 表示一个用于扩展和修改 <see cref="System.Security.Claims.ClaimsPrincipal"/> 的上下文类。
/// </summary>
public class BingClaimsPrincipalContributorContext
{
    /// <summary>
    /// 初始化一个<see cref="BingClaimsPrincipalContributorContext"/> 类型的实例。
    /// </summary>
    /// <param name="claimsPrincipal">身份主体</param>
    /// <param name="serviceProvider">服务提供程序</param>
    public BingClaimsPrincipalContributorContext(ClaimsPrincipal claimsPrincipal, IServiceProvider serviceProvider)
    {
        ClaimsPrincipal = claimsPrincipal;
        ServiceProvider = serviceProvider;
    }

    /// <summary>
    /// 身份主体
    /// </summary>
    public ClaimsPrincipal ClaimsPrincipal { get; set; }

    /// <summary>
    /// 服务提供程序
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 获取指定类型的服务实例。
    /// </summary>
    /// <typeparam name="T">要解析的服务类型。</typeparam>
    /// <returns>返回 T 类型的服务实例。</returns>
    public virtual T GetRequiredService<T>() where T : notnull
    {
        Check.NotNull(ServiceProvider, nameof(ServiceProvider));
        return ServiceProvider.GetRequiredService<T>();
    }
}
