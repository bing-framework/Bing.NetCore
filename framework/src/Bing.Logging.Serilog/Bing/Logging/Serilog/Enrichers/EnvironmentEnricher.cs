using System;
using Serilog.Core;
using Serilog.Events;

namespace Bing.Logging.Serilog.Enrichers
{
    /// <summary>
    /// 环境变量扩展属性
    /// </summary>
    internal class EnvironmentEnricher : ILogEventEnricher
    {
        /// <summary>
        /// 环境变量名称
        /// </summary>
        private readonly string _environmentVariable;

        /// <summary>
        /// 初始化一个<see cref="EnvironmentEnricher"/>类型的实例
        /// </summary>
        /// <param name="variableName">环境变量名称</param>
        public EnvironmentEnricher(string variableName) => _environmentVariable = variableName;

        /// <summary>
        /// 扩展属性
        /// </summary>
        /// <param name="logEvent">日志事件</param>
        /// <param name="propertyFactory">日志事件属性工厂</param>
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var envValue = Environment.GetEnvironmentVariable(_environmentVariable);
            if (!string.IsNullOrWhiteSpace(envValue))
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(_environmentVariable, envValue));
        }
    }
}
