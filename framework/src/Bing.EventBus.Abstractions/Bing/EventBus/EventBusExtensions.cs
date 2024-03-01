namespace Bing.EventBus;

/// <summary>
/// 事件总线(<see cref="IEventBus"/>) 扩展
/// </summary>
public static class EventBusExtensions
{
    /// <summary>
    /// 发布事件
    /// </summary>
    /// <param name="eventBus">事件总线</param>
    /// <param name="events">事件集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static async Task PublishAsync(this IEventBus eventBus, IEnumerable<IEvent> events, CancellationToken cancellationToken = default)
    {
        if (eventBus == null)
            throw new ArgumentNullException(nameof(eventBus));
        if (events == null)
            return;
        foreach (var @event in events)
            await eventBus.PublishAsync(@event, cancellationToken);
    }
}
