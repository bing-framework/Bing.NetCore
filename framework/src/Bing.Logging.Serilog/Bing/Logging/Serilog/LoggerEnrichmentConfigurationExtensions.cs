using System;
using Bing.Logging.Serilog.Enrichers;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Configuration;

namespace Bing.Logging.Serilog
{
    /// <summary>
    /// Serilog扩展属性配置(<see cref="LoggerEnrichmentConfiguration"/>) 扩展
    /// </summary>
    public static class LoggerEnrichmentConfigurationExtensions
    {
        /// <summary>
        /// 添加日志上下文扩展属性
        /// </summary>
        /// <param name="source">日志扩展配置</param>
        /// <param name="serviceProvider">服务提供程序</param>
        public static LoggerConfiguration WithLogContext(this LoggerEnrichmentConfiguration source, IServiceProvider serviceProvider)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));
            var enricher = serviceProvider.GetRequiredService<LogContextEnricher>();
            return source.With(enricher);
        }

        /// <summary>
        /// 添加日志级别扩展属性
        /// </summary>
        /// <param name="source">日志扩展配置</param>
        public static LoggerConfiguration WithLogLevel(this LoggerEnrichmentConfiguration source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            return source.With<LogLevelEnricher>();
        }
    }
}
