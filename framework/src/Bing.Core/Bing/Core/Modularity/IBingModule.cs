using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Modularity;

/// <summary>
/// 定义 Bing 模块的服务注册和启用生命周期。
/// </summary>
public interface IBingModule
{
    /// <summary>
    /// 获取模块启动级别。
    /// </summary>
    /// <remarks>级别较小的模块优先启动。</remarks>
    ModuleLevel Level { get; }

    /// <summary>
    /// 获取同一模块级别中的启动顺序。
    /// </summary>
    /// <remarks>模块先按 <see cref="Level"/> 排序，再按此值排序；默认值为 <c>0</c>。</remarks>
    int Order { get; }

    /// <summary>
    /// 获取模块是否已完成启用。
    /// </summary>
    bool Enabled { get; }

    /// <summary>
    /// 将模块服务添加到依赖注入服务集合。
    /// </summary>
    /// <param name="services">要添加模块服务的服务集合。</param>
    /// <returns>注册完成后的服务集合。</returns>
    IServiceCollection AddServices(IServiceCollection services);

    /// <summary>
    /// 启用已注册的模块服务。
    /// </summary>
    /// <param name="provider">用于解析模块服务的服务提供程序。</param>
    void UseModule(IServiceProvider provider);
}
