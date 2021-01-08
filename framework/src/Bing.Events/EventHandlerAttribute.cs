using DotNetCore.CAP.Internal;

namespace Bing.Events
{
    /// <summary>
    /// 事件处理器
    /// </summary>
    public class EventHandlerAttribute : TopicAttribute
    {
        /// <summary>
        /// 初始化一个<see cref="EventHandlerAttribute"/>类型的实例
        /// </summary>
        /// <param name="name">消息名称</param>
        public EventHandlerAttribute(string name) : base(name)
        {
        }
    }
}
