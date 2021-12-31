using System;

namespace Bing.EventBus
{
    /// <summary>
    /// 事件处理释放包装器
    /// </summary>
    public interface IEventHandlerDisposeWrapper : IDisposable
    {
        /// <summary>
        /// 事件处理器
        /// </summary>
        IEventHandler EventHandler { get; }
    }
}
