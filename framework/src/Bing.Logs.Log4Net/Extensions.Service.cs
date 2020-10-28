using Bing.Logs.Abstractions;
using Bing.Logs.Core;
using Bing.Logs.Formats;
using Bing.Users;
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

        /// <summary>
        /// 注册Log4Net日志操作。使用日志工厂，实现混合日志
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="name">名称</param>
        /// <param name="configFile">log4net配置文件</param>
        public static void AddLog4NetWithFactory(this IServiceCollection services,
            string name = LogConst.DefaultLog4NetName, string configFile = "log4net.config")
        {
            services.TryAddSingleton<ILogFormat, ContentFormat>();
            services.TryAddScoped<ILogContext, Bing.Logs.Core.LogContext>();
            services.AddSingleton<ILogFactory, DefaultLogFactory>();
            services.AddScoped<ILog, Log>(x =>
            {
                var format = x.GetService<ILogFormat>();
                var provider = new LogProviderFactory().Create(name, format);
                var context = x.GetService<ILogContext>();
                var currentUser = x.GetService<ICurrentUser>();
                return new Log(name, provider, context, currentUser, "");
            });
            Log4NetProvider.InitRepository(configFile);
        }
    }
}
