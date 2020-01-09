using System.Collections.Specialized;
using Bing.Quartz.Abstractions;
using Bing.Quartz.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Quartz.Web
{
    /// <summary>
    /// Quartz服务扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册Quartz服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="props">属性参数</param>
        public static void AddQuartz(this IServiceCollection services, NameValueCollection props)
        {
            // TODO: 此处需要通过反射注入所有模块的任务服务
            services.TryAddTransient<IJobLogger, JobLogger>();
            services.TryAddSingleton<IQuartzServer>(sp => new QuartzServer(props, sp));
        }
    }
}
