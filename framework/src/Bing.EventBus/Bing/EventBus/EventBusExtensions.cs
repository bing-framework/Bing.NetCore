using System.Collections.Generic;
using System.Threading.Tasks;

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
    public static async Task PublishAsync(this IEventBus eventBus, IEnumerable<IEvent> events)
    {
        foreach (var @event in events)
            await eventBus.PublishAsync(@event);
    }

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <param name="messageEventBus">消息事件总线</param>
    /// <param name="events">事件集合</param>
    public static async Task PublishAsync(this IMessageEventBus messageEventBus, IEnumerable<IMessageEvent> events)
    {
        foreach (var @event in events)
            await messageEventBus.PublishAsync(@event);
    }
}