using Bing.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Profiling;

namespace Bing.Samples.Modules
{
    /// <summary>
    /// MiniProfiler 模块
    /// </summary>
    public class MiniProfilerModule : BingModule
    {
        /// <summary>
        /// 配置服务集合
        /// </summary>
        /// <param name="context">配置服务上下文</param>
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            // 注册MiniProfiler
            context.Services.AddMiniProfiler(options =>
            {
                // 设置弹出框位置为左下角
                options.PopupRenderPosition = RenderPosition.BottomLeft;
                // 设置弹出的明细窗口里显示Time With Children这列
                options.PopupShowTimeWithChildren = true;
                // 设置访问分析结果URL的路由基地址
                options.RouteBasePath = "/profiler";
            }).AddEntityFramework();
        }

        /// <summary>
        /// 应用程序预初始化
        /// </summary>
        /// <param name="context">应用程序初始化上下文</param>
        public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
        {
            context.GetApplicationBuilder().UseMiniProfiler();
        }
    }
}
