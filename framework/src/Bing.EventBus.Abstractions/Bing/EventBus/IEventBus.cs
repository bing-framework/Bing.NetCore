namespace Bing.EventBus;

/// <summary>
/// 在已注册事件处理器之间分发领域或集成事件。
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// 将事件发布给与事件类型匹配的已注册处理器。
    /// </summary>
    /// <typeparam name="TEvent">要发布的事件类型。</typeparam>
    /// <param name="event">要发布的事件实例。</param>
    /// <param name="cancellationToken">用于取消发布或处理过程的取消令牌。</param>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IEvent;
}
