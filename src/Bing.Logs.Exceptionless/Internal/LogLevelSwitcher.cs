using el = global::Exceptionless;

namespace Bing.Logs.Exceptionless.Internal
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
        public static el.Logging.LogLevel Switch(LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => el.Logging.LogLevel.Trace,
                LogLevel.Debug => el.Logging.LogLevel.Debug,
                LogLevel.Information => el.Logging.LogLevel.Info,
                LogLevel.Warning => el.Logging.LogLevel.Warn,
                LogLevel.Error => el.Logging.LogLevel.Error,
                LogLevel.Fatal => el.Logging.LogLevel.Fatal,
                LogLevel.None => el.Logging.LogLevel.Off,
                _ => el.Logging.LogLevel.Off
            };
        }
    }
}
