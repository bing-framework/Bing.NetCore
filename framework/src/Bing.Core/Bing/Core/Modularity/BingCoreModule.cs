using System.ComponentModel;
using Bing.Logging;
using Bing.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Core.Modularity;

/// <summary>
/// Bing 核心模块
/// </summary>
[Description("Bing核心模块")]
public class BingCoreModule : BingModule
{
    /// <inheritdoc />
    public override ModuleLevel Level => ModuleLevel.Core;

    /// <inheritdoc />
    public override IServiceCollection AddServices(IServiceCollection services)
    {
        services.TryAddSingleton<StartupLogger>();
        // 注册系统时钟，测试中可替换为 FakeClock 以控制时间
        services.TryAddSingleton<IClock, SystemClock>();
        return services;
    }
}
