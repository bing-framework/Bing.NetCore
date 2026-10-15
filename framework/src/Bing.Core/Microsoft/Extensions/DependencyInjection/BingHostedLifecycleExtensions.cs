using Bing.Core.Modularity;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 配置 Generic Host 承担 Bing 模块启动初始化。
/// </summary>
public static class BingHostedLifecycleExtensions
{
    /// <summary>
    /// 启用 Generic Host 启动时的异步模块初始化。
    /// </summary>
    /// <param name="services">应用服务集合。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>
    /// 适用于没有 ASP.NET Core Web 管道的 Consumer 或 Task 宿主。
    /// Web 宿主不调用此入口，继续在 <c>UseBing</c> 或 <c>UseBingAsync</c> 中装配管道并初始化模块。
    /// </remarks>
    public static IServiceCollection EnableBingHostedInitialization(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        var registration = BingModuleRegistration.Get(services)
            ?? throw new InvalidOperationException("请先调用 AddBing 或 AddBingApplication 注册模块。");
        registration.EnableHostedInitialization();
        return services;
    }
}
