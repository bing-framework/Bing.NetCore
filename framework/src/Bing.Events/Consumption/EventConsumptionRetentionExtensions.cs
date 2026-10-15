using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Events.Consumption;

/// <summary>
/// 提供事件消费收据保留服务的依赖注入注册扩展。
/// </summary>
public static class EventConsumptionRetentionExtensions
{
    /// <summary>
    /// 注册经过授权的事件消费收据清理服务。
    /// </summary>
    /// <typeparam name="TAuthorizer">授权器类型。</typeparam>
    /// <param name="services">服务集合。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>
    /// 该入口不会替换调用方已有的授权器或低层存储，也不会注册后台调度器。
    /// </remarks>
    public static IServiceCollection AddBingEventConsumptionRetention<TAuthorizer>(
        this IServiceCollection services)
        where TAuthorizer : class, IEventConsumptionRetentionAuthorizer
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.TryAddScoped<IEventConsumptionRetentionAuthorizer, TAuthorizer>();
        services.TryAddScoped<IEventConsumptionRetentionService, EventConsumptionRetentionService>();
        return services;
    }
}
