using Bing.Data.Transaction;
using Bing.Events.Cap;
using Bing.Events.Messages;
using DotNetCore.CAP;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.EventBus.Tests.Cap;

/// <summary>
/// 验证业务事件元数据与 CAP 发布路径。
/// </summary>
public class ClusterEventHeadersTest
{
    /// <summary>
    /// 有序消息元数据通过 CAP 消息头完整传递。
    /// </summary>
    [Fact]
    public async Task PublishAsync_OrderedEvent_ShouldWriteMetadataOnce()
    {
        var publisher = new Mock<ICapPublisher>();
        IDictionary<string, string> publishedHeaders = null;
        publisher.Setup(x => x.PublishAsync("stock.changed", It.IsAny<object>(),
                It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, object, IDictionary<string, string>, CancellationToken>((_, _, headers, _) =>
                publishedHeaders = new Dictionary<string, string>(headers))
            .Returns(Task.CompletedTask);
        var bus = new MessageEventBus(publisher.Object, Mock.Of<ITransactionActionManager>(),
            NullLogger<MessageEventBus>.Instance);
        var message = new Bing.Events.Messages.MessageEvent
        {
            Id = "event-1", Name = "stock.changed", Data = 1, Send = true,
            TenantId = "tenant-1", Stream = "inventory", EntityKey = "sku-1", Sequence = 3,
            ContractVersion = "v1", TraceId = "trace-1"
        };

        await bus.PublishAsync(message);

        publishedHeaders.ShouldNotBeNull();
        var restored = ClusterEventHeaders.Read(new Dictionary<string, string>(publishedHeaders));
        restored.EventId.ShouldBe("event-1");
        restored.TenantId.ShouldBe("tenant-1");
        restored.Stream.ShouldBe("inventory");
        restored.EntityKey.ShouldBe("sku-1");
        restored.Sequence.ShouldBe(3);
        restored.ContractVersion.ShouldBe("v1");
        restored.TraceId.ShouldBe("trace-1");
        publisher.Verify(x => x.PublishAsync("stock.changed", It.IsAny<object>(),
            It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 旧消息缺少业务头时保持可解析，并拒绝无效有序元数据。
    /// </summary>
    [Fact]
    public void Read_LegacyAndInvalidOrderedHeaders_ShouldHandleExplicitly()
    {
        ClusterEventHeaders.Read(new Dictionary<string, string>()).ShouldBeNull();
        var invalid = new Dictionary<string, string>
        {
            [ClusterEventHeaders.EventId] = "event-1",
            [ClusterEventHeaders.Sequence] = "3"
        };
        Should.Throw<FormatException>(() => ClusterEventHeaders.Read(invalid));
        var incomplete = new Bing.Events.Messages.MessageEvent { Id = "event-1", Sequence = 1 };
        Should.Throw<ArgumentException>(() => ClusterEventHeaders.Write(new Dictionary<string, string>(), incomplete));
    }

    /// <summary>
    /// 验证发布元数据超过模型边界时立即拒绝。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Write_WhenMetadataExceedsLimit_ShouldReject(int field)
    {
        var message = new Bing.Events.Messages.MessageEvent { Id = "event-1" };
        switch (field)
        {
            case 0: message.Id = new string('i', 129); break;
            case 1: message.TenantId = new string('t', 201); break;
            case 2: message.Stream = new string('s', 201); break;
            case 3: message.EntityKey = new string('e', 201); break;
            case 4: message.ContractVersion = new string('v', 101); break;
            default: message.TraceId = new string('x', 129); break;
        }

        Should.Throw<ArgumentException>(() => ClusterEventHeaders.Write(
            new Dictionary<string, string>(), message));
    }

    /// <summary>
    /// 验证远端消息头超过模型边界时拒绝解析。
    /// </summary>
    [Theory]
    [InlineData(ClusterEventHeaders.EventId, 129)]
    [InlineData(ClusterEventHeaders.TenantId, 201)]
    [InlineData(ClusterEventHeaders.Stream, 201)]
    [InlineData(ClusterEventHeaders.EntityKey, 201)]
    [InlineData(ClusterEventHeaders.ContractVersion, 101)]
    [InlineData(ClusterEventHeaders.TraceId, 129)]
    [InlineData(ClusterEventHeaders.Sequence, 21)]
    public void Read_WhenMetadataExceedsLimit_ShouldReject(string header, int length)
    {
        var headers = new Dictionary<string, string>
        {
            [ClusterEventHeaders.EventId] = "event-1",
            [header] = new string('x', length)
        };

        Should.Throw<FormatException>(() => ClusterEventHeaders.Read(headers));
    }

    /// <summary>
    /// 验证远端消息头集合超过总字节上限时拒绝解析。
    /// </summary>
    [Fact]
    public void Read_WhenHeaderCollectionExceedsByteLimit_ShouldReject()
    {
        var headers = new Dictionary<string, string>
        {
            [ClusterEventHeaders.EventId] = "event-1"
        };
        for (var index = 0; index < 700; index++)
            headers[$"x-extra-{index:D4}"] = new string('x', 32);

        var exception = Should.Throw<FormatException>(() => ClusterEventHeaders.Read(headers));

        exception.Message.ShouldContain("UTF-8 字节");
    }

    /// <summary>
    /// 验证本地发布的消息头集合超过总字节上限时拒绝写入。
    /// </summary>
    [Fact]
    public void Write_WhenHeaderCollectionExceedsByteLimit_ShouldReject()
    {
        var headers = new Dictionary<string, string>
        {
            ["x-existing"] = "existing"
        };
        for (var index = 0; index < 700; index++)
            headers[$"x-extra-{index:D4}"] = new string('x', 32);

        var exception = Should.Throw<ArgumentException>(() => ClusterEventHeaders.Write(
            headers, new Bing.Events.Messages.MessageEvent { Id = "event-1" }));

        exception.Message.ShouldContain("UTF-8 字节");
    }
}
