using System.Collections.Concurrent;
using System.Security.Claims;
using AspectCore.Extensions.Hosting;
using Bing.AspNetCore.Auditing;
using Bing.Auditing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bing.AspNetCore.Mvc.Tests.Bing.AspNetCore.Auditing;

/// <summary>
/// 验证请求管道中的审计作用域。
/// </summary>
public class BingHttpAuditingPipelineTest
{
    /// <summary>
    /// 开启作用域校验后，请求业务使用中间件创建的审计作用域。
    /// </summary>
    /// <param name="useAspectCore">是否使用 AspectCore 服务容器。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pipeline_WhenScopesAreValidated_ShouldShareRequestAudit(bool useAspectCore)
    {
        var store = new RecordingStore();
        using var host = CreateHost(store, useAspectCore, async context =>
        {
            var manager = context.RequestServices.GetRequiredService<IAuditingManager>();
            manager.Current.ShouldNotBeNull();
            manager.Current.Log.Actions.Add(new AuditLogActionInfo { MethodName = "request" });
            await context.Response.WriteAsync("ok");
        });

        await host.StartAsync();
        using var client = host.GetTestClient();
        using var response = await client.GetAsync("/orders?token=secret");

        response.EnsureSuccessStatusCode();
        store.Logs.ShouldHaveSingleItem().Actions.ShouldHaveSingleItem().MethodName.ShouldBe("request");
        await host.StopAsync();
    }

    /// <summary>
    /// 同时执行的请求各自保存独立审计作用域。
    /// </summary>
    /// <param name="useAspectCore">是否使用 AspectCore 服务容器。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pipeline_ConcurrentRequests_ShouldKeepAuditsIsolated(bool useAspectCore)
    {
        var store = new RecordingStore();
        var entered = 0;
        var bothEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var host = CreateHost(store, useAspectCore, async context =>
        {
            var manager = context.RequestServices.GetRequiredService<IAuditingManager>();
            var log = manager.Current.Log;
            log.Actions.Add(new AuditLogActionInfo { MethodName = context.Request.Path.Value });
            if (Interlocked.Increment(ref entered) == 2)
                bothEntered.TrySetResult(true);
            await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            manager.Current.Log.ShouldBeSameAs(log);
            await context.Response.WriteAsync("ok");
        });
        await host.StartAsync();
        using var client = host.GetTestClient();

        var responses = await Task.WhenAll(client.GetAsync("/one"), client.GetAsync("/two"));
        foreach (var response in responses)
        {
            using (response)
                response.EnsureSuccessStatusCode();
        }

        store.Logs.Count.ShouldBe(2);
        var logs = store.Logs.OrderBy(log => log.Url, StringComparer.Ordinal).ToArray();
        logs[0].ShouldNotBeSameAs(logs[1]);
        logs[0].Actions.ShouldHaveSingleItem().MethodName.ShouldBe("/one");
        logs[1].Actions.ShouldHaveSingleItem().MethodName.ShouldBe("/two");
        logs[0].UserId.ShouldBe("/one");
        logs[1].UserId.ShouldBe("/two");
        logs[0].TenantId.ShouldBe("trusted-tenant");
        await host.StopAsync();
    }

    /// <summary>
    /// 存储失败仍保留普通业务响应且日志不输出查询和异常秘密。
    /// </summary>
    [Fact]
    public async Task Pipeline_WhenStoreFails_ShouldLogOnlySafeMetadata()
    {
        var store = new RecordingStore { Failure = new InvalidOperationException("secret-store-value") };
        var logger = new RecordingLoggerProvider();
        using var host = CreateHost(store, false, context => context.Response.WriteAsync("ok"), logger);
        await host.StartAsync();
        using var client = host.GetTestClient();
        using var response = await client.GetAsync("/orders?token=secret-query&password=secret-password");

        response.EnsureSuccessStatusCode();
        store.Logs.ShouldHaveSingleItem().Url.ShouldBe("/orders");
        var message = logger.Messages.Single(text => text.Contains("ErrorType="));
        message.ShouldContain("InvalidOperationException");
        message.ShouldNotContain("secret-");
        message.ShouldNotContain("/orders");
        await host.StopAsync();
    }

    /// <summary>
    /// 真实请求管道不从后续已认证身份补齐缺失审计声明。
    /// </summary>
    /// <param name="useAspectCore">是否使用 AspectCore 服务容器。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pipeline_WhenSelectedIdentityLacksClaims_ShouldNotMixAuthenticatedIdentities(bool useAspectCore)
    {
        var store = new RecordingStore();
        using var host = CreateHost(store, useAspectCore, context => context.Response.WriteAsync("ok"),
            configureIdentity: context => context.User = new ClaimsPrincipal(new[]
            {
                new ClaimsIdentity(new[]
                {
                    new Claim("sub", "untrusted-user"), new Claim("tenant_id", "untrusted-tenant"),
                    new Claim(ClaimTypes.Name, "untrusted-name"), new Claim("client_id", "untrusted-client")
                }),
                new ClaimsIdentity(new[] { new Claim("tenant_id", "selected-tenant") }, "selected"),
                new ClaimsIdentity(new[]
                {
                    new Claim("sub", "later-user"), new Claim("tenant_id", "later-tenant"),
                    new Claim(ClaimTypes.Name, "later-name"), new Claim("client_id", "later-client")
                }, "later")
            }));
        await host.StartAsync();
        using var client = host.GetTestClient();
        using var response = await client.GetAsync("/orders");

        response.EnsureSuccessStatusCode();
        var audit = store.Logs.ShouldHaveSingleItem();
        audit.UserId.ShouldBeNull();
        audit.UserName.ShouldBeNull();
        audit.TenantId.ShouldBe("selected-tenant");
        audit.ClientId.ShouldBeNull();
        await host.StopAsync();
    }

    /// <summary>
    /// 创建验证实际请求作用域的宿主。
    /// </summary>
    /// <param name="store">保存请求审计结果的测试存储。</param>
    /// <param name="useAspectCore">是否使用 AspectCore 服务容器。</param>
    /// <param name="handler">审计中间件后的请求处理器。</param>
    /// <param name="logger">可选的测试日志提供程序。</param>
    /// <param name="configureIdentity">请求进入审计前设置身份的回调；为空时使用默认测试身份。</param>
    /// <returns>使用所选容器的未启动宿主。</returns>
    private static IHost CreateHost(RecordingStore store, bool useAspectCore, RequestDelegate handler,
        RecordingLoggerProvider logger = null, Action<HttpContext> configureIdentity = null)
    {
        var builder = new HostBuilder().UseDefaultServiceProvider(options => options.ValidateScopes = true);
        if (useAspectCore)
            builder.UseServiceContext();
        return builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                if (logger != null)
                    logging.AddProvider(logger);
            })
            .ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
                {
                    services.AddSingleton<IAuditingStore>(store);
                    services.AddBingHttpAuditing(options => options.UserIdClaimType = "sub");
                })
                .Configure(app =>
                {
                    app.Use((context, next) =>
                    {
                        if (configureIdentity != null)
                            configureIdentity(context);
                        else
                            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                            {
                                new Claim("sub", context.Request.Path.Value), new Claim("tenant_id", "trusted-tenant")
                            }, "test"));
                        return next();
                    });
                    app.UseBingHttpAuditing();
                    app.Run(handler);
                })).Build();
    }

    /// <summary>
    /// 记录并发请求保存结果的审计存储。
    /// </summary>
    private sealed class RecordingStore : IAuditingStore
    {
        /// <summary>
        /// 获取保存的审计日志。
        /// </summary>
        public ConcurrentQueue<AuditLogInfo> Logs { get; } = new();

        /// <summary>
        /// 获取或设置模拟的保存异常。
        /// </summary>
        public Exception Failure { get; set; }

        /// <inheritdoc />
        public Task SaveAsync(AuditLogInfo auditInfo)
        {
            Logs.Enqueue(auditInfo);
            return Failure == null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }

    /// <summary>
    /// 记录中间件实际输出的日志。
    /// </summary>
    private sealed class RecordingLoggerProvider : ILoggerProvider, ILogger
    {
        /// <summary>
        /// 获取日志内容。
        /// </summary>
        public ConcurrentQueue<string> Messages { get; } = new();

        /// <inheritdoc />
        public ILogger CreateLogger(string categoryName) => this;

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state) => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter) => Messages.Enqueue(formatter(state, exception) + exception);

        /// <inheritdoc />
        public void Dispose() { }
    }
}
