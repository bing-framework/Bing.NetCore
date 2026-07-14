using System.Data;
using Bing.Data.Transaction;
using Bing.Events.Cap;
using Bing.Logging;
using Bing.Tracing;
using DotNetCore.CAP;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.Events.Tests.Cap;

/// <summary>
/// <see cref="MessageEventBus"/> 单元测试
/// </summary>
public class MessageEventBusTest
{
    /// <summary>
    /// 测试目的：事务延迟发布应使用注册回调时捕获的日志上下文，而不是提交时的环境上下文。
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenDeferred_ShouldKeepCapturedSnapshot()
    {
        // Arrange
        var publisher = new Mock<ICapPublisher>();
        IDictionary<string, string> publishedHeaders = null;
        publisher
            .Setup(x => x.PublishAsync<object>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object, IDictionary<string, string>, CancellationToken>((_, _, headers, _) => publishedHeaders = headers)
            .Returns(Task.CompletedTask);
        var transactionManager = new Mock<ITransactionActionManager>();
        Func<IDbTransaction, Task> deferredAction = null;
        transactionManager.Setup(x => x.Register(It.IsAny<Func<IDbTransaction, Task>>()))
            .Callback<Func<IDbTransaction, Task>>(action => deferredAction = action);
        var provider = new DefaultCorrelationIdProvider();
        var accessor = new LogContextAccessor(provider);
        var eventBus = new MessageEventBus(
            publisher.Object,
            transactionManager.Object,
            NullLogger<MessageEventBus>.Instance,
            accessor);
        var captured = new LogContextSnapshot(
            "captured-trace",
            new LogIdentityContext(userId: "captured-user"));
        var later = new LogContextSnapshot(
            "later-trace",
            new LogIdentityContext(userId: "later-user"));

        // Act
        using (accessor.BeginScope(captured))
            await eventBus.PublishAsync("test", new object());
        using (accessor.BeginScope(later))
            await deferredAction(null);

        // Assert
        publishedHeaders.ShouldNotBeNull();
        publishedHeaders[Headers.TraceId].ShouldBe("captured-trace");
        publishedHeaders[Headers.UserId].ShouldBe("captured-user");
    }
}