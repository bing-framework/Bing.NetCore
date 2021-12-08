using System.Threading.Tasks;

namespace Bing.EventBus.Local
{
    /// <summary>
    /// 本地事件处理器基类
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    public abstract class LocalEventHandlerBase<TEvent> : ILocalEventHandler<TEvent> where TEvent : IEvent
    {
        /// <inheritdoc />
        public abstract Task HandleAsync(TEvent @event);

        /// <inheritdoc />
        public virtual int Order => 0;

        /// <inheritdoc />
        public virtual bool Enabled => true;
    }
}
