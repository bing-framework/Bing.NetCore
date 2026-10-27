using System.Net;
using Bing.Http.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

public sealed class HttpProviderMatrixTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var provider in new[] { "builtin", "fences", "polly" })
        foreach (var kind in new[] { "read", "unapproved", "body", "write", "stream", "429" })
            yield return new object[] { provider, kind };
    }
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task HttpReplayCountsAndResponseOwnershipMatchEveryProvider(string provider, string kind)
    {
        var responses = new List<CountedContent>(); var sends = 0;
        await using var host = Create(provider, (_, _) =>
        {
            var content = new CountedContent(); responses.Add(content);
            var status = ++sends == 1 ? kind == "429" ? HttpStatusCode.TooManyRequests : HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status) { Content = content });
        }, o => o.CanRetryRequest = _ => kind != "unapproved");
        using var request = new HttpRequestMessage(kind == "write" ? HttpMethod.Post : HttpMethod.Get, "http://matrix.test/read");
        if (kind is "body" or "write") request.Content = new StringContent("body");
        if (kind == "stream") request.Options.Set(BingHttpOperationMetadata.IsStreamResult, true);
        using var client = host.GetRequiredService<IHttpClientFactory>().CreateClient("matrix");
        var result = await client.SendAsync(request);
        Assert.Equal(kind is "read" or "429" ? 2 : 1, sends);
        Assert.All(responses.Take(responses.Count - 1), c => Assert.Equal(1, c.Disposals));
        Assert.Equal(0, responses.Last().Disposals);
        result.Dispose();
        Assert.Equal(1, responses.Last().Disposals);
    }
    [Theory]
    [InlineData("builtin")]
    [InlineData("fences")]
    [InlineData("polly")]
    public async Task HedgingWaitsForLoserCleanupAndRetainsWinnerOwnership(string provider)
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleaned = false; var sends = 0; var winner = new CountedContent();
        await using var host = Create(provider, async (request, token) =>
        {
            Interlocked.Increment(ref sends);
            if (request.RequestUri!.Host == "matrix.test")
            {
                started.SetResult(true);
                try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
                finally { cleaned = true; }
            }
            await started.Task;
            return new(HttpStatusCode.OK) { Content = winner };
        }, o =>
        {
            o.Mode = BingHttpResilienceMode.Hedging; o.CanHedgeRequest = _ => true; o.Hedging.Delay = TimeSpan.FromMilliseconds(20);
            o.Hedging.Endpoints.Add(new("main", new Uri("http://matrix.test")));
            o.Hedging.Endpoints.Add(new("backup", new Uri("http://backup.test")));
            o.Hedging.ValidateCustomTransport = _ => true;
        });
        using var client = host.GetRequiredService<IHttpClientFactory>().CreateClient("matrix");
        var response = await client.GetAsync("http://matrix.test/read");
        Assert.True(cleaned);
        Assert.Equal(2, sends);
        Assert.Equal(0, winner.Disposals);
        response.Dispose(); Assert.Equal(1, winner.Disposals);
    }
    [Theory]
    [InlineData("builtin")]
    [InlineData("fences")]
    [InlineData("polly")]
    public async Task CancellationDuringRetryAfterNeverSendsEarly(string provider)
    {
        var sent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        await using var host = Create(provider, (_, _) =>
        {
            Interlocked.Increment(ref calls); sent.TrySetResult(true);
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            response.Headers.RetryAfter = new(TimeSpan.FromSeconds(30)); return Task.FromResult(response);
        }, o => o.CanRetryRequest = _ => true);
        using var client = host.GetRequiredService<IHttpClientFactory>().CreateClient("matrix");
        using var cancel = new CancellationTokenSource();
        var call = client.GetAsync("http://matrix.test/read", cancel.Token);
        await sent.Task; cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        Assert.Equal(1, calls);
    }
    [Theory]
    [InlineData("builtin")]
    [InlineData("fences")]
    [InlineData("polly")]
    public async Task QueuedCallUsesExecutionBudgetAndDoesNotEnterCallback(string provider)
    {
        var clock = new FakeTimeProvider();
        var services = new ServiceCollection(); services.AddSingleton<TimeProvider>(clock);
        Register(services, provider);
        services.AddBingResiliencePipeline<int>("queue", o =>
        {
            o.ExecutionTimeout = TimeSpan.FromSeconds(1);
            o.Concurrency.Enabled = true; o.Concurrency.PermitLimit = 1; o.Concurrency.QueueLimit = 1;
        });
        await using var host = services.BuildServiceProvider();
        var pipeline = host.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<int>("queue");
        var firstEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = pipeline.ExecuteAsync(async _ => { firstEntered.SetResult(true); await release.Task; return 1; }).AsTask();
        await firstEntered.Task;
        var invoked = false;
        var second = pipeline.ExecuteAsync(_ => { invoked = true; return ValueTask.FromResult(2); }).AsTask();
        clock.Advance(TimeSpan.FromSeconds(2));
        var error = await Assert.ThrowsAsync<ResilienceTimeoutException>(() => second);
        Assert.Equal(ResilienceTimeoutKind.Execution, error.Kind);
        Assert.False(invoked);
        release.SetResult(true);
        await Assert.ThrowsAsync<ResilienceTimeoutException>(() => first);
    }
    private static ServiceProvider Create(string provider, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
        Action<BingHttpResilienceOptions> configure)
    {
        var services = new ServiceCollection(); Register(services, provider);
        services.AddHttpClient("matrix").ConfigurePrimaryHttpMessageHandler(() => new Handler(send)).AddBingHttpResilience("shared", o =>
        {
            o.Retry.Delay = TimeSpan.Zero; o.Retry.UseJitter = false; o.CircuitBreaker.Enabled = false; configure(o);
        });
        return services.BuildServiceProvider();
    }
    private static void Register(IServiceCollection services, string provider)
    {
        var builder = services.AddBingResilience();
        if (provider == "builtin") builder.AddBuiltInProvider();
        else if (provider == "fences") builder.AddFencesProvider("fences");
        else builder.AddPollyProvider("polly");
        builder.UseDefaultProvider(provider);
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken); }
    private sealed class CountedContent : ByteArrayContent
    {
        internal int Disposals;
        internal CountedContent() : base(new byte[] { 1 }) { }
        protected override void Dispose(bool disposing) { if (disposing) Interlocked.Increment(ref Disposals); base.Dispose(disposing); }
    }
}
