using Bing.Core.Modularity;
using Bing.DependencyInjection;

namespace Bing.Core.Builders;

/// <summary>
/// Bing构建器(<see cref="IBingBuilder"/>) 扩展
/// </summary>
public static class BingBuilderExtensions
{
    /// <summary>
    /// 添加核心模块
    /// </summary>
    /// <param name="builder">Bing构建器</param>
    /// <returns>添加核心模块和依赖注入模块后的构建器。</returns>
    internal static IBingBuilder AddCoreModule(this IBingBuilder builder) =>
        builder.AddModule<BingCoreModule>()
            .AddModule<DependencyModule>();
}
