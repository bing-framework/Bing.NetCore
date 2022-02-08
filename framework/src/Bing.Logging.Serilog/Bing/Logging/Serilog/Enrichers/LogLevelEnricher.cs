using Bing.Logging.Serilog.Internals;
using Serilog.Core;
using Serilog.Events;

namespace Bing.Logging.Serilog.Enrichers
{
    /// <summary>
    /// 日志级别扩展属性 - 用于显示标准日志级别
    /// </summary>
    internal class LogLevelEnricher : ILogEventEnricher
    {
        /// <summary>
        /// 扩展属性
        /// </summary>
        /// <param name="logEvent">日志事件</param>
        /// <param name="propertyFactory">日志事件属性工厂</param>
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var property = propertyFactory.CreateProperty("LogLevel", LogLevelSwitcher.Switch(logEvent.Level));
            logEvent.AddOrUpdateProperty(property);
        }
    }
}
