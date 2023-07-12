using Bing;
using Bing.Core.Builders;
using Bing.Helpers;
using Bing.Options;
using Bing.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 服务集合 - 应用程序 扩展
/// </summary>
public static class ServiceCollectionApplicationExtensions
{
    /// <summary>
    /// 创建<see cref="IBingBuilder"/>，开始构建Bing服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">服务配置项操作</param>
    public static IBingBuilder AddBing(this IServiceCollection services, Action<BingOptions> setupAction = null)
    {
        Check.NotNull(services, nameof(services));
        var configuration = services.GetConfiguration();
        var options = new BingOptions();
        setupAction?.Invoke(options);

        Singleton<IConfiguration>.Instance = configuration;
        // 初始化所有程序集查找器
        services.TryAddSingleton<IAllAssemblyFinder>(new AppDomainAllAssemblyFinder());

        var builder = services.GetOrAddSingletonInstance<IBingBuilder>(() => new BingBuilder(services));

        builder.AddCoreModule();
        BingLoader.RegisterTypes(services);

        foreach (var extension in options.Extensions)
            extension.AddServices(services);
        return builder;
    }
}
