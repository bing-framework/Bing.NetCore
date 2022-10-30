using Microsoft.Extensions.DependencyInjection;

namespace Bing.Logging;

/// <summary>
/// Bing 日志选项配置扩展
/// </summary>
public interface IBingLoggingOptionsExtension
{
    /// <summary>
    /// 注册子服务
    /// </summary>
    /// <param name="services">服务集合</param>
    void AddServices(IServiceCollection services);
}