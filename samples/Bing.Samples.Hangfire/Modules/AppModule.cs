using System.ComponentModel;
using System.Text;
using AspectCore.Configuration;
using Bing.AspNetCore;
using Bing.Core.Modularity;
using Bing.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Samples.Hangfire.Modules
{
    /// <summary>
    /// 应用程序模块
    /// </summary>
    [Description("应用程序模块")]
    [DependsOnModule(typeof(AspNetCoreModule))]
    public class AppModule : AspNetCoreBingModule
    {
        /// <summary>
        /// 模块级别。级别越小越先启动
        /// </summary>
        public override ModuleLevel Level => ModuleLevel.Application;

        /// <summary>
        /// 添加服务。将模块服务添加到依赖注入服务容器中
        /// </summary>
        /// <param name="services">服务集合</param>
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            services.EnableAop(o =>
            {
                o.ThrowAspectException = false;
                o.NonAspectPredicates.AddNamespace("Bing.Swashbuckle");
                o.NonAspectPredicates.AddNamespace("DotNetCore.CAP");
            });
            return services;
        }

        /// <summary>
        /// 应用AspNetCore的服务业务
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        public override void UseModule(IApplicationBuilder app)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            app.UseAuthentication();
            Enabled = true;
        }
    }
}
