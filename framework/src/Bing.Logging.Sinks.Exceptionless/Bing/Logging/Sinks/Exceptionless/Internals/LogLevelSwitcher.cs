namespace Bing.Logging.Sinks.Exceptionless.Internals
{
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
    }
}
