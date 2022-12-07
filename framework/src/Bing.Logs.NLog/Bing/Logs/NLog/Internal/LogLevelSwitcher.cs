using NLogs = NLog;

namespace Bing.Logs.NLog.Internal;

/// <summary>
/// 日志级别切换器
/// </summary>
internal static class LogLevelSwitcher
{
    /// <summary>
    /// 转换日志等级
    /// </summary>
    /// <param name="level">平台日志等级</param>
    public static NLogs.LogLevel Switch(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => NLogs.LogLevel.Trace,
            LogLevel.Debug => NLogs.LogLevel.Debug,
            LogLevel.Information => NLogs.LogLevel.Info,
            LogLevel.Warning => NLogs.LogLevel.Warn,
            LogLevel.Error => NLogs.LogLevel.Error,
            LogLevel.Fatal => NLogs.LogLevel.Fatal,
            _ => NLogs.LogLevel.Off
        };
    }
}