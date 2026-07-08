using Bing.EventBus;
using Bing.EventBus.Local;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.EventBus.Tests;

/// <summary>
/// <see cref="NullLocalEventBus"/> 单元测试
/// 验证空本地事件总线的所有方法均为空操作：不抛异常，Subscribe 返回 NullDisposable
/// </summary>
public class NullLocalEventBusTest
{
    private readonly ILocalEventBus _bus = NullLocalEventBus.Instance;

    /// <summary>
    /// 测试目的：NullLocalEventBus.Instance 为静态单例，不应为 null。
    /// </summary>
    [Fact]
    public void Instance_ShouldNotBeNull()
    {
        NullLocalEventBus.Instance.ShouldNotBeNull();
    }

    /// <summary>
    /// 测试目的：多次访问 Instance 应返回同一引用（单例语义）。
    /// </summary>
    [Fact]
    public void Instance_ShouldBeSameReference()
    {
        NullLocalEventBus.Instance.ShouldBeSameAs(NullLocalEventBus.Instance);
    }

    /// <summary>
    /// 测试目的：PublishAsync&lt;TEvent&gt; 应返回 CompletedTask，不抛异常。
    /// </summary>
    [Fact]
    public async Task PublishAsync_Generic_ShouldCompleteWithoutThrowing()
    {
        await Should.NotThrowAsync(() => _bus.PublishAsync(new SampleEvent()));
    }

    /// <summary>
    /// 测试目的：PublishAsync(Type, object) 应返回 CompletedTask，不抛异常。
    /// </summary>
    [Fact]
    public async Task PublishAsync_ByType_ShouldCompleteWithoutThrowing()
    {
        await Should.NotThrowAsync(() => _bus.PublishAsync(typeof(SampleEvent), new SampleEvent()));
    }

    /// <summary>
    /// 测试目的：Subscribe(Func) 应返回非 null 的 IDisposable，且 Dispose 不抛异常。
    /// </summary>
    [Fact]
    public void Subscribe_WithAction_ShouldReturnDisposable()
    {
        var disposable = _bus.Subscribe<SampleEvent>(_ => Task.CompletedTask);
        disposable.ShouldNotBeNull();
        Should.NotThrow(() => disposable.Dispose());
    }

    /// <summary>
    /// 测试目的：Subscribe&lt;TEvent&gt;(ILocalEventHandler) 应返回非 null 的 IDisposable。
    /// </summary>
    [Fact]
    public void Subscribe_WithHandler_ShouldReturnDisposable()
    {
        var mockHandler = new Mock<ILocalEventHandler<SampleEvent>>();
        var disposable = _bus.Subscribe(mockHandler.Object);
        disposable.ShouldNotBeNull();
    }

    /// <summary>
    /// 测试目的：Subscribe&lt;TEvent, THandler&gt;() 应返回非 null 的 IDisposable。
    /// </summary>
    [Fact]
    public void Subscribe_GenericHandler_ShouldReturnDisposable()
    {
        var disposable = _bus.Subscribe<SampleEvent, SampleEventHandler>();
        disposable.ShouldNotBeNull();
    }

    /// <summary>
    /// 测试目的：Subscribe(Type, IEventHandler) 应返回非 null 的 IDisposable。
    /// </summary>
    [Fact]
    public void Subscribe_ByType_ShouldReturnDisposable()
    {
        var mockHandler = new Mock<IEventHandler>();
        var disposable = _bus.Subscribe(typeof(SampleEvent), mockHandler.Object);
        disposable.ShouldNotBeNull();
    }

    /// <summary>
    /// 测试目的：Subscribe(Type, IEventHandlerFactory) 应返回非 null 的 IDisposable。
    /// </summary>
    [Fact]
    public void Subscribe_ByTypeAndFactory_ShouldReturnDisposable()
    {
        var mockFactory = new Mock<IEventHandlerFactory>();
        var disposable = _bus.Subscribe(typeof(SampleEvent), mockFactory.Object);
        disposable.ShouldNotBeNull();
    }

    /// <summary>
    /// 测试目的：所有 Unsubscribe 重载均不应抛异常（空操作）。
    /// </summary>
    [Fact]
    public void Unsubscribe_AllOverloads_ShouldNotThrow()
    {
        var mockHandler = new Mock<ILocalEventHandler<SampleEvent>>();
        var mockEventHandler = new Mock<IEventHandler>();
        var mockFactory = new Mock<IEventHandlerFactory>();

        Should.NotThrow(() => _bus.Unsubscribe<SampleEvent>(_ => Task.CompletedTask));
        Should.NotThrow(() => _bus.Unsubscribe(mockHandler.Object));
        Should.NotThrow(() => _bus.Unsubscribe(typeof(SampleEvent), mockEventHandler.Object));
        Should.NotThrow(() => _bus.Unsubscribe<SampleEvent>(mockFactory.Object));
        Should.NotThrow(() => _bus.Unsubscribe(typeof(SampleEvent), mockFactory.Object));
    }

    /// <summary>
    /// 测试目的：所有 UnsubscribeAll 重载均不应抛异常（空操作）。
    /// </summary>
    [Fact]
    public void UnsubscribeAll_AllOverloads_ShouldNotThrow()
    {
        Should.NotThrow(() => _bus.UnsubscribeAll<SampleEvent>());
        Should.NotThrow(() => _bus.UnsubscribeAll(typeof(SampleEvent)));
    }
}

/// <summary>
/// <see cref="SingleInstanceHandlerFactory"/> 单元测试
/// 验证单例工厂每次 GetHandler 都包装同一个 handler 实例；IsInFactories 判断逻辑
/// </summary>
public class SingleInstanceHandlerFactoryTest
{
    private readonly Mock<IEventHandler> _mockHandler = new();

    /// <summary>
    /// 测试目的：HandlerInstance 应与构造时传入的 handler 引用相等。
    /// </summary>
    [Fact]
    public void HandlerInstance_ShouldBeTheSameAsConstructorArg()
    {
        var factory = new SingleInstanceHandlerFactory(_mockHandler.Object);
        factory.HandlerInstance.ShouldBeSameAs(_mockHandler.Object);
    }

    /// <summary>
    /// 测试目的：GetHandler 应返回包含 HandlerInstance 的 IEventHandlerDisposeWrapper，不为 null。
    /// </summary>
    [Fact]
    public void GetHandler_ShouldReturnWrapperContainingHandlerInstance()
    {
        var factory = new SingleInstanceHandlerFactory(_mockHandler.Object);
        using var wrapper = factory.GetHandler();
        wrapper.ShouldNotBeNull();
        wrapper.EventHandler.ShouldBeSameAs(_mockHandler.Object);
    }

    /// <summary>
    /// 测试目的：IsInFactories 对包含相同 handler 实例的工厂列表，应返回 true。
    /// </summary>
    [Fact]
    public void IsInFactories_WhenSameHandlerInList_ShouldReturnTrue()
    {
        var factory = new SingleInstanceHandlerFactory(_mockHandler.Object);
        var list = new List<IEventHandlerFactory> { factory };
        factory.IsInFactories(list).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：IsInFactories 对包含不同 handler 实例的工厂列表，应返回 false。
    /// </summary>
    [Fact]
    public void IsInFactories_WhenDifferentHandlerInList_ShouldReturnFalse()
    {
        var factory = new SingleInstanceHandlerFactory(_mockHandler.Object);
        var other = new SingleInstanceHandlerFactory(new Mock<IEventHandler>().Object);
        var list = new List<IEventHandlerFactory> { other };
        factory.IsInFactories(list).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：IsInFactories 对空列表，应返回 false。
    /// </summary>
    [Fact]
    public void IsInFactories_EmptyList_ShouldReturnFalse()
    {
        var factory = new SingleInstanceHandlerFactory(_mockHandler.Object);
        factory.IsInFactories(new List<IEventHandlerFactory>()).ShouldBeFalse();
    }
}

/// <summary>
/// <see cref="TransientEventHandlerFactory"/> 单元测试
/// 验证瞬时工厂每次 GetHandler 创建新实例；IsInFactories 按类型匹配
/// </summary>
public class TransientEventHandlerFactoryTest
{
    /// <summary>
    /// 测试目的：HandlerType 应与构造时传入的类型一致。
    /// </summary>
    [Fact]
    public void HandlerType_ShouldMatchConstructorArg()
    {
        var factory = new TransientEventHandlerFactory(typeof(SampleEventHandler));
        factory.HandlerType.ShouldBe(typeof(SampleEventHandler));
    }

    /// <summary>
    /// 测试目的：GetHandler 应返回非 null 的 IEventHandlerDisposeWrapper，
    /// 内部 EventHandler 应为 SampleEventHandler 实例。
    /// </summary>
    [Fact]
    public void GetHandler_ShouldReturnWrapperWithNewHandlerInstance()
    {
        var factory = new TransientEventHandlerFactory(typeof(SampleEventHandler));
        using var wrapper = factory.GetHandler();
        wrapper.ShouldNotBeNull();
        wrapper.EventHandler.ShouldBeOfType<SampleEventHandler>();
    }

    /// <summary>
    /// 测试目的：两次 GetHandler 应返回不同的 handler 实例（Transient 语义）。
    /// </summary>
    [Fact]
    public void GetHandler_CalledTwice_ShouldReturnDifferentInstances()
    {
        var factory = new TransientEventHandlerFactory(typeof(SampleEventHandler));
        using var w1 = factory.GetHandler();
        using var w2 = factory.GetHandler();
        w1.EventHandler.ShouldNotBeSameAs(w2.EventHandler);
    }

    /// <summary>
    /// 测试目的：泛型工厂 TransientEventHandlerFactory&lt;THandler&gt; 的 GetHandler 应创建 THandler 实例。
    /// </summary>
    [Fact]
    public void GenericFactory_GetHandler_ShouldReturnCorrectHandlerType()
    {
        var factory = new TransientEventHandlerFactory<SampleEventHandler>();
        using var wrapper = factory.GetHandler();
        wrapper.EventHandler.ShouldBeOfType<SampleEventHandler>();
    }

    /// <summary>
    /// 测试目的：IsInFactories 对包含相同 HandlerType 的工厂列表，应返回 true。
    /// </summary>
    [Fact]
    public void IsInFactories_WhenSameHandlerTypeInList_ShouldReturnTrue()
    {
        var factory = new TransientEventHandlerFactory(typeof(SampleEventHandler));
        var list = new List<IEventHandlerFactory> { factory };
        factory.IsInFactories(list).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：IsInFactories 对包含不同 HandlerType 的工厂列表，应返回 false。
    /// </summary>
    [Fact]
    public void IsInFactories_WhenDifferentHandlerTypeInList_ShouldReturnFalse()
    {
        var factory = new TransientEventHandlerFactory(typeof(SampleEventHandler));
        var other = new TransientEventHandlerFactory(typeof(OtherEventHandler));
        var list = new List<IEventHandlerFactory> { other };
        factory.IsInFactories(list).ShouldBeFalse();
    }
}

/// <summary>
/// <see cref="MessageEvent"/> 单元测试
/// 验证默认值、属性赋值、ToString 输出格式
/// </summary>
public class MessageEventTest
{
    /// <summary>
    /// 测试目的：构造后 Id 应自动生成，不为 null/empty。
    /// </summary>
    [Fact]
    public void Constructor_ShouldAutoGenerateId()
    {
        var evt = new MessageEvent();
        evt.Id.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// 测试目的：构造后 Time 应接近当前时间（误差在 2 秒内）。
    /// </summary>
    [Fact]
    public void Constructor_ShouldSetTimeToNow()
    {
        var before = DateTime.Now.AddSeconds(-1);
        var evt = new MessageEvent();
        var after = DateTime.Now.AddSeconds(1);
        evt.Time.ShouldBeInRange(before, after);
    }

    /// <summary>
    /// 测试目的：两次构造的 Id 应不同（基于 Guid.NewGuid）。
    /// </summary>
    [Fact]
    public void Constructor_TwoInstances_ShouldHaveDifferentIds()
    {
        var e1 = new MessageEvent();
        var e2 = new MessageEvent();
        e1.Id.ShouldNotBe(e2.Id);
    }

    /// <summary>
    /// 测试目的：Name/Data/Callback/Send 属性应可正常读写。
    /// </summary>
    [Fact]
    public void Properties_ShouldBeReadWritable()
    {
        var evt = new MessageEvent
        {
            Name = "TestEvent",
            Data = new { Key = "value" },
            Callback = "callback-name",
            Send = true
        };
        evt.Name.ShouldBe("TestEvent");
        evt.Callback.ShouldBe("callback-name");
        evt.Send.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：ToString 应包含 Id 和时间信息，不抛异常。
    /// </summary>
    [Fact]
    public void ToString_ShouldContainIdAndTime()
    {
        var evt = new MessageEvent { Id = "test-id-123" };
        var str = evt.ToString();
        str.ShouldContain("test-id-123");
    }

    /// <summary>
    /// 测试目的：ToString 在设置 Name 时应包含 Name 信息。
    /// </summary>
    [Fact]
    public void ToString_WithName_ShouldContainName()
    {
        var evt = new MessageEvent { Name = "my-event" };
        evt.ToString().ShouldContain("my-event");
    }

    /// <summary>
    /// 测试目的：ToString 在 Name 为 null/空时不包含消息名称行，不抛异常。
    /// </summary>
    [Fact]
    public void ToString_WithoutName_ShouldNotThrow()
    {
        var evt = new MessageEvent { Name = null };
        Should.NotThrow(() => evt.ToString());
    }
}

// ─── 测试辅助类型 ──────────────────────────────────────────────────────────────

internal class SampleEvent { }

internal class SampleEventHandler : IEventHandler<SampleEvent>, ILocalEventHandler<SampleEvent>
{
    public Task HandleAsync(SampleEvent eventData) => Task.CompletedTask;
}

internal class OtherEventHandler : IEventHandler<SampleEvent>, ILocalEventHandler<SampleEvent>
{
    public Task HandleAsync(SampleEvent eventData) => Task.CompletedTask;
}
