using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bing.Events.Ordering;

/// <summary>
/// 注册有序事件收件箱留存清理。
/// </summary>
public static class OrderedEventRetentionExtensions
{
    /// <summary>
    /// 注册默认关闭的有序事件留存清理调度器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configure">清理调度配置。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>
    /// 启用前必须先注册有序事件存储、事务工厂和对应的协调器；归档清理需额外显式配置。
    /// 多节点部署应启用协调租约，并使用稳定的锁键和有限租期。
    /// </remarks>
    public static IServiceCollection AddBingOrderedEventRetentionScheduler(
        this IServiceCollection services, Action<OrderedEventRetentionScheduleOptions> configure = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.AddOptions<OrderedEventRetentionScheduleOptions>();
        if (configure != null)
            services.Configure(configure);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService,
            OrderedEventRetentionHostedService>());
        return services;
    }
}
