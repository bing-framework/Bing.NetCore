using System.Reflection;
using System.Runtime.Loader;
using AspectCore.Configuration;
using AspectCore.DynamicProxy;
using AspectCore.DynamicProxy.Parameters;
using AspectCore.Extensions.DependencyInjection;
using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Aop.AspectCore;

/// <summary>
/// 验证真实插件程序集经过 AOP 解析后可以释放加载上下文。
/// </summary>
public sealed class IgnoreAspectPluginAssemblyResourceTest
{
    /// <summary>
    /// 验证忽略拦截的插件服务可以调用且不会保留可收集加载上下文。
    /// </summary>
    [Fact]
    public void ResolveIgnoredPluginService_ShouldInvokeAndReleaseAssemblyLoadContext()
    {
        var result = LoadAndInvokePluginService();

        for (var attempt = 0; attempt < 16 && result.Context.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.Equal("plugin-ready", result.Status);
        Assert.Equal(0, result.InterceptorCount);
        Assert.False(result.Context.IsAlive);
    }

    /// <summary>
    /// 在无局部类型引用的范围内加载、解析并释放插件服务。
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (string Status, int InterceptorCount, WeakReference Context) LoadAndInvokePluginService()
    {
        var fixturePath = GetFixturePath();
        var loadContext = new PluginAssemblyLoadContext(fixturePath);
        var contextReference = new WeakReference(loadContext);
        var assembly = loadContext.LoadFromAssemblyPath(fixturePath);
        var serviceType = assembly.GetType("Bing.Core.PluginFixtures.IIgnoredPluginService", true);
        var implementationType = assembly.GetType("Bing.Core.PluginFixtures.IgnoredPluginService", true);
        var services = new ServiceCollection();
        services.AddSingleton(serviceType, implementationType);
        ProbeInterceptor.Reset();
        services.EnableAop(configuration => configuration.Interceptors.AddTyped<ProbeInterceptor>());

        using (var provider = services.BuildBingServiceProvider())
        {
            var service = provider.GetRequiredService(serviceType);
            var status = (string)serviceType.GetMethod("GetStatus").Invoke(service, null);
            services.Clear();
            loadContext.Unload();
            assembly = null;
            serviceType = null;
            implementationType = null;
            return (status, ProbeInterceptor.Count, contextReference);
        }
    }

    /// <summary>
    /// 获取插件夹具程序集路径。
    /// </summary>
    /// <returns>当前目标框架下的夹具程序集路径。</returns>
    private static string GetFixturePath()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            ".."));
        var targetFramework = AppContext.BaseDirectory.Contains("net6.0", StringComparison.OrdinalIgnoreCase)
            ? "net6.0"
            : "net8.0";
        var fixturePath = Path.Combine(repositoryRoot, "output", "debug", targetFramework, "Bing.Core.PluginFixtures.dll");
        return File.Exists(fixturePath)
            ? fixturePath
            : Path.Combine(repositoryRoot, "framework", "tests", "Bing.Core.Tests", "PluginFixtures",
                "Bing.Core.PluginFixtures", "bin", "Debug", targetFramework, "Bing.Core.PluginFixtures.dll");
    }

    /// <summary>
    /// 只共享框架程序集的可收集插件加载上下文。
    /// </summary>
    private sealed class PluginAssemblyLoadContext : AssemblyLoadContext
    {
        /// <summary>
        /// 插件程序集所在目录。
        /// </summary>
        private readonly string _pluginDirectory;

        /// <summary>
        /// 初始化一个 <see cref="PluginAssemblyLoadContext" /> 类型的实例。
        /// </summary>
        /// <param name="pluginPath">插件程序集路径。</param>
        public PluginAssemblyLoadContext(string pluginPath)
            : base("Bing.Aop.AspectCore.PluginTest", isCollectible: true)
        {
            _pluginDirectory = Path.GetDirectoryName(pluginPath);
        }

        /// <inheritdoc />
        protected override Assembly Load(AssemblyName assemblyName)
        {
            var shared = Default.Assemblies.FirstOrDefault(item =>
                string.Equals(item.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));
            if (shared != null)
                return shared;

            var candidate = Path.Combine(_pluginDirectory, $"{assemblyName.Name}.dll");
            return File.Exists(candidate) ? LoadFromAssemblyPath(candidate) : null;
        }
    }

    /// <summary>
    /// 记录插件服务是否被普通拦截器处理。
    /// </summary>
    private sealed class ProbeInterceptor : AbstractInterceptorAttribute
    {
        /// <summary>
        /// 记录拦截器调用次数。
        /// </summary>
        public static int Count;

        /// <summary>
        /// 重置拦截器调用次数。
        /// </summary>
        public static void Reset() => Interlocked.Exchange(ref Count, 0);

        /// <inheritdoc />
        public override async Task Invoke(AspectContext context, AspectDelegate next)
        {
            Interlocked.Increment(ref Count);
            await next(context);
        }
    }
}
