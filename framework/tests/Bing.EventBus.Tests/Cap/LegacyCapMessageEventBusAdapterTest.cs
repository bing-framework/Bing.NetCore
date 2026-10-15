using Bing.Events.EventBusAdapter;
using Bing.Data.Transaction;
using Bing.Events.Cap;
using CurrentMessageEvent = Bing.Events.Messages.IMessageEvent;
using DotNetCore.CAP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.EventBus.Tests.Cap;

/// <summary>
/// 验证旧消息接口显式转发到当前 CAP 入口。
/// </summary>
public class LegacyCapMessageEventBusAdapterTest
{
    /// <summary>
    /// 带标识的旧事件只被发布一次。
    /// </summary>
    [Fact]
    public async Task PublishAsync_LegacyEvent_ShouldPreserveIdAndPublishOnce()
    {
        var bus = new Mock<Bing.Events.Messages.IMessageEventBus>();
        CurrentMessageEvent published = null;
        bus.Setup(x => x.PublishAsync(It.IsAny<CurrentMessageEvent>(), It.IsAny<CancellationToken>()))
            .Callback<CurrentMessageEvent, CancellationToken>((message, _) => published = message)
            .Returns(Task.CompletedTask);
        var adapter = new LegacyCapMessageEventBusAdapter(bus.Object);
        var old = new Bing.EventBus.MessageEvent { Id = "event-1", Name = "stock.changed", Data = new { Count = 1 } };

        await adapter.PublishAsync(old);

        published.Id.ShouldBe("event-1");
        published.Name.ShouldBe("stock.changed");
        bus.Verify(x => x.PublishAsync(It.IsAny<CurrentMessageEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 注册入口保持单个适配器并尊重已有实现。
    /// </summary>
    [Fact]
    public void AddLegacyCapMessageEventBusAdapter_ShouldBeExplicitAndIdempotent()
    {
        var services = new ServiceCollection();
        services.AddLegacyCapMessageEventBusAdapter();
        services.AddLegacyCapMessageEventBusAdapter();

        services.Count(x => x.ServiceType == typeof(Bing.EventBus.IMessageEventBus)).ShouldBe(1);
        services.Single(x => x.ServiceType == typeof(Bing.EventBus.IMessageEventBus))
            .ImplementationType.ShouldBe(typeof(LegacyCapMessageEventBusAdapter));
    }

    /// <summary>
    /// 旧接口的立即发送仅调用一次实际 CAP 发布器。
    /// </summary>
    [Fact]
    public async Task PublishAsync_LegacyImmediateEvent_ShouldPublishToCapOnce()
    {
        var publisher = new Mock<ICapPublisher>();
        IDictionary<string, string> headers = null;
        publisher.Setup(x => x.PublishAsync("stock.changed", It.IsAny<object>(),
                It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, object, IDictionary<string, string>, CancellationToken>((_, _, value, _) =>
                headers = new Dictionary<string, string>(value))
            .Returns(Task.CompletedTask);
        var manager = new Mock<ITransactionActionManager>();
        var current = new MessageEventBus(publisher.Object, manager.Object,
            NullLogger<MessageEventBus>.Instance);
        var adapter = new LegacyCapMessageEventBusAdapter(current);

        await adapter.PublishAsync(new Bing.EventBus.MessageEvent
            { Id = "legacy-1", Name = "stock.changed", Data = 1, Send = true });

        headers.ShouldNotBeNull();
        ClusterEventHeaders.Read(new Dictionary<string, string>(headers)).EventId.ShouldBe("legacy-1");
        publisher.Verify(x => x.PublishAsync("stock.changed", It.IsAny<object>(),
            It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(x => x.Register(It.IsAny<Func<System.Data.IDbTransaction, Task>>()), Times.Never);
    }

    /// <summary>
    /// 旧接口的延后发送只登记一次，并在事务操作执行时发布。
    /// </summary>
    [Fact]
    public async Task PublishAsync_LegacyDeferredEvent_ShouldWaitForTransactionAction()
    {
        var publisher = new Mock<ICapPublisher>();
        publisher.Setup(x => x.PublishAsync("stock.changed", It.IsAny<object>(),
                It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Func<System.Data.IDbTransaction, Task> action = null;
        var manager = new Mock<ITransactionActionManager>();
        manager.Setup(x => x.Register(It.IsAny<Func<System.Data.IDbTransaction, Task>>()))
            .Callback<Func<System.Data.IDbTransaction, Task>>(value => action = value);
        var current = new MessageEventBus(publisher.Object, manager.Object,
            NullLogger<MessageEventBus>.Instance);
        var adapter = new LegacyCapMessageEventBusAdapter(current);

        await adapter.PublishAsync(new Bing.EventBus.MessageEvent
            { Id = "legacy-2", Name = "stock.changed", Data = 1 });

        action.ShouldNotBeNull();
        publisher.Verify(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<object>(),
            It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
        await action(null);
        publisher.Verify(x => x.PublishAsync("stock.changed", It.IsAny<object>(),
            It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(x => x.Register(It.IsAny<Func<System.Data.IDbTransaction, Task>>()), Times.Once);
    }

    /// <summary>
    /// 显式注册不覆盖调用方提供的旧接口实现。
    /// </summary>
    [Fact]
    public void AddLegacyCapMessageEventBusAdapter_ExistingBus_ShouldRemainSelected()
    {
        var services = new ServiceCollection();
        var existing = Mock.Of<Bing.EventBus.IMessageEventBus>();
        services.AddSingleton(existing);

        services.AddLegacyCapMessageEventBusAdapter();

        services.Single(x => x.ServiceType == typeof(Bing.EventBus.IMessageEventBus))
            .ImplementationInstance.ShouldBe(existing);
    }
}
