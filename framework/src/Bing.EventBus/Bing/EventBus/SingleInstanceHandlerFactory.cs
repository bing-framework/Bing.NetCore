namespace Bing.EventBus;

/// <summary>
/// 返回调用方提供的单例事件处理器的工厂。
/// </summary>
public class SingleInstanceHandlerFactory : IEventHandlerFactory
{
    /// <summary>
    /// 获取由此工厂重复返回的单例事件处理器实例。
    /// </summary>
    public IEventHandler HandlerInstance { get; }

    /// <summary>
    /// 使用指定单例事件处理器初始化 <see cref="SingleInstanceHandlerFactory"/> 的实例。
    /// </summary>
    /// <param name="handler">要由工厂返回的单例事件处理器。</param>
    public SingleInstanceHandlerFactory(IEventHandler handler)
    {
        HandlerInstance = handler;
    }

    /// <inheritdoc />
    /// <remarks>返回包装器不拥有单例处理器实例的生命周期。</remarks>
    public IEventHandlerDisposeWrapper GetHandler() => new EventHandlerDisposeWrapper(HandlerInstance);

    /// <inheritdoc />
    public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
    {
        return handlerFactories
            .OfType<SingleInstanceHandlerFactory>()
            .Any(t => t.HandlerInstance == HandlerInstance);
    }
}