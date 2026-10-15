using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Events.EventBusAdapter;

/// <summary>
/// 注册旧消息契约的 CAP 适配器。
/// </summary>
public static class LegacyCapMessageEventBusAdapterExtensions
{
    /// <summary>
    /// 显式启用旧消息事件总线到 CAP 的转发。
    /// </summary>
    public static IServiceCollection AddLegacyCapMessageEventBusAdapter(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.TryAddScoped<Bing.EventBus.IMessageEventBus, LegacyCapMessageEventBusAdapter>();
        return services;
    }
}
