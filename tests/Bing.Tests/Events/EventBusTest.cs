using System.Threading.Tasks;
using Bing.Events.Default;
using Bing.Events.Handlers;
using Bing.Tests.Samples;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Tests.Events
{
    /// <summary>
    /// 事件总线测试
    /// </summary>
    public class EventBusTest : TestBase
    {
        /// <summary>
        /// 事件处理器2
        /// </summary>
        private readonly IEventHandler<EventSample> _handler;

        public EventBusTest(ITestOutputHelper output) : base(output)
        {
            _handler = Substitute.For<IEventHandler<EventSample>>();
        }

        /// <summary>
        /// 测试发布事件
        /// </summary>
        /// <returns></returns>
        //[Fact]
        public async Task TestPublish()
        {
            var manager = new EventHandlerManagerSample(_handler);
            var eventBus = new EventBus(manager);
            var @event = new EventSample() { Name = "A" };
            await eventBus.PublishAsync(@event);
            await _handler.Received(1).HandleAsync(@event);
        }
    }
}
