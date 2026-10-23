using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Uow;

/// <summary>
/// 注册业务工作单元范围服务。
/// </summary>
public static class UnitOfWorkScopeServiceCollectionExtensions
{
    /// <summary>
    /// 以 Scoped 生命周期注册默认工作单元范围管理器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>原服务集合。</returns>
    public static IServiceCollection AddUnitOfWorkScopes(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.TryAddScoped<IUnitOfWorkScopeManager, UnitOfWorkScopeManager>();
        return services;
    }
}
