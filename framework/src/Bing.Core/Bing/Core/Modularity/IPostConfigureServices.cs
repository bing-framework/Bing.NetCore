using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Modularity;

/// <summary>
/// 定义模块服务注册后的配置阶段。
/// </summary>
public interface IPostConfigureServices
{
    /// <summary>
    /// 执行后置服务配置。
    /// </summary>
    /// <param name="services">当前应用的服务集合。</param>
    void PostConfigureServices(IServiceCollection services);

    /// <summary>
    /// 异步执行后置服务配置。
    /// </summary>
    /// <param name="services">当前应用的服务集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task PostConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken);
}
