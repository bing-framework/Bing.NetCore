using System.Threading.Tasks;
using Bing.EventBus;
using Bing.EventBus.Local;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.EventBus.Tests;

/// <summary>
/// <see cref="LocalEventMessage"/> 测试。
/// 验证构造函数传参与属性读取的正确性。
/// </summary>
public class LocalEventMessageTest
{
    /// <summary>
    /// 测试目的：构造后三个属性应与传入参数引用相等（无拷贝）。
    /// </summary>
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Arrange
        var eventId = "evt-001";
        var eventData = new object();
        var eventType = typeof(string);

        // Act
        var msg = new LocalEventMessage(eventId, eventData, eventType);

        // Assert
        msg.EventId.ShouldBe(eventId);
        msg.EventData.ShouldBeSameAs(eventData);
        msg.EventType.ShouldBe(eventType);
    }

    /// <summary>
    /// 测试目的：EventId 可为 null，不应在构造时抛异常。
    /// </summary>
    [Fact]
    public void Constructor_WithNullEventId_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => new LocalEventMessage(null, new object(), typeof(int)));
    }

    /// <summary>
    /// 测试目的：EventData 可为 null，适用于空事件载荷场景。
    /// </summary>
    [Fact]
    public void Constructor_WithNullEventData_ShouldNotThrow()
    {
        // Arrange & Act
        var msg = new LocalEventMessage("id", null, typeof(object));

        // Assert
        msg.EventData.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：EventType 应精确存储传入的 Type，支持任意类型。
    /// </summary>
    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(string))]
    [InlineData(typeof(LocalEventMessage))]
    public void EventType_ShouldMatchConstructorArg(Type type)
    {
        // Arrange & Act
        var msg = new LocalEventMessage("x", null, type);

        // Assert
        msg.EventType.ShouldBe(type);
    }
}

/// <summary>
/// <see cref="LocalEventBusOptions"/> 测试。
/// 验证 Handlers 集合的初始化状态。
/// </summary>
public class LocalEventBusOptionsTest
{
    /// <summary>
    /// 测试目的：默认构造后 Handlers 不为 null，无需外部初始化即可直接使用。
    /// </summary>
    [Fact]
    public void Constructor_Handlers_ShouldNotBeNull()
    {
        // Arrange & Act
        var options = new LocalEventBusOptions();

        // Assert
        options.Handlers.ShouldNotBeNull();
    }

    /// <summary>
    /// 测试目的：默认构造后 Handlers 应为空集合。
    /// </summary>
    [Fact]
    public void Constructor_Handlers_ShouldBeEmpty()
    {
        // Arrange & Act
        var options = new LocalEventBusOptions();

        // Assert
        options.Handlers.Count.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：可向 Handlers 添加处理器类型，Count 增加。
    /// </summary>
    [Fact]
    public void Handlers_CanAddHandlerType()
    {
        // Arrange
        var options = new LocalEventBusOptions();

        // Act
        options.Handlers.Add<TestEventHandler>();

        // Assert
        options.Handlers.Count.ShouldBe(1);
    }

    /// <summary>
    /// 辅助：用于测试注册的事件处理器
    /// </summary>
    private class TestEventHandler : IEventHandler { }
}

/// <summary>
/// <see cref="ActionEventHandler{TEvent}"/> 测试。
/// 验证 Action 属性赋值及 HandleAsync 委托行为。
/// </summary>
public class ActionEventHandlerTest
{
    /// <summary>
    /// 测试目的：构造后 Action 属性应引用传入的委托。
    /// </summary>
    [Fact]
    public void Constructor_Action_ShouldReferenceInjectedDelegate()
    {
        // Arrange
        Func<string, Task> handler = _ => Task.CompletedTask;

        // Act
        var sut = new ActionEventHandler<string>(handler);

        // Assert
        sut.Action.ShouldBeSameAs(handler);
    }

    /// <summary>
    /// 测试目的：HandleAsync 应调用 Action 并传递事件数据。
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldInvokeActionWithEventData()
    {
        // Arrange
        string received = null;
        var sut = new ActionEventHandler<string>(evt =>
        {
            received = evt;
            return Task.CompletedTask;
        });

        // Act
        await sut.HandleAsync("hello");

        // Assert
        received.ShouldBe("hello");
    }

    /// <summary>
    /// 测试目的：HandleAsync 应等待异步 Action 完成后再返回。
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldAwaitAsyncAction()
    {
        // Arrange
        var completed = false;
        var sut = new ActionEventHandler<string>(async _ =>
        {
            await Task.Yield();
            completed = true;
        });

        // Act
        await sut.HandleAsync("42");

        // Assert
        completed.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：Action 抛出异常时，HandleAsync 应将异常向上传播。
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenActionThrows_ShouldPropagateException()
    {
        // Arrange
        var sut = new ActionEventHandler<string>(_ => throw new InvalidOperationException("boom"));

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => sut.HandleAsync(string.Empty));
    }
}

/// <summary>
/// <see cref="EventHandlerDisposeWrapper"/> 测试。
/// 验证 EventHandler 属性引用及 Dispose 的委托行为。
/// </summary>
public class EventHandlerDisposeWrapperTest
{
    /// <summary>
    /// 测试目的：EventHandler 属性应引用构造时传入的处理器。
    /// </summary>
    [Fact]
    public void EventHandler_ShouldReferenceInjectedHandler()
    {
        // Arrange
        var mockHandler = new Mock<IEventHandler>();
        var wrapper = new EventHandlerDisposeWrapper(mockHandler.Object);

        // Act & Assert
        wrapper.EventHandler.ShouldBeSameAs(mockHandler.Object);
    }

    /// <summary>
    /// 测试目的：Dispose 应调用传入的 disposeAction 委托一次。
    /// </summary>
    [Fact]
    public void Dispose_WithDisposeAction_ShouldInvokeActionOnce()
    {
        // Arrange
        var callCount = 0;
        var mockHandler = new Mock<IEventHandler>();
        var wrapper = new EventHandlerDisposeWrapper(mockHandler.Object, () => callCount++);

        // Act
        wrapper.Dispose();

        // Assert
        callCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：Dispose 在 disposeAction 为 null 时不应抛出异常。
    /// </summary>
    [Fact]
    public void Dispose_WithNullDisposeAction_ShouldNotThrow()
    {
        // Arrange
        var mockHandler = new Mock<IEventHandler>();
        var wrapper = new EventHandlerDisposeWrapper(mockHandler.Object, null);

        // Act & Assert
        Should.NotThrow(() => wrapper.Dispose());
    }

    /// <summary>
    /// 测试目的：多次 Dispose 时，disposeAction 应被调用多次（无幂等保护）。
    /// </summary>
    [Fact]
    public void Dispose_CalledTwice_ShouldInvokeActionTwice()
    {
        // Arrange
        var callCount = 0;
        var mockHandler = new Mock<IEventHandler>();
        var wrapper = new EventHandlerDisposeWrapper(mockHandler.Object, () => callCount++);

        // Act
        wrapper.Dispose();
        wrapper.Dispose();

        // Assert
        callCount.ShouldBe(2);
    }
}

/// <summary>
/// <see cref="EventHandlerFactoryUnregistrar"/> 测试。
/// 验证 Dispose 时调用 IEventBus.Unsubscribe 并传递正确参数。
/// </summary>
public class EventHandlerFactoryUnregistrarTest
{
    /// <summary>
    /// 测试目的：Dispose 应调用 IEventBus.Unsubscribe，参数与构造时一致。
    /// </summary>
    [Fact]
    public void Dispose_ShouldCallUnsubscribeWithCorrectArguments()
    {
        // Arrange
        var mockBus = new Mock<IEventBus>();
        var mockFactory = new Mock<IEventHandlerFactory>();
        var eventType = typeof(string);
        var unregistrar = new EventHandlerFactoryUnregistrar(mockBus.Object, eventType, mockFactory.Object);

        // Act
        unregistrar.Dispose();

        // Assert
        mockBus.Verify(b => b.Unsubscribe(eventType, mockFactory.Object), Times.Once);
    }

    /// <summary>
    /// 测试目的：Dispose 调用时传递的 eventType 应精确匹配。
    /// </summary>
    [Fact]
    public void Dispose_ShouldPassExactEventTypeToUnsubscribe()
    {
        // Arrange
        var mockBus = new Mock<IEventBus>();
        var mockFactory = new Mock<IEventHandlerFactory>();
        Type capturedType = null;
        mockBus.Setup(b => b.Unsubscribe(It.IsAny<Type>(), It.IsAny<IEventHandlerFactory>()))
               .Callback<Type, IEventHandlerFactory>((t, _) => capturedType = t);

        var unregistrar = new EventHandlerFactoryUnregistrar(mockBus.Object, typeof(int), mockFactory.Object);

        // Act
        unregistrar.Dispose();

        // Assert
        capturedType.ShouldBe(typeof(int));
    }

    /// <summary>
    /// 测试目的：using 语句结束时应自动调用 Unsubscribe，验证与 using 的协作。
    /// </summary>
    [Fact]
    public void Dispose_ViaUsingStatement_ShouldCallUnsubscribe()
    {
        // Arrange
        var mockBus = new Mock<IEventBus>();
        var mockFactory = new Mock<IEventHandlerFactory>();

        // Act
        using (new EventHandlerFactoryUnregistrar(mockBus.Object, typeof(bool), mockFactory.Object))
        {
            // 使用范围结束即触发 Dispose
        }

        // Assert
        mockBus.Verify(b => b.Unsubscribe(typeof(bool), mockFactory.Object), Times.Once);
    }
}
