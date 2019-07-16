using Bing.Core.Modularity;
using Bing.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore
{
    /// <summary>
    /// 应用程序构建器(<see cref="IApplicationBuilder"/>) 扩展
    /// </summary>
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Bing框架初始化。适用于AspNetCore环境
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        public static IApplicationBuilder UseBing(this IApplicationBuilder app)
        {
            var provider = app.ApplicationServices;
            if (!(provider.GetService<IBingModuleManager>() is IAspNetCoreUseModule aspNetCoreModule))
                throw new Warning("接口 IBingModuleManager 的注入类型不正确，该类型应同时实现接口 IAspNetCoreUseModule");
            aspNetCoreModule.UseModule(app);
            return app;
        }

        /// <summary>
        /// 添加MVC并支持Area路由
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        /// <param name="area">是否支持Area路由</param>
        public static IApplicationBuilder UseMvcWithAreaRoute(this IApplicationBuilder app, bool area = true)
        {
            return app.UseMvc(builder =>
            {
                if (area)
                    builder.MapRoute("area", "{area:exists}/{controller}/{action=Index}/{id?}");
                builder.MapRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
