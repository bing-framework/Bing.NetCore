namespace Bing.Domain.Entities.Events;

/// <summary>
/// 定义指定领域事件的异步处理契约。
/// </summary>
/// <typeparam name="TEvent">要处理的领域事件类型。</typeparam>
public interface IDomainEventHandler<in TEvent> where TEvent : DomainEvent
{
    /// <summary>
    /// 异步处理领域事件。
    /// </summary>
    /// <param name="event">待处理的领域事件。</param>
    Task HandleAsync(TEvent @event);
}