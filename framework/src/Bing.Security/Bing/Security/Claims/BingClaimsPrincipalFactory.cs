using System.Security.Claims;
using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.Security.Claims;

/// <summary>
/// 提供用于创建 <see cref="ClaimsPrincipal"/>（身份主体）的工厂。
/// </summary>
public class BingClaimsPrincipalFactory : IBingClaimsPrincipalFactory, ITransientDependency
{
    /// <summary>
    /// 认证类型常量，默认值为 "Bing.Application"。
    /// </summary>
    public static string AuthenticationType => "Bing.Application";

    /// <summary>
    /// 服务提供程序
    /// </summary>
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 身份主体工厂选项
    /// </summary>
    protected BingClaimsPrincipalFactoryOptions Options { get; }

    /// <summary>
    /// 初始化一个<see cref="BingClaimsPrincipalFactory"/> 类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="bingClaimOptions"></param>
    public BingClaimsPrincipalFactory(IServiceProvider serviceProvider, IOptions<BingClaimsPrincipalFactoryOptions> bingClaimOptions)
    {
        ServiceProvider = serviceProvider;
        Options = bingClaimOptions.Value;
    }

    /// <inheritdoc />
    public virtual async Task<ClaimsPrincipal> CreateAsync(ClaimsPrincipal existsClaimsPrincipal = null)
    {
        return await InternalCreateAsync(Options, existsClaimsPrincipal, false);
    }

    /// <inheritdoc />
    public virtual async Task<ClaimsPrincipal> CreateDynamicAsync(ClaimsPrincipal existsClaimsPrincipal = null)
    {
        return await InternalCreateAsync(Options, existsClaimsPrincipal, true);
    }

    /// <summary>
    /// 内部创建 <see cref="ClaimsPrincipal"/> 实例的方法。
    /// </summary>
    /// <param name="options">包含贡献者的工厂选项。</param>
    /// <param name="existsClaimsPrincipal">可选的现有 <see cref="ClaimsPrincipal"/>。</param>
    /// <param name="isDynamic">是否使用动态贡献者。</param>
    /// <returns>返回创建的 <see cref="ClaimsPrincipal"/>。</returns>
    public virtual async Task<ClaimsPrincipal> InternalCreateAsync(BingClaimsPrincipalFactoryOptions options, ClaimsPrincipal existsClaimsPrincipal = null, bool isDynamic = false)
    {
        var claimsPrincipal = existsClaimsPrincipal ?? new ClaimsPrincipal(new ClaimsIdentity(AuthenticationType, BingClaimTypes.UserName, BingClaimTypes.Role));
        var context = new BingClaimsPrincipalContributorContext(claimsPrincipal, ServiceProvider);
        if (isDynamic)
        {
            foreach (var contributorType in options.Contributors)
            {
                var contributor = (IBingClaimsPrincipalContributor)ServiceProvider.GetRequiredService(contributorType);
                await contributor.ContributeAsync(context);
            }
        }
        else
        {
            foreach (var contributorType in options.DynamicContributors)
            {
                var contributor = (IBingDynamicClaimsPrincipalContributor)ServiceProvider.GetRequiredService(contributorType);
                await contributor.ContributeAsync(context);
            }
        }
        return context.ClaimsPrincipal;
    }
}
