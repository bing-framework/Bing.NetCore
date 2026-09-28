using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Tests.Modularity;

/// <summary>
/// 启动期插件描述符和来源的资源释放回归测试。
/// </summary>
[Collection("Bing启动期插件")]
public sealed class PluginResourceRegressionTest
{
    /// <summary>
    /// 顶部模块完整名称。
    /// </summary>
    private const string TopModuleName = "Bing.Core.PluginFixtures.FixtureTopModule";

    /// <summary>
    /// 验证应用关闭后插件容器不再保留描述符集合。
    /// </summary>
    [Fact]
    public void ShutdownBing_ShouldClearPluginDescriptors()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Release", "1.0.0", new[] { TopModuleName });
        var services = new ServiceCollection();
        services.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddDirectory(directory));

        using var provider = services.BuildBingServiceProvider();
        var container = provider.GetRequiredService<IBingPluginContainer>();
        container.Plugins.ShouldHaveSingleItem();

        provider.ShutdownBing();

        container.Plugins.ShouldBeEmpty();
    }

    /// <summary>
    /// 验证失败注册后插件来源不会被框架或静态回调保留。
    /// </summary>
    [Fact]
    public void FailedRegistration_ShouldNotRetainPluginSource()
    {
        var weakSource = CreateFailedRegistrationSource();

        for (var i = 0; i < 3 && weakSource.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        weakSource.IsAlive.ShouldBeFalse();
    }

    /// <summary>
    /// 验证插件成功加载后默认上下文的临时解析回调已退订。
    /// </summary>
    [Fact]
    public void SuccessfulRegistration_ShouldUnsubscribeAssemblyResolver()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.ResolverSuccess", "1.0.0", new[] { TopModuleName });
        var before = GetDefaultResolverCount();
        var services = new ServiceCollection();

        services.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddDirectory(directory));

        GetDefaultResolverCount().ShouldBe(before);
    }

    /// <summary>
    /// 验证插件加载异常后默认上下文的临时解析回调也已退订。
    /// </summary>
    [Fact]
    public void FailedRegistration_ShouldUnsubscribeAssemblyResolver()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.ResolverFailure", "1.0.0",
            new[] { "Bing.Core.PluginFixtures.MissingModule" });
        var before = GetDefaultResolverCount();

        Should.Throw<InvalidOperationException>(() => Register(directory));

        GetDefaultResolverCount().ShouldBe(before);
    }

    /// <summary>
    /// 验证默认加载上下文中的同名不同完整身份程序集不会被错误复用。
    /// </summary>
    [Fact]
    public void LoadedAssemblyIdentityMismatch_ShouldFailResolver()
    {
        var loaderType = typeof(BingApplicationOptions).Assembly
            .GetType("Bing.Core.Modularity.BingPluginLoader", true);
        var resolverType = loaderType.GetNestedType("PluginAssemblyResolver", BindingFlags.NonPublic);
        var candidateType = loaderType.GetNestedType("AssemblyCandidate", BindingFlags.NonPublic);
        var dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(string), candidateType);
        var candidates = Activator.CreateInstance(dictionaryType);
        var resolver = Activator.CreateInstance(resolverType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, args: new[] { candidates }, culture: null);
        var resolve = resolverType.GetMethod("Resolve", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var loaded = typeof(BingApplicationOptions).Assembly.GetName();
        var requested = new AssemblyName(loaded.FullName)
        {
            Version = new Version(loaded.Version.Major + 1, 0, 0, 0)
        };

        var invocation = Should.Throw<TargetInvocationException>(() => resolve.Invoke(
            resolver, new object[] { AssemblyLoadContext.Default, requested }));
        var error = invocation.InnerException.ShouldBeOfType<FileLoadException>();
        error.Data["Bing.PluginAssembly"].ShouldBe(requested.FullName);
    }

    /// <summary>
    /// 验证失败注册不会保留服务集合、选项、来源或原始异常上下文。
    /// </summary>
    [Fact]
    public void FailedRegistration_ShouldReleaseApplicationGraph()
    {
        var references = CreateFailedRegistrationReferences();

        for (var i = 0; i < 3 && (references.Services.IsAlive || references.Options.IsAlive ||
                                  references.Source.IsAlive || references.Error.IsAlive); i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        references.Services.IsAlive.ShouldBeFalse();
        references.Options.IsAlive.ShouldBeFalse();
        references.Source.IsAlive.ShouldBeFalse();
        references.Error.IsAlive.ShouldBeFalse();
    }

    /// <summary>
    /// 创建失败注册并只返回来源的弱引用，避免调用方局部变量延长其生命周期。
    /// </summary>
    /// <returns>失败注册所用插件来源的弱引用。</returns>
    private static WeakReference CreateFailedRegistrationSource()
    {
        var source = new FixedPluginSource(Path.Combine(Path.GetTempPath(), "bing-plugin-missing", Guid.NewGuid().ToString("N")));
        var weakSource = new WeakReference(source);
        var services = new ServiceCollection();
        Should.Throw<DirectoryNotFoundException>(() => services.AddBingApplication<PluginHostModule>(options =>
            options.PluginSources.Add(source)));
        return weakSource;
    }

    /// <summary>
    /// 创建失败注册并返回应用对象图的弱引用。
    /// </summary>
    /// <returns>服务集合、选项、来源和异常的弱引用。</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Services, WeakReference Options, WeakReference Source, WeakReference Error)
        CreateFailedRegistrationReferences()
    {
        var services = new ServiceCollection();
        WeakReference optionsReference = null;
        WeakReference sourceReference = null;
        WeakReference errorReference = null;
        try
        {
            services.AddBingApplication<PluginHostModule>(options =>
            {
                optionsReference = new WeakReference(options);
                var source = new FixedPluginSource(Path.Combine(Path.GetTempPath(), "bing-plugin-failed", Guid.NewGuid().ToString("N")));
                sourceReference = new WeakReference(source);
                options.PluginSources.Add(source);
                throw new InvalidOperationException("配置委托失败");
            });
        }
        catch (Exception error)
        {
            errorReference = new WeakReference(error);
        }

        return (new WeakReference(services), optionsReference, sourceReference, errorReference);
    }

    /// <summary>
    /// 获取默认加载上下文当前的程序集解析回调数量。
    /// </summary>
    /// <returns>程序集解析回调数量。</returns>
    private static int GetDefaultResolverCount()
    {
        var field = typeof(AssemblyLoadContext).GetField("_resolving", BindingFlags.Instance | BindingFlags.NonPublic);
        var handler = field?.GetValue(AssemblyLoadContext.Default) as Delegate;
        return handler?.GetInvocationList().Length ?? 0;
    }

    /// <summary>
    /// 注册指定插件目录。
    /// </summary>
    /// <param name="directory">插件目录。</param>
    private static void Register(string directory)
    {
        var services = new ServiceCollection();
        services.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddDirectory(directory));
    }
}
