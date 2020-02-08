using Microsoft.AspNetCore.Builder;

namespace Bing.Webs.Extensions
{
    /// <summary>
    /// 应用构建器(<see cref="IApplicationBuilder"/>) 扩展
    /// </summary>
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// 添加MVC并Area路由支持
        /// </summary>
        /// <param name="app">应用构建器</param>
        /// <param name="area">是否支持区域路由</param>
        /// <returns></returns>
        public static IApplicationBuilder UseMvcWithAreaRoute(this IApplicationBuilder app, bool area = true)
        {
            return app.UseMvc(builder =>
            {
                if (area)
                {
                    builder.MapRoute("area", "{area:exists}/{controller=Home}/{action=Index}/{id?}");
                }

                builder.MapRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
