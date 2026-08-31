using Microsoft.Extensions.DependencyInjection;

namespace Bing.EventBus;

/// <summary>
/// 从独立依赖注入服务作用域创建事件处理器的工厂。
/// </summary>
public class IocEventHandlerFactory : IEventHandlerFactory, IDisposable
{
    /// <summary>
    /// 获取要从服务容器解析的事件处理器类型。
    /// </summary>
    public Type HandlerType { get; }

    /// <summary>
    /// 获取用于创建处理器独立服务作用域的工厂。
    /// </summary>
    protected IServiceScopeFactory ScopeFactory { get; }

    /// <summary>
    /// 使用服务作用域工厂和事件处理器类型初始化 <see cref="IocEventHandlerFactory"/> 的实例。
    /// </summary>
    /// <param name="scopeFactory">创建处理器独立服务作用域的工厂。</param>
    /// <param name="handlerType">要从服务容器解析的事件处理器类型。</param>
    public IocEventHandlerFactory(IServiceScopeFactory scopeFactory, Type handlerType)
    {
        ScopeFactory = scopeFactory;
        HandlerType = handlerType;
    }

    /// <inheritdoc />
    /// <remarks>返回包装器会在释放时释放为该处理器创建的依赖注入服务作用域。</remarks>
    public IEventHandlerDisposeWrapper GetHandler()
    {
        var scope = ScopeFactory.CreateScope();
        return new EventHandlerDisposeWrapper(
            (IEventHandler)scope.ServiceProvider.GetRequiredService(HandlerType),
            () => scope.Dispose());
    }

    /// <inheritdoc />
    public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
    {
        return handlerFactories
            .OfType<IocEventHandlerFactory>()
            .Any(t => t.HandlerType == HandlerType);
    }

    /// <summary>
    /// 释放工厂资源。
    /// </summary>
    /// <remarks>当前工厂不持有需释放的资源；处理器服务作用域由 <see cref="GetHandler"/> 返回的包装器释放。</remarks>
    public void Dispose()
    {
    }
}