using System.ComponentModel;
using Bing.Core.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Profiling;

namespace Bing.MiniProfiler;

/// <summary>
/// MiniProfiler 模块基类
/// </summary>
[Description(nameof(Bing.AspNetCore.AspNetCoreBingModule))]
public abstract class MiniProfilerModuleBase : Bing.AspNetCore.AspNetCoreBingModule
{
    /// <inheritdoc />
    public override ModuleLevel Level => ModuleLevel.Application;

    /// <inheritdoc />
    /// <remarks>
    /// 模块启动的顺序先按级别启动，同一级别内部再按此顺序启动， 级别默认为0，表示无依赖，需要在同级别有依赖顺序的时候，再重写为&gt;0的顺序值
    /// </remarks>
    public override int Order => 0;

    /// <inheritdoc />
    public override IServiceCollection AddServices(IServiceCollection services)
    {
        var action = GetMiniProfilerAction(services);
        services.AddMiniProfiler(action);
        return services;
    }

    /// <inheritdoc />
    public override void UseModule(IApplicationBuilder app)
    {
        app.UseMiniProfiler();
        Enabled = true;
    }

    /// <summary>
    /// 重写以获取MiniProfiler的选项
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>MiniProfiler 配置操作。</returns>
    protected virtual Action<MiniProfilerOptions> GetMiniProfilerAction(IServiceCollection services)
    {
        return options =>
        {
            // 可通过访问 /profiler/results 访问分析报告
            options.RouteBasePath = "/profiler";
        };
    }
}