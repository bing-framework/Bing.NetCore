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

    /// <summary>
    /// <inheritdoc />
    /// </summary>
    public IEventHandler EventHandler { get; }

    /// <summary>
    /// 使用事件处理器和可选资源释放操作初始化 <see cref="EventHandlerDisposeWrapper"/> 的实例。
    /// </summary>
    /// <param name="eventHandler">要包装的事件处理器。</param>
    /// <param name="disposeAction">释放包装器时执行的可选资源释放操作。</param>
    public EventHandlerDisposeWrapper(IEventHandler eventHandler, Action disposeAction = null)
    {
        EventHandler = eventHandler;
        _disposeAction = disposeAction;
    }

    /// <summary>
    /// 执行构造时提供的资源释放操作。
    /// </summary>
    public void Dispose() => _disposeAction?.Invoke();
}