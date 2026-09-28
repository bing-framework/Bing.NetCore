using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Tests.Modularity;

/// <summary>
/// 启动期插件来源和依赖顺序测试。
/// </summary>
[Collection("Bing启动期插件")]
public sealed class PluginDependencyGraphTest
{
    /// <summary>
    /// 顶部模块完整名称。
    /// </summary>
    private const string TopModuleName = "Bing.Core.PluginFixtures.FixtureTopModule";
    /// <summary>
    /// 左侧模块完整名称。
    /// </summary>
    private const string LeftModuleName = "Bing.Core.PluginFixtures.FixtureLeftModule";
    /// <summary>
    /// 右侧模块完整名称。
    /// </summary>
    private const string RightModuleName = "Bing.Core.PluginFixtures.FixtureRightModule";
    /// <summary>
    /// 叶子模块完整名称。
    /// </summary>
    private const string LeafModuleName = "Bing.Core.PluginFixtures.FixtureLeafModule";
    /// <summary>
    /// 循环依赖模块完整名称。
    /// </summary>
    private const string CycleModuleName = "Bing.Core.PluginFixtures.FixtureCycleAModule";

    /// <summary>
    /// 验证 AddFolder 发现的插件按插件依赖拓扑顺序排列。
    /// </summary>
    [Fact]
    public void AddFolder_ShouldOrderPluginsByDependencyBeforeOrdinalName()
    {
        using var workspace = new PluginTestWorkspace();
        var parent = Path.Combine(workspace.RootPath, "modules");
        Directory.CreateDirectory(parent);
        workspace.CreatePlugin("Contoso.Foundation", "1.0.0", new[] { TopModuleName }, directoryName: Path.Combine("modules", "foundation"));
        workspace.CreatePlugin("Contoso.Reporting", "1.0.0", new[] { TopModuleName },
            new[] { ("Contoso.Foundation", "1.0.0") }, Path.Combine("modules", "reporting"));
        var services = new ServiceCollection();
        services.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddFolder(parent));

        using var provider = services.BuildBingServiceProvider();
        var plugins = provider.GetRequiredService<IBingPluginContainer>().Plugins;
        plugins.Select(plugin => plugin.Id).ShouldBe(new[] { "Contoso.Foundation", "Contoso.Reporting" });
        plugins.Select(plugin => plugin.ExecutionOrder).ShouldBe(new[] { 0, 1 });
        plugins[1].Dependencies.Single().Id.ShouldBe("Contoso.Foundation");
        plugins[0].EntryAssembly.ShouldBeSameAs(plugins[1].EntryAssembly);

        var moduleNames = provider.GetRequiredService<IBingModuleContainer>().Modules
            .Select(module => module.ModuleType.FullName).ToArray();
        moduleNames.Count(name => name == TopModuleName).ShouldBe(1);
        moduleNames.Count(name => name == LeftModuleName).ShouldBe(1);
        moduleNames.Count(name => name == RightModuleName).ShouldBe(1);
        moduleNames.Count(name => name == LeafModuleName).ShouldBe(1);
        Array.IndexOf(moduleNames, LeafModuleName).ShouldBeLessThan(Array.IndexOf(moduleNames, LeftModuleName));
        Array.IndexOf(moduleNames, LeafModuleName).ShouldBeLessThan(Array.IndexOf(moduleNames, RightModuleName));
        Array.IndexOf(moduleNames, LeftModuleName).ShouldBeLessThan(Array.IndexOf(moduleNames, TopModuleName));
        Array.IndexOf(moduleNames, RightModuleName).ShouldBeLessThan(Array.IndexOf(moduleNames, TopModuleName));
    }

    /// <summary>
    /// 验证插件依赖缺失时错误包含依赖关系。
    /// </summary>
    [Fact]
    public void AddBingApplication_MissingPluginDependency_ShouldFailWithDependencyPath()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Reporting", "1.0.0", new[] { TopModuleName },
            new[] { ("Contoso.Foundation", "1.0.0") });

        var exception = Should.Throw<InvalidOperationException>(() => RegisterDirectory(directory));

        exception.Message.ShouldContain("Contoso.Reporting -> Contoso.Foundation");
        exception.Data["Bing.PluginId"].ShouldBe("Contoso.Reporting");
        exception.Data["Bing.PluginPhase"].ShouldBe("ValidateDependencies");
        ((IReadOnlyList<string>)exception.Data["Bing.PluginDependencyPath"])
            .ShouldBe(new[] { "Contoso.Reporting", "Contoso.Foundation" });
    }

    /// <summary>
    /// 验证嵌套缺失依赖错误包含完整插件链。
    /// </summary>
    [Fact]
    public void AddBingApplication_NestedMissingPluginDependency_ShouldFailWithCompletePath()
    {
        using var workspace = new PluginTestWorkspace();
        workspace.CreatePlugin("Contoso.Foundation", "1.0.0", new[] { TopModuleName },
            new[] { ("Contoso.Missing", "1.0.0") });
        workspace.CreatePlugin("Contoso.Reporting", "1.0.0", new[] { TopModuleName },
            new[] { ("Contoso.Foundation", "1.0.0") });

        var exception = Should.Throw<InvalidOperationException>(() => RegisterFolder(workspace.RootPath));

        exception.Message.ShouldContain("Contoso.Reporting -> Contoso.Foundation -> Contoso.Missing");
    }

    /// <summary>
    /// 验证重复插件 ID 会使当前服务集合进入不可重试状态。
    /// </summary>
    [Fact]
    public void AddBingApplication_DuplicatePluginId_ShouldRejectRetry()
    {
        using var workspace = new PluginTestWorkspace();
        var parent = Path.Combine(workspace.RootPath, "duplicates");
        Directory.CreateDirectory(parent);
        workspace.CreatePlugin("Contoso.Duplicate", "1.0.0", new[] { TopModuleName }, directoryName: Path.Combine("duplicates", "first"));
        workspace.CreatePlugin("contoso.duplicate", "1.0.0", new[] { TopModuleName }, directoryName: Path.Combine("duplicates", "second"));
        var services = new ServiceCollection();

        var first = Should.Throw<InvalidOperationException>(() =>
            services.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddFolder(parent)));
        var retry = Should.Throw<InvalidOperationException>(() =>
            services.AddBingApplication<PluginHostModule>());

        first.Message.ShouldContain("插件 ID 重复");
        first.Data["Bing.PluginId"].ShouldBe("Contoso.Duplicate");
        first.Data["Bing.PluginPhase"].ShouldBe("ValidateManifest");
        retry.Message.ShouldContain("已经注册");
    }

    /// <summary>
    /// 验证插件依赖必须精确匹配版本。
    /// </summary>
    [Fact]
    public void AddBingApplication_PluginDependencyVersionMismatch_ShouldFailWithContext()
    {
        using var workspace = new PluginTestWorkspace();
        var parent = Path.Combine(workspace.RootPath, "versions");
        Directory.CreateDirectory(parent);
        workspace.CreatePlugin("Contoso.Foundation", "1.0.0", new[] { TopModuleName }, directoryName: Path.Combine("versions", "foundation"));
        workspace.CreatePlugin("Contoso.Reporting", "1.0.0", new[] { TopModuleName },
            new[] { ("Contoso.Foundation", "2.0.0") }, Path.Combine("versions", "reporting"));

        var exception = Should.Throw<InvalidOperationException>(() => RegisterFolder(parent));

        exception.Message.ShouldContain("插件依赖版本不匹配");
        exception.Message.ShouldContain("Contoso.Reporting");
        exception.Data["Bing.PluginId"].ShouldBe("Contoso.Reporting");
        exception.Data["Bing.PluginPhase"].ShouldBe("ValidateDependencies");
    }

    /// <summary>
    /// 验证精确版本和区间的包含边界。
    /// </summary>
    [Theory]
    [InlineData("1.2.0", "[1.2.0,2.0.0)", true)]
    [InlineData("2.0.0", "[1.2.0,2.0.0)", false)]
    [InlineData("1.2.0", "(1.2.0,2.0.0]", false)]
    [InlineData("1.2.0", "[1.2.0]", true)]
    [InlineData("1.2.0", "(,1.2.0]", true)]
    [InlineData("1.2.0", "[1.2.0,)", true)]
    [InlineData("1.2.0", "1.2.0", true)]
    [InlineData("1.2.1", "1.2.0", false)]
    public void AddBingApplication_PluginDependencyRange_ShouldRespectBounds(
        string installedVersion, string requirement, bool accepted)
    {
        using var workspace = new PluginTestWorkspace();
        workspace.CreatePlugin("Contoso.Foundation", installedVersion, new[] { TopModuleName });
        workspace.CreatePlugin("Contoso.Reporting", "1.0.0", new[] { TopModuleName },
            new[] { ("Contoso.Foundation", requirement) });
        var services = new ServiceCollection();

        if (!accepted)
        {
            var error = Should.Throw<InvalidOperationException>(() =>
                services.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddFolder(workspace.RootPath)));
            error.Message.ShouldContain("插件依赖版本不匹配");
            error.Data["Bing.PluginDependencyPath"].ShouldBeOfType<string[]>()
                .ShouldBe(new[] { "Contoso.Reporting", "Contoso.Foundation" });
            return;
        }

        services.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddFolder(workspace.RootPath));
        using var provider = services.BuildBingServiceProvider();
        var dependency = provider.GetRequiredService<IBingPluginContainer>().Plugins.Single(plugin =>
            plugin.Id == "Contoso.Reporting").Dependencies.Single();
        dependency.VersionRequirement.ShouldBe(requirement);
        dependency.Version.ShouldBe(requirement.Contains(',') ? null : new Version(1, 2, 0));
    }

    /// <summary>
    /// 验证预发布版本仅满足明确包含预发布边界的约束。
    /// </summary>
    [Theory]
    [InlineData("[1.2.0,2.0.0)", false)]
    [InlineData("[1.2.0-beta.1,2.0.0)", true)]
    [InlineData("1.2.0-beta.2", true)]
    public void AddBingApplication_PrereleaseDependency_ShouldRequireExplicitOptIn(string requirement, bool accepted)
    {
        using var workspace = new PluginTestWorkspace();
        workspace.CreatePlugin("Contoso.Foundation", "1.2.0-beta.2", new[] { TopModuleName });
        workspace.CreatePlugin("Contoso.Reporting", "1.0.0", new[] { TopModuleName },
            new[] { ("Contoso.Foundation", requirement) });
        var services = new ServiceCollection();
        if (!accepted)
        {
            Should.Throw<InvalidOperationException>(() =>
                services.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddFolder(workspace.RootPath)))
                .Message.ShouldContain("插件依赖版本不匹配");
            return;
        }

        services.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddFolder(workspace.RootPath));
        using var provider = services.BuildBingServiceProvider();
        var foundation = provider.GetRequiredService<IBingPluginContainer>().Plugins.Single(plugin =>
            plugin.Id == "Contoso.Foundation");
        foundation.Version.ShouldBe(new Version(1, 2, 0));
        foundation.VersionText.ShouldBe("1.2.0-beta.2");
    }

    /// <summary>
    /// 验证插件目录中的同名不同版本程序集在加载前失败。
    /// </summary>
    [Fact]
    public void AddBingApplication_AssemblyIdentityConflict_ShouldFailBeforeModuleLoading()
    {
        using var workspace = new PluginTestWorkspace();
        var parent = Path.Combine(workspace.RootPath, "assembly-conflict");
        Directory.CreateDirectory(parent);
        workspace.CreatePlugin("Contoso.V1", "1.0.0", new[] { TopModuleName }, directoryName: Path.Combine("assembly-conflict", "v1"));
        workspace.CreatePluginFromAssembly("Contoso.V2", "2.0.0",
            new[] { "Bing.Core.PluginFixtures.V2.FixtureV2Module" }, workspace.FixtureV2AssemblyPath,
            directoryName: Path.Combine("assembly-conflict", "v2"));

        var exception = Should.Throw<InvalidOperationException>(() => RegisterFolder(parent));

        exception.Message.ShouldContain("插件程序集同名但版本不同");
        exception.Message.ShouldContain("Bing.Core.PluginFixtures");
        exception.Data["Bing.PluginPhase"].ShouldBe("ScanAssemblies");
    }

    /// <summary>
    /// 验证插件依赖环会在入口模块构造前失败。
    /// </summary>
    [Fact]
    public void AddBingApplication_PluginDependencyCycle_ShouldFailWithCyclePath()
    {
        using var workspace = new PluginTestWorkspace();
        workspace.CreatePlugin("Contoso.A", "1.0.0", new[] { TopModuleName },
            new[] { ("Contoso.B", "1.0.0") });
        var directory = workspace.CreatePlugin("Contoso.B", "1.0.0", new[] { TopModuleName },
            new[] { ("Contoso.A", "1.0.0") });

        var exception = Should.Throw<InvalidOperationException>(() => RegisterFolder(workspace.RootPath));

        exception.Message.ShouldContain("插件依赖存在环");
        exception.Message.ShouldContain("Contoso.A");
        exception.Message.ShouldContain("Contoso.B");
    }

    /// <summary>
    /// 验证进入嵌套依赖环前的插件链不会从错误中丢失。
    /// </summary>
    [Fact]
    public void AddBingApplication_NestedPluginDependencyCycle_ShouldIncludePrefix()
    {
        using var workspace = new PluginTestWorkspace();
        workspace.CreatePlugin("Contoso.A", "1.0.0", new[] { TopModuleName },
            new[] { ("Contoso.B", "1.0.0") });
        workspace.CreatePlugin("Contoso.B", "1.0.0", new[] { TopModuleName },
            new[] { ("Contoso.C", "1.0.0") });
        workspace.CreatePlugin("Contoso.C", "1.0.0", new[] { TopModuleName },
            new[] { ("Contoso.B", "1.0.0") });

        var exception = Should.Throw<InvalidOperationException>(() => RegisterFolder(workspace.RootPath));

        exception.Message.ShouldContain("Contoso.A -> Contoso.B -> Contoso.C -> Contoso.B");
    }

    /// <summary>
    /// 验证插件入口模块自身的依赖环在模块构造前失败。
    /// </summary>
    [Fact]
    public void AddBingApplication_PluginModuleDependencyCycle_ShouldFailBeforeConstruction()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.CyclicModule", "1.0.0", new[] { CycleModuleName });

        var exception = Should.Throw<InvalidOperationException>(() => RegisterDirectory(directory));

        exception.Message.ShouldContain("模块依赖存在环");
        exception.Message.ShouldContain("FixtureCycleAModule");
        exception.Message.ShouldContain("FixtureCycleBModule");
    }

    /// <summary>
    /// 验证两个服务集合的插件描述符相互隔离。
    /// </summary>
    [Fact]
    public void AddBingApplication_TwoServiceCollections_ShouldKeepPluginContainersIsolated()
    {
        using var workspace = new PluginTestWorkspace();
        var firstDirectory = workspace.CreatePlugin("Contoso.First", "1.0.0", new[] { TopModuleName }, directoryName: "first");
        var secondDirectory = workspace.CreatePlugin("Contoso.Second", "1.0.0", new[] { TopModuleName }, directoryName: "second");
        var firstServices = new ServiceCollection();
        var secondServices = new ServiceCollection();
        firstServices.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddDirectory(firstDirectory));
        secondServices.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddDirectory(secondDirectory));

        using var firstProvider = firstServices.BuildBingServiceProvider();
        using var secondProvider = secondServices.BuildBingServiceProvider();
        var first = firstProvider.GetRequiredService<IBingPluginContainer>().Plugins;
        var second = secondProvider.GetRequiredService<IBingPluginContainer>().Plugins;

        first.Single().Id.ShouldBe("Contoso.First");
        second.Single().Id.ShouldBe("Contoso.Second");
        ReferenceEquals(first, second).ShouldBeFalse();
    }

    /// <summary>
    /// 验证两个服务集合的插件扫描、配置和生命周期对象相互隔离。
    /// </summary>
    [Fact]
    public void AddBingApplication_TwoServiceCollections_ShouldIsolatePluginServicesAndConfiguration()
    {
        using var workspace = new PluginTestWorkspace();
        var firstDirectory = workspace.CreatePlugin("Contoso.ConfigFirst", "1.0.0",
            new[] { "Bing.Core.PluginFixtures.FixtureConfigurationModule" }, directoryName: "config-first");
        var secondDirectory = workspace.CreatePlugin("Contoso.ConfigSecond", "1.0.0",
            new[] { "Bing.Core.PluginFixtures.FixtureConfigurationModule" }, directoryName: "config-second");
        var fixtureAssembly = workspace.LoadFixtureAssembly();
        var markerType = fixtureAssembly.GetType("Bing.Core.PluginFixtures.PluginConfigurationMarker", true);
        var observedType = fixtureAssembly.GetType("Bing.Core.PluginFixtures.IPluginObservedConfiguration", true);
        var serviceType = fixtureAssembly.GetType("Bing.Core.PluginFixtures.IPluginFixtureService", true);
        var firstServices = new ServiceCollection();
        var secondServices = new ServiceCollection();
        firstServices.AddSingleton(markerType, Activator.CreateInstance(markerType, "first"));
        secondServices.AddSingleton(markerType, Activator.CreateInstance(markerType, "second"));
        firstServices.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddDirectory(firstDirectory));
        secondServices.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddDirectory(secondDirectory));

        using var firstProvider = firstServices.BuildBingServiceProvider();
        using var secondProvider = secondServices.BuildBingServiceProvider();
        firstProvider.UseBing();
        secondProvider.UseBing();

        var firstConfiguration = firstProvider.GetRequiredService(observedType);
        var secondConfiguration = secondProvider.GetRequiredService(observedType);
        observedType.GetProperty("Value")!.GetValue(firstConfiguration).ShouldBe("first");
        observedType.GetProperty("Value")!.GetValue(secondConfiguration).ShouldBe("second");
        ReferenceEquals(firstConfiguration, secondConfiguration).ShouldBeFalse();
        ReferenceEquals(firstProvider.GetRequiredService(serviceType), secondProvider.GetRequiredService(serviceType))
            .ShouldBeFalse();
    }

    /// <summary>
    /// 验证自定义来源按来源实例读取，并合并到同一个插件容器。
    /// </summary>
    [Fact]
    public void AddBingApplication_CustomSources_ShouldReadEachSourceOnce()
    {
        using var workspace = new PluginTestWorkspace();
        var firstDirectory = workspace.CreatePlugin("Contoso.FirstSource", "1.0.0", new[] { TopModuleName }, directoryName: "source-first");
        var secondDirectory = workspace.CreatePlugin("Contoso.SecondSource", "1.0.0", new[] { TopModuleName }, directoryName: "source-second");
        var firstSource = new FixedPluginSource(firstDirectory);
        var secondSource = new FixedPluginSource(secondDirectory);
        var services = new ServiceCollection();
        services.AddBingApplication<PluginHostModule>(options =>
        {
            options.PluginSources.Add(firstSource);
            options.PluginSources.Add(secondSource);
        });

        using var provider = services.BuildBingServiceProvider();
        provider.GetRequiredService<IBingPluginContainer>().Plugins.Select(plugin => plugin.Id)
            .ShouldBe(new[] { "Contoso.FirstSource", "Contoso.SecondSource" });
        firstSource.CallCount.ShouldBe(1);
        secondSource.CallCount.ShouldBe(1);
    }

    /// <summary>
    /// 验证没有配置插件来源时容器返回空插件目录。
    /// </summary>
    [Fact]
    public void AddBingApplication_WithoutPlugins_ShouldExposeEmptyPluginContainer()
    {
        var services = new ServiceCollection();
        services.AddBingApplication<PluginHostModule>();

        using var provider = services.BuildBingServiceProvider();

        provider.GetRequiredService<IBingPluginContainer>().Plugins.ShouldBeEmpty();
    }

    /// <summary>
    /// 验证插件启动模块与排除列表冲突时返回完整模块链。
    /// </summary>
    [Fact]
    public void AddBingApplication_ExcludedPluginModule_ShouldFailWithConflictPath()
    {
        using var workspace = new PluginTestWorkspace();
        var pluginDirectory = workspace.CreatePlugin("Contoso.Excluded", "1.0.0", new[] { TopModuleName });
        var fixtureAssembly = workspace.LoadFixtureAssembly();
        var excludedModule = fixtureAssembly.GetType(TopModuleName, true);
        var services = new ServiceCollection();

        var exception = Should.Throw<InvalidOperationException>(() => services.AddBingApplication<PluginHostModule>(options =>
        {
            options.PluginSources.AddDirectory(pluginDirectory);
            options.ExcludedModules.Add(excludedModule);
        }));

        exception.Message.ShouldContain("模块依赖链包含被排除的模块");
        exception.Message.ShouldContain(TopModuleName);
    }

    /// <summary>
    /// 注册一个插件来源并返回服务集合。
    /// </summary>
    /// <param name="directory">插件父目录。</param>
    private static void RegisterDirectory(string directory)
    {
        var services = new ServiceCollection();
        services.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddDirectory(directory));
    }

    /// <summary>
    /// 注册一个插件父目录并返回服务集合。
    /// </summary>
    /// <param name="directory">插件父目录。</param>
    private static void RegisterFolder(string directory)
    {
        var services = new ServiceCollection();
        services.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddFolder(directory));
    }
}
