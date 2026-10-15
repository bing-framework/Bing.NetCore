using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bing.Core.Tests.Modularity;

/// <summary>
/// 验证 Generic Host 承担模块初始化和关闭的行为。
/// </summary>
public sealed class BingHostedLifecycleTest
{
    /// <summary>
    /// 启用托管初始化后，启动和停止服务应各执行一次模块生命周期。
    /// </summary>
    [Fact]
    public async Task HostedLifecycle_ShouldInitializeAndShutdownModules()
    {
        HostedModule.Reset();
        var services = new ServiceCollection();
        services.AddBingApplication<HostedModule>(options => options.AutoRegisterServices = false);
        services.EnableBingHostedInitialization();

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>()
            .Single(item => item.GetType().Name == "BingModuleHostedService");

        await hosted.StartAsync(CancellationToken.None);
        HostedModule.Initialized.ShouldBe(1);
        HostedModule.Shutdown.ShouldBe(0);

        await hosted.StopAsync(CancellationToken.None);
        HostedModule.Shutdown.ShouldBe(1);
    }

    /// <summary>
    /// 未启用托管初始化时，启动服务不能抢先初始化 Web 模块。
    /// </summary>
    [Fact]
    public async Task HostedLifecycle_WhenNotEnabled_ShouldPreserveWebInitializationTiming()
    {
        HostedModule.Reset();
        var services = new ServiceCollection();
        services.AddBingApplication<HostedModule>(options => options.AutoRegisterServices = false);

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>()
            .Single(item => item.GetType().Name == "BingModuleHostedService");

        await hosted.StartAsync(CancellationToken.None);
        HostedModule.Initialized.ShouldBe(0);
    }

    /// <summary>
    /// 记录托管生命周期调用次数的测试模块。
    /// </summary>
    private sealed class HostedModule : BingModule
    {
        /// <summary>
        /// 记录模块初始化次数。
        /// </summary>
        public static int Initialized;

        /// <summary>
        /// 记录模块关闭次数。
        /// </summary>
        public static int Shutdown;

        /// <summary>
        /// 重置托管模块生命周期计数。
        /// </summary>
        public static void Reset()
        {
            Initialized = 0;
            Shutdown = 0;
        }

        /// <inheritdoc />
        public override void OnApplicationInitialization(BingModuleInitializationContext context) =>
            Interlocked.Increment(ref Initialized);

        /// <inheritdoc />
        public override void OnApplicationShutdown(BingModuleShutdownContext context) =>
            Interlocked.Increment(ref Shutdown);
    }
}
