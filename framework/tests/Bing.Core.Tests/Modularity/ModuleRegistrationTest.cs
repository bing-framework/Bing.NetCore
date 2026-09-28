using System.Reflection;
using Bing.Core.Modularity;
using Bing.DependencyInjection;
using Bing.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Tests.Modularity;

/// <summary>
/// 新旧模块注册入口的行为契约测试。
/// </summary>
[Collection("Module static compatibility")]
public class ModuleRegistrationTest
{
    /// <summary>
    /// 验证新入口不会构造未选中的模块。
    /// </summary>
    [Fact]
    public void NewEntry_ShouldNotConstructUnselectedFailureModule()
    {
        var probe = new ModuleProbe();
        var services = Services(probe);

        services.AddBingApplication<SafeStartupModule>();

        probe.Events.ShouldNotContain(nameof(UnselectedFailureModule));
    }

    /// <summary>
    /// 验证旧入口立即配置且重复添加幂等。
    /// </summary>
    [Fact]
    public void LegacyEntry_ShouldConfigureModuleImmediately_AndBeIdempotent()
    {
        var probe = new ModuleProbe();
        var services = Services(probe);
        var builder = services.AddBing();

        builder.AddModule<LegacyImmediateModule>();
        builder.AddModule<LegacyImmediateModule>();

        probe.Events.Count(x => x == "Add:" + nameof(LegacyImmediateModule)).ShouldBe(1);
    }

    /// <summary>
    /// 验证旧入口在服务注册期间仍可读取静态服务集合，并在关闭后解绑。
    /// </summary>
    [Fact]
    public void LegacyEntry_ShouldExposeStaticServiceCollectionDuringRegistration()
    {
        var probe = new ModuleProbe();
        var services = Services(probe);
        var builder = services.AddBing();

        builder.AddModule<LegacyStaticCompatibilityModule>();

        probe.Events.ShouldContain("legacy-static-visible");
        ServiceLocator.Instance.GetServiceDescriptors().ShouldNotBeEmpty();

        using var provider = services.BuildServiceProvider();
        provider.ShutdownBing();
        ServiceLocator.Instance.GetServiceDescriptors().ShouldBeEmpty();
    }

    /// <summary>
    /// 验证旧入口注册失败后不会遗留注册阶段的静态服务集合引用。
    /// </summary>
    [Fact]
    public void LegacyRegistrationFailure_ShouldUnbindStaticServiceCollection()
    {
        var services = Services(new ModuleProbe());

        Should.Throw<InvalidOperationException>(() => services.AddBing(_ =>
            throw new InvalidOperationException("registration failure")));

        ServiceLocator.Instance.GetServiceDescriptors().ShouldBeEmpty();
    }

    /// <summary>
    /// 验证旧宿主运行时切换到失败注册不会混合两个宿主的静态绑定。
    /// </summary>
    [Fact]
    public void LegacyRegistrationFailure_ShouldNotMixActiveProviderWithNewServices()
    {
        var first = new ServiceCollection();
        var firstMarker = new LegacyHostMarker("first");
        first.AddSingleton(firstMarker);
        first.AddBing();
        using var firstProvider = first.BuildServiceProvider();
        firstProvider.UseBing();

        try
        {
            ServiceLocator.Instance.GetService<LegacyHostMarker>().ShouldBeSameAs(firstMarker);

            var second = new ServiceCollection();
            second.AddSingleton(new LegacyHostMarker("second"));
            var error = Should.Throw<InvalidOperationException>(() => second.AddBing(_ =>
                throw new InvalidOperationException("second registration failure")));
            error.Message.ShouldBe("second registration failure");

            ServiceLocator.Instance.IsProviderEnabled.ShouldBeFalse();
            ServiceLocator.Instance.ScopedProvider.ShouldBeNull();
            ServiceLocator.Instance.GetService<LegacyHostMarker>().ShouldBeNull();
            ServiceLocator.Instance.GetServiceDescriptors().ShouldBeEmpty();
            firstProvider.GetRequiredService<LegacyHostMarker>().ShouldBeSameAs(firstMarker);

            var third = new ServiceCollection();
            var thirdMarker = new LegacyHostMarker("third");
            third.AddSingleton(thirdMarker);
            third.AddBing();
            using var thirdProvider = third.BuildServiceProvider();
            thirdProvider.UseBing();
            ServiceLocator.Instance.GetService<LegacyHostMarker>().ShouldBeSameAs(thirdMarker);
            thirdProvider.ShutdownBing();
            ServiceLocator.Instance.IsProviderEnabled.ShouldBeFalse();
        }
        finally
        {
            firstProvider.ShutdownBing();
        }
    }

    /// <summary>
    /// 验证旧宿主关闭时不会清除新宿主共享的配置引用。
    /// </summary>
    [Fact]
    public void StoppingOlderLegacyHost_ShouldKeepSharedConfigurationOfNewerHost()
    {
        var configuration = new ConfigurationBuilder().Build();
        var first = new ServiceCollection().AddSingleton<IConfiguration>(configuration);
        var second = new ServiceCollection().AddSingleton<IConfiguration>(configuration);
        first.AddBing();
        second.AddBing();
        using var firstProvider = first.BuildServiceProvider();
        using var secondProvider = second.BuildServiceProvider();

        firstProvider.UseBing();
        secondProvider.UseBing();
        firstProvider.ShutdownBing();

        Singleton<IConfiguration>.Instance.ShouldBeSameAs(configuration);
        secondProvider.ShutdownBing();
        Singleton<IConfiguration>.Instance.ShouldBeNull();
    }

    /// <summary>
    /// 验证配置失败后不能复用服务集合并释放已拥有模块。
    /// </summary>
    [Fact]
    public void ConfigurationFailure_ShouldPreventReuseAndDisposeOwnedModules()
    {
        var probe = new ModuleProbe();
        var services = Services(probe);

        Should.Throw<InvalidOperationException>(() => services.AddBingApplication<FailingStartupModule>());

        probe.Events.ShouldContain("Dispose:" + nameof(DisposableConfiguredModule));
        Should.Throw<InvalidOperationException>(() => services.AddBingApplication<SafeStartupModule>());

        using var provider = services.BuildServiceProvider();
        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IBingModuleManager>().Initialize());
    }

    /// <summary>
    /// 验证新入口配置委托失败后不能继续复用服务集合。
    /// </summary>
    [Fact]
    public void NewEntry_ConfigureFailure_ShouldPreventReuse()
    {
        var services = new ServiceCollection();

        Should.Throw<InvalidOperationException>(() => services.AddBingApplication<SafeStartupModule>(_ =>
            throw new InvalidOperationException("options failure")));

        Should.Throw<InvalidOperationException>(() => services.AddBingApplication<SafeStartupModule>());
    }

    /// <summary>
    /// 验证新旧注册入口不能混用。
    /// </summary>
    [Fact]
    public void NewAndLegacyEntries_ShouldNotBeMixed()
    {
        var legacyServices = Services(new ModuleProbe());
        legacyServices.AddBing();
        Should.Throw<InvalidOperationException>(() => legacyServices.AddBingApplication<SafeStartupModule>());

        var newServices = Services(new ModuleProbe());
        newServices.AddBingApplication<SafeStartupModule>();
        Should.Throw<InvalidOperationException>(() => newServices.AddBing());
    }

    /// <summary>
    /// 验证新入口优先执行依赖而不是相反级别的根模块。
    /// </summary>
    [Fact]
    public void NewEntry_ShouldRunDependencyBeforeOppositeLevelRoot()
    {
        var probe = new ModuleProbe();
        var services = Services(probe);

        services.AddBingApplication<LowLevelRootModule>();
        var container = services.BuildServiceProvider().GetRequiredService<IBingModuleContainer>();

        container.Modules.Select(x => x.ModuleType).ToList()
            .IndexOf(typeof(HighLevelDependencyModule))
            .ShouldBeLessThan(container.Modules.Select(x => x.ModuleType).ToList()
                .IndexOf(typeof(LowLevelRootModule)));
    }

    /// <summary>
    /// 验证前置、服务注册和后置配置分轮执行。
    /// </summary>
    [Fact]
    public void NewEntry_ShouldRunPreAddPostInSeparateRounds()
    {
        var probe = new ModuleProbe();
        Services(probe).AddBingApplication<LifecycleRootModule>();

        var pre = probe.Events.Where(x => x.StartsWith("Pre:")).ToArray();
        var add = probe.Events.Where(x => x.StartsWith("Add:")).ToArray();
        var post = probe.Events.Where(x => x.StartsWith("Post:")).ToArray();
        pre.Length.ShouldBe(2);
        add.Length.ShouldBe(2);
        post.Length.ShouldBe(2);
        probe.Events.IndexOf(pre[^1]).ShouldBeLessThan(probe.Events.IndexOf(add[0]));
        probe.Events.IndexOf(add[^1]).ShouldBeLessThan(probe.Events.IndexOf(post[0]));
    }

    /// <summary>
    /// 验证关闭自动服务注册后跳过依赖扫描。
    /// </summary>
    [Fact]
    public void NewEntry_AutoRegisterServicesFalse_ShouldSkipDependencyScan()
    {
        var probe = new ModuleProbe();
        var services = Services(probe);

        services.AddBingApplication<SafeStartupModule>(options => options.AutoRegisterServices = false);

        using var provider = services.BuildServiceProvider();
        provider.GetService<IAutoRegisterProbe>().ShouldBeNull();
    }

    /// <summary>
    /// 验证新入口使用服务集合中提供的模块加载器实例。
    /// </summary>
    [Fact]
    public void NewEntry_ShouldUseProvidedModuleLoaderInstance()
    {
        var probe = new ModuleProbe();
        var loader = new RecordingModuleLoader();
        var services = Services(probe).AddSingleton<IBingModuleLoader>(loader);

        services.AddBingApplication<SafeStartupModule>();

        loader.Calls.ShouldBe(1);
    }

    /// <summary>
    /// 验证自定义加载器返回的合法顺序会被新入口实际采用。
    /// </summary>
    [Fact]
    public void NewEntry_ShouldUseCustomLoaderResultOrder()
    {
        var probe = new ModuleProbe();
        var loader = new ReorderingModuleLoader();
        var services = Services(probe).AddSingleton<IBingModuleLoader>(loader);

        services.AddBingApplication<LoaderTieStartupModule>(options =>
            options.AdditionalModules.Add(typeof(LoaderTieAdditionalModule)));

        probe.Events.Where(x => x.StartsWith("loader-", StringComparison.Ordinal))
            .ShouldBe(new[] { "loader-startup", "loader-additional" });
    }

    /// <summary>
    /// 验证自定义加载器返回非法目录时注册立即失败且服务集合不可复用。
    /// </summary>
    [Fact]
    public void NewEntry_ShouldRejectInvalidCustomLoaderResult()
    {
        var services = Services(new ModuleProbe()).AddSingleton<IBingModuleLoader>(new InvalidModuleLoader());

        Should.Throw<InvalidOperationException>(() => services.AddBingApplication<SafeStartupModule>());
        Should.Throw<InvalidOperationException>(() => services.AddBingApplication<SafeStartupModule>());
    }

    /// <summary>
    /// 验证自定义加载器返回空目录时注册立即失败且服务集合不可复用。
    /// </summary>
    [Fact]
    public void NewEntry_ShouldRejectNullCustomLoaderResult()
    {
        var services = Services(new ModuleProbe()).AddSingleton<IBingModuleLoader>(new NullModuleLoader());

        Should.Throw<InvalidOperationException>(() => services.AddBingApplication<SafeStartupModule>());
        Should.Throw<InvalidOperationException>(() => services.AddBingApplication<SafeStartupModule>());
    }

    /// <summary>
    /// 验证自定义加载器返回空描述符时注册立即失败且服务集合不可复用。
    /// </summary>
    [Fact]
    public void NewEntry_ShouldRejectNullModuleDescriptor()
    {
        var services = Services(new ModuleProbe()).AddSingleton<IBingModuleLoader>(new NullDescriptorModuleLoader());

        Should.Throw<InvalidOperationException>(() => services.AddBingApplication<SafeStartupModule>());
        Should.Throw<InvalidOperationException>(() => services.AddBingApplication<SafeStartupModule>());
    }

    /// <summary>
    /// 验证显式根模块保留其父依赖。
    /// </summary>
    [Fact]
    public void NewEntry_ShouldKeepExplicitParentDependencyInGraph()
    {
        var services = Services(new ModuleProbe());
        services.AddBingApplication<ExplicitChildModule>();

        using var provider = services.BuildServiceProvider();
        var types = provider.GetRequiredService<IBingModuleContainer>().Modules.Select(x => x.ModuleType).ToArray();
        types.ShouldContain(typeof(ExplicitParentModule));
        Array.IndexOf(types, typeof(ExplicitParentModule)).ShouldBeLessThan(Array.IndexOf(types, typeof(ExplicitChildModule)));
    }

    /// <summary>
    /// 创建包含模块探针的服务集合。
    /// </summary>
    /// <param name="probe">模块事件探针。</param>
    /// <returns>已登记探针的服务集合。</returns>
    private static IServiceCollection Services(ModuleProbe probe) =>
        new ServiceCollection().AddSingleton(probe);

    /// <summary>
    /// 标识旧入口宿主的测试服务。
    /// </summary>
    private sealed class LegacyHostMarker
    {
        /// <summary>
        /// 初始化旧入口宿主标记。
        /// </summary>
        /// <param name="name">宿主名称。</param>
        public LegacyHostMarker(string name) => Name = name;

        /// <summary>
        /// 获取宿主名称。
        /// </summary>
        public string Name { get; }
    }

    /// <summary>
    /// 验证旧入口配置失败后会污染注册状态。
    /// </summary>
    [Fact]
    public void LegacySetupFailure_ShouldPoisonRegistration()
    {
        var services = Services(new ModuleProbe());
        var error = Should.Throw<InvalidOperationException>(() => services.AddBing(_ =>
            throw new InvalidOperationException("setup failure")));
        error.Message.ShouldBe("setup failure");
        Should.Throw<InvalidOperationException>(() => services.AddBing());
        using var provider = services.BuildServiceProvider();
        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IBingModuleManager>());
    }

    /// <summary>
    /// 验证旧批量入口保留按根模块分组的注册顺序。
    /// </summary>
    [Fact]
    public void LegacyAddModules_ShouldKeepPerRootRegistrationOrder()
    {
        var probe = new ModuleProbe();
        var services = Services(probe);
        services.AddSingleton<IBingModuleTypeFinder>(new SelectedModuleFinder());
        services.AddBing().AddModules();

        // 批量入口先选择低 Level 根，再在该根内部按旧规则注册其依赖。
        // 不能把所有节点全局排序成 Root、Independent、Dependency。
        probe.Events.ShouldBe(new[] { "root", "dependency", "independent" });
        using var provider = services.BuildServiceProvider();
        provider.ShutdownBing();
    }

    /// <summary>
    /// 返回旧入口批量测试候选模块的类型查找器。
    /// </summary>
    private sealed class SelectedModuleFinder : IBingModuleTypeFinder
    {
        /// <inheritdoc />
        public Type[] FindAll(bool fromCache = false) => new[]
            { typeof(LegacyIndependentModule), typeof(LegacyRootModule) };
        /// <inheritdoc />
        public Type[] Find(Func<Type, bool> predicate, bool fromCache = false) =>
            FindAll(fromCache).Where(predicate).ToArray();
    }

    /// <summary>
    /// 验证旧入口依赖图失败后禁止继续使用并释放已创建模块。
    /// </summary>
    [Theory]
    [InlineData("cycle")]
    [InlineData("invalid")]
    [InlineData("excluded")]
    public void LegacyGraphFailure_ShouldPoisonBuilderAndReleaseExistingModules(string failure)
    {
        var probe = new ModuleProbe();
        var services = Services(probe);
        services.AddSingleton<IBingModuleTypeFinder>(new SelectedModuleFinder());
        var builder = services.AddBing().AddModule<DisposableConfiguredModule>();
        if (failure == "cycle")
            Should.Throw<InvalidOperationException>(() => builder.AddModule<LegacyCycleModule>());
        else if (failure == "invalid")
            Should.Throw<ArgumentException>(() => builder.AddModule<LegacyInvalidDependencyModule>());
        else
            Should.Throw<InvalidOperationException>(() => builder.AddModules(typeof(LegacyHighDependencyModule)));

        probe.Events.ShouldBe(new[] { "Dispose:" + nameof(DisposableConfiguredModule) });
        Should.Throw<InvalidOperationException>(() => builder.AddModule<SafeStartupModule>());
        Should.Throw<InvalidOperationException>(() => builder.AddModules());
        Should.Throw<InvalidOperationException>(() => services.AddBing());
        using var provider = services.BuildServiceProvider();
        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IBingModuleManager>());
        Should.Throw<InvalidOperationException>(() => provider.UseBing());
        probe.Events.ShouldBe(new[] { "Dispose:" + nameof(DisposableConfiguredModule) });
    }

    /// <summary>
    /// 验证相等模块类型仍按类型各配置一次。
    /// </summary>
    [Fact]
    public void LegacyEqualModules_ShouldConfigureEachTypeOnlyOnce()
    {
        var probe = new ModuleProbe();
        var services = Services(probe);
        var builder = services.AddBing();
        builder.AddModule<LegacyEqualFirstModule>();
        builder.AddModule<LegacyEqualSecondModule>();
        builder.AddModule<LegacyEqualFirstModule>();
        probe.Events.ShouldBe(new[] { nameof(LegacyEqualFirstModule), nameof(LegacyEqualSecondModule) });
        using var provider = services.BuildServiceProvider();
        provider.ShutdownBing();
    }

    /// <summary>
    /// 旧入口自环依赖模块。
    /// </summary>
    [DependsOnModule(typeof(LegacyCycleModule))]
    public class LegacyCycleModule : BingModule { }

    /// <summary>
    /// 旧入口非法依赖模块。
    /// </summary>
    [DependsOnModule(typeof(string))]
    public class LegacyInvalidDependencyModule : BingModule { }

    /// <summary>
    /// 让不同模块实例在相等性比较中相等的基类。
    /// </summary>
    public abstract class LegacyEqualBase : BingModule
    {
        /// <inheritdoc />
        public override bool Equals(object obj) => obj is LegacyEqualBase;
        /// <inheritdoc />
        public override int GetHashCode() => 1;
        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            Probe(services).Events.Add(GetType().Name);
            return services;
        }
    }

    /// <summary>
    /// 相等模块测试的第一个类型。
    /// </summary>
    public class LegacyEqualFirstModule : LegacyEqualBase { }
    /// <summary>
    /// 相等模块测试的第二个类型。
    /// </summary>
    public class LegacyEqualSecondModule : LegacyEqualBase { }

    /// <summary>
    /// 旧入口批量注册的根模块。
    /// </summary>
    [DependsOnModule(typeof(LegacyHighDependencyModule))]
    public class LegacyRootModule : BingModule
    {
        /// <inheritdoc />
        public override ModuleLevel Level => ModuleLevel.Core;
        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            Probe(services).Events.Add("root");
            return services;
        }
    }

    /// <summary>
    /// 旧入口批量注册的高等级依赖模块。
    /// </summary>
    public class LegacyHighDependencyModule : BingModule
    {
        /// <inheritdoc />
        public override ModuleLevel Level => ModuleLevel.Business;
        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            Probe(services).Events.Add("dependency");
            return services;
        }
    }

    /// <summary>
    /// 旧入口批量注册的独立模块。
    /// </summary>
    public class LegacyIndependentModule : BingModule
    {
        /// <inheritdoc />
        public override ModuleLevel Level => ModuleLevel.Application;
        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            Probe(services).Events.Add("independent");
            return services;
        }
    }

    /// <summary>
    /// 记录旧入口模块配置事件。
    /// </summary>
    private sealed class ModuleProbe
    {
        /// <summary>
        /// 获取模块配置事件列表。
        /// </summary>
        public List<string> Events { get; } = new();
    }

    /// <summary>
    /// 从服务集合获取模块探针。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>已注册的模块探针。</returns>
    private static ModuleProbe Probe(IServiceCollection services) =>
        services.Last(d => d.ServiceType == typeof(ModuleProbe)).ImplementationInstance as ModuleProbe;

    /// <summary>
    /// 从服务提供程序获取模块探针。
    /// </summary>
    /// <param name="provider">服务提供程序。</param>
    /// <returns>已解析的模块探针。</returns>
    private static ModuleProbe Probe(IServiceProvider provider) => provider.GetRequiredService<ModuleProbe>();

    /// <summary>
    /// 记录自定义模块加载器调用次数的加载器。
    /// </summary>
    private sealed class RecordingModuleLoader : IBingModuleLoader
    {
        /// <summary>
        /// 获取加载器调用次数。
        /// </summary>
        public int Calls { get; private set; }

        /// <inheritdoc />
        public IReadOnlyList<BingModuleDescriptor> LoadModules(IEnumerable<Type> roots, IEnumerable<Type> excludedModules)
        {
            Calls++;
            return new BingModuleLoader().LoadModules(roots, excludedModules);
        }
    }

    /// <summary>
    /// 返回合法但与默认平局顺序不同的模块目录。
    /// </summary>
    private sealed class ReorderingModuleLoader : IBingModuleLoader
    {
        /// <inheritdoc />
        public IReadOnlyList<BingModuleDescriptor> LoadModules(IEnumerable<Type> roots, IEnumerable<Type> excludedModules)
        {
            var loaded = new BingModuleLoader().LoadModules(roots, excludedModules).ToList();
            var startupIndex = loaded.FindIndex(x => x.ModuleType == typeof(LoaderTieStartupModule));
            var additionalIndex = loaded.FindIndex(x => x.ModuleType == typeof(LoaderTieAdditionalModule));
            (loaded[startupIndex], loaded[additionalIndex]) = (loaded[additionalIndex], loaded[startupIndex]);

            return loaded.Select((descriptor, index) => new BingModuleDescriptor(
                GetInstance(descriptor), descriptor.Dependencies, index)).ToArray();
        }

        /// <summary>
        /// 读取描述符中的模块实例以构造无效目录。
        /// </summary>
        /// <param name="descriptor">待读取的模块描述符。</param>
        /// <returns>描述符持有的模块实例。</returns>
        private static BingModule GetInstance(BingModuleDescriptor descriptor) =>
            (BingModule)typeof(BingModuleDescriptor)
                .GetProperty("Instance", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(descriptor)!;
    }

    /// <summary>
    /// 返回重复描述符以验证新入口的目录校验。
    /// </summary>
    private sealed class InvalidModuleLoader : IBingModuleLoader
    {
        /// <inheritdoc />
        public IReadOnlyList<BingModuleDescriptor> LoadModules(IEnumerable<Type> roots, IEnumerable<Type> excludedModules)
        {
            var loaded = new BingModuleLoader().LoadModules(roots, excludedModules).ToList();
            return loaded.Concat(new[] { loaded[0] }).ToArray();
        }
    }

    /// <summary>
    /// 返回空模块目录的加载器。
    /// </summary>
    private sealed class NullModuleLoader : IBingModuleLoader
    {
        /// <inheritdoc />
        public IReadOnlyList<BingModuleDescriptor> LoadModules(IEnumerable<Type> roots, IEnumerable<Type> excludedModules) => null;
    }

    /// <summary>
    /// 返回包含空描述符的加载器。
    /// </summary>
    private sealed class NullDescriptorModuleLoader : IBingModuleLoader
    {
        /// <inheritdoc />
        public IReadOnlyList<BingModuleDescriptor> LoadModules(IEnumerable<Type> roots, IEnumerable<Type> excludedModules) =>
            new BingModuleDescriptor[] { null };
    }

    /// <summary>
    /// 不执行额外操作的安全启动模块。
    /// </summary>
    public class SafeStartupModule : BingModule { }

    /// <summary>
    /// 在注册阶段读取旧静态服务集合的模块。
    /// </summary>
    public class LegacyStaticCompatibilityModule : BingModule
    {
        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            var visible = ServiceLocator.Instance.GetServiceDescriptors()
                .Any(x => x.ServiceType == typeof(BingModule));
            Probe(services).Events.Add(visible ? "legacy-static-visible" : "legacy-static-missing");
            return services;
        }
    }

    /// <summary>
    /// 自定义 loader 顺序测试的启动模块。
    /// </summary>
    public class LoaderTieStartupModule : BingModule
    {
        /// <inheritdoc />
        public override ModuleLevel Level => ModuleLevel.Core;

        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            Probe(services).Events.Add("loader-startup");
            return services;
        }
    }

    /// <summary>
    /// 自定义 loader 顺序测试的附加模块。
    /// </summary>
    public class LoaderTieAdditionalModule : BingModule
    {
        /// <inheritdoc />
        public override ModuleLevel Level => ModuleLevel.Core;

        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            Probe(services).Events.Add("loader-additional");
            return services;
        }
    }

    /// <summary>
    /// 构造时抛出异常以验证未选中模块不会被创建。
    /// </summary>
    public class UnselectedFailureModule : BingModule
    {
        /// <summary>
        /// 初始化并抛出测试异常。
        /// </summary>
        public UnselectedFailureModule() => throw new InvalidOperationException("不应构造未选中的模块");
    }

    /// <summary>
    /// 记录旧入口立即服务注册的模块。
    /// </summary>
    public class LegacyImmediateModule : BingModule
    {
        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            Probe(services).Events.Add("Add:" + nameof(LegacyImmediateModule));
            return services;
        }
    }

    /// <summary>
    /// 在服务注册阶段失败的启动模块。
    /// </summary>
    [DependsOnModule(typeof(DisposableConfiguredModule))]
    public class FailingStartupModule : BingModule
    {
        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services) =>
            throw new InvalidOperationException("配置失败");
    }

    /// <summary>
    /// 记录配置服务集合并在释放时写入事件的模块。
    /// </summary>
    public class DisposableConfiguredModule : BingModule, IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => Probe(Services).Events.Add("Dispose:" + nameof(DisposableConfiguredModule));
        /// <summary>
        /// 获取或设置模块配置时使用的服务集合。
        /// </summary>
        private IServiceCollection Services { get; set; }

        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            Services = services;
            return services;
        }
    }

    /// <summary>
    /// 级别低于其依赖的根模块。
    /// </summary>
    [DependsOnModule(typeof(HighLevelDependencyModule))]
    public class LowLevelRootModule : BingModule
    {
        /// <inheritdoc />
        public override ModuleLevel Level => ModuleLevel.Core;
    }

    /// <summary>
    /// 级别高于根模块但必须先执行的依赖模块。
    /// </summary>
    public class HighLevelDependencyModule : BingModule
    {
        /// <inheritdoc />
        public override ModuleLevel Level => ModuleLevel.Business;
    }

    /// <summary>
    /// 使用前置和后置配置钩子的根模块。
    /// </summary>
    [DependsOnModule(typeof(LifecycleDependencyModule))]
    public class LifecycleRootModule : LifecycleModuleBase { }

    /// <summary>
    /// 使用前置和后置配置钩子的依赖模块。
    /// </summary>
    public class LifecycleDependencyModule : LifecycleModuleBase { }

    /// <summary>
    /// 记录三阶段服务配置顺序的模块基类。
    /// </summary>
    public abstract class LifecycleModuleBase : BingModule, IBingPreConfigureServices, IBingPostConfigureServices
    {
        /// <inheritdoc />
        public new void PreConfigureServices(IServiceCollection services) => Probe(services).Events.Add("Pre:" + GetType().Name);
        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            Probe(services).Events.Add("Add:" + GetType().Name);
            return services;
        }
        /// <inheritdoc />
        public new void PostConfigureServices(IServiceCollection services) => Probe(services).Events.Add("Post:" + GetType().Name);
    }

    /// <summary>
    /// 自动注册服务的测试接口。
    /// </summary>
    public interface IAutoRegisterProbe { }

    /// <summary>
    /// 自动注册服务的测试实现。
    /// </summary>
    [Dependency(ServiceLifetime.Singleton)]
    public class AutoRegisterProbe : IAutoRegisterProbe { }

    /// <summary>
    /// 显式父依赖测试的子模块。
    /// </summary>
    [DependsOnModule(typeof(ExplicitParentModule))]
    public class ExplicitChildModule : BingModule { }

    /// <summary>
    /// 显式父依赖测试的父模块。
    /// </summary>
    public class ExplicitParentModule : BingModule { }
}
