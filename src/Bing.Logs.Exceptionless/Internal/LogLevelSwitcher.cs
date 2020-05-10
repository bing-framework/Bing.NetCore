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
            switch (level)
            {
                case LogLevel.Trace:
                    return el.Logging.LogLevel.Trace;
                case LogLevel.Debug:
                    return el.Logging.LogLevel.Debug;
                case LogLevel.Information:
                    return el.Logging.LogLevel.Info;
                case LogLevel.Warning:
                    return el.Logging.LogLevel.Warn;
                case LogLevel.Error:
                    return el.Logging.LogLevel.Error;
                case LogLevel.Fatal:
                    return el.Logging.LogLevel.Fatal;
                case LogLevel.None:
                    return el.Logging.LogLevel.Off;
                default:
                    return el.Logging.LogLevel.Off;
            }
        }
    }
}
