using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Modularity;

/// <summary>
/// 定义模块服务注册前的配置阶段。
/// </summary>
public interface IPreConfigureServices
{
    /// <summary>
    /// 执行前置服务配置。
    /// </summary>
    /// <param name="services">当前应用的服务集合。</param>
    void PreConfigureServices(IServiceCollection services);

    /// <summary>
    /// 异步执行前置服务配置。
    /// </summary>
    /// <param name="services">当前应用的服务集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task PreConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken);
}
