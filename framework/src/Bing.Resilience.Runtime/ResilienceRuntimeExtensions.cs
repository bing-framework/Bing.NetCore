using Microsoft.Extensions.DependencyInjection;

namespace Bing.Resilience;

/// <summary>为稳定管线句柄启用切换；不会动态加载未登记的引擎。</summary>
public static class ResilienceRuntimeExtensions
{
    public static BingResilienceBuilder AddRuntimeSwitching(this BingResilienceBuilder builder, Action<ResilienceRuntimeOptions>? configure = null)
    {
        if (builder.Services.Any(x => x.ServiceType == typeof(ResilienceRuntimeOptions)))
            throw new ResilienceConfigurationException("运行时切换已登记。");
        var options = new ResilienceRuntimeOptions(); configure?.Invoke(options);
        if (options.MaxRetainedGenerations < 2 || options.DrainTimeout <= TimeSpan.Zero || options.DrainTimeout > TimeSpan.FromDays(1))
            throw new ResilienceConfigurationException("代数量或排空等待时间无效。");
        builder.Services.AddSingleton(new ResilienceRuntimeOptions { MaxRetainedGenerations = options.MaxRetainedGenerations, DrainTimeout = options.DrainTimeout });
        builder.Services.AddSingleton(sp => (IResilienceRuntimeManager)sp.GetRequiredService<IResiliencePipelineProvider>());
        return builder;
    }
}
