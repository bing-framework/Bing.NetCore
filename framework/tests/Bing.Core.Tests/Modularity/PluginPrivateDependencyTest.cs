using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Tests.Modularity;

/// <summary>
/// 验证私有方法体依赖在临时解析结束后仍然可用。
/// </summary>
[Collection("Bing启动期插件")]
public sealed class PluginPrivateDependencyTest
{
    /// <summary>
    /// 私有依赖入口模块名称。
    /// </summary>
    private const string ModuleName = "Bing.Core.PluginLazyFixture.LazyModule";

    /// <summary>
    /// 验证默认上下文的配置、初始化和运行期调用。
    /// </summary>
    [Fact]
    public async Task DefaultContext_ShouldLoadPrivateClosureWithoutScanningIt()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = CreatePlugin(workspace);
        var services = new ServiceCollection();
        await services.AddBingApplicationAsync<PluginHostModule>(options => options.PluginSources.AddDirectory(directory));
        await using var provider = services.BuildBingServiceProvider();
        await provider.UseBingAsync();
        AssertService(provider);
        await provider.ShutdownBingAsync();
    }

    /// <summary>
    /// 验证热更新私有依赖与旧代一起回收。
    /// </summary>
    [Fact]
    public async Task HotContext_ShouldLoadPrivateClosureAndCollectOldGeneration()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = CreatePlugin(workspace);
        await using var host = new BingHotPluginHost<PluginHostModule>(options => options.PluginSources.AddDirectory(directory));
        await host.ReloadAsync();
        var previous = await InspectGenerationAsync(host);
        await host.ReloadAsync();
        await InspectGenerationAsync(host);
        await AssertCollectedAsync(previous);
    }

    /// <summary>
    /// 验证候选配置失败后私有依赖可回收且旧代继续服务。
    /// </summary>
    [Fact]
    public async Task FailedCandidate_ShouldCollectPrivateClosureAndKeepOldGeneration()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = CreatePlugin(workspace);
        var fail = false;
        await using var host = new BingHotPluginHost<PluginHostModule>(options =>
        {
            options.PluginSources.AddDirectory(directory);
            if (fail) options.AdditionalModules.Add(typeof(FailingConfigurationModule));
        });
        await host.ReloadAsync();
        fail = true;
        var candidate = await CaptureFailedCandidateAsync(host);
        await InspectGenerationAsync(host);
        await AssertCollectedAsync(candidate);
    }

    /// <summary>
    /// 创建包含私有依赖链的插件。
    /// </summary>
    /// <param name="workspace">测试工作区。</param>
    /// <returns>插件目录。</returns>
    private static string CreatePlugin(PluginTestWorkspace workspace)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "PluginFixtures");
        var directory = workspace.CreatePluginFromAssembly("Lazy.Private", "1.0.0", new[] { ModuleName },
            Path.Combine(source, "Bing.Core.PluginLazyFixture.dll"));
        foreach (var name in new[] { "Bing.Core.PluginLazyHelper", "Bing.Core.PluginLazyLeaf" })
            File.Copy(Path.Combine(source, name + ".dll"), Path.Combine(directory, name + ".dll"));
        return directory;
    }

    /// <summary>
    /// 验证插件服务与程序集扫描边界。
    /// </summary>
    /// <param name="provider">插件服务提供程序。</param>
    private static void AssertService(IServiceProvider provider)
    {
        provider.GetRequiredService<Func<string>>()().ShouldBe("private:private:private");
        var module = provider.GetRequiredService<IBingModuleContainer>().Modules.Single(item => item.ModuleType.FullName == ModuleName);
        module.Assemblies.Count.ShouldBe(1);
        module.Assemblies.Single().ShouldBeSameAs(module.Assembly);
        var context = AssemblyLoadContext.GetLoadContext(module.Assembly);
        foreach (var name in new[] { "Bing.Core.PluginLazyHelper", "Bing.Core.PluginLazyLeaf" })
            context.Assemblies.Count(item => item.GetName().Name == name).ShouldBe(1);
    }

    /// <summary>
    /// 获取不持有插件对象的上下文弱引用。
    /// </summary>
    /// <param name="host">插件宿主。</param>
    /// <returns>当前上下文弱引用。</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task<WeakReference> InspectGenerationAsync(BingHotPluginHost<PluginHostModule> host) =>
        host.RunAsync((provider, _) =>
        {
            AssertService(provider);
            var assembly = provider.GetRequiredService<IBingPluginContainer>().Plugins.Single().EntryAssembly;
            return Task.FromResult(new WeakReference(AssemblyLoadContext.GetLoadContext(assembly)));
        });

    /// <summary>
    /// 捕获失败候选的上下文弱引用。
    /// </summary>
    /// <param name="host">插件宿主。</param>
    /// <returns>失败候选的上下文弱引用。</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> CaptureFailedCandidateAsync(BingHotPluginHost<PluginHostModule> host)
    {
        WeakReference candidate = null;
        AssemblyLoadEventHandler observe = (_, args) =>
        {
            if (args.LoadedAssembly.GetName().Name == "Bing.Core.PluginLazyHelper")
                candidate = new WeakReference(AssemblyLoadContext.GetLoadContext(args.LoadedAssembly));
        };
        AppDomain.CurrentDomain.AssemblyLoad += observe;
        try
        {
            var error = await Should.ThrowAsync<InvalidOperationException>(() => host.ReloadAsync());
            error.Message.ShouldBe("candidate configuration failed");
            error.Data["Bing.ModulePhase"].ShouldBe("AddServices");
        }
        finally { AppDomain.CurrentDomain.AssemblyLoad -= observe; }
        candidate.ShouldNotBeNull();
        return candidate;
    }

    /// <summary>
    /// 验证加载上下文可被回收。
    /// </summary>
    /// <param name="reference">待验证的弱引用。</param>
    private static async Task AssertCollectedAsync(WeakReference reference)
    {
        for (var attempt = 0; attempt < 20 && reference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            if (reference.IsAlive) await Task.Delay(25);
        }
        reference.IsAlive.ShouldBeFalse();
    }

    /// <summary>
    /// 服务配置失败模块。
    /// </summary>
    public sealed class FailingConfigurationModule : BingModule
    {
        /// <inheritdoc />
        public override void ConfigureServices(IServiceCollection services)
            => throw new InvalidOperationException("candidate configuration failed");
    }
}
