namespace Bing.Logging.Sinks.Exceptionless.Internals;

/// <summary>
/// 日志级别切换器
/// </summary>
internal static class LogLevelSwitcher
{
    /// <summary>
    /// 转换日志级别
    /// </summary>
    /// <param name="level">MS日志级别</param>
    public static global::Exceptionless.Logging.LogLevel Switch(Microsoft.Extensions.Logging.LogLevel level)
    {
        return level switch
        {
            Microsoft.Extensions.Logging.LogLevel.Trace => global::Exceptionless.Logging.LogLevel.Trace,
            Microsoft.Extensions.Logging.LogLevel.Debug => global::Exceptionless.Logging.LogLevel.Debug,
            Microsoft.Extensions.Logging.LogLevel.Information => global::Exceptionless.Logging.LogLevel.Info,
            Microsoft.Extensions.Logging.LogLevel.Warning => global::Exceptionless.Logging.LogLevel.Warn,
            Microsoft.Extensions.Logging.LogLevel.Error => global::Exceptionless.Logging.LogLevel.Error,
            Microsoft.Extensions.Logging.LogLevel.Critical => global::Exceptionless.Logging.LogLevel.Fatal,
            Microsoft.Extensions.Logging.LogLevel.None => global::Exceptionless.Logging.LogLevel.Off,
            _ => global::Exceptionless.Logging.LogLevel.Off
        };
    }

    /// <summary>
    /// 转换日志级别
    /// </summary>
    /// <param name="level">Serilog日志级别</param>
    public static global::Exceptionless.Logging.LogLevel Switch(Serilog.Events.LogEventLevel level)
    {
        return level switch
        {
            Serilog.Events.LogEventLevel.Verbose => global::Exceptionless.Logging.LogLevel.Trace,
            Serilog.Events.LogEventLevel.Debug => global::Exceptionless.Logging.LogLevel.Debug,
            Serilog.Events.LogEventLevel.Information => global::Exceptionless.Logging.LogLevel.Info,
            Serilog.Events.LogEventLevel.Warning => global::Exceptionless.Logging.LogLevel.Warn,
            Serilog.Events.LogEventLevel.Error => global::Exceptionless.Logging.LogLevel.Error,
            Serilog.Events.LogEventLevel.Fatal => global::Exceptionless.Logging.LogLevel.Fatal,
            _ => global::Exceptionless.Logging.LogLevel.Other
        };
    }
}
