using Bing.Events.Messages;

namespace Bing.Events.EventBusAdapter;

/// <summary>
/// 将旧消息事件契约显式转发至当前 CAP 发布入口。
/// </summary>
public sealed class LegacyCapMessageEventBusAdapter : Bing.EventBus.IMessageEventBus
{
    /// <summary>
    /// 当前消息事件总线。
    /// </summary>
    private readonly Bing.Events.Messages.IMessageEventBus _bus;

    /// <summary>
    /// 初始化一个 <see cref="LegacyCapMessageEventBusAdapter" /> 类型的实例。
    /// </summary>
    public LegacyCapMessageEventBusAdapter(Bing.Events.Messages.IMessageEventBus bus) =>
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));

    /// <inheritdoc />
    public Task PublishAsync<TEvent>(TEvent @event) where TEvent : Bing.EventBus.IMessageEvent
    {
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));
        var message = new MessageEvent
        {
            Id = @event.Id,
            Name = @event.Name,
            Data = @event.Data,
            Callback = @event.Callback,
            Send = @event.Send
        };
        return _bus.PublishAsync(message);
    }

    /// <inheritdoc />
    public Task PublishAsync(string name, object data, string callback, bool send = false) =>
        _bus.PublishAsync(name, data, callback, send);
}
