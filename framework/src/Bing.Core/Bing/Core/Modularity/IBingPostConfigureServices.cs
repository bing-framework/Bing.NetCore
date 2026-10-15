using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Modularity;

/// <summary>
/// 定义模块注册服务之后执行的可选配置阶段。
/// </summary>
public interface IBingPostConfigureServices
{
    /// <summary>
    /// 在当前应用的服务集合中执行后置配置。
    /// </summary>
    /// <param name="services">当前应用的服务集合。</param>
    void PostConfigureServices(IServiceCollection services);
}
