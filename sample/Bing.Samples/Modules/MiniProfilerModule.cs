using Bing.AspNetCore;
using Bing.Core.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Profiling;

namespace Bing.Samples.Modules
{
    /// <summary>
    /// MiniProfiler 模块
    /// </summary>
    [DependsOnModule(typeof(AspNetCoreModule))]
    public class MiniProfilerModule : AspNetCoreBingModule
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
            // 注册MiniProfiler
            services.AddMiniProfiler(options =>
            {
                // 设置弹出框位置为左下角
                options.PopupRenderPosition = RenderPosition.BottomLeft;
                // 设置弹出的明细窗口里显示Time With Children这列
                options.PopupShowTimeWithChildren = true;
                // 设置访问分析结果URL的路由基地址
                options.RouteBasePath = "/profiler";
            }).AddEntityFramework();
            return services;
        }

        /// <summary>
        /// 应用AspNetCore的服务业务
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        public override void UseModule(IApplicationBuilder app)
        {
            app.UseMiniProfiler();
            Enabled = false;
        }
    }
}
