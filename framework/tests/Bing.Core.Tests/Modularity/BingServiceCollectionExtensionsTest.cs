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
    /// 用于触发根容器校验失败的服务。
    /// </summary>
    public sealed class RequiresMissingService
    {
        /// <summary>
        /// 初始化缺少依赖的测试服务。
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
