using Bing.Logs.Abstractions;
using Bing.Logs.Core;
using Bing.Logs.Formats;
using Bing.Users;
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
            services.TryAddSingleton<ILogProviderFactory, Bing.Logs.NLog.LogProviderFactory>();
            services.TryAddSingleton<ILogFormat, ContentFormat>();
            services.TryAddScoped<ILogContext, Bing.Logs.Core.LogContext>();
            services.TryAddScoped<ILog, Log>();
        }

        /// <summary>
        /// 注册NLog日志操作。使用日志工厂，实现混合日志
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="name">名称</param>
        public static void AddNLogWithFactory(this IServiceCollection services, string name = LogConst.DefaultNLogName)
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
        }
    }
}
