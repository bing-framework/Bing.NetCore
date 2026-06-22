using Bing.EventBus.Local;
using Shouldly;

namespace Bing.EventBus.Tests;

/// <summary>
/// NullLocalEventBus 空实现测试
/// </summary>
public class NullLocalEventBusTest
{
    // ==================== 单例 ====================

    /// <summary>
    /// 测试目的：NullLocalEventBus.Instance 不为 null，且是单例（两次访问同一对象）。
    /// </summary>
    [Fact]
    public void Instance_IsNotNull_And_IsSingleton()
    {
        // Act
        var a = NullLocalEventBus.Instance;
        var b = NullLocalEventBus.Instance;

        // Assert
        a.ShouldNotBeNull();
        a.ShouldBeSameAs(b);
    }

    /// <summary>
    /// 测试目的：NullLocalEventBus 实现 ILocalEventBus 接口。
    /// </summary>
    [Fact]
    public void Instance_Implements_ILocalEventBus()
    {
        NullLocalEventBus.Instance.ShouldBeAssignableTo<ILocalEventBus>();
    }

    // ==================== Publish 无副作用 ====================

    /// <summary>
    /// 测试目的：PublishAsync&lt;TEvent&gt; 不抛异常，Task 正常完成。
    /// </summary>
    [Fact]
    public async Task PublishAsync_Generic_DoesNotThrowAndCompletes()
    {
        // Arrange
        var bus = NullLocalEventBus.Instance;
        var evt = new NullBusEvent();

        // Act & Assert
        await Should.NotThrowAsync(() => bus.PublishAsync(evt));
    }

    /// <summary>
    /// 测试目的：PublishAsync(Type, object) 不抛异常。
    /// </summary>
    [Fact]
    public async Task PublishAsync_TypeObject_DoesNotThrow()
    {
        // Arrange
        var bus = NullLocalEventBus.Instance;
        var evt = new NullBusEvent();

        // Act & Assert
        await Should.NotThrowAsync(() => bus.PublishAsync(typeof(NullBusEvent), evt));
    }

    // ==================== Subscribe 返回 NullDisposable ====================

    /// <summary>
    /// 测试目的：Subscribe(Action) 返回可被安全 Dispose 的 IDisposable。
    /// </summary>
    [Fact]
    public void Subscribe_Action_ReturnsDisposable()
    {
        // Arrange
        var bus = NullLocalEventBus.Instance;

        // Act
        var handle = bus.Subscribe<NullBusEvent>(_ => Task.CompletedTask);

        // Assert
        handle.ShouldNotBeNull();
        Should.NotThrow(() => handle.Dispose());
    }

    /// <summary>
    /// 测试目的：Subscribe(Type, IEventHandler) 返回可被安全 Dispose 的 IDisposable。
    /// </summary>
    [Fact]
    public void Subscribe_TypeHandler_ReturnsDisposable()
    {
        // Arrange
        var bus = NullLocalEventBus.Instance;

        // Act
        var handle = bus.Subscribe(typeof(NullBusEvent), new NullEventHandler());

        // Assert
        handle.ShouldNotBeNull();
        Should.NotThrow(() => handle.Dispose());
    }

    // ==================== Unsubscribe 不抛 ====================

    /// <summary>
    /// 测试目的：Unsubscribe(Func) 不抛异常（即使从未订阅）。
    /// </summary>
    [Fact]
    public void Unsubscribe_Action_DoesNotThrow()
    {
        // Arrange
        var bus = NullLocalEventBus.Instance;
        Func<NullBusEvent, Task> handler = _ => Task.CompletedTask;

        // Act & Assert
        Should.NotThrow(() => bus.Unsubscribe(handler));
    }

    /// <summary>
    /// 测试目的：Unsubscribe(Type, IEventHandler) 不抛异常。
    /// </summary>
    [Fact]
    public void Unsubscribe_TypeHandler_DoesNotThrow()
    {
        // Arrange
        var bus = NullLocalEventBus.Instance;

        // Act & Assert
        Should.NotThrow(() => bus.Unsubscribe(typeof(NullBusEvent), new NullEventHandler()));
    }

    /// <summary>
    /// 测试目的：UnsubscribeAll&lt;TEvent&gt; 不抛异常。
    /// </summary>
    [Fact]
    public void UnsubscribeAll_Generic_DoesNotThrow()
    {
        // Arrange
        var bus = NullLocalEventBus.Instance;

        // Act & Assert
        Should.NotThrow(() => bus.UnsubscribeAll<NullBusEvent>());
    }

    /// <summary>
    /// 测试目的：UnsubscribeAll(Type) 不抛异常。
    /// </summary>
    [Fact]
    public void UnsubscribeAll_Type_DoesNotThrow()
    {
        // Arrange
        var bus = NullLocalEventBus.Instance;

        // Act & Assert
        Should.NotThrow(() => bus.UnsubscribeAll(typeof(NullBusEvent)));
    }

    // ==================== 发布后事件对象不变 ====================

    /// <summary>
    /// 测试目的：NullLocalEventBus 发布事件后，事件对象状态不被修改（空实现不做任何操作）。
    /// </summary>
    [Fact]
    public async Task PublishAsync_EventStateUnchanged_AfterPublish()
    {
        // Arrange
        var bus = NullLocalEventBus.Instance;
        var evt = new NullBusEvent { Value = "original" };

        // Act
        await bus.PublishAsync(evt);

        // Assert：空实现不修改事件
        evt.Value.ShouldBe("original");
    }

    // ==================== 辅助类型 ====================

    private class NullBusEvent : IEvent
    {
        public string? Value { get; set; }
    }

    private class NullEventHandler : ILocalEventHandler<NullBusEvent>
    {
        public Task HandleAsync(NullBusEvent eventData) => Task.CompletedTask;
    }
}
