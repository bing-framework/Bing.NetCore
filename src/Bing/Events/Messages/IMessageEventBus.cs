using System.Threading.Tasks;

namespace Bing.Events.Messages
{
    /// <summary>
    /// 消息事件总线
    /// </summary>
    public interface IMessageEventBus
    {        
        /// <summary>
        /// 发布事件
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="event">事件</param>
        /// <returns></returns>
        Task PublishAsync<TEvent>(TEvent @event) where TEvent : IMessageEvent;

        /// <summary>
        /// 发布事件
        /// </summary>
        /// <param name="name">消息名称</param>
        /// <param name="data">事件数据</param>
        /// <param name="callback">回调名称</param>
        /// <param name="send">是否立即发送消息</param>
        /// <returns></returns>
        Task PublishAsync(string name, object data, string callback, bool send = false);
    }
}
