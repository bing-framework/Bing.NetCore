using Bing.IM.Models.Messages;

namespace Bing.IM.Models
{
    /// <summary>
    /// 发送到客户端消息
    /// </summary>
    /// <typeparam name="TMessage">消息类型</typeparam>
    // ReSharper disable once InconsistentNaming
    public class IMToClientMessage<TMessage>
    {
        /// <summary>
        /// 消息类型
        /// </summary>
        public IMMessageType Type { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public TMessage Message { get; set; }

        /// <summary>
        /// 创建消息
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="type">消息类型</param>
        /// <returns></returns>
        public static IMToClientMessage<TMessage> Create(TMessage message, IMMessageType type)
        {
            return new IMToClientMessage<TMessage>()
            {
                Message = message,
                Type = type
            };
        }
    }
}
