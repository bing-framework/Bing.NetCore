using System.Threading.Tasks;
using Bing.EventBus.Tests.Samples;
using Xunit;

namespace Bing.EventBus.Tests;

/// <summary>
/// 事件总线测试
/// </summary>
public class EventBusTest
{
    /// <summary>
    /// 事件总线
    /// </summary>
    private readonly IEventBus _eventBus;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public EventBusTest(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// 测试 - 发布事件
    /// </summary>
    [Fact]
    public async Task Test_PublishAsync()
    {
        var @event = new EventSample {Value = "a"};
        await _eventBus.PublishAsync(@event);
        Assert.Equal("1:a", @event.Result);
    }

    /// <summary>
    /// 测试 - 发布事件 - 测试排序 - 未设置序号
    /// </summary>
    [Fact]
    public async Task Test_PublishAsync_Order_1()
    {
        var @event = new EventSample2();
        await _eventBus.PublishAsync(@event);
        Assert.Equal("23", @event.Result);
    }

    /// <summary>
    /// 测试 - 发布事件 - 测试排序 - 设置序号
    /// </summary>
    [Fact]
    public async Task Test_PublishAsync_Order_2()
    {
        var @event = new EventSample3();
        await _eventBus.PublishAsync(@event);
        Assert.Equal("54", @event.Result);
    }
}