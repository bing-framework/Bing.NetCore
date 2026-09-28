using Bing;
using Bing.Configuration;
using Bing.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;

namespace Bing.Core.Tests.Modularity;

/// <summary>
/// 选项自动注册的集合隔离、调用顺序和配置变更测试。
/// </summary>
public class OptionsTypeRegistrationTest
{
    /// <summary>
    /// 验证不同服务集合使用彼此隔离的选项配置。
    /// </summary>
    [Fact]
    public void AddOptionsType_ShouldIsolateServiceCollections()
    {
        var first = new ServiceCollection();
        var second = new ServiceCollection();
        first.AddOptionsType(Configuration("first"));
        second.AddOptionsType(Configuration("second"));
        BingLoader.RegisterTypes(first);
        BingLoader.RegisterTypes(second);

        using var firstProvider = first.BuildServiceProvider();
        using var secondProvider = second.BuildServiceProvider();
        firstProvider.GetRequiredService<IOptions<OptionsRegistrationSample>>().Value.Value.ShouldBe("first");
        secondProvider.GetRequiredService<IOptions<OptionsRegistrationSample>>().Value.Value.ShouldBe("second");
    }

    /// <summary>
    /// 验证在类型扫描后登记选项仍能完成绑定。
    /// </summary>
    [Fact]
    public void AddOptionsType_AfterRegisterTypes_ShouldStillBindOptions()
    {
        var services = new ServiceCollection();
        BingLoader.RegisterTypes(services);
        services.AddOptionsType(Configuration("late"));

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<OptionsRegistrationSample>>().Value.Value.ShouldBe("late");
    }

    /// <summary>
    /// 验证重复登记保留首次配置源。
    /// </summary>
    [Fact]
    public void AddOptionsType_RepeatedRegistration_ShouldKeepFirstConfiguration()
    {
        var services = new ServiceCollection();
        services.AddOptionsType(Configuration("first"));
        services.AddOptionsType(Configuration("second"));
        BingLoader.RegisterTypes(services);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<OptionsRegistrationSample>>().Value.Value.ShouldBe("first");
    }

    /// <summary>
    /// 验证公开类型注册事件仍按原契约触发。
    /// </summary>
    [Fact]
    public void AddOptionsType_ShouldPreservePublicRegisterTypeEvent()
    {
        var count = 0;
        Action<Type> handler = _ => count++;
        BingLoader.RegisterType += handler;
        try
        {
            BingLoader.RegisterTypes(new ServiceCollection());
            count.ShouldBeGreaterThan(0);
        }
        finally
        {
            BingLoader.RegisterType -= handler;
        }
    }

    /// <summary>
    /// 验证选项注册器不会长期保留服务集合和配置。
    /// </summary>
    [Fact]
    public void AddOptionsType_ShouldNotKeepServiceCollectionAlive()
    {
        var references = CreateWeakReferences();
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        references.Services.TryGetTarget(out _).ShouldBeFalse();
        references.Configuration.TryGetTarget(out _).ShouldBeFalse();
        references.Monitor.TryGetTarget(out _).ShouldBeFalse();
    }

    /// <summary>
    /// 验证配置源变更仍能更新选项监视值。
    /// </summary>
    [Fact]
    public void AddOptionsType_ShouldKeepConfigurationReloadBehavior()
    {
        var values = new Dictionary<string, string> { ["OptionsRegistrationSample:Value"] = "before" };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection().AddOptionsType(configuration);
        BingLoader.RegisterTypes(services);
        using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<OptionsRegistrationSample>>();
        monitor.CurrentValue.Value.ShouldBe("before");

        configuration["OptionsRegistrationSample:Value"] = "after";
        configuration.Reload();
        monitor.CurrentValue.Value.ShouldBe("after");
    }

    /// <summary>
    /// 验证扫描另一个服务集合不会修改已有描述符。
    /// </summary>
    [Fact]
    public void RegisterTypes_OnAnotherCollection_ShouldNotChangeExistingDescriptors()
    {
        var first = new ServiceCollection().AddOptionsType(Configuration("first"));
        BingLoader.RegisterTypes(first);
        var firstCount = first.Count;

        var second = new ServiceCollection().AddOptionsType(Configuration("second"));
        BingLoader.RegisterTypes(second);

        first.Count.ShouldBe(firstCount);
    }

    /// <summary>
    /// 验证自定义程序集查找器限定选项扫描范围。
    /// </summary>
    [Fact]
    public void RegisterTypes_UsesCustomAssemblyFinder()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAllAssemblyFinder>(new FixedAssemblyFinder(typeof(OptionsRegistrationSample).Assembly));
        services.AddOptionsType(Configuration("selected"));

        BingLoader.RegisterTypes(services);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<OptionsRegistrationSample>>().Value.Value.ShouldBe("selected");
    }

    /// <summary>
    /// 创建包含示例选项值的配置。
    /// </summary>
    /// <param name="value">配置值。</param>
    /// <returns>内存配置对象。</returns>
    private static IConfiguration Configuration(string value) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["OptionsRegistrationSample:Value"] = value
            })
            .Build();

    /// <summary>
    /// 创建用于弱引用回收验证的应用对象。
    /// </summary>
    /// <returns>服务集合、配置和选项监视器的弱引用。</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference<IServiceCollection> Services, WeakReference<IConfiguration> Configuration,
        WeakReference<IOptionsMonitor<OptionsRegistrationSample>> Monitor) CreateWeakReferences()
    {
        var services = new ServiceCollection();
        var configuration = Configuration("collectable");
        services.AddOptionsType(configuration);
        services.AddBingApplication<OptionsLifetimeStartupModule>();
        var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<OptionsRegistrationSample>>();
        monitor.CurrentValue.Value.ShouldBe("collectable");
        provider.ShutdownBing();
        provider.Dispose();
        return (new WeakReference<IServiceCollection>(services), new WeakReference<IConfiguration>(configuration),
            new WeakReference<IOptionsMonitor<OptionsRegistrationSample>>(monitor));
    }

    /// <summary>
    /// 返回固定程序集集合的测试查找器。
    /// </summary>
    private sealed class FixedAssemblyFinder : IAllAssemblyFinder
    {
        /// <summary>
        /// 固定程序集集合。
        /// </summary>
        private readonly Assembly[] _assemblies;

        /// <summary>
        /// 初始化固定程序集查找器。
        /// </summary>
        /// <param name="assemblies">待返回的程序集。</param>
        public FixedAssemblyFinder(params Assembly[] assemblies) => _assemblies = assemblies;

        /// <inheritdoc />
        public Assembly[] FindAll(bool fromCache = false) => _assemblies;

        /// <inheritdoc />
        public Assembly[] Find(Func<Assembly, bool> predicate, bool fromCache = false) =>
            _assemblies.Where(predicate).ToArray();
    }
}

/// <summary>
/// 选项生命周期测试的启动模块。
/// </summary>
internal sealed class OptionsLifetimeStartupModule : Bing.Core.Modularity.BingModule { }

/// <summary>
/// 选项自动注册测试类型。
/// </summary>
[OptionsType("OptionsRegistrationSample")]
public class OptionsRegistrationSample
{
    /// <summary>
    /// 获取或设置测试选项值。
    /// </summary>
    public string Value { get; set; }
}
