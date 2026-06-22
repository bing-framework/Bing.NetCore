using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.EventBus;
using Bing.EventBus.Local;
using Shouldly;
using Xunit;

namespace Bing.EventBus.Tests;

// ─── 辅助 Stub ───────────────────────────────────────────────────────────────

/// <summary>
/// 最简事件处理器 Stub（仅实现标记接口）
/// </summary>
internal class StubEventHandler : IEventHandler { }

/// <summary>
/// 可创建的瞬时处理器 Stub（满足 new() 约束）
/// </summary>
internal class NewableStubHandler : IEventHandler { }

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// ActionEventHandler、SingleInstanceHandlerFactory、TransientEventHandlerFactory、
/// EventHandlerDisposeWrapper 单元测试
/// </summary>
public class EventHandlerFactoryTest
{
    // ════════════════════════════════════════════════════════════════
    // ActionEventHandler<TEvent>
    // ════════════════════════════════════════════════════════════════

    private class SimpleEvent { public string Value { get; set; } }

    /// <summary>
    /// 测试目的：构造时传入 Func，Action 属性应保存该委托。
    /// </summary>
    [Fact]
    public void ActionEventHandler_Action_ShouldBeStoredDelegate()
    {
        // Arrange
        Func<SimpleEvent, Task> func = _ => Task.CompletedTask;

        // Act
        var handler = new ActionEventHandler<SimpleEvent>(func);

        // Assert
        handler.Action.ShouldBeSameAs(func);
    }

    /// <summary>
    /// 测试目的：HandleAsync 应调用传入的委托，并将事件数据正确传递给委托。
    /// </summary>
    [Fact]
    public async Task ActionEventHandler_HandleAsync_ShouldInvokeAction()
    {
        // Arrange
        SimpleEvent received = null;
        var handler = new ActionEventHandler<SimpleEvent>(evt =>
        {
            received = evt;
            return Task.CompletedTask;
        });
        var eventData = new SimpleEvent { Value = "hello" };

        // Act
        await handler.HandleAsync(eventData);

        // Assert
        received.ShouldBeSameAs(eventData);
        received.Value.ShouldBe("hello");
    }

    /// <summary>
    /// 测试目的：HandleAsync 应等待异步委托完成，而不是 fire-and-forget。
    /// </summary>
    [Fact]
    public async Task ActionEventHandler_HandleAsync_ShouldAwaitAsyncAction()
    {
        // Arrange
        var completed = false;
        var handler = new ActionEventHandler<SimpleEvent>(async _ =>
        {
            await Task.Yield();
            completed = true;
        });

        // Act
        await handler.HandleAsync(new SimpleEvent());

        // Assert
        completed.ShouldBeTrue();
    }

    // ════════════════════════════════════════════════════════════════
    // EventHandlerDisposeWrapper
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：构造后 EventHandler 属性应保持传入的处理器引用。
    /// </summary>
    [Fact]
    public void EventHandlerDisposeWrapper_EventHandler_ShouldMatchConstructorArg()
    {
        // Arrange
        var handler = new StubEventHandler();

        // Act
        var wrapper = new EventHandlerDisposeWrapper(handler);

        // Assert
        wrapper.EventHandler.ShouldBeSameAs(handler);
    }

    /// <summary>
    /// 测试目的：不传 disposeAction 时 Dispose 不应抛异常（null 安全）。
    /// </summary>
    [Fact]
    public void EventHandlerDisposeWrapper_Dispose_WithNoAction_ShouldNotThrow()
    {
        // Arrange
        var wrapper = new EventHandlerDisposeWrapper(new StubEventHandler());

        // Act & Assert
        Should.NotThrow(() => wrapper.Dispose());
    }

    /// <summary>
    /// 测试目的：传入 disposeAction 时 Dispose 应调用该委托。
    /// </summary>
    [Fact]
    public void EventHandlerDisposeWrapper_Dispose_WithAction_ShouldInvokeAction()
    {
        // Arrange
        var disposed = false;
        var wrapper = new EventHandlerDisposeWrapper(new StubEventHandler(), () => disposed = true);

        // Act
        wrapper.Dispose();

        // Assert
        disposed.ShouldBeTrue();
    }

    // ════════════════════════════════════════════════════════════════
    // SingleInstanceHandlerFactory
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：HandlerInstance 属性应保持构造时传入的处理器引用。
    /// </summary>
    [Fact]
    public void SingleInstanceFactory_HandlerInstance_ShouldMatchConstructorArg()
    {
        // Arrange
        var handler = new StubEventHandler();

        // Act
        var factory = new SingleInstanceHandlerFactory(handler);

        // Assert
        factory.HandlerInstance.ShouldBeSameAs(handler);
    }

    /// <summary>
    /// 测试目的：GetHandler 应返回包含相同处理器实例的 Wrapper，且不为 null。
    /// </summary>
    [Fact]
    public void SingleInstanceFactory_GetHandler_ShouldReturnWrapperWithSameHandler()
    {
        // Arrange
        var handler = new StubEventHandler();
        var factory = new SingleInstanceHandlerFactory(handler);

        // Act
        using var wrapper = factory.GetHandler();

        // Assert
        wrapper.ShouldNotBeNull();
        wrapper.EventHandler.ShouldBeSameAs(handler);
    }

    /// <summary>
    /// 测试目的：多次 GetHandler 应每次均返回包含相同处理器的 Wrapper（单例语义）。
    /// </summary>
    [Fact]
    public void SingleInstanceFactory_GetHandler_MultipleCalls_ShouldReturnSameHandler()
    {
        // Arrange
        var handler = new StubEventHandler();
        var factory = new SingleInstanceHandlerFactory(handler);

        // Act
        using var w1 = factory.GetHandler();
        using var w2 = factory.GetHandler();

        // Assert
        w1.EventHandler.ShouldBeSameAs(w2.EventHandler);
    }

    /// <summary>
    /// 测试目的：IsInFactories - 工厂列表中包含相同实例时应返回 true。
    /// </summary>
    [Fact]
    public void SingleInstanceFactory_IsInFactories_WhenPresent_ShouldReturnTrue()
    {
        // Arrange
        var handler = new StubEventHandler();
        var factory = new SingleInstanceHandlerFactory(handler);
        var factories = new List<IEventHandlerFactory> { factory };

        // Act
        var result = factory.IsInFactories(factories);

        // Assert
        result.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：IsInFactories - 工厂列表为空时应返回 false。
    /// </summary>
    [Fact]
    public void SingleInstanceFactory_IsInFactories_WhenEmpty_ShouldReturnFalse()
    {
        // Arrange
        var handler = new StubEventHandler();
        var factory = new SingleInstanceHandlerFactory(handler);

        // Act
        var result = factory.IsInFactories(new List<IEventHandlerFactory>());

        // Assert
        result.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：IsInFactories - 工厂列表中包含不同实例（相同类型）时应返回 false。
    /// </summary>
    [Fact]
    public void SingleInstanceFactory_IsInFactories_WhenDifferentInstance_ShouldReturnFalse()
    {
        // Arrange
        var factory1 = new SingleInstanceHandlerFactory(new StubEventHandler());
        var factory2 = new SingleInstanceHandlerFactory(new StubEventHandler());
        var factories = new List<IEventHandlerFactory> { factory2 };

        // Act
        var result = factory1.IsInFactories(factories);

        // Assert
        result.ShouldBeFalse();
    }

    // ════════════════════════════════════════════════════════════════
    // TransientEventHandlerFactory
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：HandlerType 属性应与构造时传入的类型一致。
    /// </summary>
    [Fact]
    public void TransientFactory_HandlerType_ShouldMatchConstructorArg()
    {
        // Act
        var factory = new TransientEventHandlerFactory(typeof(NewableStubHandler));

        // Assert
        factory.HandlerType.ShouldBe(typeof(NewableStubHandler));
    }

    /// <summary>
    /// 测试目的：泛型重载创建的工厂，HandlerType 应为对应泛型参数类型。
    /// </summary>
    [Fact]
    public void TransientFactory_Generic_HandlerType_ShouldMatchGenericArg()
    {
        // Act
        var factory = new TransientEventHandlerFactory<NewableStubHandler>();

        // Assert
        factory.HandlerType.ShouldBe(typeof(NewableStubHandler));
    }

    /// <summary>
    /// 测试目的：IsInFactories - 工厂列表中包含相同 HandlerType 时应返回 true。
    /// </summary>
    [Fact]
    public void TransientFactory_IsInFactories_WhenSameHandlerType_ShouldReturnTrue()
    {
        // Arrange
        var factory = new TransientEventHandlerFactory(typeof(NewableStubHandler));
        var factories = new List<IEventHandlerFactory> { factory };

        // Act
        var result = factory.IsInFactories(factories);

        // Assert
        result.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：IsInFactories - 工厂列表中不包含相同 HandlerType 时应返回 false。
    /// </summary>
    [Fact]
    public void TransientFactory_IsInFactories_WhenDifferentHandlerType_ShouldReturnFalse()
    {
        // Arrange
        var factory1 = new TransientEventHandlerFactory(typeof(NewableStubHandler));
        var factory2 = new TransientEventHandlerFactory(typeof(StubEventHandler));
        var factories = new List<IEventHandlerFactory> { factory2 };

        // Act
        var result = factory1.IsInFactories(factories);

        // Assert
        result.ShouldBeFalse();
    }
}
