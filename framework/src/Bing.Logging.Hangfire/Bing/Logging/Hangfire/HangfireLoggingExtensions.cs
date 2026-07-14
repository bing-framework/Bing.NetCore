using Hangfire;

namespace Bing.Logging.Hangfire;

/// <summary>
/// Hangfire日志上下文扩展
/// </summary>
public static class HangfireLoggingExtensions
{
    /// <summary>
    /// 配置Bing日志上下文过滤器
    /// </summary>
    public static IGlobalConfiguration UseBingLogging(
        this IGlobalConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (serviceProvider == null)
            throw new ArgumentNullException(nameof(serviceProvider));
        return configuration.UseFilter(new HangfireLogContextFilter(serviceProvider));
    }
}