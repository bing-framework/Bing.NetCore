using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Bing.DynamicApi.WebApiClient;
using Bing.Http.Resilience;
using Microsoft.Extensions.DependencyInjection;
using BrokenCircuitException = Bing.Resilience.ResilienceCircuitOpenException;
using TimeoutRejectedException = Bing.Resilience.ResilienceTimeoutException;
using WebApiClientCore.Attributes;
using WebApiClientCore.Exceptions;
using WebApiClientCore.Parameters;
using Xunit;
using static Bing.DynamicApi.Client.Resilience.Tests.TestSupport;

namespace Bing.DynamicApi.Client.Resilience.Tests;

public sealed record SdkResult(string Value);

public interface IResilientSdk
{
    [HttpGet("read")][JsonReturn] Task<SdkResult> ReadAsync(CancellationToken cancellationToken = default);
    [HttpGet("raw")][RawReturn] Task<string> RawAsync(CancellationToken cancellationToken = default);
    [HttpGet("download")][RawReturn] Task<HttpResponseMessage> DownloadAsync(CancellationToken cancellationToken = default);
    [HttpPost("upload")][RawReturn] Task<string> UploadAsync(FormDataFile file, CancellationToken cancellationToken = default);
}

public sealed class WebApiClientResilienceTests
{
    private static ServiceProvider Create(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback,
        Action<BingHttpResilienceOptions>? configure = null, string url = "http://service.test")
    {
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        var builder = services.AddBingWebApiClient<IResilientSdk>(o => o.BaseAddress = new Uri(url));
        Assert.Same(builder, builder.AddBingHttpResilience("sdk", o =>
        {
            o.CanRetryRequest = r => r.RequestUri!.AbsolutePath == "/read";
            o.Retry.Delay = TimeSpan.Zero;
            o.Retry.UseJitter = false;
            o.CircuitBreaker.Enabled = false;
            configure?.Invoke(o);
        }));
        builder.ConfigurePrimaryHttpMessageHandler(() => new CallbackHandler(callback));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ExistingBuilderRetriesReadButNeverUploadOrDownload()
    {
        int count = 0;
        using var p = Create((_, _) => Task.FromResult(++count == 1 ? Json("busy", HttpStatusCode.ServiceUnavailable) : Json(new SdkResult("ok"))));
        Assert.Equal("ok", (await p.GetRequiredService<IResilientSdk>().ReadAsync()).Value);
        Assert.Equal(2, count);
        int transferCount = 0;
        using var transfers = Create((_, _) =>
        {
            ++transferCount;
            return Task.FromResult(Json("busy", HttpStatusCode.ServiceUnavailable));
        });
        var api = transfers.GetRequiredService<IResilientSdk>();
        await Assert.ThrowsAsync<HttpRequestException>(() => api.DownloadAsync());
        await Assert.ThrowsAsync<HttpRequestException>(() => api.UploadAsync(new FormDataFile(Encoding.UTF8.GetBytes("file"), "file.txt")));
        Assert.Equal(2, transferCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerLinkedDeadlineCancelsSuccessfulBodyAndPreservesSdkWrapper(bool raw)
    {
        using var stream = new BlockingStream();
        using var p = Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(stream) { Headers = { ContentType = new("application/json") } }
        }));
        using var caller = new CancellationTokenSource();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(caller.Token);
        deadline.CancelAfter(TimeSpan.FromMilliseconds(200));
        var api = p.GetRequiredService<IResilientSdk>();
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => raw ? api.RawAsync(deadline.Token) : api.ReadAsync(deadline.Token));
        Assert.IsAssignableFrom<OperationCanceledException>(error.InnerException);
        // SDK 2.1.6 的 RawReturn 字符串取消不释放响应；保留并记录其既有行为。
        Assert.Equal(!raw, stream.Disposed);
    }

    [Fact]
    public async Task NonSuccessKeepsSdkErrorWithoutReadingErrorBody()
    {
        var stream = new BlockingStream();
        using var p = Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StreamContent(stream)
        }));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            p.GetRequiredService<IResilientSdk>().ReadAsync().WaitAsync(TimeSpan.FromSeconds(3)));
        var status = Assert.IsType<ApiResponseStatusException>(error.InnerException);
        Assert.False(stream.Started.Task.IsCompleted);
        Assert.False(stream.Disposed);
        status.ResponseMessage.Dispose();
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task AttemptTimeoutAndCircuitExceptionsRemainInnerExceptions()
    {
        using var timeoutProvider = Create(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Json("unreachable");
        }, o => { o.Retry.MaxRetryAttempts = 0; o.AttemptTimeout = TimeSpan.FromMilliseconds(100); });
        var timeout = await Assert.ThrowsAsync<HttpRequestException>(() => timeoutProvider.GetRequiredService<IResilientSdk>().ReadAsync());
        Assert.IsType<TimeoutRejectedException>(timeout.InnerException);

        int count = 0;
        using var circuitProvider = Create((_, _) =>
        {
            ++count; return Task.FromResult(Json("busy", HttpStatusCode.ServiceUnavailable));
        }, o =>
        {
            o.Retry.MaxRetryAttempts = 0;
            o.CircuitBreaker.Enabled = true;
            o.CircuitBreaker.MinimumThroughput = 2;
        });
        var api = circuitProvider.GetRequiredService<IResilientSdk>();
        await Assert.ThrowsAsync<HttpRequestException>(() => api.ReadAsync());
        await Assert.ThrowsAsync<HttpRequestException>(() => api.ReadAsync());
        var broken = await Assert.ThrowsAsync<HttpRequestException>(() => api.ReadAsync());
        Assert.IsAssignableFrom<BrokenCircuitException>(broken.InnerException);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task SignatureRefreshesEachAttemptAndConcurrentContextsStayIsolated()
    {
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        var ambient = new AmbientContext();
        var attempts = new ConcurrentDictionary<string, List<string>>();
        services.AddSingleton<IBingWebApiClientRequestContextAccessor>(ambient);
        services.AddBingWebApiClient<IResilientSdk>(o => o.BaseAddress = new Uri("http://service.test"))
            .AddBingHttpResilience("signed", o =>
            {
                o.CanRetryRequest = _ => true;
                o.Retry.Delay = TimeSpan.Zero;
                o.CircuitBreaker.Enabled = false;
            })
            .AddHttpMessageHandler(() => new SignHandler())
            .ConfigurePrimaryHttpMessageHandler(() => new CallbackHandler(async (r, ct) =>
            {
                await Task.Yield();
                var tenant = r.Headers.GetValues("X-Tenant-Id").Single();
                var nonce = r.Headers.GetValues("X-Nonce").Single();
                var list = attempts.GetOrAdd(tenant, _ => new());
                lock (list)
                {
                    list.Add(nonce);
                    return list.Count == 1 ? Json("busy", HttpStatusCode.ServiceUnavailable) : Json(new SdkResult(tenant));
                }
            }));
        using var p = services.BuildServiceProvider();
        async Task<string> Call(string tenant)
        {
            ambient.Tenant.Value = tenant;
            return (await p.GetRequiredService<IResilientSdk>().ReadAsync()).Value;
        }
        Assert.Equal(new[] { "a", "b" }, await Task.WhenAll(Call("a"), Call("b")));
        foreach (var pair in attempts)
        {
            Assert.Equal(2, pair.Value.Count);
            Assert.Equal(2, pair.Value.Distinct().Count());
        }
    }

    [Fact]
    public async Task RawDownloadResponseRemainsCallerOwned()
    {
        var stream = new BlockingStream();
        using var p = Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) }));
        using var response = await p.GetRequiredService<IResilientSdk>().DownloadAsync();
        Assert.False(stream.Disposed);
        response.Dispose();
        Assert.True(stream.Disposed);
    }

    private sealed class AmbientContext : IBingWebApiClientRequestContextAccessor
    {
        internal AsyncLocal<string> Tenant { get; } = new();
        public BingWebApiClientRequestContext GetCurrentContext() => new(tenantId: Tenant.Value);
    }

    private sealed class SignHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            request.Headers.Remove("X-Nonce");
            request.Headers.TryAddWithoutValidation("X-Nonce", Guid.NewGuid().ToString("N"));
            return base.SendAsync(request, token);
        }
    }
}
