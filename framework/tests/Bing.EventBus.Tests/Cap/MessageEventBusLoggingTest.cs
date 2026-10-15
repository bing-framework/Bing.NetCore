using Bing.Data.Transaction;
using Bing.Events.Cap;
using DotNetCore.CAP;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Bing.EventBus.Tests.Cap;

/// <summary>
/// 验证 CAP 发布日志不会记录消息正文。
/// </summary>
public sealed class MessageEventBusLoggingTest
{
    /// <summary>
    /// Trace 日志只包含低敏元数据，不应包含事件正文。
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenTraceEnabled_ShouldNotLogEventPayload()
    {
        var publisher = new Mock<ICapPublisher>();
        publisher.Setup(x => x.PublishAsync("orders.created", It.IsAny<object>(),
                It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var logger = new CapturingLogger();
        var bus = new MessageEventBus(publisher.Object, Mock.Of<ITransactionActionManager>(), logger);
        var payload = new { Secret = "should-not-be-logged", Password = "token-value" };

        await bus.PublishAsync("orders.created", payload, send: true);

        Assert.Equal(2, logger.Messages.Count);
        Assert.All(logger.Messages, message =>
        {
            Assert.Contains("orders.created", message, StringComparison.Ordinal);
            Assert.DoesNotContain("should-not-be-logged", message, StringComparison.Ordinal);
            Assert.DoesNotContain("token-value", message, StringComparison.Ordinal);
            Assert.DoesNotContain("EventData", message, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// 捕获结构化日志文本的测试日志组件。
    /// </summary>
    private sealed class CapturingLogger : ILogger<MessageEventBus>
    {
        /// <summary>
        /// 记录已格式化日志。
        /// </summary>
        internal List<string> Messages { get; } = new List<string>();

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Trace;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception exception, Func<TState, Exception, string> formatter)
        {
            if (IsEnabled(logLevel))
                Messages.Add(formatter(state, exception));
        }

        /// <summary>
        /// 不持有外部状态的日志作用域。
        /// </summary>
        private sealed class NullScope : IDisposable
        {
            /// <summary>
            /// 共享的空作用域。
            /// </summary>
            internal static readonly NullScope Instance = new NullScope();

            /// <inheritdoc />
            public void Dispose()
            {
            }
        }
    }
}
