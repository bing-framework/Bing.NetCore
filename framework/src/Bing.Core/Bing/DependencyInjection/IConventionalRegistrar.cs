using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// 定义按约定将程序集或类型注册到依赖注入容器的注册器。
/// </summary>
public interface IConventionalRegistrar
{
    /// <summary>
    /// 扫描程序集并按注册约定添加服务。
    /// </summary>
    /// <param name="services">要写入服务描述符的服务集合。</param>
    /// <param name="assembly">要扫描的程序集。</param>
    void AddAssembly(IServiceCollection services, Assembly assembly);

    /// <summary>
    /// 按注册约定添加一组类型。
    /// </summary>
    /// <param name="services">要写入服务描述符的服务集合。</param>
    /// <param name="types">要检查并注册的类型集合。</param>
    void AddTypes(IServiceCollection services, params Type[] types);

    /// <summary>
    /// 按注册约定添加单个类型。
    /// </summary>
    /// <param name="services">要写入服务描述符的服务集合。</param>
    /// <param name="type">要检查并注册的类型。</param>
    void AddType(IServiceCollection services, Type type);
}
