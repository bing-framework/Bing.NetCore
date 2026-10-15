using AspectCore.Extensions.Hosting;
using Bing.Aspects;
using Bing.Core.Modularity;
using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bing.Aop.AspectCore;

/// <summary>
/// 验证 AspectCore 宿主与 Bing 模块共同运行时的释放职责。
/// </summary>
public class AspectCoreBingModuleLifecycleTest
{
    /// <summary>
    /// 验证宿主停止执行模块关闭，容器释放执行服务释放。
    /// </summary>
    [Fact]
    public async Task HostStopAndDispose_ShouldShutdownModuleAndDisposeInjectedServiceOnce()
    {
        var host = new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.EnableAutowired();
                services.AddSingleton<OwnedService>();
                services.AddTransient<PropertyConsumer>();
                services.AddBingApplication<LifecycleModule>(options => options.AutoRegisterServices = false);
            })
            .Build();
        await host.StartAsync();
        await host.Services.UseBingAsync();
        var owned = host.Services.GetRequiredService<OwnedService>();
        Assert.Same(owned, host.Services.GetRequiredService<PropertyConsumer>().Service);

        await host.StopAsync();
        Assert.Equal(1, owned.ShutdownCount);
        Assert.Equal(0, owned.DisposeCount);
        host.Dispose();
        Assert.Equal(1, owned.DisposeCount);
    }

    /// <summary>
    /// 验证模块注册的服务可以通过属性注入获得延迟服务提供程序。
    /// </summary>
    [Fact]
    public async Task ModuleService_ShouldUseInjectedLazyServiceProvider()
    {
        using var host = new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.EnableAutowired();
                services.AddBingApplication<LazyServiceModule>(options => options.AutoRegisterServices = false);
            })
            .Build();

        await host.StartAsync();
        await host.Services.UseBingAsync();

        var consumer = host.Services.GetRequiredService<ModuleLazyConsumer>();

        Assert.NotNull(consumer.LazyServiceProvider);
        Assert.Same(
            host.Services.GetRequiredService<ModuleLazyDependency>(),
            consumer.GetDependency());
    }

    /// <summary>
    /// 接收属性注入的服务。
    /// </summary>
    public sealed class PropertyConsumer
    {
        /// <summary>
        /// 获取或设置注入的服务。
        /// </summary>
        [Autowired]
        public OwnedService Service { get; set; }
    }

    /// <summary>
    /// 由容器创建和释放的服务。
    /// </summary>
    public sealed class OwnedService : IDisposable
    {
        /// <summary>
        /// 获取模块关闭次数。
        /// </summary>
        public int ShutdownCount;

        /// <summary>
        /// 获取服务释放次数。
        /// </summary>
        public int DisposeCount;

        /// <inheritdoc />
        public void Dispose() => Interlocked.Increment(ref DisposeCount);
    }

    /// <summary>
    /// 在宿主停止时记录关闭的模块。
    /// </summary>
    public sealed class LifecycleModule : BingModule
    {
        /// <inheritdoc />
        public override void OnApplicationShutdown(BingModuleShutdownContext context) =>
            Interlocked.Increment(ref context.ServiceProvider.GetRequiredService<OwnedService>().ShutdownCount);
    }

    /// <summary>
    /// 注册使用延迟服务提供程序的模块服务。
    /// </summary>
    public sealed class LazyServiceModule : BingModule
    {
        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            services.AddTransient<ILazyServiceProvider, LazyServiceProvider>();
            services.AddSingleton<ModuleLazyDependency>();
            services.AddTransient<ModuleLazyConsumer>();
            return services;
        }
    }

    /// <summary>
    /// 通过延迟服务提供程序解析模块依赖的服务。
    /// </summary>
    public sealed class ModuleLazyConsumer
    {
        /// <summary>
        /// 获取或设置延迟服务提供程序。
        /// </summary>
        [Autowired]
        public ILazyServiceProvider LazyServiceProvider { get; set; }

        /// <summary>
        /// 解析模块依赖。
        /// </summary>
        public ModuleLazyDependency GetDependency() =>
            LazyServiceProvider.LazyGetRequiredService<ModuleLazyDependency>();
    }

    /// <summary>
    /// 模块注册的延迟解析依赖。
    /// </summary>
    public sealed class ModuleLazyDependency;
}
