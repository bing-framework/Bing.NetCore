using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Uow;

/// <summary>
/// 注册显式业务操作执行能力。
/// </summary>
public static class BingOperationServiceCollectionExtensions
{
    /// <summary>
    /// 注册工作单元操作管理器、保存扩展点和独立操作执行器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>原服务集合。</returns>
    public static IServiceCollection AddBingOperations(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        services.AddUnitOfWorkScopes();
        services.TryAddSingleton<UnitOfWorkOperationsRegistrationMarker>();
        services.TryAddScoped<IUnitOfWorkOperationManager>(provider =>
        {
            var manager = provider.GetRequiredService<IUnitOfWorkScopeManager>();
            return manager as IUnitOfWorkOperationManager ?? throw new InvalidOperationException(
                $"工作单元管理器 {manager.GetType().FullName} 未实现 {nameof(IUnitOfWorkOperationManager)}，不能启动业务操作。");
        });
        services.TryAddSingleton<IBingOperationExecutor, BingOperationExecutor>();
        return services;
    }
}

internal sealed class UnitOfWorkOperationsRegistrationMarker
{
}
