using Serilog.Events;
using Serilogs = Serilog;

namespace Bing.Logs.Serilog.Internal
{
    /// <summary>
    /// 日志级别切换器
    /// </summary>
    internal static class LogLevelSwitcher
    {
        /// <summary>
        /// 转换日志等级
        /// </summary>
        /// <param name="level">平台日志等级</param>
        public static Serilogs.Events.LogEventLevel? Switch(LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => LogEventLevel.Verbose,
                LogLevel.Debug => LogEventLevel.Debug,
                LogLevel.Information => LogEventLevel.Information,
                LogLevel.Warning => LogEventLevel.Warning,
                LogLevel.Error => LogEventLevel.Error,
                LogLevel.Fatal => LogEventLevel.Fatal,
                _ => null
            };
        }
    }
}
