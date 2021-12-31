using System.Threading.Tasks;

namespace Bing.EventBus.Local
{
    /// <summary>
    /// 本地事件处理器
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    public interface ILocalEventHandler<in TEvent> : IEventHandler where TEvent : class
    {
        /// <summary>
        /// 处理事件
        /// </summary>
        /// <param name="eventData">事件数据</param>
        Task HandleAsync(TEvent eventData);
    }
}
