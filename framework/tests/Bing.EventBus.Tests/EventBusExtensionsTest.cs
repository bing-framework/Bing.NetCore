using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.EventBus.Local;
using Bing.EventBus.Tests.Samples;
using Xunit;

namespace Bing.EventBus.Tests;

/// <summary>
/// 事件总线扩展测试
/// </summary>
public class EventBusExtensionsTest
{
    /// <summary>
    /// 本地事件总线
    /// </summary>
    private readonly ILocalEventBus _eventBus;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public EventBusExtensionsTest(ILocalEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// 测试 - 发布事件集合
    /// </summary>
    [Fact]
    public async Task Test_PublishAsync()
    {
        var event1 = new EventSample {Value = "a"};
        var event2 = new EventSample2();
        var event3 = new EventSample3();
        var events = new List<IEvent> {event1, event2, event3};
        await _eventBus.PublishAsync(events);
        Assert.Equal("1:a",event1.Result);
        Assert.Equal("23", event2.Result);
        Assert.Equal("54", event3.Result);
    }
}