using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Tests.Modularity;

/// <summary>
/// 应用级根容器构建入口测试。
/// </summary>
public class BingServiceCollectionExtensionsTest
{
    /// <summary>
    /// 验证构建器管理器可在根容器释放时兜底释放模块。
    /// </summary>
    [Fact]
    public void BuildBingServiceProvider_ShouldAttachManagerForDisposeFallback()
    {
        UninitializedDisposableModule.DisposeCount = 0;
        var services = new ServiceCollection();
        services.AddBingApplication<UninitializedDisposableModule>(o => o.AutoRegisterServices = false);

        using (services.BuildBingServiceProvider()) { }

        UninitializedDisposableModule.DisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 验证未初始化的纯异步模块使用异步释放路径。
    /// </summary>
    [Fact]
    public async Task BuildBingServiceProvider_ShouldUseAsyncDisposeForUninitializedAsyncModule()
    {
        UninitializedAsyncModule.DisposeCount = 0;
        var services = new ServiceCollection();
        services.AddBingApplication<UninitializedAsyncModule>(o => o.AutoRegisterServices = false);

        await using (services.BuildBingServiceProvider()) { }

        UninitializedAsyncModule.DisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 验证构建容器校验失败时释放已构造模块。
    /// </summary>
    [Fact]
    public void BuildBingServiceProvider_ValidateOnBuildFailure_ShouldReleaseConstructedModules()
    {
        UninitializedDisposableModule.DisposeCount = 0;
        var services = new ServiceCollection();
        services.AddBingApplication<UninitializedDisposableModule>(o => o.AutoRegisterServices = false);
        services.AddSingleton<RequiresMissingService>();

        Should.Throw<Exception>(() => services.BuildBingServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true
        }));

        UninitializedDisposableModule.DisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 验证构建容器校验失败时同步兜底释放纯异步模块。
    /// </summary>
    [Fact]
    public void BuildBingServiceProvider_ValidateOnBuildFailure_ShouldReleaseAsyncOnlyModulesSynchronously()
    {
        UninitializedAsyncModule.DisposeCount = 0;
        var services = new ServiceCollection();
        services.AddBingApplication<UninitializedAsyncModule>(o => o.AutoRegisterServices = false);
        services.AddSingleton<RequiresMissingService>();

        Should.Throw<Exception>(() => services.BuildBingServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true
        }));

        UninitializedAsyncModule.DisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 验证重复构建在进入底层容器前被拒绝，且不释放已运行应用的模块。
    /// </summary>
    /// <param name="validateOnBuild">是否启用服务提供程序构建时校验。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildBingServiceProvider_RepeatedBuild_ShouldRejectBeforeReleasingAttachedRuntime(bool validateOnBuild)
    {
        var services = new ServiceCollection();
        services.AddBingApplication<TrackedLifecycleModule>(o => o.AutoRegisterServices = false);
        services.AddSingleton<RequiresMissingService>();
        var module = services.Select(d => d.ImplementationInstance)
            .OfType<TrackedLifecycleModule>().Single();
        var first = services.BuildBingServiceProvider();
        try
        {
            first.UseBing();
            module.Initialized.ShouldBe(1);

            var error = Should.Throw<InvalidOperationException>(() =>
                services.BuildBingServiceProvider(new ServiceProviderOptions { ValidateOnBuild = validateOnBuild }));
            error.Message.ShouldContain("模块运行时已创建");

            module.Initialized.ShouldBe(1);
            module.Shutdowns.ShouldBe(0);
            module.Disposals.ShouldBe(0);

            first.ShutdownBing();
            module.Shutdowns.ShouldBe(1);
            module.ShutdownAfterDisposed.ShouldBe(0);
        }
        finally
        {
            first.Dispose();
        }

        module.Disposals.ShouldBe(1);
        module.ShutdownAfterDisposed.ShouldBe(0);
    }

    /// <summary>
    /// 验证已释放的注册记录不能在原集合上重新构建根容器。
    /// </summary>
    [Fact]
    public void BuildBingServiceProvider_AfterRuntimeReleased_ShouldRejectBeforeBuilding()
    {
        var services = new ServiceCollection();
        services.AddBingApplication<TrackedLifecycleModule>(o => o.AutoRegisterServices = false);
        var module = services.Select(d => d.ImplementationInstance)
            .OfType<TrackedLifecycleModule>().Single();
        using (services.BuildBingServiceProvider()) { }
        module.Disposals.ShouldBe(1);

        var error = Should.Throw<InvalidOperationException>(() => services.BuildBingServiceProvider());
        error.Message.ShouldContain("模块运行时已释放");
        module.Disposals.ShouldBe(1);
    }

    /// <summary>
    /// 验证另一集合首次构建失败不会释放已运行应用的模块。
    /// </summary>
    [Fact]
    public void BuildBingServiceProvider_IndependentCollectionFailure_ShouldKeepAttachedRuntimeIntact()
    {
        var firstServices = new ServiceCollection();
        firstServices.AddBingApplication<TrackedLifecycleModule>(o => o.AutoRegisterServices = false);
        var firstModule = firstServices.Select(d => d.ImplementationInstance)
            .OfType<TrackedLifecycleModule>().Single();
        var first = firstServices.BuildBingServiceProvider();
        try
        {
            first.UseBing();

            var otherServices = new ServiceCollection();
            otherServices.AddBingApplication<TrackedLifecycleModule>(o => o.AutoRegisterServices = false);
            otherServices.AddSingleton<RequiresMissingService>();
            var otherModule = otherServices.Select(d => d.ImplementationInstance)
                .OfType<TrackedLifecycleModule>().Single();
            Should.Throw<Exception>(() => otherServices.BuildBingServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true
            }));

            firstModule.Disposals.ShouldBe(0);
            otherModule.Disposals.ShouldBe(1);
            first.ShutdownBing();
            firstModule.Shutdowns.ShouldBe(1);
        }
        finally
        {
            first.Dispose();
        }

        firstModule.Disposals.ShouldBe(1);
        firstModule.ShutdownAfterDisposed.ShouldBe(0);
    }

    /// <summary>
    /// 用于触发根容器校验失败的服务。
    /// </summary>
    public sealed class RequiresMissingService
    {
        /// <summary>
        /// 初始化一个 <see cref="RequiresMissingService" /> 类的新实例。
        /// </summary>
        /// <param name="dependency">故意未注册的依赖。</param>
        public RequiresMissingService(MissingService dependency) { }
    }

    /// <summary>
    /// 未注册的依赖服务标记。
    /// </summary>
    public sealed class MissingService { }

    /// <summary>
    /// 记录同步释放次数的测试模块。
    /// </summary>
    public sealed class UninitializedDisposableModule : BingModule, IDisposable
    {
        /// <summary>
        /// 获取或设置释放次数。
        /// </summary>
        public static int DisposeCount;

        /// <inheritdoc />
        public void Dispose() => DisposeCount++;
    }

    /// <summary>
    /// 记录初始化、关闭和释放顺序的测试模块。
    /// </summary>
    public sealed class TrackedLifecycleModule : BingModule, IDisposable
    {
        /// <summary>
        /// 获取初始化次数。
        /// </summary>
        public int Initialized { get; private set; }

        /// <summary>
        /// 获取关闭次数。
        /// </summary>
        public int Shutdowns { get; private set; }

        /// <summary>
        /// 获取释放次数。
        /// </summary>
        public int Disposals { get; private set; }

        /// <summary>
        /// 获取在释放后执行关闭钩子的次数。
        /// </summary>
        public int ShutdownAfterDisposed { get; private set; }

        /// <inheritdoc />
        public override void OnApplicationInitialization(BingModuleInitializationContext context) =>
            Initialized++;

        /// <inheritdoc />
        public override void OnApplicationShutdown(BingModuleShutdownContext context)
        {
            Shutdowns++;
            if (Disposals != 0)
                ShutdownAfterDisposed++;
        }

        /// <inheritdoc />
        public void Dispose() => Disposals++;
    }

    /// <summary>
    /// 记录异步释放次数的测试模块。
    /// </summary>
    public sealed class UninitializedAsyncModule : BingModule, IAsyncDisposable
    {
        /// <summary>
        /// 获取或设置释放次数。
        /// </summary>
        public static int DisposeCount;

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
