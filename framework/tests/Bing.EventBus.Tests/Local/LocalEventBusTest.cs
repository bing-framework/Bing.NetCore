﻿using System.Threading.Tasks;
using Bing.EventBus.Local;
using Bing.EventBus.Tests.Samples;
using Xunit;

namespace Bing.EventBus.Tests.Local;

/// <summary>
/// 本地事件总线测试
/// </summary>
public class LocalEventBusTest
{
    /// <summary>
    /// 本地事件总线
    /// </summary>
    private readonly ILocalEventBus _eventBus;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public LocalEventBusTest(ILocalEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// 测试 - 发布事件 - 传入事件参数是具体事件类型
    /// </summary>
    [Fact]
    public async Task Test_PublishAsync()
    {
        var @event = new EventSample { Value = "a" };
        await _eventBus.PublishAsync(@event);
        Assert.Equal("1:a", @event.Result);
    }

    /// <summary>
    /// 测试 - 发布事件 - 传入事件参数是IEvent接口
    /// </summary>
    [Fact]
    public async Task Test_PublishAsync_2()
    {
        IEvent @event = new EventSample { Value = "a" };
        await _eventBus.PublishAsync(@event);
        Assert.Equal("1:a", ((EventSample)@event).Result);
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

    /// <summary>
    /// 测试 - 发布事件 - 测试排序 - 设置序号 - 传入事件参数是IEvent接口
    /// </summary>
    [Fact]
    public async Task Test_PublishAsync_Order_3()
    {
        IEvent @event = new EventSample3();
        await _eventBus.PublishAsync(@event);
        Assert.Equal("54", ((EventSample3)@event).Result);
    }
}