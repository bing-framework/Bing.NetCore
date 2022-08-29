using System.Threading;
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
        /// <param name="cancellationToken">取消令牌</param>
        Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IMessageEvent;

        /// <summary>
        /// 发布事件
        /// </summary>
        /// <param name="name">消息名称</param>
        /// <param name="data">事件数据</param>
        /// <param name="callback">回调名称</param>
        /// <param name="send">是否立即发送消息</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task PublishAsync(string name, object data, string callback, bool send = false, CancellationToken cancellationToken = default);
    }
}
