using System;
using System.Collections.Generic;
using System.Text;
using Bing.Logs.Abstractions;
using Bing.Logs.Formats;
using Microsoft.Extensions.DependencyInjection;

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
            services.AddScoped<ILogProviderFactory, Bing.Logs.NLog.LogProviderFactory>();
            services.AddSingleton<ILogFormat, ContentFormat>();
            services.AddScoped<ILogContext, Bing.Logs.Core.LogContext>();
            services.AddScoped<ILog, Log>();
        }
    }
}
