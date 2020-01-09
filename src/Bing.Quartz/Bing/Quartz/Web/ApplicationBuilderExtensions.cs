using Bing.Quartz.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Quartz.Web
{
    /// <summary>
    /// 应用程序构建器(<see cref="IApplicationBuilder"/>) 扩展
    /// </summary>
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// 启用Quartz
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        public static IApplicationBuilder UseQuartz(this IApplicationBuilder app)
        {
            app.ApplicationServices.GetService<IQuartzServer>().StartAsync().GetAwaiter().GetResult();
            return app;
        }
    }
}
