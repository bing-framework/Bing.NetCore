using System;
using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.PluginLazyFixture;

/// <summary>
/// 仅在方法体中使用私有依赖的插件模块。
/// </summary>
public sealed class LazyModule : BingModule
{
    /// <summary>
    /// 保存初始化阶段从私有依赖读取的测试值。
    /// </summary>
    private string _initialized;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        var configured = Bing.Core.PluginLazyHelper.Worker.Read();
        services.AddSingleton<Func<string>>(() => configured + ":" + _initialized + ":" + ReadRuntime());
    }

    /// <inheritdoc />
    public override void OnApplicationInitialization(BingModuleInitializationContext context)
        => _initialized = Bing.Core.PluginLazyHelper.Worker.Read();

    /// <summary>
    /// 执行运行期私有依赖调用。
    /// </summary>
    /// <returns>私有依赖的测试值。</returns>
    private static string ReadRuntime() => Bing.Core.PluginLazyHelper.Worker.Read();
}
