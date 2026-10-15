using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Tests.Modularity;

/// <summary>
/// 验证迁移指南中的统一接口消费者写法。
/// </summary>
[Collection("Module static compatibility")]
public class ModuleUnifiedInterfaceConsumerTest
{
    /// <summary>
    /// 显式实现的消费者完成异步注册和初始化。
    /// </summary>
    [Fact]
    public async Task ExplicitUnifiedInterfaces_RegisterAndInitialize()
    {
        var services = new ServiceCollection();
        await services.AddBingApplicationAsync<CacheModule>(options => options.AutoRegisterServices = false);
        await using var provider = services.BuildServiceProvider();

        await provider.UseBingAsync();
        provider.GetRequiredService<CacheService>().Initialized.ShouldBeTrue();
        await provider.ShutdownBingAsync();
    }

    /// <summary>
    /// 示例缓存服务。
    /// </summary>
    public sealed class CacheService
    {
        /// <summary>
        /// 获取是否已初始化。
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// 初始化缓存。
        /// </summary>
        public Task WarmUpAsync(CancellationToken cancellationToken)
        {
            Initialized = true;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 使用统一接口的示例模块。
    /// </summary>
    public sealed class CacheModule : BingModule, IPreConfigureServices,
        IOnPostApplicationInitialization
    {
        /// <inheritdoc />
        void IPreConfigureServices.PreConfigureServices(IServiceCollection services) =>
            services.AddSingleton<CacheService>();

        /// <inheritdoc />
        Task IPreConfigureServices.PreConfigureServicesAsync(
            IServiceCollection services, CancellationToken cancellationToken)
        {
            services.AddSingleton<CacheService>();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        void IOnPostApplicationInitialization.OnPostApplicationInitialization(
            BingModuleInitializationContext context)
        {
        }

        /// <inheritdoc />
        Task IOnPostApplicationInitialization.OnPostApplicationInitializationAsync(
            BingModuleInitializationContext context, CancellationToken cancellationToken) =>
            context.ServiceProvider.GetRequiredService<CacheService>().WarmUpAsync(cancellationToken);
    }
}
