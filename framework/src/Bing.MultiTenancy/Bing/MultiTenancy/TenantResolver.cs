using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.MultiTenancy;

/// <summary>
/// 租户解析器
/// </summary>
public class TenantResolver : ITenantResolver, ITransientDependency
{
    /// <summary>
    /// 服务提供程序
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 租户解析器选项配置
    /// </summary>
    private readonly BingTenantResolveOptions _options;

    /// <summary>
    /// 初始化一个<see cref="TenantResolver"/>类型的实例
    /// </summary>
    /// <param name="options">租户解析器选项配置</param>
    /// <param name="serviceProvider">服务提供程序</param>
    public TenantResolver(IOptions<BingTenantResolveOptions> options, IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    /// <summary>
    /// 解析当前租户
    /// </summary>
    /// <returns>
    /// 租户ID，租户名称，null（如果无法解析则返回空）
    /// </returns>
    public virtual async Task<TenantResolveResult> ResolveTenantIdOrNameAsync()
    {
        var result = new TenantResolveResult();
        using (var serviceScope = _serviceProvider.CreateScope())
        {
            var context = new TenantResolveContext(serviceScope.ServiceProvider);
            foreach (var tenantResolve in _options.TenantResolvers)
            {
                await tenantResolve.ResolveAsync(context);
                result.AppliedResolvers.Add(tenantResolve.Name);

                if (context.HasResolvedTenantOrHost())
                {
                    result.TenantIdOrName = context.TenantIdOrName;
                    break;
                }
            }
        }
        return result;
    }
}
