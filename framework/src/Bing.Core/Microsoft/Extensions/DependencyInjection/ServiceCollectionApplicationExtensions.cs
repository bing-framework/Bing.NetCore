using Bing;
using Bing.Core.Builders;
using Bing.Core.Modularity;
using Bing.Helpers;
using Bing.Internal;
using Bing.Options;
using Microsoft.Extensions.Configuration;

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
    /// <returns>已完成核心服务和模块注册的 Bing 构建器。</returns>
    public static IBingBuilder AddBing(this IServiceCollection services, Action<BingOptions> setupAction = null)
    {
        Check.NotNull(services, nameof(services));
        var registration = BingModuleRegistration.Register(services, true);
        var options = new BingOptions();
        
        try
        {
            // 配置委托和类型发现同样属于注册阶段，失败后不能重用部分配置的集合。
            services.AddCoreServices();
            services.AddCoreBingServices();
            setupAction?.Invoke(options);
            var builder = services.GetOrAddSingletonInstance<IBingBuilder>(() => new BingBuilder(services));
            builder.AddCoreModule();
            BingLoader.RegisterTypes(services);
            foreach (var extension in options.Extensions)
                extension.AddServices(services);
            return builder;
        }
        catch (Exception ex) { registration.FailConfiguration(ex); throw; }
    }
}
