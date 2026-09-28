using System.Text;
using System.Text.Json;
using Bing.AspNetCore;
using Bing.Core.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// 启动期插件在 Web 管道和 Generic Host 中的生命周期测试。
/// </summary>
[Collection("Bing插件 Web 生命周期")]
public sealed class PluginWebLifecycleTest
{
    /// <summary>
    /// 验证 Web 管道会初始化插件异步模块并传递实际应用构建器。
    /// </summary>
    [Fact]
    public async Task UseBingAsync_ShouldInitializeWebPlugin()
    {
        var directory = CreatePluginDirectory();
        try
        {
            PluginWebState.Reset();
            var services = CreateServices(directory);
            using var provider = services.BuildServiceProvider();
            var app = new ApplicationBuilder(provider);

            await app.UseBingAsync();

            PluginWebState.InitializeCount.ShouldBe(1);
            PluginWebState.HostContext.ShouldBeSameAs(app);
            await provider.ShutdownBingAsync();
            PluginWebState.ShutdownCount.ShouldBe(1);
            PluginWebState.HostContext = null;
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// 验证 Generic Host 停止流程会关闭插件异步模块。
    /// </summary>
    [Fact]
    public async Task HostStop_ShouldShutdownWebPlugin()
    {
        var directory = CreatePluginDirectory();
        try
        {
            PluginWebState.Reset();
            var host = new HostBuilder()
                .ConfigureServices(services => services.AddBingApplication<PluginWebHostModule>(options =>
                {
                    options.AutoRegisterServices = false;
                    options.PluginSources.AddDirectory(directory);
                }))
                .ConfigureWebHostDefaults(webHost => webHost
                    .UseTestServer()
                    .Configure(app => app.UseBingAsync().GetAwaiter().GetResult()))
                .Build();

            try
            {
                await host.StartAsync();
                await host.StopAsync();
                PluginWebState.InitializeCount.ShouldBe(1);
                PluginWebState.ShutdownCount.ShouldBe(1);
            }
            finally
            {
                host.Dispose();
                PluginWebState.HostContext = null;
            }
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// 创建以当前测试程序集为入口的可信插件目录。
    /// </summary>
    /// <returns>插件目录路径。</returns>
    private static string CreatePluginDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "bing-web-plugin-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var entryAssembly = typeof(PluginWebLifecycleModule).Assembly.Location;
        var entryName = Path.GetFileName(entryAssembly);
        File.Copy(entryAssembly, Path.Combine(directory, entryName));
        var manifest = new
        {
            id = "Contoso.WebLifecycle",
            version = "1.0.0",
            entryAssembly = entryName,
            startupModules = new[] { typeof(PluginWebLifecycleModule).FullName },
            dependencies = Array.Empty<object>()
        };
        File.WriteAllText(Path.Combine(directory, "bing-plugin.json"),
            JsonSerializer.Serialize(manifest), new UTF8Encoding(false));
        return directory;
    }

    /// <summary>
    /// 创建插件测试用的服务集合。
    /// </summary>
    /// <param name="directory">插件目录。</param>
    /// <returns>已注册插件的服务集合。</returns>
    private static IServiceCollection CreateServices(string directory)
    {
        var services = new ServiceCollection();
        services.AddBingApplication<PluginWebHostModule>(options =>
        {
            options.AutoRegisterServices = false;
            options.PluginSources.AddDirectory(directory);
        });
        return services;
    }

    /// <summary>
    /// 删除测试插件目录。
    /// </summary>
    /// <param name="directory">插件目录。</param>
    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);
    }
}

/// <summary>
/// Web 插件测试的宿主根模块。
/// </summary>
public sealed class PluginWebHostModule : BingModule { }

/// <summary>
/// Web 插件测试的异步生命周期模块。
/// </summary>
public sealed class PluginWebLifecycleModule : BingModule, IBingAsyncModuleInitializer, IBingAsyncModuleShutdown
{
    /// <inheritdoc />
    public Task InitializeAsync(BingModuleInitializationContext context, CancellationToken cancellationToken)
    {
        PluginWebState.InitializeCount++;
        PluginWebState.HostContext = context.HostContext;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ShutdownAsync(BingModuleShutdownContext context, CancellationToken cancellationToken)
    {
        PluginWebState.ShutdownCount++;
        return Task.CompletedTask;
    }
}

/// <summary>
/// 保存 Web 插件测试状态。
/// </summary>
public static class PluginWebState
{
    /// <summary>
    /// 获取或设置初始化次数。
    /// </summary>
    public static int InitializeCount { get; set; }

    /// <summary>
    /// 获取或设置关闭次数。
    /// </summary>
    public static int ShutdownCount { get; set; }

    /// <summary>
    /// 获取或设置宿主上下文。
    /// </summary>
    public static object HostContext { get; set; }

    /// <summary>
    /// 重置测试状态。
    /// </summary>
    public static void Reset()
    {
        InitializeCount = 0;
        ShutdownCount = 0;
        HostContext = null;
    }
}

/// <summary>
/// 串行执行 Web 插件生命周期测试。
/// </summary>
[CollectionDefinition("Bing插件 Web 生命周期", DisableParallelization = true)]
public sealed class PluginWebLifecycleCollectionDefinition { }
