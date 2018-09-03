using System;
using Bing.Logs.Abstractions;
using Bing.Logs.Formats;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;

namespace Bing.Logs.Serilog
{
    /// <summary>
    /// 日志扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册Serilog日志操作
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configAction">日志配置</param>
        public static void AddSerilog(this IServiceCollection services, Action<LoggerConfiguration> configAction = null)
        {
            if (configAction != null)
            {
                var configuration = new LoggerConfiguration();
                configAction.Invoke(configuration);
            }
            else
            {
                SerilogProvider.InitConfiguration();
            }
            services.TryAddSingleton<ILogProviderFactory,Bing.Logs.Serilog.LogProviderFatory>();
            services.TryAddSingleton<ILogFormat, ContentFormat>();
            services.TryAddScoped<ILogContext,Bing.Logs.Core.LogContext>();
            services.TryAddScoped<ILog, Log>();
        }
    }
}
