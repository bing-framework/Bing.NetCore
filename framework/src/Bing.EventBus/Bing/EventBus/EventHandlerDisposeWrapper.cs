namespace Bing.EventBus;

/// <summary>
/// 保存事件处理器和可选释放操作的默认包装器。
/// </summary>
public class EventHandlerDisposeWrapper : IEventHandlerDisposeWrapper, IAsyncDisposable
{
    /// <summary>
    /// 释放处理器关联资源的可选操作。
    /// </summary>
    private readonly Action _disposeAction;

    /// <summary>
    /// 异步释放处理器关联资源的可选操作。
    /// </summary>
    private readonly Func<ValueTask> _disposeAsyncAction;

    /// <inheritdoc />
    public IEventHandler EventHandler { get; }

    /// <summary>
    /// 初始化一个 <see cref="EventHandlerDisposeWrapper" /> 类的新实例。
    /// </summary>
    /// <param name="eventHandler">要包装的事件处理器。</param>
    /// <param name="disposeAction">释放包装器时执行的可选资源释放操作。</param>
    public EventHandlerDisposeWrapper(IEventHandler eventHandler, Action disposeAction = null)
        : this(eventHandler, disposeAction, null)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="EventHandlerDisposeWrapper" /> 类的新实例。
    /// </summary>
    /// <param name="eventHandler">要包装的事件处理器。</param>
    /// <param name="disposeAction">同步释放包装器时执行的可选资源释放操作。</param>
    /// <param name="disposeAsyncAction">异步释放包装器时执行的可选资源释放操作。</param>
    public EventHandlerDisposeWrapper(IEventHandler eventHandler, Action disposeAction,
        Func<ValueTask> disposeAsyncAction)
    {
        EventHandler = eventHandler;
        _disposeAction = disposeAction;
        _disposeAsyncAction = disposeAsyncAction;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行构造时提供的同步资源释放操作。
    /// </remarks>
    public void Dispose() => _disposeAction?.Invoke();

    /// <inheritdoc />
    /// <remarks>
    /// 优先执行异步释放操作；未提供异步操作时回退到同步释放。
    /// </remarks>
    public ValueTask DisposeAsync()
    {
        if (_disposeAsyncAction != null)
            return _disposeAsyncAction();
        _disposeAction?.Invoke();
        return default;
    }
}