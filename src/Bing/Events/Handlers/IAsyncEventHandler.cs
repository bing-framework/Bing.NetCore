using System.Threading.Tasks;

namespace Bing.Events.Handlers
{
    /// <summary>
    /// 异步事件处理器
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    public interface IAsyncEventHandler<in TEvent>:IEventHandler where TEvent:IEvent
    {
        /// <summary>
        /// 处理事件
        /// </summary>
        /// <param name="event">事件</param>
        /// <returns></returns>
        Task HandleAsync(TEvent @event);
    }
}
