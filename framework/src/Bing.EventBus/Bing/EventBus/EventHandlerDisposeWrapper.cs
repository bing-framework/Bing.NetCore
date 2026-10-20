namespace Bing.EventBus;

/// <summary>
/// 保存事件处理器和可选释放操作的默认包装器。
/// </summary>
public class EventHandlerDisposeWrapper : IEventHandlerDisposeWrapper
{
    /// <summary>
    /// 释放处理器关联资源的可选操作。
    /// </summary>
    private readonly Action _disposeAction;

    /// <inheritdoc />
    public IEventHandler EventHandler { get; }

    /// <summary>
    /// 初始化一个 <see cref="EventHandlerDisposeWrapper"/> 类型的实例。
    /// </summary>
    /// <param name="eventHandler">要包装的事件处理器。</param>
    /// <param name="disposeAction">释放包装器时执行的可选资源释放操作。</param>
    public EventHandlerDisposeWrapper(IEventHandler eventHandler, Action disposeAction = null)
    {
        EventHandler = eventHandler;
        _disposeAction = disposeAction;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行构造时提供的资源释放操作。
    /// </remarks>
    public void Dispose() => _disposeAction?.Invoke();
}