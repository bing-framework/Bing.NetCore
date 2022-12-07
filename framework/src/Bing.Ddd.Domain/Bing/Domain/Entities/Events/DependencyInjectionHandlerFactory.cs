using System;

namespace Bing.Domain.Entities.Events;

/// <summary>
/// 通过依赖注入实现的领域事件处理器工厂
/// </summary>
public class DependencyInjectionHandlerFactory : IDomainHandlerFactory
{
    /// <summary>
    /// 服务提供程序
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 初始化一个<see cref="DependencyInjectionHandlerFactory"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    public DependencyInjectionHandlerFactory(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    /// <summary>
    /// 创建领域事件处理器
    /// </summary>
    /// <param name="handlerType">领域事件处理器类型</param>
    public object Create(Type handlerType) => _serviceProvider.GetService(handlerType);
}