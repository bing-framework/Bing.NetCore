using System.ComponentModel;
using Bing.AspNetCore;
using Bing.Core.Modularity;

namespace Bing.Samples.WebApi.Modules;

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
        services.AddControllers()
            .AddControllersAsServices();
        services.AddSingleton<SampleLazyDependency>();
        return services;
    }

    /// <summary>
    /// 应用AspNetCore的服务业务
    /// </summary>
    /// <param name="app">应用程序构建器</param>
    public override void UseModule(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapControllers());
    }
}

/// <summary>
/// 示例中通过延迟服务提供程序解析的业务服务。
/// </summary>
public sealed class SampleLazyDependency
{
    /// <summary>
    /// 获取示例业务值。
    /// </summary>
    public string Value => "lazy-service-ready";
}
