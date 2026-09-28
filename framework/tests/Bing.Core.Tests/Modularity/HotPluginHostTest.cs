using System.Reflection;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Tests.Modularity;

/// <summary>
/// 可回收插件运行时的切换与资源测试。
/// </summary>
[Collection("Bing启动期插件")]
public sealed class HotPluginHostTest
{
    /// <summary>
    /// 顶部模块。
    /// </summary>
    private const string TopModule = "Bing.Core.PluginFixtures.FixtureTopModule";
    /// <summary>
    /// 第二版本模块。
    /// </summary>
    private const string V2Module = "Bing.Core.PluginFixtures.V2.FixtureV2Module";
    /// <summary>
    /// 生命周期模块。
    /// </summary>
    private const string LifecycleModule = "Bing.Core.PluginFixtures.FixtureHotLifecycleModule";
    /// <summary>
    /// 初始化前置阶段失败模块。
    /// </summary>
    private const string PreFailureModule = "Bing.Core.PluginFixtures.FixtureHotPreFailureModule";
    /// <summary>
    /// 初始化后置阶段失败模块。
    /// </summary>
    private const string PostFailureModule = "Bing.Core.PluginFixtures.FixtureHotPostFailureModule";
    /// <summary>
    /// 服务配置前置阶段失败模块。
    /// </summary>
    private const string PreConfigurationFailureModule = "Bing.Core.PluginFixtures.FixtureHotPreConfigurationFailureModule";
    /// <summary>
    /// 服务配置主阶段失败模块。
    /// </summary>
    private const string ConfigurationFailureModule = "Bing.Core.PluginFixtures.FixtureHotConfigurationFailureModule";
    /// <summary>
    /// 服务配置后置阶段失败模块。
    /// </summary>
    private const string PostConfigurationFailureModule = "Bing.Core.PluginFixtures.FixtureHotPostConfigurationFailureModule";
    /// <summary>
    /// 服务配置前置阶段取消模块。
    /// </summary>
    private const string PreConfigurationCancellationModule = "Bing.Core.PluginFixtures.FixtureHotPreConfigurationCancellationModule";
    /// <summary>
    /// 服务配置主阶段取消模块。
    /// </summary>
    private const string ConfigurationCancellationModule = "Bing.Core.PluginFixtures.FixtureHotConfigurationCancellationModule";
    /// <summary>
    /// 服务配置后置阶段取消模块。
    /// </summary>
    private const string PostConfigurationCancellationModule = "Bing.Core.PluginFixtures.FixtureHotPostConfigurationCancellationModule";
    /// <summary>
    /// 等待后置初始化的模块。
    /// </summary>
    private const string PendingPostModule = "Bing.Core.PluginFixtures.FixtureHotPendingPostModule";
    /// <summary>
    /// 附加程序集模块。
    /// </summary>
    private const string AdditionalAssemblyModule = "Bing.Core.PluginFixtures.FixtureAdditionalAssemblyModule";
    /// <summary>
    /// 第二版本附加程序集模块。
    /// </summary>
    private const string V2AdditionalAssemblyModule = "Bing.Core.PluginFixtures.V2.FixtureV2AdditionalAssemblyModule";

    /// <summary>
    /// 验证插件附加程序集进入本代服务扫描并在切换后回收。
    /// </summary>
    [Fact]
    public async Task ReloadAsync_AdditionalAssembly_ShouldRegisterServiceAndUnloadWithGeneration()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Live", "1.0.0", new[] { AdditionalAssemblyModule });
        File.Copy(workspace.FixtureScanAssemblyPath,
            Path.Combine(directory, "Bing.Core.PluginScanFixtures.dll"));
        File.Copy(workspace.FixtureScanDependencyPath,
            Path.Combine(directory, "Bing.Core.PluginScanDependency.dll"));
        var next = workspace.CreatePlugin("Contoso.Next", "1.0.0", new[] { TopModule });
        await using var host = new BingHotPluginHost<PluginHostModule>(
            options => options.PluginSources.AddDirectory(directory));

        await host.ReloadAsync();
        var context = await CaptureContextAsync(host);
        var serviceLoadedInGeneration = await host.RunAsync((provider, _) =>
        {
            var module = provider.GetRequiredService<IBingModuleContainer>().Modules.Single(descriptor =>
                descriptor.ModuleType.FullName == AdditionalAssemblyModule);
            var assembly = module.Assemblies.Single(candidate =>
                candidate.GetName().Name == "Bing.Core.PluginScanFixtures");
            var contract = assembly.GetType("Bing.Core.PluginScanFixtures.IPluginScanService", true);
            return Task.FromResult(provider.GetService(contract) != null &&
                AssemblyLoadContext.GetLoadContext(assembly) == AssemblyLoadContext.GetLoadContext(module.Assembly));
        });
        serviceLoadedInGeneration.ShouldBeTrue();

        directory = next;
        await host.ReloadAsync();
        await AssertCollectedAsync(context);
    }

    /// <summary>
    /// 验证附加程序集缺失时保留当前插件代。
    /// </summary>
    [Fact]
    public async Task ReloadAsync_MissingAdditionalAssembly_ShouldKeepCurrentGeneration()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Live", "1.0.0", new[] { TopModule },
            directoryName: "valid");
        var candidate = workspace.CreatePlugin("Contoso.Candidate", "1.0.0",
            new[] { AdditionalAssemblyModule }, directoryName: "candidate");
        await using var host = new BingHotPluginHost<PluginHostModule>(
            options => options.PluginSources.AddDirectory(directory));
        await host.ReloadAsync();
        directory = candidate;

        WeakReference candidateContext = null;
        AssemblyLoadEventHandler observe = (_, args) =>
        {
            if (args.LoadedAssembly.GetName().Name != "Bing.Core.PluginFixtures") return;
            var context = AssemblyLoadContext.GetLoadContext(args.LoadedAssembly);
            if (context?.IsCollectible == true) candidateContext = new WeakReference(context);
        };
        AppDomain.CurrentDomain.AssemblyLoad += observe;
        try { await AssertMissingAdditionalAssemblyFailureAsync(host); }
        finally { AppDomain.CurrentDomain.AssemblyLoad -= observe; }

        candidateContext.ShouldNotBeNull();
        await AssertCollectedAsync(candidateContext);
        host.Plugins.Single().Id.ShouldBe("Contoso.Live");
        var current = await host.RunAsync((provider, _) => Task.FromResult(
            provider.GetRequiredService<IBingPluginContainer>().Plugins.Single().Id));
        current.ShouldBe("Contoso.Live");
    }

    /// <summary>
    /// 在独立栈帧验证附加程序集缺失的失败信息。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task AssertMissingAdditionalAssemblyFailureAsync(BingHotPluginHost<PluginHostModule> host)
    {
        var error = await Should.ThrowAsync<FileNotFoundException>(() => host.ReloadAsync());
        error.Data["Bing.PluginId"].ShouldBe("Contoso.Candidate");
        error.Data["Bing.PluginPhase"].ShouldBe("PrepareModules");
    }

    /// <summary>
    /// 验证附加程序集两个版本在不同插件代中保持身份及服务隔离。
    /// </summary>
    [Fact]
    public async Task ReloadAsync_AdditionalAssemblyVersions_ShouldSwitchAndCollectOldContext()
    {
        using var workspace = new PluginTestWorkspace();
        var first = workspace.CreatePlugin("Contoso.Live", "1.0.0", new[] { AdditionalAssemblyModule },
            directoryName: "scan-v1");
        File.Copy(workspace.FixtureScanAssemblyPath, Path.Combine(first, "Bing.Core.PluginScanFixtures.dll"));
        File.Copy(workspace.FixtureScanDependencyPath, Path.Combine(first, "Bing.Core.PluginScanDependency.dll"));
        var second = workspace.CreatePluginFromAssembly("Contoso.Live", "2.0.0",
            new[] { V2AdditionalAssemblyModule }, workspace.FixtureV2AssemblyPath, directoryName: "scan-v2");
        File.Copy(workspace.FixtureScanV2AssemblyPath, Path.Combine(second, "Bing.Core.PluginScanFixtures.dll"));
        File.Copy(workspace.FixtureScanDependencyPath, Path.Combine(second, "Bing.Core.PluginScanDependency.dll"));
        var directory = first;
        await using var host = new BingHotPluginHost<PluginHostModule>(
            options => options.PluginSources.AddDirectory(directory));

        await host.ReloadAsync();
        var oldContext = await CaptureContextAsync(host);
        var firstVersion = await ReadAdditionalServiceVersionAsync(host, AdditionalAssemblyModule);
        directory = second;
        await host.ReloadAsync();
        var secondVersion = await ReadAdditionalServiceVersionAsync(host, V2AdditionalAssemblyModule);

        firstVersion.ShouldBe("v1:7.0.0.0");
        secondVersion.ShouldBe("v2:2.0.0.0");
        await AssertCollectedAsync(oldContext);
    }

    /// <summary>
    /// 验证附加类型依赖缺失时，错误可定位且失败候选被回收。
    /// </summary>
    [Fact]
    public async Task ReloadAsync_AdditionalTypeDependencyMissing_ShouldDiagnoseAndCollectCandidate()
    {
        using var workspace = new PluginTestWorkspace();
        var first = workspace.CreatePlugin("Contoso.Live", "1.0.0", new[] { AdditionalAssemblyModule },
            directoryName: "valid-scan");
        File.Copy(workspace.FixtureScanAssemblyPath, Path.Combine(first, "Bing.Core.PluginScanFixtures.dll"));
        File.Copy(workspace.FixtureScanDependencyPath, Path.Combine(first, "Bing.Core.PluginScanDependency.dll"));
        var second = workspace.CreatePluginFromAssembly("Contoso.Live", "2.0.0",
            new[] { V2AdditionalAssemblyModule }, workspace.FixtureV2AssemblyPath,
            directoryName: "missing-type-dependency");
        File.Copy(workspace.FixtureScanV2AssemblyPath, Path.Combine(second, "Bing.Core.PluginScanFixtures.dll"));
        var directory = first;
        await using var host = new BingHotPluginHost<PluginHostModule>(
            options => options.PluginSources.AddDirectory(directory));
        await host.ReloadAsync();
        WeakReference candidateContext = null;
        AssemblyLoadEventHandler observe = (_, args) =>
        {
            if (args.LoadedAssembly.GetName().Name != "Bing.Core.PluginFixtures") return;
            var context = AssemblyLoadContext.GetLoadContext(args.LoadedAssembly);
            if (context?.IsCollectible == true) candidateContext = new WeakReference(context);
        };
        AppDomain.CurrentDomain.AssemblyLoad += observe;
        try
        {
            directory = second;
            await AssertAdditionalTypeLoadFailureAsync(host, V2AdditionalAssemblyModule);
        }
        finally { AppDomain.CurrentDomain.AssemblyLoad -= observe; }

        candidateContext.ShouldNotBeNull();
        host.Plugins.Single().VersionText.ShouldBe("1.0.0");
        (await ReadAdditionalServiceVersionAsync(host, AdditionalAssemblyModule)).ShouldBe("v1:7.0.0.0");
        await AssertCollectedAsync(candidateContext);
    }

    /// <summary>
    /// 在独立栈帧内验证附加程序集扫描失败的诊断。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task AssertAdditionalTypeLoadFailureAsync(BingHotPluginHost<PluginHostModule> host,
        string moduleType)
    {
        var error = await Should.ThrowAsync<InvalidOperationException>(() => host.ReloadAsync());
        error.Data["Bing.ScanPhase"].ShouldBe("DiscoverTypes");
        ((string)error.Data["Bing.ScanAssembly"]).ShouldContain("Bing.Core.PluginScanFixtures");
        ((string[])error.Data["Bing.ScanOwners"]).ShouldContain(moduleType);
        ((string[])error.Data["Bing.ScanLoaderErrors"]).ShouldNotBeEmpty();
        error.Data["Bing.PluginId"].ShouldBe("Contoso.Live");
        error.Data["Bing.PluginPhase"].ShouldBe("PrepareModules");
        ((string)error.Data["Bing.PluginManifest"]).ShouldContain("bing-plugin.json");
        error.InnerException.ShouldBeOfType<ReflectionTypeLoadException>();
    }

    /// <summary>
    /// 读取当前代附加服务的版本和程序集版本，不保留插件对象。
    /// </summary>
    /// <param name="host">当前插件宿主。</param>
    /// <param name="moduleType">目标模块完整类型名。</param>
    /// <returns>服务版本与程序集版本组成的文本。</returns>
    private static Task<string> ReadAdditionalServiceVersionAsync(BingHotPluginHost<PluginHostModule> host,
        string moduleType) => host.RunAsync((provider, _) =>
    {
        var module = provider.GetRequiredService<IBingModuleContainer>().Modules.Single(descriptor =>
            descriptor.ModuleType.FullName == moduleType);
        var assembly = module.Assemblies.Single(candidate =>
            candidate.GetName().Name == "Bing.Core.PluginScanFixtures");
        var contract = assembly.GetType("Bing.Core.PluginScanFixtures.IPluginScanService", true);
        var service = provider.GetRequiredService(contract);
        var version = (string)contract.GetProperty("Version")!.GetValue(service);
        return Task.FromResult(version + ":" + assembly.GetName().Version);
    });

    /// <summary>
    /// 验证异步释放夹具能记录每次释放调用。
    /// </summary>
    [Fact]
    public async Task ConfigurationDisposalFixture_ShouldRecordEveryInvocation()
    {
        using var workspace = new PluginTestWorkspace();
        var fixtureAssembly = workspace.LoadFixtureAssembly();
        var moduleType = fixtureAssembly.GetType(PreConfigurationFailureModule, throwOnError: true);
        var module = (BingModule)Activator.CreateInstance(moduleType);
        var disposalCount = 0;
        Action<int> recordDispose = _ => Interlocked.Increment(ref disposalCount);
        var services = new ServiceCollection();
        services.AddSingleton(recordDispose);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            module.PreConfigureServicesAsync(services, CancellationToken.None));
        await ((IAsyncDisposable)module).DisposeAsync();
        await ((IAsyncDisposable)module).DisposeAsync();

        disposalCount.ShouldBe(2);
    }

    /// <summary>
    /// 验证同名不同版本程序集可以切换，旧上下文在关闭后可回收。
    /// </summary>
    [Fact]
    public async Task ReloadAsync_ShouldReplaceAssemblyVersionAndCollectOldContext()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Live", "1.0.0", new[] { TopModule }, directoryName: "v1");
        var second = workspace.CreatePluginFromAssembly("Contoso.Live", "2.0.0", new[] { V2Module },
            workspace.FixtureV2AssemblyPath, directoryName: "v2");
        await using var host = new BingHotPluginHost<PluginHostModule>(options => options.PluginSources.AddDirectory(directory));

        await host.ReloadAsync();
        var oldContext = await CaptureContextAsync(host);
        var serviceContextIsCollectible = await host.RunAsync((provider, _) =>
        {
            var assembly = provider.GetRequiredService<IBingPluginContainer>().Plugins.Single().EntryAssembly;
            var serviceType = assembly.GetType("Bing.Core.PluginFixtures.IPluginFixtureService", true);
            var service = provider.GetRequiredService(serviceType);
            return Task.FromResult(AssemblyLoadContext.GetLoadContext(service.GetType().Assembly).IsCollectible);
        });
        serviceContextIsCollectible.ShouldBeTrue();
        host.Plugins.Single().Version.ShouldBe(new Version(1, 0, 0));
        host.Plugins.Single().VersionText.ShouldBe("1.0.0");
        directory = second;
        await host.ReloadAsync();

        host.Plugins.Single().Version.ShouldBe(new Version(2, 0, 0));
        host.Plugins.Single().VersionText.ShouldBe("2.0.0");
        var secondVersion = await host.RunAsync((provider, _) =>
            Task.FromResult(provider.GetRequiredService<IBingPluginContainer>().Plugins.Single()
                .EntryAssembly.GetName().Version));
        secondVersion.ShouldBe(new Version(2, 0, 0, 0));
        await AssertCollectedAsync(oldContext);
    }

    /// <summary>
    /// 验证候选加载失败不会切断旧插件代的服务。
    /// </summary>
    [Fact]
    public async Task ReloadAsync_FailedCandidate_ShouldKeepCurrentGeneration()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Live", "1.0.0", new[] { TopModule }, directoryName: "valid");
        var invalid = workspace.CreateRawPlugin("invalid", "{ invalid json }");
        await using var host = new BingHotPluginHost<PluginHostModule>(options => options.PluginSources.AddDirectory(directory));
        await host.ReloadAsync();
        directory = invalid;

        await Should.ThrowAsync<InvalidOperationException>(() => host.ReloadAsync());

        host.Plugins.Single().Version.ShouldBe(new Version(1, 0, 0));
        var loaded = await host.RunAsync((provider, _) => Task.FromResult(
            provider.GetRequiredService<IBingPluginContainer>().Plugins.Single().Id));
        loaded.ShouldBe("Contoso.Live");
    }

    /// <summary>
    /// 候选代在初始化的前置或后置阶段失败时保留旧代服务。
    /// </summary>
    [Theory]
    [InlineData(PreFailureModule, "PreInitialize", "candidate pre failure")]
    [InlineData(PostFailureModule, "PostInitialize", "candidate post failure")]
    public async Task ReloadAsync_InitializationPhaseFailure_ShouldKeepCurrentGeneration(
        string candidateModule, string phase, string message)
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Live", "1.0.0", new[] { TopModule }, directoryName: "valid");
        var invalid = workspace.CreatePlugin("Contoso.Candidate", "1.0.0", new[] { candidateModule },
            directoryName: "candidate");
        WeakReference failedContext = null;
        Action<WeakReference> captureContext = reference => failedContext = reference;
        await using var host = new BingHotPluginHost<PluginHostModule>(
            options => options.PluginSources.AddDirectory(directory),
            services => services.AddSingleton(captureContext));
        await host.ReloadAsync();
        directory = invalid;

        await AssertFailedReloadAsync(host, phase, message);

        host.Plugins.Single().Id.ShouldBe("Contoso.Live");
        var current = await host.RunAsync((provider, _) => Task.FromResult(
            provider.GetRequiredService<IBingPluginContainer>().Plugins.Single().Id));
        current.ShouldBe("Contoso.Live");
        failedContext.ShouldNotBeNull();
        await AssertCollectedAsync(failedContext);
    }

    /// <summary>
    /// 验证候选代在任一异步服务配置阶段失败时保留旧代并回收候选上下文。
    /// </summary>
    [Theory]
    [InlineData(PreConfigurationFailureModule, "PreConfigureServices", "candidate pre configure failure")]
    [InlineData(ConfigurationFailureModule, "AddServices", "candidate configure failure")]
    [InlineData(PostConfigurationFailureModule, "PostConfigureServices", "candidate post configure failure")]
    public async Task ReloadAsync_AsyncConfigurationFailureAtAnyPhase_ShouldKeepCurrentGenerationAndCollectCandidate(
        string candidateModule, string phase, string message)
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Live", "1.0.0", new[] { TopModule },
            directoryName: "valid");
        var candidate = workspace.CreatePlugin("Contoso.Candidate", "1.0.0",
            new[] { candidateModule }, directoryName: "candidate");
        WeakReference failedContext = null;
        Action<WeakReference> captureContext = reference => failedContext = reference;
        var disposalCount = 0;
        Action<int> recordDispose = _ => Interlocked.Increment(ref disposalCount);
        TaskCompletionSource<bool> disposeEntered = null;
        TaskCompletionSource<bool> disposeRelease = null;
        Func<Task> disposeGate = null;
        if (phase == "PreConfigureServices")
        {
            disposeEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            disposeRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            disposeGate = async () =>
            {
                disposeEntered.TrySetResult(true);
                await disposeRelease.Task.ConfigureAwait(false);
            };
        }
        await using var host = new BingHotPluginHost<PluginHostModule>(
            options => options.PluginSources.AddDirectory(directory),
            services =>
            {
                services.AddSingleton(captureContext);
                services.AddSingleton(recordDispose);
                if (disposeGate != null) services.AddSingleton(disposeGate);
            });

        await host.ReloadAsync();
        directory = candidate;

        if (phase == "PreConfigureServices")
            await AssertGatedFailedReloadAsync(host, disposeEntered, disposeRelease, phase, message);
        else
            await AssertFailedReloadAsync(host, phase, message);

        host.Plugins.Single().Id.ShouldBe("Contoso.Live");
        var current = await host.RunAsync((provider, _) => Task.FromResult(
            provider.GetRequiredService<IBingPluginContainer>().Plugins.Single().Id));
        current.ShouldBe("Contoso.Live");
        failedContext.ShouldNotBeNull();
        await AssertCollectedAsync(failedContext);
        disposalCount.ShouldBe(1);
        await host.DisposeAsync();
        disposalCount.ShouldBe(1);
    }

    /// <summary>
    /// 验证候选代在任一异步服务配置阶段合作式取消时保留旧代并回收候选上下文。
    /// </summary>
    [Theory]
    [InlineData(PreConfigurationCancellationModule, "PreConfigureServices")]
    [InlineData(ConfigurationCancellationModule, "AddServices")]
    [InlineData(PostConfigurationCancellationModule, "PostConfigureServices")]
    public async Task ReloadAsync_AsyncConfigurationCancellationAtAnyPhase_ShouldKeepCurrentGenerationAndCollectCandidate(
        string candidateModule, string phase)
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Live", "1.0.0", new[] { TopModule },
            directoryName: "valid");
        var candidate = workspace.CreatePlugin("Contoso.Candidate", "1.0.0",
            new[] { candidateModule }, directoryName: "candidate");
        using var cancellation = new CancellationTokenSource();
        WeakReference canceledContext = null;
        string canceledPhase = null;
        Action<WeakReference> captureContext = reference => canceledContext = reference;
        Action<string> capturePhase = value => canceledPhase = value;
        Action cancel = cancellation.Cancel;
        var disposalCount = 0;
        Action<int> recordDispose = _ => Interlocked.Increment(ref disposalCount);
        await using var host = new BingHotPluginHost<PluginHostModule>(
            options => options.PluginSources.AddDirectory(directory),
            services =>
            {
                services.AddSingleton(captureContext);
                services.AddSingleton(capturePhase);
                services.AddSingleton(cancel);
                services.AddSingleton(recordDispose);
            });

        await host.ReloadAsync();
        directory = candidate;

        await AssertCanceledReloadAsync(host, cancellation);
        canceledPhase.ShouldBe(phase);

        host.Plugins.Single().Id.ShouldBe("Contoso.Live");
        var current = await host.RunAsync((provider, _) => Task.FromResult(
            provider.GetRequiredService<IBingPluginContainer>().Plugins.Single().Id));
        current.ShouldBe("Contoso.Live");
        canceledContext.ShouldNotBeNull();
        await AssertCollectedAsync(canceledContext);
        disposalCount.ShouldBe(1);
        await host.DisposeAsync();
        disposalCount.ShouldBe(1);
    }

    /// <summary>
    /// 验证候选代后置初始化未完成时不切换当前服务入口，完成后才发布候选代。
    /// </summary>
    [Fact]
    public async Task ReloadAsync_PendingPostInitialization_ShouldKeepCurrentGenerationUntilCandidateCompletes()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Live", "1.0.0", new[] { TopModule },
            directoryName: "valid");
        var candidate = workspace.CreatePlugin("Contoso.Candidate", "1.0.0",
            new[] { PendingPostModule }, directoryName: "candidate");
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action signalEntered = () => entered.TrySetResult(true);
        await using var host = new BingHotPluginHost<PluginHostModule>(
            options => options.PluginSources.AddDirectory(directory),
            services =>
            {
                services.AddSingleton(entered);
                services.AddSingleton(release);
                services.AddSingleton(signalEntered);
            });

        await host.ReloadAsync();
        var oldContext = await CaptureContextAsync(host);
        directory = candidate;

        var reload = host.ReloadAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        host.Plugins.Single().Id.ShouldBe("Contoso.Live");
        reload.IsCompleted.ShouldBeFalse();
        var current = await host.RunAsync((provider, _) => Task.FromResult(
            provider.GetRequiredService<IBingPluginContainer>().Plugins.Single().Id));
        current.ShouldBe("Contoso.Live");

        release.TrySetResult(true);
        await reload;

        host.Plugins.Single().Id.ShouldBe("Contoso.Candidate");
        await AssertCollectedAsync(oldContext);
    }

    /// <summary>
    /// 验证热更新后的快照保留预发布版本和构建元数据。
    /// </summary>
    [Fact]
    public async Task ReloadAsync_PrereleaseVersion_ShouldPreserveFullVersionText()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Live", "1.2.0-beta.2+build.7", new[] { TopModule });
        await using var host = new BingHotPluginHost<PluginHostModule>(options =>
            options.PluginSources.AddDirectory(directory));

        await host.ReloadAsync();

        host.Plugins.Single().Version.ShouldBe(new Version(1, 2, 0));
        host.Plugins.Single().VersionText.ShouldBe("1.2.0-beta.2+build.7");
    }

    /// <summary>
    /// 验证旧代正在执行的调用结束前不会停止和卸载。
    /// </summary>
    [Fact]
    public async Task ReloadAsync_ShouldWaitForActiveCallBeforeClosingOldGeneration()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Live", "1.0.0", new[] { TopModule }, directoryName: "v1");
        var second = workspace.CreatePluginFromAssembly("Contoso.Live", "2.0.0", new[] { V2Module },
            workspace.FixtureV2AssemblyPath, directoryName: "v2");
        await using var host = new BingHotPluginHost<PluginHostModule>(options => options.PluginSources.AddDirectory(directory));
        await host.ReloadAsync();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = host.RunAsync(async (provider, _) =>
        {
            var id = provider.GetRequiredService<IBingPluginContainer>().Plugins.Single().Id;
            entered.SetResult(true);
            await release.Task;
            return id;
        });
        await entered.Task;
        directory = second;

        var reload = host.ReloadAsync();
        await WaitUntilAsync(() => host.Plugins.Single().Version == new Version(2, 0, 0));
        reload.IsCompleted.ShouldBeFalse();
        release.SetResult(true);

        (await active).ShouldBe("Contoso.Live");
        await reload;
        host.Plugins.Single().Version.ShouldBe(new Version(2, 0, 0));
    }

    /// <summary>
    /// 验证关闭宿主后当前上下文可回收，且后续调用被拒绝。
    /// </summary>
    [Fact]
    public async Task DisposeAsync_ShouldUnloadCurrentContextAndRejectCalls()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Live", "1.0.0", new[] { TopModule });
        var host = new BingHotPluginHost<PluginHostModule>(options => options.PluginSources.AddDirectory(directory));
        await host.ReloadAsync();
        var context = await CaptureContextAsync(host);

        await host.DisposeAsync();

        host.Plugins.ShouldBeEmpty();
        await Should.ThrowAsync<ObjectDisposedException>(() => host.RunAsync((_, _) => Task.FromResult(0)));
        await AssertCollectedAsync(context);
    }

    /// <summary>
    /// 验证多插件依赖顺序与旧模块关闭、释放均在切换时完成。
    /// </summary>
    [Fact]
    public async Task ReloadAsync_ShouldInitializeDependencyGraphAndCloseOldModules()
    {
        using var workspace = new PluginTestWorkspace();
        var folder = Path.Combine(workspace.RootPath, "plugins");
        Directory.CreateDirectory(folder);
        workspace.CreatePlugin("Contoso.Foundation", "1.0.0", new[] { LifecycleModule },
            directoryName: Path.Combine("plugins", "foundation"));
        workspace.CreatePlugin("Contoso.Reporting", "1.0.0", new[] { TopModule },
            new[] { ("Contoso.Foundation", "1.0.0") }, Path.Combine("plugins", "reporting"));
        var events = new List<string>();
        await using var host = new BingHotPluginHost<PluginHostModule>(
            options => options.PluginSources.AddFolder(folder),
            services => services.AddSingleton<Action<string>>(events.Add));

        await host.ReloadAsync();
        host.Plugins.Select(plugin => plugin.Id).ShouldBe(new[] { "Contoso.Foundation", "Contoso.Reporting" });
        events.ShouldBe(new[] { "initialize" });
        await host.ReloadAsync();

        events.ShouldBe(new[] { "initialize", "initialize", "shutdown", "dispose" });
        await host.DisposeAsync();
        events.ShouldBe(new[] { "initialize", "initialize", "shutdown", "dispose", "shutdown", "dispose" });
    }

    /// <summary>
    /// 验证插件服务调用期间的重新加载和关闭重入立即失败。
    /// </summary>
    [Fact]
    public async Task RunAsync_ReentrantReloadOrDispose_ShouldFailWithoutDeadlock()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Live", "1.0.0", new[] { TopModule });
        await using var host = new BingHotPluginHost<PluginHostModule>(options => options.PluginSources.AddDirectory(directory));
        await host.ReloadAsync();

        await host.RunAsync(async (_, _) =>
        {
            await Should.ThrowAsync<InvalidOperationException>(() => host.ReloadAsync());
            await Should.ThrowAsync<InvalidOperationException>(async () => await host.DisposeAsync());
            return true;
        });

        host.Plugins.Single().Id.ShouldBe("Contoso.Live");
    }

    /// <summary>
    /// 验证插件目录包含宿主契约副本时仍使用相同的模块和 DI 类型身份。
    /// </summary>
    [Fact]
    public async Task ReloadAsync_CopiedHostContracts_ShouldShareContractIdentity()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Live", "1.0.0", new[] { TopModule });
        File.Copy(typeof(BingModule).Assembly.Location, Path.Combine(directory, "Bing.Core.dll"));
        var diAssembly = typeof(IServiceCollection).Assembly;
        File.Copy(diAssembly.Location, Path.Combine(directory, Path.GetFileName(diAssembly.Location)));
        await using var host = new BingHotPluginHost<PluginHostModule>(options => options.PluginSources.AddDirectory(directory));

        await host.ReloadAsync();

        var identities = await host.RunAsync((provider, _) =>
        {
            var moduleType = provider.GetRequiredService<IBingPluginContainer>().Plugins.Single().StartupModules.Single();
            return Task.FromResult((typeof(BingModule).IsAssignableFrom(moduleType),
                moduleType.GetMethod("AddServices")!.GetParameters().Single().ParameterType == typeof(IServiceCollection)));
        });
        identities.Item1.ShouldBeTrue();
        identities.Item2.ShouldBeTrue();
    }

    /// <summary>
    /// 验证动态上下文中的第二版本不会污染旧入口的默认上下文程序集选择。
    /// </summary>
    [Fact]
    public async Task ReloadAsync_ThenStaticRegistration_ShouldKeepDefaultContextIsolated()
    {
        using var workspace = new PluginTestWorkspace();
        var dynamicDirectory = workspace.CreatePluginFromAssembly("Contoso.Live", "2.0.0", new[] { V2Module },
            workspace.FixtureV2AssemblyPath, directoryName: "dynamic");
        var staticDirectory = workspace.CreatePlugin("Contoso.Static", "1.0.0", new[] { TopModule },
            directoryName: "static");
        await using var host = new BingHotPluginHost<PluginHostModule>(options =>
            options.PluginSources.AddDirectory(dynamicDirectory));
        await host.ReloadAsync();

        var services = new ServiceCollection();
        services.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddDirectory(staticDirectory));
        using var provider = services.BuildBingServiceProvider();

        var loaded = provider.GetRequiredService<IBingPluginContainer>().Plugins.Single().EntryAssembly;
        loaded.GetName().Version.ShouldBe(AssemblyName.GetAssemblyName(workspace.FixtureAssemblyPath).Version);
        AssemblyLoadContext.GetLoadContext(loaded).ShouldBeSameAs(AssemblyLoadContext.Default);
    }

    /// <summary>
    /// 只返回弱引用，避免测试调用栈本身保留插件程序集。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task<WeakReference> CaptureContextAsync(BingHotPluginHost<PluginHostModule> host) =>
        host.RunAsync((provider, _) =>
        {
            var assembly = provider.GetRequiredService<IBingPluginContainer>().Plugins.Single().EntryAssembly;
            var context = AssemblyLoadContext.GetLoadContext(assembly);
            context.IsCollectible.ShouldBeTrue();
            return Task.FromResult(new WeakReference(context));
        });

    /// <summary>
    /// 在独立调用栈内检查候选失败，以免异常保留加载上下文。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task AssertFailedReloadAsync(BingHotPluginHost<PluginHostModule> host,
        string phase, string message)
    {
        var error = await Should.ThrowAsync<InvalidOperationException>(() => host.ReloadAsync());
        error.Message.ShouldBe(message);
        error.Data["Bing.ModulePhase"].ShouldBe(phase);
    }

    /// <summary>
    /// 验证候选异步释放完成前重新加载不会结束。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task AssertGatedFailedReloadAsync(BingHotPluginHost<PluginHostModule> host,
        TaskCompletionSource<bool> disposeEntered, TaskCompletionSource<bool> disposeRelease,
        string phase, string message)
    {
        var reload = host.ReloadAsync();
        try
        {
            await disposeEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            reload.IsCompleted.ShouldBeFalse();

            var current = await host.RunAsync((provider, _) => Task.FromResult(
                provider.GetRequiredService<IBingPluginContainer>().Plugins.Single().Id));
            current.ShouldBe("Contoso.Live");
        }
        finally
        {
            disposeRelease.TrySetResult(true);
        }
        var error = await Should.ThrowAsync<InvalidOperationException>(() => reload);
        error.Message.ShouldBe(message);
        error.Data["Bing.ModulePhase"].ShouldBe(phase);
    }

    /// <summary>
    /// 在独立调用栈内检查候选配置取消。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task AssertCanceledReloadAsync(BingHotPluginHost<PluginHostModule> host,
        CancellationTokenSource cancellation)
    {
        var error = await Should.ThrowAsync<OperationCanceledException>(() => host.ReloadAsync(cancellation.Token));
        error.CancellationToken.ShouldBe(cancellation.Token);
    }

    /// <summary>
    /// 在有界异步重试中验证旧上下文不再被框架持有。
    /// </summary>
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
    /// 等待新一代变为可见，避免使用固定时间延迟推测切换时机。
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }
}
