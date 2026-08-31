namespace Bing.DependencyInjection;

/// <summary>
/// 定义对当前底层服务提供程序的只读访问。
/// </summary>
public interface IServiceProviderAccessor
{
    /// <summary>
    /// 获取当前服务提供程序。
    /// </summary>
    /// <remarks>服务的生命周期和作用域由返回的提供程序决定。</remarks>
    IServiceProvider ServiceProvider { get; }
}
