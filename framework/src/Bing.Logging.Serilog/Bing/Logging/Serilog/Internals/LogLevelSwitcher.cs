namespace Bing.Logging.Serilog.Internals;

/// <summary>
/// 日志级别切换器
/// </summary>
internal static class LogLevelSwitcher
{
    /// <summary>
    /// 转换日志级别
    /// </summary>
    /// <param name="level">Serilog日志级别</param>
    /// <returns>MS日志级别</returns>
    public static string Switch(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose => "Trace",
            LogEventLevel.Debug => "Debug",
            LogEventLevel.Information => "Information",
            LogEventLevel.Warning => "Warning",
            LogEventLevel.Error => "Error",
            LogEventLevel.Fatal => "Critical",
            _ => null
        };
    }

    /// <summary>
    /// 转换日志级别
    /// </summary>
    /// <param name="level">MS日志级别</param>
    /// <returns>Serilog日志级别</returns>
    public static LogEventLevel Switch(string level)
    {
        return level.ToUpperInvariant() switch
        {
            "TRACE" => LogEventLevel.Verbose,
            "DEBUG" => LogEventLevel.Debug,
            "INFORMATION" => LogEventLevel.Information,
            "WARNING" => LogEventLevel.Warning,
            "ERROR" => LogEventLevel.Error,
            "CRITICAL" => LogEventLevel.Fatal,
            "NONE" => LogEventLevel.Fatal,
            _ => LogEventLevel.Warning
        };
    }
}
