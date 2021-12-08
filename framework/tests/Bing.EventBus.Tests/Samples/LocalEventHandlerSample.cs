using System.Threading.Tasks;
using Bing.EventBus.Local;

namespace Bing.EventBus.Tests.Samples
{
    /// <summary>
    /// 本地事件处理器样例
    /// </summary>
    public class LocalEventHandlerSample : LocalEventHandlerBase<EventSample>
    {
        /// <inheritdoc />
        public override Task HandleAsync(EventSample @event)
        {
            @event.Result = $"1:{@event.Value}";
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 本地事件处理器样例2
    /// </summary>
    public class LocalEventHandlerSample2 : LocalEventHandlerBase<EventSample2>
    {
        /// <inheritdoc />
        public override Task HandleAsync(EventSample2 @event)
        {
            @event.Result += "2";
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 本地事件处理器样例3
    /// </summary>
    public class LocalEventHandlerSample3 : LocalEventHandlerBase<EventSample2>
    {
        /// <inheritdoc />
        public override Task HandleAsync(EventSample2 @event)
        {
            @event.Result += "3";
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 本地事件处理器样例4
    /// </summary>
    public class LocalEventHandlerSample4 : LocalEventHandlerBase<EventSample3>
    {
        /// <inheritdoc />
        public override int Order => 2;

        /// <inheritdoc />
        public override Task HandleAsync(EventSample3 @event)
        {
            @event.Result += "4";
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 本地事件处理器样例5
    /// </summary>
    public class LocalEventHandlerSample5 : LocalEventHandlerBase<EventSample3>
    {
        /// <inheritdoc />
        public override int Order => 1;

        /// <inheritdoc />
        public override Task HandleAsync(EventSample3 @event)
        {
            @event.Result += "5";
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 本地事件处理器样例6
    /// </summary>
    public class LocalEventHandlerSample6 : LocalEventHandlerBase<EventSample3>
    {
        /// <inheritdoc />
        public override int Order => 3;

        /// <inheritdoc />
        public override bool Enabled => false;

        /// <inheritdoc />
        public override Task HandleAsync(EventSample3 @event)
        {
            @event.Result += "6";
            return Task.CompletedTask;
        }
    }
}
