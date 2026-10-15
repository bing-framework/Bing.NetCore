using System.Text;
using System.Text.Json;
using Bing.Core.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// 验证真实 Web 宿主构建后的模块资源释放。
/// </summary>
public sealed class WebModuleResourceReleaseTest
{
    /// <summary>
    /// 显式关闭清空插件容器，重复释放不重复关闭模块。
    /// </summary>
    [Fact]
    public async Task ShutdownBingAsync_AfterWebBuild_ShouldReleaseModuleAndClearPluginsOnce()
    {
        using var directory = new PluginManifestDirectory();
        var state = new ReleaseState();
        var app = await CreateApplicationAsync(directory, state);
        var plugins = app.Services.GetRequiredService<IBingPluginContainer>();
        try
        {
            plugins.Plugins.Count.ShouldBe(1);
            await app.UseBingAsync();

            await app.Services.ShutdownBingAsync();
            await app.Services.ShutdownBingAsync();

            plugins.Plugins.ShouldBeEmpty();
            state.InitializeCount.ShouldBe(1);
            state.ShutdownCount.ShouldBe(1);
            state.DisposeCount.ShouldBe(1);
        }
        finally
        {
            await app.DisposeAsync();
            await app.DisposeAsync();
        }
        state.DisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 宿主停止清空插件容器并释放模块一次。
    /// </summary>
    [Fact]
    public async Task StopAsync_AfterWebBuild_ShouldReleaseModuleAndClearPluginsOnce()
    {
        using var directory = new PluginManifestDirectory();
        var state = new ReleaseState();
        var app = await CreateApplicationAsync(directory, state);
        var plugins = app.Services.GetRequiredService<IBingPluginContainer>();
        try
        {
            plugins.Plugins.Count.ShouldBe(1);
            await app.UseBingAsync();
            await app.StartAsync();

            await app.StopAsync();

            plugins.Plugins.ShouldBeEmpty();
            state.ShutdownCount.ShouldBe(1);
            state.DisposeCount.ShouldBe(1);
        }
        finally
        {
            await app.DisposeAsync();
            await app.DisposeAsync();
        }
        state.DisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 初始化失败保留原始异常并完成资源清理。
    /// </summary>
    /// <param name="failShutdown">是否让关闭钩子抛出清理异常。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UseBingAsync_FailureAfterWebBuild_ShouldPreserveCauseAndFinishCleanup(bool failShutdown)
    {
        using var directory = new PluginManifestDirectory();
        var state = new ReleaseState { FailInitialization = true, FailShutdown = failShutdown };
        var app = await CreateApplicationAsync(directory, state);
        var plugins = app.Services.GetRequiredService<IBingPluginContainer>();
        try
        {
            plugins.Plugins.Count.ShouldBe(1);

            var error = await Should.ThrowAsync<InvalidOperationException>(() => app.UseBingAsync());

            error.ShouldBeSameAs(state.InitializationError);
            error.Data["Bing.ModulePhase"].ShouldBe("Initialize");
            if (failShutdown)
            {
                var cleanup = error.Data["Bing.ModuleCleanupErrors"].ShouldBeOfType<AggregateException>();
                cleanup.Flatten().InnerExceptions.ShouldContain(state.ShutdownError);
            }
            else
            {
                error.Data.Contains("Bing.ModuleCleanupErrors").ShouldBeFalse();
            }
            plugins.Plugins.ShouldBeEmpty();
            state.ShutdownCount.ShouldBe(1);
            state.DisposeCount.ShouldBe(1);
            await app.Services.ShutdownBingAsync();
        }
        finally
        {
            await app.DisposeAsync();
            await app.DisposeAsync();
        }
        state.ShutdownCount.ShouldBe(1);
        state.DisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 创建带插件描述符的真实 Web 应用。
    /// </summary>
    /// <param name="directory">插件目录。</param>
    /// <param name="state">生命周期状态。</param>
    /// <returns>已构建的 Web 应用。</returns>
    private static async Task<WebApplication> CreateApplicationAsync(PluginManifestDirectory directory, ReleaseState state)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = Array.Empty<string>() });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(state);
        await builder.Services.AddBingApplicationAsync<WebResourceReleaseModule>(options =>
        {
            options.AutoRegisterServices = false;
            options.PluginSources.AddDirectory(directory.Path);
        });
        var app = builder.Build();
#if NET8_0_OR_GREATER
        builder.Services.IsReadOnly.ShouldBeTrue();
#endif
        return app;
    }

    /// <summary>
    /// 保存模块生命周期调用与异常。
    /// </summary>
    internal sealed class ReleaseState
    {
        /// <summary>
        /// 获取或设置初始化调用次数。
        /// </summary>
        public int InitializeCount { get; set; }

        /// <summary>
        /// 获取或设置关闭调用次数。
        /// </summary>
        public int ShutdownCount { get; set; }

        /// <summary>
        /// 获取或设置释放调用次数。
        /// </summary>
        public int DisposeCount { get; set; }

        /// <summary>
        /// 获取或设置是否触发初始化失败。
        /// </summary>
        public bool FailInitialization { get; set; }

        /// <summary>
        /// 获取或设置是否触发关闭失败。
        /// </summary>
        public bool FailShutdown { get; set; }

        /// <summary>
        /// 获取原始初始化异常。
        /// </summary>
        public InvalidOperationException InitializationError { get; } = new("original-startup-error");

        /// <summary>
        /// 获取原始关闭异常。
        /// </summary>
        public InvalidOperationException ShutdownError { get; } = new("original-shutdown-error");
    }

    /// <summary>
    /// 指向当前测试程序集的插件清单目录。
    /// </summary>
    private sealed class PluginManifestDirectory : IDisposable
    {
        /// <summary>
        /// 获取插件目录。
        /// </summary>
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bing-web-release-" + Guid.NewGuid().ToString("N"));

        /// <summary>
        /// 初始化一个 <see cref="PluginManifestDirectory" /> 类型的实例。
        /// </summary>
        public PluginManifestDirectory()
        {
            Directory.CreateDirectory(Path);
            var assemblyPath = typeof(WebResourceReleaseModule).Assembly.Location;
            var entryAssembly = System.IO.Path.GetFileName(assemblyPath);
            File.Copy(assemblyPath, System.IO.Path.Combine(Path, entryAssembly));
            File.WriteAllText(System.IO.Path.Combine(Path, "bing-plugin.json"), JsonSerializer.Serialize(new
            {
                id = "WebResourceRelease",
                version = "1.0.0",
                entryAssembly,
                startupModules = new[] { typeof(WebResourceReleaseModule).FullName }
            }), Encoding.UTF8);
        }

        /// <inheritdoc />
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}

/// <summary>
/// 记录 Web 宿主模块的关闭与释放。
/// </summary>
public sealed class WebResourceReleaseModule : BingModule, IAsyncDisposable
{
    /// <summary>
    /// 当前测试的生命周期状态。
    /// </summary>
    private WebModuleResourceReleaseTest.ReleaseState _state;

    /// <inheritdoc />
    public override void OnApplicationInitialization(BingModuleInitializationContext context)
    {
        _state = context.ServiceProvider.GetRequiredService<WebModuleResourceReleaseTest.ReleaseState>();
        _state.InitializeCount++;
        if (_state.FailInitialization)
            throw _state.InitializationError;
    }

    /// <inheritdoc />
    public override void OnApplicationShutdown(BingModuleShutdownContext context)
    {
        _state.ShutdownCount++;
        if (_state.FailShutdown)
            throw _state.ShutdownError;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_state != null)
            _state.DisposeCount++;
        return default;
    }
}
