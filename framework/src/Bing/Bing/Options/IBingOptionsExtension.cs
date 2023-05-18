using Microsoft.Extensions.DependencyInjection;

namespace Bing.Options;

/// <summary>
/// Bing配置项扩展
/// </summary>
public interface IBingOptionsExtension
{
    /// <summary>
    /// 注册服务
    /// </summary>
    /// <param name="services">服务集合</param>
    void AddServices(IServiceCollection services);
}
