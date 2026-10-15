using Bing.Events.Cap;
using Bing.Events.Messages;
using Bing.Events.Ordering;
using Bing.Data.Events.Ordering;
using DotNetCore.CAP;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 验证 CAP 有序入箱适配器的边界行为。
/// </summary>
public sealed class CapOrderedEventIngressTest
{
    /// <summary>
    /// 验证适配器保留 CAP 头和正文并提交到有序收件箱。
    /// </summary>
    [Fact]
    public async Task ReceiveAsync_ShouldPersistCapMetadataAndPayload()
    {
        using var database = new OrderingSqliteFixture();
        var stream = OrderingSqliteFixture.Stream();
        var coordinator = new OrderedEventCoordinator(database.CreateFactory(), database.CreateStore());
        var ingress = new CapOrderedEventIngress(coordinator);
        var message = new Bing.Events.Messages.MessageEvent
        {
            Id = "event-1",
            TenantId = stream.TenantId,
            Stream = stream.Stream,
            EntityKey = stream.EntityKey,
            Sequence = 1,
            ContractVersion = "v1"
        };
        var headers = new Dictionary<string, string>();
        ClusterEventHeaders.Write(headers, message);

        await ingress.ReceiveAsync("consumer", new CapHeader(headers), "{\"value\":1}");

        var state = await coordinator.GetStateAsync("consumer", stream);
        Assert.Equal(1, state.FirstPendingSequence);
        Assert.Equal(1, state.PendingCount);
        string payload = null;
        Assert.Equal(OrderedEventProcessingResult.Processed,
            await coordinator.ProcessNextAsync("consumer", stream, (message, _, _) =>
            {
                payload = message.Payload;
                return Task.CompletedTask;
            }));
        Assert.Equal("{\"value\":1}", payload);
    }

    /// <summary>
    /// 验证入箱入口拒绝首尾带空白的逻辑消费者。
    /// </summary>
    [Fact]
    public async Task ReceiveAsync_WhenConsumerHasBoundaryWhitespace_ShouldRejectBeforeOpeningTransaction()
    {
        using var database = new OrderingSqliteFixture();
        var coordinator = new OrderedEventCoordinator(database.CreateFactory(), database.CreateStore());
        var ingress = new CapOrderedEventIngress(coordinator);
        var headers = new Dictionary<string, string>
        {
            [ClusterEventHeaders.EventId] = "event-whitespace",
            [ClusterEventHeaders.TenantId] = "tenant",
            [ClusterEventHeaders.Stream] = "inventory",
            [ClusterEventHeaders.EntityKey] = "item",
            [ClusterEventHeaders.Sequence] = "1"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => ingress.ReceiveAsync("consumer ",
            new CapHeader(headers), "{}"));
        Assert.Equal(0, (await coordinator.GetStateAsync("consumer", OrderingSqliteFixture.Stream())).LastSequence);
    }

    /// <summary>
    /// 验证无效输入在打开入箱事务前被拒绝。
    /// </summary>
    [Fact]
    public async Task ReceiveAsync_WhenPayloadIsNull_ShouldRejectInput()
    {
        using var database = new OrderingSqliteFixture();
        var coordinator = new OrderedEventCoordinator(database.CreateFactory(), database.CreateStore());
        var ingress = new CapOrderedEventIngress(coordinator);

        await Assert.ThrowsAsync<ArgumentNullException>(() => ingress.ReceiveAsync(
            "consumer", new CapHeader(new Dictionary<string, string>()), null));
    }

    /// <summary>
    /// 验证有序消费者注册时自动提供入箱适配器且重复注册保持幂等。
    /// </summary>
    [Fact]
    public void AddBingCapOrderedEventIngress_ShouldRegisterOnce()
    {
        var services = new ServiceCollection();
        services.AddBingCapOrderedEventIngress();
        services.AddBingCapOrderedEventIngress();

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(CapOrderedEventIngress));
    }
}
