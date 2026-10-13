namespace Bing.EventBus.Local;

/// <summary>
/// 空本地事件总线
/// </summary>
public sealed class NullLocalEventBus : ILocalEventBus
{
    /// <summary>
    /// 获取空本地事件总线实例
    /// </summary>
    public static NullLocalEventBus Instance { get; } = new NullLocalEventBus();

    /// <summary>
    /// 初始化一个 <see cref="NullLocalEventBus"/> 类型的实例。
    /// </summary>
    private NullLocalEventBus() { }

    /// <inheritdoc />
    public Task PublishAsync<TEvent>(TEvent eventData) where TEvent : class => Task.CompletedTask;

    /// <inheritdoc />
    public Task PublishAsync(Type eventType, object eventData) => Task.CompletedTask;

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class => NullDisposable.Instance;

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class => NullDisposable.Instance;

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent, THandler>() where TEvent : class where THandler : IEventHandler, new() => NullDisposable.Instance;

    /// <inheritdoc />
    public IDisposable Subscribe(Type eventType, IEventHandler handler) => NullDisposable.Instance;

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class => NullDisposable.Instance;

    /// <inheritdoc />
    public IDisposable Subscribe(Type eventType, IEventHandlerFactory factory) => NullDisposable.Instance;

    /// <inheritdoc />
    public void Unsubscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
    {
    }

    /// <inheritdoc />
    public void Unsubscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class
    {
    }

    /// <inheritdoc />
    public void Unsubscribe(Type eventType, IEventHandler handler)
    {
    }

    /// <inheritdoc />
    public void Unsubscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
    {
    }

    /// <inheritdoc />
    public void Unsubscribe(Type eventType, IEventHandlerFactory factory)
    {
    }

    /// <inheritdoc />
    public void UnsubscribeAll<TEvent>() where TEvent : class
    {
    }

    /// <inheritdoc />
    public void UnsubscribeAll(Type eventType)
    {
    }
}