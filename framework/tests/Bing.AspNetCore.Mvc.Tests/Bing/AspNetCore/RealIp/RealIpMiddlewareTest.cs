using System.Net;
using Bing.AspNetCore.RealIp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bing.AspNetCore.Mvc.Tests.Bing.AspNetCore.RealIp;

/// <summary>
/// 验证真实 IP 中间件的可信代理边界。
/// </summary>
public sealed class RealIpMiddlewareTest
{
    /// <summary>
    /// 未配置可信代理时忽略伪造的转发头。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenRemoteProxyIsUntrusted_ShouldKeepConnectionAddress()
    {
        var context = CreateContext("10.0.0.5", "203.0.113.10");
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        Assert.Equal(IPAddress.Parse("10.0.0.5"), context.Connection.RemoteIpAddress);
    }

    /// <summary>
    /// 可信代理提交转发头时使用链中最右侧的非可信地址。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenRemoteProxyIsTrusted_ShouldUseRightmostUntrustedAddress()
    {
        var context = CreateContext("10.0.0.5", "198.51.100.20, 10.0.0.6, 203.0.113.10");
        var middleware = CreateMiddleware("10.0.0.5", "10.0.0.6");

        await middleware.InvokeAsync(context);

        Assert.Equal(IPAddress.Parse("203.0.113.10"), context.Connection.RemoteIpAddress);
    }

    /// <summary>
    /// 转发链只包含可信代理地址时不应伪造客户端地址。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenForwardedChainHasNoClientAddress_ShouldKeepConnectionAddress()
    {
        var context = CreateContext("10.0.0.5", "10.0.0.6");
        var middleware = CreateMiddleware("10.0.0.5", "10.0.0.6");

        await middleware.InvokeAsync(context);

        Assert.Equal(IPAddress.Parse("10.0.0.5"), context.Connection.RemoteIpAddress);
    }

    /// <summary>
    /// 无效的可信代理配置应在构造中失败。
    /// </summary>
    [Fact]
    public void Constructor_WhenTrustedProxyIsInvalid_ShouldThrow()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateMiddleware("not-an-ip"));

        Assert.Contains("可信代理地址无效", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 不同 IPv6 地址不能因为映射比较而被当作同一代理。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenUnrelatedIpv6AddressesAreConfigured_ShouldKeepConnectionAddress()
    {
        var context = CreateContext("2001:db8::1", "203.0.113.10");
        var middleware = CreateMiddleware("2001:db8::2");

        await middleware.InvokeAsync(context);

        Assert.Equal(IPAddress.Parse("2001:db8::1"), context.Connection.RemoteIpAddress);
    }

    /// <summary>
    /// 转发头包含非法内容时，诊断日志不应回显原始请求头值。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenForwardedHeaderContainsInvalidValue_ShouldNotLogHeaderValue()
    {
        var logger = new RecordingLogger();
        var options = new RealIpOptions();
        options.TrustedProxies.Add("10.0.0.5");
        var middleware = new RealIpMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(options), logger);
        var context = CreateContext("10.0.0.5", "secret-token, 203.0.113.10");

        await middleware.InvokeAsync(context);

        Assert.DoesNotContain(logger.Messages, message => message.Contains("secret-token", StringComparison.Ordinal));
    }

    /// <summary>
    /// 创建使用指定可信代理的真实 IP 中间件。
    /// </summary>
    /// <param name="trustedProxies">可信代理地址。</param>
    /// <returns>配置完成的中间件。</returns>
    private static RealIpMiddleware CreateMiddleware(params string[] trustedProxies)
    {
        var options = new RealIpOptions();
        foreach (var proxy in trustedProxies)
            options.TrustedProxies.Add(proxy);
        return new RealIpMiddleware(_ => Task.CompletedTask, Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<RealIpMiddleware>.Instance);
    }

    /// <summary>
    /// 创建带有远端地址和转发头的 HTTP 上下文。
    /// </summary>
    /// <param name="remoteIp">连接远端地址。</param>
    /// <param name="forwardedFor">转发地址头内容。</param>
    /// <returns>配置完成的 HTTP 上下文。</returns>
    private static DefaultHttpContext CreateContext(string remoteIp, string forwardedFor)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        return context;
    }

    /// <summary>
    /// 记录中间件诊断消息以验证请求头不会被回显。
    /// </summary>
    private sealed class RecordingLogger : ILogger<RealIpMiddleware>
    {
        /// <summary>
        /// 已记录的日志文本。
        /// </summary>
        public List<string> Messages { get; } = new List<string>();

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        /// <summary>
        /// 不执行任何操作的日志作用域。
        /// </summary>
        private sealed class NullScope : IDisposable
        {
            /// <summary>
            /// 共享实例。
            /// </summary>
            public static readonly NullScope Instance = new NullScope();

            /// <inheritdoc />
            public void Dispose()
            {
            }
        }
    }
}
