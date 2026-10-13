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
    /// 初始化一个 <see cref="DependencyInjectionHandlerFactory"/> 类型的实例。
    /// </summary>
    /// <param name="serviceProvider">用于解析处理器的服务提供程序。</param>
    public DependencyInjectionHandlerFactory(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    /// <inheritdoc />
    /// <remarks>
    /// 从依赖注入容器解析领域事件处理器。
    /// </remarks>
    public object Create(Type handlerType) => _serviceProvider.GetService(handlerType);
}