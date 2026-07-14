using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Logging.BackgroundJobs;

/// <summary>
/// 后台任务队列扩展
/// </summary>
public static class BackgroundTaskQueueExtensions
{
    /// <summary>
    /// 注册日志上下文感知后台任务队列
    /// </summary>
    public static IServiceCollection AddBingBackgroundTaskQueue(
        this IServiceCollection services,
        Action<BackgroundTaskQueueOptions> setupAction = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (setupAction == null)
            services.AddOptions<BackgroundTaskQueueOptions>();
        else
            services.Configure(setupAction);
        services.TryAddSingleton<BackgroundTaskQueue>();
        services.TryAddSingleton<IBackgroundTaskQueue>(provider => provider.GetRequiredService<BackgroundTaskQueue>());
        services.AddHostedService<ContextAwareBackgroundService>();
        return services;
    }
}