using Bing.Logging.Serilog.Internals;

namespace Bing.Logging.Serilog.Enrichers;

/// <summary>
/// 日志级别扩展属性 - 用于显示标准日志级别
/// </summary>
internal class LogLevelEnricher : ILogEventEnricher
{
    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var property = propertyFactory.CreateProperty("LogLevel", LogLevelSwitcher.Switch(logEvent.Level));
        logEvent.AddOrUpdateProperty(property);
    }
}
