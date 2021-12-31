using System.Threading.Tasks;

namespace Bing.EventBus.Local
{
    /// <summary>
    /// 本地事件处理器基类
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    public abstract class LocalEventHandlerBase<TEvent> : ILocalEventHandler<TEvent> where TEvent : class
    {
        /// <summary>
        /// 处理事件
        /// </summary>
        /// <param name="eventData">事件</param>
        public abstract Task HandleAsync(TEvent eventData);
    }
}
