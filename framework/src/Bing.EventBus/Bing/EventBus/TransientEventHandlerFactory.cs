namespace Bing.EventBus;

/// <summary>
/// 按指定处理器类型创建瞬时事件处理器的泛型工厂。
/// </summary>
/// <typeparam name="THandler">具有公共无参构造函数的事件处理器类型。</typeparam>
public class TransientEventHandlerFactory<THandler> : TransientEventHandlerFactory, IEventHandlerFactory where THandler : IEventHandler, new()
{
    /// <summary>
    /// 初始化 <see cref="TransientEventHandlerFactory{THandler}"/> 的实例。
    /// </summary>
    public TransientEventHandlerFactory() : base(typeof(THandler))
    {
    }

    /// <summary>
    /// 创建泛型参数指定的瞬时事件处理器。
    /// </summary>
    /// <returns>新创建的事件处理器实例。</returns>
    protected override IEventHandler CreateHandler() => new THandler();
}

/// <summary>
/// 按运行时处理器类型创建瞬时事件处理器的工厂。
/// </summary>
public class TransientEventHandlerFactory : IEventHandlerFactory
{
    /// <summary>
    /// 获取要实例化的事件处理器类型。
    /// </summary>
    public Type HandlerType { get; }

    /// <summary>
    /// 使用指定事件处理器类型初始化 <see cref="TransientEventHandlerFactory"/> 的实例。
    /// </summary>
    /// <param name="handlerType">每次获取处理器时要实例化的事件处理器类型。</param>
    public TransientEventHandlerFactory(Type handlerType)
    {
        HandlerType = handlerType;
    }

    /// <inheritdoc />
    /// <remarks>返回包装器会在释放时释放实现 <see cref="IDisposable"/> 的瞬时处理器。</remarks>
    public virtual IEventHandlerDisposeWrapper GetHandler()
    {
        var handler = CreateHandler();
        return new EventHandlerDisposeWrapper(
            handler,
            () => (handler as IDisposable)?.Dispose());
    }

    /// <inheritdoc />
    public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
    {
        return handlerFactories
            .OfType<TransientEventHandlerFactory>()
            .Any(f => f.HandlerType == HandlerType);
    }

    /// <summary>
    /// 创建新的瞬时事件处理器实例。
    /// </summary>
    /// <returns>按 <see cref="HandlerType"/> 创建的事件处理器实例。</returns>
    protected virtual IEventHandler CreateHandler()
    {
        return (IEventHandler)Activator.CreateInstance(HandlerType);
    }
}