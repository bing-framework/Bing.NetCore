using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Logging;

/// <summary>
/// 服务集合扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册日志
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">安装配置</param>
    public static BingLoggingBuilder AddBingLogging(this IServiceCollection services, Action<BingLoggingOptions> setupAction)
    {
        services.TryAddSingleton<ILogFactory, LogFactory>();
        services.TryAddScoped<ILogContextAccessor, LogContextAccessor>();
        services.TryAddTransient(typeof(ILog<>), typeof(Log<>));
        services.TryAddTransient(typeof(ILog), t => t.GetService<ILogFactory>()?.CreateLog("default") ?? NullLog.Instance);
        var options = new BingLoggingOptions();
        setupAction(options);
        foreach (var serviceExtension in options.Extensions) 
            serviceExtension.AddServices(services);
        return new BingLoggingBuilder(services);
    }
}
