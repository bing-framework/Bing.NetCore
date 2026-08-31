namespace Bing.Domain.Entities.Events;

/// <summary>
/// 使用依赖注入容器创建领域事件处理器实例。
/// </summary>
public class DependencyInjectionHandlerFactory : IDomainHandlerFactory
{
    /// <summary>
    /// 用于解析处理器实例的服务提供程序。
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 使用指定服务提供程序初始化工厂。
    /// </summary>
    /// <param name="serviceProvider">用于解析处理器的服务提供程序。</param>
    public DependencyInjectionHandlerFactory(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    /// <summary>
    /// 从依赖注入容器解析领域事件处理器。
    /// </summary>
    /// <param name="handlerType">要解析的领域事件处理器类型。</param>
    /// <returns>解析出的处理器实例；未注册时返回 <c>null</c>。</returns>
    public object Create(Type handlerType) => _serviceProvider.GetService(handlerType);
}