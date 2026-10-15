using AspectCore.Extensions.Hosting;
using Bing.Aspects;
using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

try
{
    var mode = args.Single();
    if (mode.StartsWith("plain-", StringComparison.Ordinal) || mode == "bing-lazy")
    {
        RunPlainMicrosoftDependencyInjectionProbe(mode);
        return;
    }

    using var host = new HostBuilder()
        .UseServiceContext()
        .ConfigureServices(services =>
        {
            services.EnableAutowired();
            if (mode == "property") services.AddTransient<PropertyCycle>();
            if (mode == "mixed")
            {
                services.AddTransient<MixedConstructor>();
                services.AddTransient<MixedProperty>();
            }
            if (mode == "factory")
                services.AddTransient<FactoryCycle>(provider => provider.GetRequiredService<FactoryCycle>());
            if (mode == "lazy")
            {
                services.AddTransient<Lazy<LazyValueCycle>>(provider =>
                    new Lazy<LazyValueCycle>(() => provider.GetRequiredService<LazyValueCycle>()));
                services.AddTransient<LazyValueCycle>();
            }
        })
        .Build();

    if (mode == "property")
        Console.WriteLine(host.Services.GetRequiredService<PropertyCycle>().Next == null
            ? "resolved:null" : "resolved:value");
    else if (mode == "mixed")
        Console.WriteLine(host.Services.GetRequiredService<MixedConstructor>().Dependency == null
            ? "resolved:null" : "resolved:value");
    else if (mode == "factory")
        host.Services.GetRequiredService<FactoryCycle>();
    else if (mode == "lazy")
        host.Services.GetRequiredService<LazyValueCycle>();
    else
        throw new ArgumentException("Unknown cycle probe mode.", nameof(args));
}
catch (Exception exception)
{
    Console.Error.WriteLine($"{exception.GetType().Name}: {exception.Message}");
    Environment.ExitCode = 1;
}

/// <summary>
/// 在不启用 AspectCore 宿主适配的情况下运行 Microsoft DI 循环探针。
/// </summary>
static void RunPlainMicrosoftDependencyInjectionProbe(string mode)
{
    var services = new ServiceCollection();
    switch (mode)
    {
        case "plain-factory":
            var depth = 0;
            services.AddTransient<PlainFactoryCycle>(provider =>
            {
                if (++depth == 64)
                    throw new InvalidOperationException("bounded Microsoft DI factory recursion");
                return provider.GetRequiredService<PlainFactoryCycle>();
            });
            break;
        case "plain-lazy":
            var holder = new PlainLazyHolder();
            services.AddSingleton<Lazy<PlainLazyCycle>>(_ => holder.Value);
            services.AddTransient<PlainLazyCycle>();
            using (var provider = services.BuildServiceProvider())
            {
                holder.Value = new Lazy<PlainLazyCycle>(() =>
                    provider.GetRequiredService<PlainLazyCycle>());
                provider.GetRequiredService<PlainLazyCycle>();
            }
            return;
        case "bing-lazy":
            new DependencyModule().AddServices(services);
            services.AddTransient<BingLazyCycle>();
            using (var provider = services.BuildServiceProvider())
                provider.GetRequiredService<BingLazyCycle>();
            return;
        default:
            throw new ArgumentException("Unknown plain cycle probe mode.", nameof(mode));
    }

    using var root = services.BuildServiceProvider();
    root.GetRequiredService<PlainFactoryCycle>();
}

/// <summary>
/// 自引用属性注入探针。
/// </summary>
public sealed class PropertyCycle
{
    /// <summary>
    /// 获取或设置同类型属性。
    /// </summary>
    [Autowired]
    public PropertyCycle Next { get; set; }
}

/// <summary>
/// 构造函数指向属性节点。
/// </summary>
public sealed class MixedConstructor
{
    /// <summary>
    /// 初始化一个 <see cref="MixedConstructor" /> 类型的实例。
    /// </summary>
    public MixedConstructor(MixedProperty dependency) => Dependency = dependency;

    /// <summary>
    /// 获取属性节点。
    /// </summary>
    public MixedProperty Dependency { get; }
}

/// <summary>
/// 属性指向构造函数节点。
/// </summary>
public sealed class MixedProperty
{
    /// <summary>
    /// 获取或设置构造函数节点。
    /// </summary>
    [Autowired]
    public MixedConstructor Dependency { get; set; }
}

/// <summary>
/// 通过工厂再次解析自身的循环探针。
/// </summary>
public sealed class FactoryCycle
{
}

/// <summary>
/// 在构造期间访问延迟依赖，形成递归解析。
/// </summary>
public sealed class LazyValueCycle
{
    /// <summary>
    /// 初始化一个 <see cref="LazyValueCycle" /> 类型的实例。
    /// </summary>
    public LazyValueCycle(Lazy<LazyValueCycle> dependency) => _ = dependency.Value;
}

/// <summary>
/// 通过普通 Microsoft DI 工厂再次解析自身的循环探针。
/// </summary>
public sealed class PlainFactoryCycle
{
}

/// <summary>
/// 在普通 Microsoft DI 中访问同一个延迟实例的循环探针。
/// </summary>
public sealed class PlainLazyCycle
{
    /// <summary>
    /// 初始化一个 <see cref="PlainLazyCycle" /> 类型的实例。
    /// </summary>
    public PlainLazyCycle(Lazy<PlainLazyCycle> dependency) => _ = dependency.Value;
}

/// <summary>
/// 保存普通 Microsoft DI 的共享延迟实例。
/// </summary>
public sealed class PlainLazyHolder
{
    /// <summary>
    /// 共享延迟实例。
    /// </summary>
    public Lazy<PlainLazyCycle> Value { get; set; }
}

/// <summary>
/// 通过 Bing 内置 Lazier&lt;T&gt; 在构造期间访问自身的循环探针。
/// </summary>
public sealed class BingLazyCycle
{
    /// <summary>
    /// 记录受控递归深度。
    /// </summary>
    private static int _depth;

    /// <summary>
    /// 初始化一个 <see cref="BingLazyCycle" /> 类型的实例。
    /// </summary>
    public BingLazyCycle(Lazy<BingLazyCycle> dependency)
    {
        if (Interlocked.Increment(ref _depth) == 64)
            throw new InvalidOperationException("bounded Bing Lazier recursion");
        _ = dependency.Value;
    }
}
