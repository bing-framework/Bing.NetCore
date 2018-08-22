using Bing.Logs.Abstractions;
using Bing.Logs.Formats;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Logs.NLog
{
    /// <summary>
    /// 日志扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册NLog日志操作
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="name">服务名称</param>
        public static void AddNLog(this IServiceCollection services, string name = null)
        {            
            services.TryAddScoped<ILogProviderFactory, Bing.Logs.NLog.LogProviderFactory>();
            services.TryAddSingleton<ILogFormat, ContentFormat>();
            services.TryAddScoped<ILogContext, Bing.Logs.Core.LogContext>();
            services.TryAddScoped<ILog, Log>();
        }
    }
}
