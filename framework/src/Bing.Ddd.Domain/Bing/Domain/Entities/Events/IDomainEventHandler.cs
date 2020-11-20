using System.Threading.Tasks;

namespace Bing.Domain.Entities.Events
{
    /// <summary>
    /// 领域事件处理器
    /// </summary>
    /// <typeparam name="TEvent">领域事件类型</typeparam>
    public interface IDomainEventHandler<in TEvent> where TEvent : DomainEvent
    {
        /// <summary>
        /// 处理
        /// </summary>
        /// <param name="event">领域事件</param>
        Task HandleAsync(TEvent @event);
    }
}
