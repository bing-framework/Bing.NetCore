using System.Threading.Tasks;

namespace Bing.EventBus.Local
{
    /// <summary>
    /// 空本地事件总线
    /// </summary>
    public sealed class NullLocalEventBus : ILocalEventBus
    {
        /// <summary>
        /// 空本地事件总线实例
        /// </summary>
        public static NullLocalEventBus Instance { get; } = new NullLocalEventBus();

        /// <inheritdoc />
        public Task PublishAsync<TEvent>(TEvent @event) where TEvent : IEvent => Task.CompletedTask;
    }
}
