using System.Net;
using Microsoft.Extensions.DependencyInjection;
using TimeoutRejectedException = Bing.Resilience.ResilienceTimeoutException;
using Xunit;

namespace Bing.Http.Resilience.Tests;

public sealed class BingHttpLifecycleTests
{
    [Fact]
    public async Task RateLimitedResponsesDoNotOpenCircuit()
    {
        var calls = 0;
        using var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            ++calls; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        });
        using var host = ResilienceTestHost.Create("rate-limit", handler, o =>
        {
            o.CircuitBreaker.Enabled = true;
            o.CircuitBreaker.MinimumThroughput = 2;
        });
        for (var i = 0; i < 4; i++)
        {
            using var response = await host.Client.GetAsync("http://service.test");
            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
        Assert.Equal(4, calls);
    }

    [Fact]
    public async Task TotalBudgetIncludesQueueAndCancelledWaiterDoesNotLeakPermit()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sends = 0;
        using var handler = new DelegateHttpMessageHandler(async (_, ct) =>
        {
            if (Interlocked.Increment(ref sends) == 1)
            {
                started.TrySetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var host = ResilienceTestHost.Create("queued", handler, o =>
        {
            o.RequestTimeout = TimeSpan.FromMilliseconds(300);
            o.AttemptTimeout = TimeSpan.FromMilliseconds(300);
            o.Concurrency.Enabled = true;
            o.Concurrency.PermitLimit = 1;
            o.Concurrency.QueueLimit = 1;
        });
        var first = host.Client.GetAsync("http://service.test/first");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        using var cts = new CancellationTokenSource();
        var queued = host.Client.GetAsync("http://service.test/queued", cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        await Assert.ThrowsAsync<TimeoutRejectedException>(() => first);
        using var recovered = await host.Client.GetAsync("http://service.test/recovered");
        Assert.Equal(2, sends);
    }

    [Fact]
    public async Task CancelDuringBackoffDoesNotSendAgainAndReleasesFailedResponse()
    {
        using var cts = new CancellationTokenSource();
        var content = new TrackingContent();
        var returned = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        using var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            ++calls;
            returned.TrySetResult(true);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = content });
        });
        using var host = ResilienceTestHost.Create("backoff", handler, o =>
        {
            o.CanRetryRequest = _ => true;
            o.Retry.MaxRetryAttempts = 2;
            o.Retry.Delay = TimeSpan.FromSeconds(1);
            o.Retry.MaxDelay = TimeSpan.FromSeconds(1);
        });
        var send = host.Client.GetAsync("http://service.test", cts.Token);
        await returned.Task;
        // 等待响应释放表明已进入退避，而非取消网络发送。
        for (var i = 0; i < 100 && content.DisposeCount == 0; i++) await Task.Delay(5);
        Assert.Equal(1, content.DisposeCount);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ConfigurationIsFrozenAndDiscardedResponseIsDisposedBeforeNextAttempt()
    {
        BingHttpResilienceOptions? captured = null;
        var content = new TrackingContent();
        int calls = 0;
        using var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            if (++calls == 1) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = content });
            Assert.Equal(1, content.DisposeCount);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        services.AddHttpClient("snapshot").ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddBingHttpResilience("snapshot", o =>
            {
                captured = o;
                o.CanRetryRequest = _ => true;
                o.Retry.Delay = TimeSpan.Zero;
                o.CircuitBreaker.Enabled = false;
            });
        captured!.Retry.MaxRetryAttempts = 0;
        captured.CanRetryRequest = _ => false;
        using var p = services.BuildServiceProvider();
        using var client = p.GetRequiredService<IHttpClientFactory>().CreateClient("snapshot");
        using var response = await client.GetAsync("http://service.test");
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task InvalidRetryAfterFallsBackToLocalDelay()
    {
        int calls = 0;
        using var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            var r = new HttpResponseMessage(++calls == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
            r.Headers.TryAddWithoutValidation("Retry-After", "invalid");
            return Task.FromResult(r);
        });
        using var host = ResilienceTestHost.Create("invalid-header", handler, o =>
        {
            o.CanRetryRequest = _ => true;
            o.Retry.MaxRetryAttempts = 1;
        });
        using var result = await host.Client.GetAsync("http://service.test");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void RemovedPipelineIsRejectedAtClientConstruction()
    {
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        var builder = services.AddHttpClient("removed").AddBingHttpResilience("removed");
        builder.ConfigureAdditionalHttpMessageHandlers((handlers, _) => handlers.Clear());
        using var provider = services.BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IHttpClientFactory>().CreateClient("removed"));
    }

    [Fact]
    public async Task RetryAfterWithinBudgetWaitsForServerInsteadOfLocalMaximum()
    {
        int calls = 0;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        using var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            var result = new HttpResponseMessage(++calls == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
            result.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
            return Task.FromResult(result);
        });
        using var host = ResilienceTestHost.Create("server-wait", handler, o =>
        {
            o.CanRetryRequest = _ => true;
            o.Retry.MaxRetryAttempts = 1;
            o.Retry.MaxDelay = TimeSpan.FromMilliseconds(20);
        });
        using var result = await host.Client.GetAsync("http://service.test");
        Assert.Equal(2, calls);
        Assert.True(timer.Elapsed >= TimeSpan.FromMilliseconds(950));
    }

#if NET8_0_OR_GREATER
    [Theory]
    [InlineData(HttpRequestError.SecureConnectionError)]
    [InlineData(HttpRequestError.HttpProtocolError)]
    [InlineData(HttpRequestError.VersionNegotiationError)]
    [InlineData(HttpRequestError.UserAuthenticationError)]
    [InlineData(HttpRequestError.ConfigurationLimitExceeded)]
    public async Task PermanentTransportErrorsAreNotRetried(HttpRequestError reason)
    {
        int calls = 0;
        using var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            ++calls;
            return Task.FromException<HttpResponseMessage>(new HttpRequestException(reason, "permanent"));
        });
        using var host = ResilienceTestHost.Create("permanent-transport", handler, o =>
        {
            o.CanRetryRequest = _ => true;
            o.Retry.MaxRetryAttempts = 2;
        });
        await Assert.ThrowsAsync<HttpRequestException>(() => host.Client.GetAsync("http://service.test"));
        Assert.Equal(1, calls);
    }
#endif
}
