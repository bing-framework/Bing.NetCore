using System.ComponentModel;
using Bing.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Core.Modularity;

/// <summary>
/// Bing 核心模块
/// </summary>
[Description("Bing核心模块")]
public class BingCoreModule : BingModule
{
    /// <summary>
    /// 模块级别
    /// </summary>
    public override ModuleLevel Level => ModuleLevel.Core;

    /// <summary>
    /// 添加服务。将模块服务添加到依赖注入服务容器中
    /// </summary>
    /// <param name="services">服务集合</param>
    public override IServiceCollection AddServices(IServiceCollection services)
    {
        services.TryAddSingleton<StartupLogger>();
        return services;
    }

    /// <summary>
    /// 应用模块服务
    /// </summary>
    /// <param name="provider">服务提供程序</param>
    public override void UseModule(IServiceProvider provider)
    {
        //var environment = provider.GetService<IHostingEnvironment>();
        //Web.Environment = environment;
        base.UseModule(provider);
    }
}
