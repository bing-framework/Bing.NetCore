using System;
using Bing.Logs.Abstractions;
using Bing.Logs.Core;
using Bing.Sessions;
using Exceptionless;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Logs.Exceptionless
{
    /// <summary>
    /// 日志扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册Exceptionless日志操作
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configAction">配置操作</param>
        /// <param name="name">服务名称</param>
        public static void AddExceptionless(this IServiceCollection services,
            Action<ExceptionlessConfiguration> configAction, string name = null)
        {
            services.TryAddScoped<ILogProviderFactory, Bing.Logs.Exceptionless.LogProviderFactory>();
            services.TryAddSingleton(typeof(ILogFormat), t => NullLogFormat.Instance);
            services.TryAddScoped<ILogContext, Bing.Logs.Core.LogContext>();
            services.TryAddScoped<ILog, Log>();

            configAction?.Invoke(ExceptionlessClient.Default.Configuration);
        }

        /// <summary>
        /// 注册Exceptionless日志操作。使用日志工厂，实现混合日志
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configAction">配置操作</param>
        /// <param name="name">名称</param>
        public static void AddExceptionlessWithFactory(this IServiceCollection services, Action<ExceptionlessConfiguration> configAction, string name = LogConst.DefaultExceptionlessName)
        {
            services.AddScoped<ILogFactory, DefaultLogFactory>();
            services.TryAddScoped<ILogContext, Bing.Logs.Core.LogContext>();
            services.AddScoped<ILog, Log>(x =>
            {
                var provider = new LogProviderFactory().Create(name, NullLogFormat.Instance);
                var logContext = x.GetService<ILogContext>();
                var session = x.GetService<ISession>();
                return new Log(name, provider, logContext, session, "");
            });
            configAction?.Invoke(ExceptionlessClient.Default.Configuration);
        }
    }
}
