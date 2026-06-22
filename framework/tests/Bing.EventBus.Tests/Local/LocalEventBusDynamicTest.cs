using Bing.EventBus.Local;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Bing.EventBus.Tests.Local;

/// <summary>
/// LocalEventBus 动态订阅 / 取消订阅测试
/// </summary>
public class LocalEventBusDynamicTest
{
    // ==================== 辅助 ====================

    /// <summary>
    /// 创建带空 Handler 的 LocalEventBus（不依赖 EventBusModule）
    /// </summary>
    private static LocalEventBus CreateBus()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var options = Options.Create(new LocalEventBusOptions());
        return new LocalEventBus(options, sp.GetRequiredService<IServiceScopeFactory>());
    }

    private class DynEvent : IEvent
    {
        public string? Value { get; set; }
    }

    // ==================== Subscribe + Publish ====================

    /// <summary>
    /// 测试目的：动态 Subscribe(Func) 后，Publish 应触发该委托。
    /// </summary>
    [Fact]
    public async Task Subscribe_Action_ThenPublish_HandlerIsCalled()
    {
        // Arrange
        var bus = CreateBus();
        string? received = null;
        bus.Subscribe<DynEvent>(e =>
        {
            received = e.Value;
            return Task.CompletedTask;
        });

        // Act
        await bus.PublishAsync(new DynEvent { Value = "hello" });

        // Assert
        received.ShouldBe("hello");
    }

    /// <summary>
    /// 测试目的：同一事件注册多个委托，Publish 后所有委托均被调用。
    /// </summary>
    [Fact]
    public async Task Subscribe_MultipleActions_AllCalled_OnPublish()
    {
        // Arrange
        var bus = CreateBus();
        var callLog = new List<int>();

        bus.Subscribe<DynEvent>(_ => { callLog.Add(1); return Task.CompletedTask; });
        bus.Subscribe<DynEvent>(_ => { callLog.Add(2); return Task.CompletedTask; });

        // Act
        await bus.PublishAsync(new DynEvent());

        // Assert
        callLog.Count.ShouldBe(2);
        callLog.ShouldContain(1);
        callLog.ShouldContain(2);
    }

    // ==================== Unsubscribe ====================

    /// <summary>
    /// 测试目的：Unsubscribe(Func) 后，Publish 不应再触发该委托。
    /// </summary>
    [Fact]
    public async Task Unsubscribe_Action_ThenPublish_HandlerNotCalled()
    {
        // Arrange
        var bus = CreateBus();
        var called = false;
        Func<DynEvent, Task> handler = _ =>
        {
            called = true;
            return Task.CompletedTask;
        };

        bus.Subscribe(handler);

        // Act
        bus.Unsubscribe(handler);
        await bus.PublishAsync(new DynEvent());

        // Assert
        called.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：取消一个委托后，另一个委托仍然被调用。
    /// </summary>
    [Fact]
    public async Task Unsubscribe_OneAction_OtherHandlerStillCalled()
    {
        // Arrange
        var bus = CreateBus();
        var log = new List<string>();

        Func<DynEvent, Task> h1 = _ => { log.Add("h1"); return Task.CompletedTask; };
        Func<DynEvent, Task> h2 = _ => { log.Add("h2"); return Task.CompletedTask; };

        bus.Subscribe(h1);
        bus.Subscribe(h2);

        // Act：只取消 h1
        bus.Unsubscribe(h1);
        await bus.PublishAsync(new DynEvent());

        // Assert
        log.ShouldNotContain("h1");
        log.ShouldContain("h2");
    }

    // ==================== Subscribe/Unsubscribe 通过 IDisposable ====================

    /// <summary>
    /// 测试目的：Subscribe 返回的 IDisposable.Dispose() 应与 Unsubscribe 效果相同。
    /// </summary>
    [Fact]
    public async Task Subscribe_Returns_Disposable_That_Unsubscribes()
    {
        // Arrange
        var bus = CreateBus();
        var called = false;

        var handle = bus.Subscribe<DynEvent>(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        // Act：通过 Dispose 取消订阅
        handle.Dispose();
        await bus.PublishAsync(new DynEvent());

        // Assert
        called.ShouldBeFalse();
    }

    // ==================== UnsubscribeAll ====================

    /// <summary>
    /// 测试目的：UnsubscribeAll&lt;TEvent&gt; 后，Publish 不应触发任何委托。
    /// </summary>
    [Fact]
    public async Task UnsubscribeAll_Generic_ClearsAllHandlers()
    {
        // Arrange
        var bus = CreateBus();
        var callCount = 0;

        bus.Subscribe<DynEvent>(_ => { callCount++; return Task.CompletedTask; });
        bus.Subscribe<DynEvent>(_ => { callCount++; return Task.CompletedTask; });

        // Act
        bus.UnsubscribeAll<DynEvent>();
        await bus.PublishAsync(new DynEvent());

        // Assert
        callCount.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：UnsubscribeAll(Type) 仅清除指定事件类型的处理器，其他类型不受影响。
    /// </summary>
    [Fact]
    public async Task UnsubscribeAll_Type_DoesNotAffectOtherEventTypes()
    {
        // Arrange
        var bus = CreateBus();
        var dynCalled = false;
        var otherCalled = false;

        bus.Subscribe<DynEvent>(_ => { dynCalled = true; return Task.CompletedTask; });
        bus.Subscribe<OtherEvent>(_ => { otherCalled = true; return Task.CompletedTask; });

        // Act：只清除 DynEvent 的处理器
        bus.UnsubscribeAll(typeof(DynEvent));
        await bus.PublishAsync(new DynEvent());
        await bus.PublishAsync(new OtherEvent());

        // Assert
        dynCalled.ShouldBeFalse();
        otherCalled.ShouldBeTrue();
    }

    // ==================== Subscribe 通过 IEventHandler 实例 ====================

    /// <summary>
    /// 测试目的：Subscribe(Type, IEventHandler) 注册处理器实例后，Publish 应触发 HandleAsync。
    /// </summary>
    [Fact]
    public async Task Subscribe_EventHandlerInstance_IsInvokedOnPublish()
    {
        // Arrange
        var bus = CreateBus();
        var handler = new TrackingEventHandler();

        bus.Subscribe(typeof(DynEvent), handler);

        // Act
        await bus.PublishAsync(new DynEvent { Value = "test" });

        // Assert
        handler.ReceivedValues.ShouldContain("test");
    }

    /// <summary>
    /// 测试目的：Unsubscribe(Type, IEventHandler) 后，处理器不再被调用。
    /// </summary>
    [Fact]
    public async Task Unsubscribe_EventHandlerInstance_NotCalledAfterUnsubscribe()
    {
        // Arrange
        var bus = CreateBus();
        var handler = new TrackingEventHandler();

        bus.Subscribe(typeof(DynEvent), handler);
        bus.Unsubscribe(typeof(DynEvent), handler);

        // Act
        await bus.PublishAsync(new DynEvent { Value = "test" });

        // Assert
        handler.ReceivedValues.ShouldBeEmpty();
    }

    // ==================== 辅助类型 ====================

    private class OtherEvent : IEvent { }

    private class TrackingEventHandler : ILocalEventHandler<DynEvent>
    {
        public List<string?> ReceivedValues { get; } = new();

        public Task HandleAsync(DynEvent eventData)
        {
            ReceivedValues.Add(eventData.Value);
            return Task.CompletedTask;
        }
    }
}
