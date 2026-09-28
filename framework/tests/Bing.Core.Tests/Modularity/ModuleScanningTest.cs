using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Runtime.CompilerServices;
using Bing.Configuration;
using Bing.Core.Modularity;
using Bing.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.Core.Tests.Modularity;

/// <summary>
/// 新入口按实际模块程序集扫描服务与选项的边界测试。
/// </summary>
public class ModuleScanningTest
{
    /// <summary>
    /// 验证配置钩子修改原选项后，本轮扫描时机及事件次数仍固定。
    /// </summary>
    [Theory]
    [InlineData(BingServiceRegistrationMode.Compatible, BingServiceRegistrationMode.BeforeConfigureServices, true, false)]
    [InlineData(BingServiceRegistrationMode.Compatible, BingServiceRegistrationMode.BeforeConfigureServices, false, true)]
    [InlineData(BingServiceRegistrationMode.BeforeConfigureServices, BingServiceRegistrationMode.Compatible, true, false)]
    [InlineData(BingServiceRegistrationMode.BeforeConfigureServices, BingServiceRegistrationMode.Compatible, false, true)]
    [InlineData(BingServiceRegistrationMode.Compatible, BingServiceRegistrationMode.BeforeConfigureServices, true, true)]
    [InlineData(BingServiceRegistrationMode.Compatible, BingServiceRegistrationMode.BeforeConfigureServices, false, false)]
    [InlineData(BingServiceRegistrationMode.BeforeConfigureServices, BingServiceRegistrationMode.Compatible, true, true)]
    [InlineData(BingServiceRegistrationMode.BeforeConfigureServices, BingServiceRegistrationMode.Compatible, false, false)]
    public async Task RegistrationModeMutation_ShouldNotRepeatOrSkipTypeEvents(
        BingServiceRegistrationMode initial, BingServiceRegistrationMode changed, bool mutateInPre, bool asynchronous)
    {
        var extra = DynamicScanAssembly.Create("ModeSnapshot");
        var late = DynamicScanAssembly.Create("LateModeSnapshot");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            [extra.OptionsSection + ":Value"] = "bound"
        }).Build();
        var services = new ServiceCollection();
        services.AddOptionsType(configuration);
        ModeMutationModule.Changed = changed;
        ModeMutationModule.MutateInPre = mutateInPre;
        ModeMutationModule.LateAssembly = late.ModuleType.Assembly;
        var observed = 0;
        Action<Type> observer = type => { if (type == extra.OptionsType) observed++; };
        BingLoader.RegisterType += observer;
        try
        {
            void Configure(BingApplicationOptions options)
            {
                options.ServiceRegistrationMode = initial;
                options.ServiceScanning.AdditionalAssemblies.Add(extra.ModuleType.Assembly);
                ModeMutationModule.Options = options;
            }
            if (asynchronous)
                await services.AddBingApplicationAsync<ModeMutationModule>(Configure);
            else
                services.AddBingApplication<ModeMutationModule>(Configure);

            using var provider = services.BuildServiceProvider();
            observed.ShouldBe(1);
            provider.GetService(late.ServiceType).ShouldBeNull();
            var options = provider.GetRequiredService(typeof(IOptions<>).MakeGenericType(extra.OptionsType));
            extra.OptionsValueProperty.GetValue(options.GetType().GetProperty("Value")!.GetValue(options))
                .ShouldBe("bound");
        }
        finally
        {
            BingLoader.RegisterType -= observer;
            ModeMutationModule.Options = null;
            ModeMutationModule.LateAssembly = null;
        }
    }

    /// <summary>
    /// 验证服务集合仍存活时，扫描筛选器的捕获对象已释放。
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ConventionalFilter_ShouldReleaseCapturedObject(bool autoRegister, bool fail)
    {
        var result = CreateFilteredRegistration(autoRegister, fail);
        for (var attempt = 0; attempt < 10 && result.Target.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        result.Target.IsAlive.ShouldBeFalse();
        if (autoRegister && !fail)
        {
            var finder = result.Services.Single(item => item.ServiceType == typeof(IDependencyTypeFinder))
                .ImplementationInstance.ShouldBeOfType<DependencyTypeFinder>();
            finder.FindAll(false).ShouldContain(type => type.FullName == "DynamicService");
            finder.FindAll(false).ShouldNotContain(type => type.FullName == "SiblingService");
        }
        GC.KeepAlive(result.Services);
    }

    /// <summary>
    /// 验证追加查找器后，原筛选器在成功、失败及取消时均可释放。
    /// </summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    public void ConventionalFilter_WithAppendedFinder_ShouldReleaseCapturedObject(
        bool appendInstance, bool fail, bool cancel)
    {
        var result = CreateRegistrationWithAppendedFinder(appendInstance, fail, cancel);
        for (var attempt = 0; attempt < 10 && result.Target.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        result.Target.IsAlive.ShouldBeFalse();
        if (!fail && !cancel)
        {
            var originalFinder = result.Services.First(item => item.ServiceType == typeof(IDependencyTypeFinder))
                .ImplementationInstance.ShouldBeOfType<DependencyTypeFinder>();
            originalFinder.FindAll(false).ShouldContain(type => type.FullName == "DynamicService");
            originalFinder.FindAll(false).ShouldNotContain(type => type.FullName == "SiblingService");
        }
        GC.KeepAlive(result.Services);
    }

    /// <summary>
    /// 在独立栈帧中执行追加查找器的注册。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (IServiceCollection Services, WeakReference Target) CreateRegistrationWithAppendedFinder(
        bool appendInstance, bool fail, bool cancel)
    {
        var extra = DynamicScanAssembly.Create("AppendedFinderLifetime");
        var services = new ServiceCollection();
        using var cancellation = new CancellationTokenSource();
        services.AddSingleton(new FinderOverridePlan
        {
            AppendInstance = appendInstance,
            Fail = fail,
            Cancel = cancel,
            Cancellation = cancellation
        });
        var filter = new FilterOwner();
        void Configure(BingApplicationOptions options)
        {
            options.ServiceScanning.AdditionalAssemblies.Add(extra.ModuleType.Assembly);
            options.ServiceScanning.ConventionalTypeFilter = filter.Include;
        }

        if (cancel)
            Should.Throw<OperationCanceledException>(() =>
                services.AddBingApplicationAsync<FinderOverrideModule>(Configure, cancellation.Token)
                    .GetAwaiter().GetResult());
        else if (fail)
            Should.Throw<InvalidOperationException>(() => services.AddBingApplication<FinderOverrideModule>(Configure));
        else
            services.AddBingApplication<FinderOverrideModule>(Configure);

        services.Count(item => item.ServiceType == typeof(IDependencyTypeFinder)).ShouldBe(2);
        if (fail || cancel)
            Should.Throw<InvalidOperationException>(() => services.BuildBingServiceProvider());
        return (services, new WeakReference(filter));
    }

    /// <summary>
    /// 控制追加查找器的测试状态。
    /// </summary>
    private sealed class FinderOverridePlan
    {
        /// <summary>
        /// 是否追加实例注册。
        /// </summary>
        public bool AppendInstance { get; set; }

        /// <summary>
        /// 是否在追加后使配置失败。
        /// </summary>
        public bool Fail { get; set; }

        /// <summary>
        /// 是否在追加后取消注册。
        /// </summary>
        public bool Cancel { get; set; }

        /// <summary>
        /// 用于取消异步注册。
        /// </summary>
        public CancellationTokenSource Cancellation { get; set; }
    }

    /// <summary>
    /// 在后置配置阶段追加查找器。
    /// </summary>
    public sealed class FinderOverrideModule : BingModule
    {
        /// <inheritdoc />
        public override void PostConfigureServices(IServiceCollection services)
        {
            var plan = services.Single(item => item.ServiceType == typeof(FinderOverridePlan))
                .ImplementationInstance.ShouldBeOfType<FinderOverridePlan>();
            if (plan.AppendInstance)
            {
                var finder = services.Last(item => item.ServiceType == typeof(Bing.Reflection.IAllAssemblyFinder))
                    .ImplementationInstance.ShouldBeAssignableTo<Bing.Reflection.IAllAssemblyFinder>();
                services.AddSingleton<IDependencyTypeFinder>(new DependencyTypeFinder(finder));
            }
            else
                services.AddSingleton<IDependencyTypeFinder, DependencyTypeFinder>();

            if (plan.Fail)
                throw new InvalidOperationException("post configuration failure");
            if (plan.Cancel)
                plan.Cancellation.Cancel();
        }
    }

    /// <summary>
    /// 在独立栈帧创建筛选器，以免测试局部变量干扰回收验证。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (IServiceCollection Services, WeakReference Target) CreateFilteredRegistration(bool autoRegister,
        bool fail)
    {
        var extra = DynamicScanAssembly.Create("FilterLifetime");
        var services = new ServiceCollection();
        var filter = new FilterOwner { ThrowOnService = fail };
        if (fail)
            Should.Throw<InvalidOperationException>(() => services.AddBingApplication<ScanStartupModule>(options =>
            {
                options.ServiceScanning.AdditionalAssemblies.Add(extra.ModuleType.Assembly);
                options.ServiceScanning.ConventionalTypeFilter = filter.Include;
            }));
        else
            services.AddBingApplication<ScanStartupModule>(options =>
            {
                options.AutoRegisterServices = autoRegister;
                options.ServiceScanning.AdditionalAssemblies.Add(extra.ModuleType.Assembly);
                options.ServiceScanning.ConventionalTypeFilter = filter.Include;
            });
        return (services, new WeakReference(filter));
    }

    /// <summary>
    /// 承载需要验证释放时机的筛选器。
    /// </summary>
    private sealed class FilterOwner
    {
        /// <summary>
        /// 筛选器是否对目标类型抛出异常。
        /// </summary>
        public bool ThrowOnService { get; set; }

        /// <summary>
        /// 只允许目标类型之外的约定服务。
        /// </summary>
        /// <param name="type">待筛选的服务类型。</param>
        /// <returns>允许注册时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        public bool Include(Type type)
        {
            if (ThrowOnService && type.FullName == "DynamicService")
                throw new InvalidOperationException("filter failure");
            return type.FullName != "SiblingService";
        }
    }

    /// <summary>
    /// 在配置阶段尝试修改调用方原选项的模块。
    /// </summary>
    public sealed class ModeMutationModule : BingModule
    {
        /// <summary>
        /// 当前测试传入的应用选项。
        /// </summary>
        public static BingApplicationOptions Options { get; set; }

        /// <summary>
        /// 测试钩子要写入的扫描时机。
        /// </summary>
        public static BingServiceRegistrationMode Changed { get; set; }

        /// <summary>
        /// 是否在前置阶段修改选项。
        /// </summary>
        public static bool MutateInPre { get; set; }

        /// <summary>
        /// 配置阶段追加的程序集。
        /// </summary>
        public static Assembly LateAssembly { get; set; }

        /// <inheritdoc />
        public override void PreConfigureServices(IServiceCollection services)
        {
            if (MutateInPre)
            {
                Options.ServiceRegistrationMode = Changed;
                Options.ServiceScanning.AdditionalAssemblies.Add(LateAssembly);
            }
        }

        /// <inheritdoc />
        public override void PostConfigureServices(IServiceCollection services)
        {
            if (!MutateInPre)
            {
                Options.ServiceRegistrationMode = Changed;
                Options.ServiceScanning.AdditionalAssemblies.Add(LateAssembly);
            }
        }
    }

    /// <summary>
    /// 验证关闭约定 DI 后仍可扫描选项而不执行类型筛选器。
    /// </summary>
    [Theory]
    [InlineData(BingServiceRegistrationMode.Compatible)]
    [InlineData(BingServiceRegistrationMode.BeforeConfigureServices)]
    public void DisabledAutomaticRegistration_ShouldKeepOptionsAndSkipFilter(BingServiceRegistrationMode mode)
    {
        var extra = DynamicScanAssembly.Create("DisabledFilter");
        var services = new ServiceCollection();
        services.AddOptionsType(new ConfigurationBuilder().Build());
        services.AddBingApplication<ScanStartupModule>(options =>
        {
            options.ServiceRegistrationMode = mode;
            options.AutoRegisterServices = false;
            options.ServiceScanning.AdditionalAssemblies.Add(extra.ModuleType.Assembly);
            options.ServiceScanning.ConventionalTypeFilter = _ => throw new InvalidOperationException("filter ran");
        });

        using var provider = services.BuildServiceProvider();
        provider.GetService(extra.ServiceType).ShouldBeNull();
        provider.GetRequiredService(typeof(IOptions<>).MakeGenericType(extra.OptionsType)).ShouldNotBeNull();
    }

    /// <summary>
    /// 验证筛选异常使注册失败并包含类型诊断。
    /// </summary>
    [Fact]
    public void ConventionalFilterFailure_ShouldRejectRegistration()
    {
        var extra = DynamicScanAssembly.Create("ThrowingFilter");
        var services = new ServiceCollection();
        var candidate = extra.ModuleType.Assembly.GetType("DynamicService")!;
        var error = Should.Throw<InvalidOperationException>(() =>
            services.AddBingApplication<ScanStartupModule>(options =>
            {
                options.ServiceScanning.AdditionalAssemblies.Add(extra.ModuleType.Assembly);
                options.ServiceScanning.ConventionalTypeFilter = type =>
                    type == candidate ? throw new InvalidOperationException("filter ran") : true;
            }));

        error.Data["Bing.ModulePhase"].ShouldBe("AddServices");
        error.Data["Bing.ServiceScanPhase"].ShouldBe("RegisterConventionServices");
        error.Data["Bing.ServiceType"].ShouldBe(candidate.FullName);
        Should.Throw<InvalidOperationException>(() => services.BuildBingServiceProvider());
    }

    /// <summary>
    /// 验证应用拒绝扫描其他可回收插件代的程序集。
    /// </summary>
    [Fact]
    public void AdditionalAssemblyFromForeignContext_ShouldBeRejected()
    {
        using var workspace = new PluginTestWorkspace();
        var foreign = new AssemblyLoadContext("foreign-scan", isCollectible: true);
        try
        {
            var assembly = foreign.LoadFromAssemblyPath(workspace.FixtureScanAssemblyPath);
            var services = new ServiceCollection();
            var error = Should.Throw<InvalidOperationException>(() =>
                services.AddBingApplication<ScanStartupModule>(options =>
                    options.ServiceScanning.AdditionalAssemblies.Add(assembly)));
            error.Message.ShouldContain("其他插件代");
            Should.Throw<InvalidOperationException>(() => services.BuildBingServiceProvider());
        }
        finally { foreign.Unload(); }
    }

    /// <summary>
    /// 验证模块继承的程序集声明会扩展服务和选项扫描范围。
    /// </summary>
    [Theory]
    [InlineData(BingServiceRegistrationMode.Compatible, false)]
    [InlineData(BingServiceRegistrationMode.Compatible, true)]
    [InlineData(BingServiceRegistrationMode.BeforeConfigureServices, false)]
    [InlineData(BingServiceRegistrationMode.BeforeConfigureServices, true)]
    public void ModuleAssemblyDeclaration_ShouldScanSelectedModulesOnly(BingServiceRegistrationMode mode,
        bool registerOptionsLate)
    {
        var included = DynamicScanAssembly.Create("Included");
        var selected = DynamicScanAssembly.Create("SelectedWithAssembly", typeof(ModuleScanningTest), true);
        var ignored = DynamicScanAssembly.Create("Ignored");
        var unselected = DynamicScanAssembly.Create("UnselectedWithAssembly", typeof(ModuleScanningTest));
        var services = new ServiceCollection();
        var loader = new RecordingModuleLoader();
        services.AddSingleton<IBingModuleLoader>(loader);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            [included.OptionsSection + ":Value"] = "included"
        }).Build();
        if (!registerOptionsLate) services.AddOptionsType(configuration);
        var seen = 0;
        Action<Type> observe = type => { if (type == included.OptionsType) seen++; };
        BingLoader.RegisterType += observe;
        try
        {
            services.AddBingApplication<ScanStartupModule>(options =>
            {
                options.AdditionalModules.Add(selected.ModuleType);
                options.ServiceRegistrationMode = mode;
                options.ServiceScanning.AdditionalAssemblies.Add(included.ModuleType.Assembly);
                options.ServiceScanning.AdditionalAssemblies.Add(included.ModuleType.Assembly);
            });
            if (registerOptionsLate) services.AddOptionsType(configuration);
            using var provider = services.BuildServiceProvider();
            provider.GetService(included.ServiceType).ShouldNotBeNull();
            provider.GetService(ignored.ServiceType).ShouldBeNull();
            seen.ShouldBe(1);
            loader.Calls.ShouldBe(1);
            provider.GetRequiredService<Bing.Reflection.IAllAssemblyFinder>().FindAll()
                .Count(assembly => assembly == included.ModuleType.Assembly).ShouldBe(1);
            var descriptor = provider.GetRequiredService<IBingModuleContainer>().Modules.Single(module =>
                module.ModuleType == selected.ModuleType);
            descriptor.Assemblies.ShouldContain(typeof(ModuleScanningTest).Assembly);
            descriptor.Assemblies.Count.ShouldBe(2);
            Should.Throw<NotSupportedException>(() =>
                ((IList<Assembly>)descriptor.Assemblies).Add(included.ModuleType.Assembly));
            var options = provider.GetRequiredService(typeof(IOptions<>).MakeGenericType(included.OptionsType));
            included.OptionsValueProperty.GetValue(options.GetType().GetProperty("Value")!.GetValue(options))
                .ShouldBe("included");
        }
        finally { BingLoader.RegisterType -= observe; }
    }

    /// <summary>
    /// 验证未选模块的附加程序集声明不会扩大扫描范围。
    /// </summary>
    [Fact]
    public void UnselectedModuleDeclaration_ShouldNotScanItsUniqueAssembly()
    {
        var unique = DynamicScanAssembly.Create("UnselectedUniqueTarget");
        var unselected = DynamicScanAssembly.Create("UnselectedUniqueOwner", unique.ModuleType);
        var services = new ServiceCollection();
        services.AddOptionsType(new ConfigurationBuilder().Build());
        services.AddBingApplication<ScanStartupModule>();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<Bing.Reflection.IAllAssemblyFinder>().FindAll()
            .ShouldNotContain(unique.ModuleType.Assembly);
        provider.GetService(unique.ServiceType).ShouldBeNull();
        provider.GetService(typeof(IConfigureOptions<>).MakeGenericType(unique.OptionsType)).ShouldBeNull();
        provider.GetRequiredService<IBingModuleContainer>().Modules
            .ShouldNotContain(module => module.ModuleType == unselected.ModuleType);
    }

    /// <summary>
    /// 记录自定义模块加载器的调用。
    /// </summary>
    private sealed class RecordingModuleLoader : BingModuleLoader
    {
        /// <summary>
        /// 模块图加载次数。
        /// </summary>
        public int Calls { get; private set; }

        /// <inheritdoc />
        public override IReadOnlyList<BingModuleDescriptor> LoadModules(IEnumerable<Type> roots,
            IEnumerable<Type> excludedModules)
        {
            Calls++;
            return base.LoadModules(roots, excludedModules);
        }
    }

    /// <summary>
    /// 验证约定类型筛选不会改变手工注册和选项事件。
    /// </summary>
    [Theory]
    [InlineData(BingServiceRegistrationMode.Compatible)]
    [InlineData(BingServiceRegistrationMode.BeforeConfigureServices)]
    public void ConventionalFilter_ShouldOnlyAffectAutomaticServices(BingServiceRegistrationMode mode)
    {
        var extra = DynamicScanAssembly.Create("Filtered");
        var services = new ServiceCollection();
        services.AddOptionsType(new ConfigurationBuilder().Build());
        services.AddSingleton(extra.ServiceType,
            Activator.CreateInstance(extra.ModuleType.Assembly.GetType("DynamicService")!));
        var seen = 0;
        Action<Type> observe = type => { if (type == extra.OptionsType) seen++; };
        BingLoader.RegisterType += observe;
        try
        {
            services.AddBingApplication<ScanStartupModule>(options =>
            {
                options.ServiceRegistrationMode = mode;
                options.ServiceScanning.AdditionalAssemblies.Add(extra.ModuleType.Assembly);
                options.ServiceScanning.ConventionalTypeFilter = type => type != extra.ServiceType &&
                    type.FullName != "DynamicService" && type.FullName != "SiblingService";
            });
            using var provider = services.BuildServiceProvider();
            provider.GetService(extra.ServiceType).ShouldNotBeNull();
            provider.GetService(extra.SiblingServiceType).ShouldBeNull();
            provider.GetRequiredService(typeof(IOptions<>).MakeGenericType(extra.OptionsType)).ShouldNotBeNull();
            seen.ShouldBe(1);
        }
        finally { BingLoader.RegisterType -= observe; }
    }

    /// <summary>
    /// 验证新入口只扫描选中模块所属程序集。
    /// </summary>
    [Theory]
    [InlineData(BingServiceRegistrationMode.Compatible, false)]
    [InlineData(BingServiceRegistrationMode.BeforeConfigureServices, true)]
    public void AddBingApplication_ShouldScanSelectedAssembly_AndIgnoreUnselectedAssembly(
        BingServiceRegistrationMode mode, bool registerOptionsLate)
    {
        var selected = DynamicScanAssembly.Create("Selected");
        var unselected = DynamicScanAssembly.Create("Unselected");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                [selected.OptionsSection + ":Value"] = "selected",
                [unselected.OptionsSection + ":Value"] = "unselected"
            })
            .Build();
        var services = new ServiceCollection();
        if (!registerOptionsLate) services.AddOptionsType(configuration);

        services.AddBingApplication<ScanStartupModule>(options =>
        {
            options.AdditionalModules.Add(selected.ModuleType);
            options.ServiceRegistrationMode = mode;
        });
        if (registerOptionsLate) services.AddOptionsType(configuration);

        using var provider = services.BuildServiceProvider();
        provider.GetService(selected.ServiceType).ShouldNotBeNull();
        provider.GetService(selected.SiblingServiceType).ShouldNotBeNull();
        provider.GetService(unselected.ServiceType).ShouldBeNull();
        provider.GetRequiredService<IBingModuleContainer>().Modules
            .Select(module => module.ModuleType)
            .ShouldNotContain(selected.SiblingModuleType);

        var selectedOptions = provider.GetRequiredService(
            typeof(IOptions<>).MakeGenericType(selected.OptionsType));
        selected.OptionsValueProperty.GetValue(selectedOptions.GetType().GetProperty("Value")!.GetValue(selectedOptions))
            .ShouldBe("selected");
        provider.GetService(typeof(IConfigureOptions<>).MakeGenericType(unselected.OptionsType)).ShouldBeNull();
        provider.GetService(typeof(IOptionsChangeTokenSource<>).MakeGenericType(unselected.OptionsType)).ShouldBeNull();
    }

    /// <summary>
    /// 模块扫描测试的启动模块。
    /// </summary>
    public sealed class ScanStartupModule : BingModule { }

    /// <summary>
    /// 保存动态程序集扫描测试对象的描述信息。
    /// </summary>
    private sealed class DynamicScanAssembly
    {
        /// <summary>
        /// 初始化动态程序集描述信息。
        /// </summary>
        /// <param name="moduleType">动态模块类型。</param>
        /// <param name="siblingModuleType">同程序集中的未选模块类型。</param>
        /// <param name="optionsType">动态选项类型。</param>
        /// <param name="serviceType">动态服务类型。</param>
        /// <param name="siblingServiceType">同程序集中的未选服务类型。</param>
        /// <param name="optionsValueProperty">选项值属性。</param>
        /// <param name="optionsSection">选项配置节名称。</param>
        private DynamicScanAssembly(Type moduleType, Type siblingModuleType, Type optionsType, Type serviceType,
            Type siblingServiceType, PropertyInfo optionsValueProperty, string optionsSection)
        {
            ModuleType = moduleType;
            SiblingModuleType = siblingModuleType;
            OptionsType = optionsType;
            ServiceType = serviceType;
            SiblingServiceType = siblingServiceType;
            OptionsValueProperty = optionsValueProperty;
            OptionsSection = optionsSection;
        }

        /// <summary>
        /// 获取动态模块类型。
        /// </summary>
        public Type ModuleType { get; }
        /// <summary>
        /// 获取同程序集中的未选模块类型。
        /// </summary>
        public Type SiblingModuleType { get; }
        /// <summary>
        /// 获取动态选项类型。
        /// </summary>
        public Type OptionsType { get; }
        /// <summary>
        /// 获取动态服务类型。
        /// </summary>
        public Type ServiceType { get; }
        /// <summary>
        /// 获取同程序集中的未选服务类型。
        /// </summary>
        public Type SiblingServiceType { get; }
        /// <summary>
        /// 获取选项值属性。
        /// </summary>
        public PropertyInfo OptionsValueProperty { get; }
        /// <summary>
        /// 获取选项配置节名称。
        /// </summary>
        public string OptionsSection { get; }

        /// <summary>
        /// 创建包含测试模块、服务和选项的动态程序集。
        /// </summary>
        /// <param name="suffix">动态程序集名称后缀。</param>
        /// <param name="markerType">标记动态模块的类型；为空时不附加标记。</param>
        /// <param name="inheritDeclaration">是否从基类继承程序集声明。</param>
        /// <returns>动态程序集描述信息。</returns>
        public static DynamicScanAssembly Create(string suffix, Type markerType = null,
            bool inheritDeclaration = false)
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("Bing.Core.Tests.DynamicScan." + suffix + "." + Guid.NewGuid().ToString("N")),
                AssemblyBuilderAccess.Run);
            var module = assembly.DefineDynamicModule("Main");

            var moduleBase = typeof(BingModule);
            if (markerType != null && inheritDeclaration)
            {
                var baseBuilder = module.DefineType("AttributedModuleBase", TypeAttributes.Public | TypeAttributes.Class,
                    typeof(BingModule));
                baseBuilder.DefineDefaultConstructor(MethodAttributes.Public);
                var moduleAssemblyAttributeConstructor = typeof(BingModuleAssemblyAttribute)
                    .GetConstructor(new[] { typeof(Type) })!;
                baseBuilder.SetCustomAttribute(new CustomAttributeBuilder(moduleAssemblyAttributeConstructor,
                    new object[] { markerType }));
                moduleBase = baseBuilder.CreateType()!;
            }
            var moduleTypeBuilder = module.DefineType("DynamicModule", TypeAttributes.Public | TypeAttributes.Class,
                moduleBase);
            if (markerType != null && !inheritDeclaration)
            {
                var moduleAssemblyAttributeConstructor = typeof(BingModuleAssemblyAttribute)
                    .GetConstructor(new[] { typeof(Type) })!;
                moduleTypeBuilder.SetCustomAttribute(new CustomAttributeBuilder(moduleAssemblyAttributeConstructor,
                    new object[] { markerType }));
            }
            moduleTypeBuilder.DefineDefaultConstructor(MethodAttributes.Public);
            var moduleType = moduleTypeBuilder.CreateType()!;
            var siblingModuleBuilder = module.DefineType("SiblingModule", TypeAttributes.Public | TypeAttributes.Class,
                typeof(BingModule));
            siblingModuleBuilder.DefineDefaultConstructor(MethodAttributes.Public);
            var siblingModuleType = siblingModuleBuilder.CreateType()!;

            var optionsSection = "Dynamic" + suffix + "Options";
            var optionsBuilder = module.DefineType(optionsSection, TypeAttributes.Public | TypeAttributes.Class);
            optionsBuilder.DefineDefaultConstructor(MethodAttributes.Public);
            var valueField = optionsBuilder.DefineField("_value", typeof(string), FieldAttributes.Private);
            var valueBuilder = optionsBuilder.DefineProperty("Value", PropertyAttributes.None, typeof(string), null);
            var getter = optionsBuilder.DefineMethod("get_Value", MethodAttributes.Public | MethodAttributes.SpecialName |
                MethodAttributes.HideBySig, typeof(string), Type.EmptyTypes);
            var getterIl = getter.GetILGenerator();
            getterIl.Emit(OpCodes.Ldarg_0);
            getterIl.Emit(OpCodes.Ldfld, valueField);
            getterIl.Emit(OpCodes.Ret);
            valueBuilder.SetGetMethod(getter);
            var setter = optionsBuilder.DefineMethod("set_Value", MethodAttributes.Public | MethodAttributes.SpecialName |
                MethodAttributes.HideBySig, typeof(void), new[] { typeof(string) });
            var setterIl = setter.GetILGenerator();
            setterIl.Emit(OpCodes.Ldarg_0);
            setterIl.Emit(OpCodes.Ldarg_1);
            setterIl.Emit(OpCodes.Stfld, valueField);
            setterIl.Emit(OpCodes.Ret);
            valueBuilder.SetSetMethod(setter);
            var attributeConstructor = typeof(OptionsTypeAttribute).GetConstructor(new[] { typeof(string) })!;
            optionsBuilder.SetCustomAttribute(new CustomAttributeBuilder(attributeConstructor, new object[] { optionsSection }));
            var optionsType = optionsBuilder.CreateType()!;

            var serviceBuilder = module.DefineType("DynamicService", TypeAttributes.Public | TypeAttributes.Class);
            var serviceType = module.DefineType("IDynamicService", TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract).CreateType()!;
            serviceBuilder.AddInterfaceImplementation(serviceType);
            serviceBuilder.DefineDefaultConstructor(MethodAttributes.Public);
            var dependencyConstructor = typeof(Bing.DependencyInjection.DependencyAttribute)
                .GetConstructor(new[] { typeof(ServiceLifetime) })!;
            serviceBuilder.SetCustomAttribute(new CustomAttributeBuilder(dependencyConstructor,
                new object[] { ServiceLifetime.Singleton }));
            serviceBuilder.CreateType();

            var siblingServiceBuilder = module.DefineType("SiblingService", TypeAttributes.Public | TypeAttributes.Class);
            var siblingServiceType = module.DefineType("ISiblingService", TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract)
                .CreateType()!;
            siblingServiceBuilder.AddInterfaceImplementation(siblingServiceType);
            siblingServiceBuilder.DefineDefaultConstructor(MethodAttributes.Public);
            siblingServiceBuilder.SetCustomAttribute(new CustomAttributeBuilder(dependencyConstructor,
                new object[] { ServiceLifetime.Singleton }));
            siblingServiceBuilder.CreateType();

            return new DynamicScanAssembly(moduleType, siblingModuleType, optionsType, serviceType, siblingServiceType,
                optionsType.GetProperty("Value")!, optionsSection);
        }
    }
}
