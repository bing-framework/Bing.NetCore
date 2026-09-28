using Bing.Core.Modularity;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 为 Bing 应用提供带生命周期激活的根容器构建入口。
/// </summary>
public static class BingServiceCollectionExtensions
{
    /// <summary>
    /// 构建服务提供程序，并在返回前解析 Bing 模块管理器。
    /// </summary>
    /// <param name="services">应用服务集合。</param>
    /// <param name="options">服务提供程序选项。</param>
    /// <returns>已附加 Bing 模块管理器的根服务提供程序。</returns>
    /// <remarks>
    /// 该入口不初始化模块；应用仍需调用 UseBing/UseBingAsync。
    /// 管理器提前解析后，根容器的 using/await using 释放可以覆盖尚未初始化的模块；
    /// 仅实现 IAsyncDisposable 的模块必须使用 await using。
    /// </remarks>
    public static ServiceProvider BuildBingServiceProvider(this IServiceCollection services,
        ServiceProviderOptions options = null)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        BingModuleRegistration.Get(services)?.EnsureBuildable();

        ServiceProvider provider;
        try
        {
            provider = options == null
                ? services.BuildServiceProvider()
                : services.BuildServiceProvider(options);
        }
        catch (Exception error)
        {
            // ValidateOnBuild 可能在根容器返回前失败；此时仍须释放已构造的模块。
            BingModuleRegistration.Get(services)?.FailConfiguration(error);
            throw;
        }

        try
        {
            if (services.Any(d => d.ServiceType == typeof(IBingModuleManager)))
                provider.GetRequiredService<IBingModuleManager>();
            return provider;
        }
        catch (Exception error)
        {
            try { provider.Dispose(); }
            catch (Exception cleanupError) { error.Data["Bing.ProviderCleanupError"] = cleanupError; }
            throw;
        }
    }
}
