namespace Bing.EventBus.Distributed;

/// <summary>
/// 分布式事件总线
/// </summary>
public interface IDistributedEventBus
{
    /// <summary>
    /// 发布事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="event">事件</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IDistributedEvent;
}
