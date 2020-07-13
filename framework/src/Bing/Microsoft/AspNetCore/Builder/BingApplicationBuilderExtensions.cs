using Bing.DependencyInjection;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// 应用程序构建器(<see cref="IApplicationBuilder"/>) 扩展
    /// </summary>
    public static partial class BingApplicationBuilderExtensions
    {
        /// <summary>
        /// 注册服务服务定位器
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        public static IApplicationBuilder UseServiceLocator(this IApplicationBuilder app)
        {
            ServiceLocator.Instance.SetApplicationServiceProvider(app.ApplicationServices);
            return app;
        }
    }
}
