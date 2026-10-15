using AspectCore.Extensions.Hosting;
using AspectCore.DependencyInjection;
using Bing.Aspects;
using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// 独立审查探针，不修改生产代码或既有测试。
using (var host = new HostBuilder().UseServiceContext().ConfigureServices(services =>
{
    services.EnableAutowired();
    services.AddTransient<Dependency>();
    services.AddTransient<MixedConsumer>();
    services.AddTransient<LeafConsumer>();
}).Build())
{
    var mixed = host.Services.GetRequiredService<MixedConsumer>();
    Console.WriteLine($"MIXED bingSets={mixed.BingSets} nativeSets={mixed.NativeSets}");
    var leaf = host.Services.GetRequiredService<LeafConsumer>();
    Console.WriteLine($"MIDDLE_OVERRIDE injected={leaf.Service != null}");
}

using (var host = Bing.Samples.WebApi.Program.CreateHostBuilder(new[] { "--urls", "http://127.0.0.1:18743" }).Build())
{
    await host.StartAsync();
    using var client = new HttpClient();
    using var response = await client.GetAsync("http://127.0.0.1:18743/");
    Console.WriteLine($"SAMPLE rootStatus={(int)response.StatusCode}");
    using var scope = host.Services.CreateScope();
    Console.WriteLine($"SAMPLE factory={scope.ServiceProvider.GetService<IPropertyInjectorFactory>()?.GetType().FullName}");
    await host.StopAsync();
    Console.WriteLine("SAMPLE stopped=true");
}

/// <summary>
/// 属性注入探针依赖。
/// </summary>
public sealed class Dependency { }

/// <summary>
/// 统计两种属性注入标记的执行次数。
/// </summary>
public sealed class MixedConsumer
{
    /// <summary>
    /// Bing 特性属性的注入次数。
    /// </summary>
    public int BingSets { get; private set; }

    /// <summary>
    /// AspectCore 原生特性属性的注入次数。
    /// </summary>
    public int NativeSets { get; private set; }

    /// <summary>
    /// 由 Bing 特性标记的依赖属性。
    /// </summary>
    [Autowired] public Dependency Bing { get => null; set => BingSets++; }

    /// <summary>
    /// 由 AspectCore 原生特性标记的依赖属性。
    /// </summary>
    [FromServiceContext] public Dependency Native { get => null; set => NativeSets++; }
}

/// <summary>
/// 定义虚拟依赖属性的探针基类。
/// </summary>
public class RootConsumer
{
    /// <summary>
    /// 属性注入依赖。
    /// </summary>
    public virtual Dependency Service { get; set; }
}

/// <summary>
/// 在中间继承层声明属性注入标记。
/// </summary>
public class MiddleConsumer : RootConsumer
{
    /// <inheritdoc />
    [Autowired]
    public override Dependency Service { get; set; }
}

/// <summary>
/// 覆写中间层属性以验证继承注入行为。
/// </summary>
public sealed class LeafConsumer : MiddleConsumer
{
    /// <inheritdoc />
    public override Dependency Service { get; set; }
}
