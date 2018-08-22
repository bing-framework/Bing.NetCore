using Bing.Logs.Abstractions;
using Bing.Logs.Formats;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Logs.Log4Net
{
    /// <summary>
    /// 日志扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册Log4Net日志操作
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configFile">log4net配置文件</param>
        /// <param name="name">服务名</param>
        public static void AddLog4Net(this IServiceCollection services, string configFile = "log4net.config",
            string name = null)
        {
            services.TryAddScoped<ILogProviderFactory, Bing.Logs.Log4Net.LogProviderFactory>();
            services.TryAddSingleton<ILogFormat, ContentFormat>();
            services.TryAddScoped<ILogContext, Bing.Logs.Core.LogContext>();
            services.TryAddScoped<ILog, Log>();

            Log4NetProvider.InitRepository(configFile);
        }
    }
}
