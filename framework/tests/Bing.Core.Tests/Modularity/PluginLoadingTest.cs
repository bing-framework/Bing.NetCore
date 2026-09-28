using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Tests.Modularity;

/// <summary>
/// 启动期插件加载和应用程序集扫描测试。
/// </summary>
[Collection("Bing启动期插件")]
public sealed class PluginLoadingTest
{
    /// <summary>
    /// 顶部模块完整名称。
    /// </summary>
    private const string TopModuleName = "Bing.Core.PluginFixtures.FixtureTopModule";
    /// <summary>
    /// 叶子模块完整名称。
    /// </summary>
    private const string LeafModuleName = "Bing.Core.PluginFixtures.FixtureLeafModule";
    /// <summary>
    /// 插件服务类型完整名称。
    /// </summary>
    private const string ServiceTypeName = "Bing.Core.PluginFixtures.IPluginFixtureService";

    /// <summary>
    /// 验证插件入口程序集、模块图和自动服务扫描均已接入应用注册。
    /// </summary>
    [Fact]
    public void AddBingApplication_ShouldLoadPluginAndScanSelectedPluginAssembly()
    {
        using var workspace = new PluginTestWorkspace();
        var pluginDirectory = workspace.CreatePlugin(
            "Contoso.Reporting", "1.2.0", new[] { TopModuleName });
        var services = new ServiceCollection();
        services.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddDirectory(pluginDirectory));

        using var provider = services.BuildBingServiceProvider();
        var pluginContainer = provider.GetRequiredService<IBingPluginContainer>();
        var plugin = pluginContainer.Plugins.Single();
        PluginTestWorkspace.ResetFixtureState(plugin.EntryAssembly);
        plugin.Id.ShouldBe("Contoso.Reporting");
        plugin.Version.ShouldBe(new Version(1, 2, 0));
        plugin.ExecutionOrder.ShouldBe(0);
        plugin.DirectoryPath.ShouldBe(Path.GetFullPath(pluginDirectory));
        plugin.EntryAssemblyPath.ShouldBe(Path.GetFullPath(Path.Combine(pluginDirectory, "Bing.Core.PluginFixtures.dll")));
        plugin.StartupModules.Select(type => type.FullName).ShouldBe(new[] { TopModuleName });

        var modules = provider.GetRequiredService<IBingModuleContainer>().Modules;
        modules.Select(module => module.ModuleType.FullName).ShouldContain(TopModuleName);
        modules.Select(module => module.ModuleType.FullName).ShouldContain(LeafModuleName);

        provider.GetService(plugin.EntryAssembly.GetType(ServiceTypeName, true)).ShouldNotBeNull();
    }

    /// <summary>
    /// 验证插件启动模块参与统一模块生命周期初始化。
    /// </summary>
    [Fact]
    public void UseBing_ShouldInitializePluginModules()
    {
        using var workspace = new PluginTestWorkspace();
        var pluginDirectory = workspace.CreatePlugin(
            "Contoso.Lifecycle", "1.0.0", new[] { TopModuleName });
        var services = new ServiceCollection();
        services.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddDirectory(pluginDirectory));

        using var provider = services.BuildBingServiceProvider();
        var plugin = provider.GetRequiredService<IBingPluginContainer>().Plugins.Single();
        PluginTestWorkspace.ResetFixtureState(plugin.EntryAssembly);
        provider.UseBing();

        var stateType = plugin.EntryAssembly.GetType("Bing.Core.PluginFixtures.PluginFixtureState", true);
        ((int)stateType.GetProperty("TopModuleUseModuleCount")!.GetValue(null)!).ShouldBeGreaterThan(0);
    }
}
