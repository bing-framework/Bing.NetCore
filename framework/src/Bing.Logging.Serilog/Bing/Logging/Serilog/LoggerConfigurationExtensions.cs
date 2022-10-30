using Bing.Logging.Serilog.Internals;
using Microsoft.Extensions.Configuration;

namespace Bing.Logging.Serilog;

/// <summary>
/// Serilog日志配置操作(<see cref="LoggerConfiguration"/>) 扩展
/// </summary>
public static class LoggerConfigurationExtensions
{
    /// <summary>
    /// 配置日志级别
    /// </summary>
    /// <param name="source">Serilog日志配置</param>
    /// <param name="configuration">配置</param>
    public static LoggerConfiguration ConfigLogLevel(this LoggerConfiguration source, IConfiguration configuration)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        var section = configuration.GetSection("Logging:LogLevel");
        foreach (var item in section.GetChildren())
        {
            if (item.Key == "Default")
            {
                source.MinimumLevel.ControlledBy(new LoggingLevelSwitch(LogLevelSwitcher.Switch(item.Value)));
                continue;
            }
            source.MinimumLevel.Override(item.Key, LogLevelSwitcher.Switch(item.Value));
        }
        return source;
    }
}
