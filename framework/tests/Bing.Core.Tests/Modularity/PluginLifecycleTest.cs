using System.Reflection;
using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Tests.Modularity;

/// <summary>
/// 启动期插件异步生命周期测试。
/// </summary>
[Collection("Bing启动期插件")]
public sealed class PluginLifecycleTest
{
    /// <summary>
    /// 异步生命周期模块完整名称。
    /// </summary>
    private const string AsyncModuleName = "Bing.Core.PluginFixtures.FixtureAsyncLifecycleModule";

    /// <summary>
    /// 验证插件异步初始化优先于同步钩子，并在异步关闭时只释放一次。
    /// </summary>
    [Fact]
    public async Task UseBingAsync_ShouldRunPluginAsyncLifecycleAndDisposeOnce()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.AsyncLifecycle", "1.0.0", new[] { AsyncModuleName });
        var services = new ServiceCollection();
        services.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddDirectory(directory));

        using var provider = services.BuildBingServiceProvider();
        var assembly = provider.GetRequiredService<IBingPluginContainer>().Plugins.Single().EntryAssembly;
        PluginTestWorkspace.ResetFixtureState(assembly);

        Assert.Throws<InvalidOperationException>(() => provider.UseBing());
        var entered = GetTask(assembly, "AsyncInitializeEntered");
        var initialization = provider.UseBingAsync();
        await entered.WaitAsync(TimeSpan.FromSeconds(2));
        Invoke(assembly, "ReleaseAsyncInitialization");
        await initialization;

        GetCount(assembly, "AsyncInitializeCount").ShouldBe(1);
        GetCount(assembly, "AsyncUseModuleCount").ShouldBe(0);

        await provider.ShutdownBingAsync();
        await provider.ShutdownBingAsync();
        GetCount(assembly, "AsyncShutdownCount").ShouldBe(1);
        GetCount(assembly, "AsyncDisposeCount").ShouldBe(1);
    }

    /// <summary>
    /// 验证插件异步初始化取消后执行清理并禁止隐式重试。
    /// </summary>
    [Fact]
    public async Task UseBingAsync_Cancellation_ShouldShutdownAndReleasePluginOnce()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.AsyncCancellation", "1.0.0", new[] { AsyncModuleName });
        var services = new ServiceCollection();
        services.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddDirectory(directory));

        using var provider = services.BuildBingServiceProvider();
        var assembly = provider.GetRequiredService<IBingPluginContainer>().Plugins.Single().EntryAssembly;
        PluginTestWorkspace.ResetFixtureState(assembly);
        var entered = GetTask(assembly, "AsyncInitializeEntered");
        using var cancellation = new CancellationTokenSource();
        var initialization = provider.UseBingAsync(cancellation.Token);

        await entered.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initialization);

        GetCount(assembly, "AsyncShutdownCount").ShouldBe(1);
        GetCount(assembly, "AsyncDisposeCount").ShouldBe(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.UseBingAsync());
    }

    /// <summary>
    /// 读取插件夹具状态属性。
    /// </summary>
    /// <param name="assembly">插件夹具程序集。</param>
    /// <param name="propertyName">属性名称。</param>
    /// <returns>状态计数。</returns>
    private static int GetCount(Assembly assembly, string propertyName) =>
        (int)assembly.GetType("Bing.Core.PluginFixtures.PluginFixtureState", true)!
            .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;

    /// <summary>
    /// 获取插件夹具中的异步信号。
    /// </summary>
    /// <param name="assembly">插件夹具程序集。</param>
    /// <param name="propertyName">信号属性名称。</param>
    /// <returns>异步信号任务。</returns>
    private static Task GetTask(Assembly assembly, string propertyName) =>
        (Task)assembly.GetType("Bing.Core.PluginFixtures.PluginFixtureState", true)!
            .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;

    /// <summary>
    /// 调用插件夹具中的静态控制方法。
    /// </summary>
    /// <param name="assembly">插件夹具程序集。</param>
    /// <param name="methodName">方法名称。</param>
    private static void Invoke(Assembly assembly, string methodName) =>
        assembly.GetType("Bing.Core.PluginFixtures.PluginFixtureState", true)!
            .GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, null);
}
