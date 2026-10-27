using Bing.Http.Clients;
using System.Net;
using System.Text.Json;
using Bing.Content;
using Bing.DynamicApi.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using static Bing.DynamicApi.Client.Resilience.Tests.TestSupport;

namespace Bing.DynamicApi.Client.Resilience.Tests;

public sealed class RuntimeResilienceTests
{
    private static ServiceProvider Create(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback,
        Action<BingDynamicApiClientResilienceOptions>? configure = null,
        Action<IServiceCollection>? servicesSetup = null,
        string proxyName = "PROBE")
    {
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        services.AddBingDynamicApiClient(o => o.AddRemoteService("Probe", r => r.BaseUrl = "http://service.test"));
        services.AddBingDynamicApiClient<IProbe>(proxyName);
        services.AddBingDynamicApiClientResilience("probe", o =>
        {
            o.Http.CanRetryRequest = _ => true;
            o.Http.Retry.Delay = TimeSpan.Zero;
            o.Http.Retry.UseJitter = false;
            o.Http.CircuitBreaker.Enabled = false;
            configure?.Invoke(o);
        }).ConfigurePrimaryHttpMessageHandler(() => new CallbackHandler(callback));
        servicesSetup?.Invoke(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ColdAndHotCallsUseBoundNameAndDoNotStackLegacyRetries()
    {
        int definitions = 0, invocations = 0;
        var legacy = new RejectLegacyPolicy();
        using var provider = Create((r, _) =>
        {
            Assert.True(BingDynamicApiRequestMetadata.TryGet(r, out var m));
            Assert.Equal("PROBE", m.RemoteName);
            Assert.DoesNotContain(r.Headers, h => h.Key.Contains("RequestMetadata", StringComparison.Ordinal));
            Assert.True(m.IsReplayable);
            Assert.False(m.IsStreamResult);
            if (IsDefinition(r))
            {
                Assert.Equal(BingDynamicApiRequestKind.Definition, m.Kind);
                return Task.FromResult(++definitions == 1 ? Json("busy", HttpStatusCode.ServiceUnavailable) : Definition());
            }
            Assert.Equal(BingDynamicApiRequestKind.Invocation, m.Kind);
            return Task.FromResult(++invocations == 1 ? Json("busy", HttpStatusCode.ServiceUnavailable) : Json("ok"));
        }, servicesSetup: s => s.AddSingleton<IBingDynamicApiRetryPolicy>(legacy));
        var api = provider.GetRequiredService<IProbe>();
        Assert.Equal("ok", await api.GetAsync());
        Assert.Equal("ok", await api.GetAsync());
        Assert.Equal(2, definitions);
        Assert.Equal(3, invocations);
    }

    [Fact]
    public async Task DefinitionHasBuiltInPermissionButBusinessNeedsHostPermission()
    {
        int definitions = 0, business = 0;
        using var provider = Create((r, _) => Task.FromResult(IsDefinition(r)
            ? ++definitions == 1 ? Json("busy", HttpStatusCode.ServiceUnavailable) : Definition()
            : ++business == 1 ? Json("busy", HttpStatusCode.ServiceUnavailable) : Json("ok")),
            o => o.Http.CanRetryRequest = null);
        await Assert.ThrowsAsync<BingRemoteCallException>(() => provider.GetRequiredService<IProbe>().GetAsync());
        Assert.Equal(2, definitions);
        Assert.Equal(1, business);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StreamAndWriteAreNotReplayed(bool stream)
    {
        int calls = 0;
        using var provider = Create((r, _) =>
        {
            if (IsDefinition(r)) return Task.FromResult(Definition());
            ++calls;
            Assert.True(BingDynamicApiRequestMetadata.TryGet(r, out var m));
            Assert.False(m.IsReplayable);
            Assert.Equal(stream, m.IsStreamResult);
            return Task.FromResult(Json("busy", HttpStatusCode.ServiceUnavailable));
        });
        var api = provider.GetRequiredService<IProbe>();
        if (stream) await Assert.ThrowsAsync<BingRemoteCallException>(() => api.DownloadAsync());
        else await Assert.ThrowsAsync<BingRemoteCallException>(() => api.WriteAsync());
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task OldHandlerRunsOncePerLogicalRequestAndCannotGrantReplayToBody()
    {
        int calls = 0, handlerCalls = 0;
        using var provider = Create((r, _) =>
        {
            if (IsDefinition(r)) return Task.FromResult(Definition());
            ++calls;
            return Task.FromResult(Json("busy", HttpStatusCode.ServiceUnavailable));
        }, servicesSetup: s => s.AddSingleton<IBingDynamicApiRequestHandler>(new RequestHandler(r =>
        {
            ++handlerCalls;
            if (!IsDefinition(r)) r.Content = new StringContent("unsafe body");
        })));
        await Assert.ThrowsAsync<BingRemoteCallException>(() => provider.GetRequiredService<IProbe>().GetAsync());
        Assert.Equal(1, calls);
        Assert.Equal(2, handlerCalls);
    }

    [Fact]
    public async Task LogicalRequestHandlerIsNotRepeatedByPipeline()
    {
        int calls = 0, handlers = 0;
        using var provider = Create((r, _) => Task.FromResult(IsDefinition(r) ? Definition() :
            ++calls < 3 ? Json("busy", HttpStatusCode.ServiceUnavailable) : Json("ok")),
            servicesSetup: s => s.AddSingleton<IBingDynamicApiRequestHandler>(new RequestHandler(_ => ++handlers)));
        Assert.Equal("ok", await provider.GetRequiredService<IProbe>().GetAsync());
        Assert.Equal(3, calls);
        Assert.Equal(2, handlers); // 一次描述、一次业务逻辑调用。
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OperationBudgetIncludesSuccessAndErrorBody(bool error)
    {
        var stream = new BlockingStream();
        using var provider = Create((r, _) => Task.FromResult(IsDefinition(r) ? Definition() :
            new HttpResponseMessage(error ? HttpStatusCode.BadRequest : HttpStatusCode.OK) { Content = new StreamContent(stream) }),
            o => o.OperationTimeout = TimeSpan.FromMilliseconds(200));
        var exception = await Assert.ThrowsAsync<TimeoutException>(() => provider.GetRequiredService<IProbe>().GetAsync());
        Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CallerCancellationRemainsCancellation()
    {
        var stream = new BlockingStream();
        using var provider = Create((r, _) => Task.FromResult(IsDefinition(r) ? Definition() :
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) }));
        using var cts = new CancellationTokenSource();
        var call = provider.GetRequiredService<IProbe>().GetAsync(cts.Token);
        await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task CancellingOneWaiterDoesNotCancelSharedDefinition()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int definitions = 0;
        CancellationToken downloadToken = default;
        using var provider = Create(async (r, ct) =>
        {
            if (!IsDefinition(r)) return Json("ok");
            ++definitions;
            downloadToken = ct;
            started.TrySetResult(true);
            await release.Task.WaitAsync(ct);
            return Definition();
        });
        var api = provider.GetRequiredService<IProbe>();
        using var cts = new CancellationTokenSource();
        var first = api.GetAsync(cts.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = api.GetAsync();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.False(downloadToken.IsCancellationRequested);
        release.TrySetResult(true);
        Assert.Equal("ok", await second);
        Assert.Equal(1, definitions);
    }

    [Fact]
    public async Task DefinitionBodyTimeoutEvictsFailedEntry()
    {
        int definitions = 0;
        var stream = new BlockingStream();
        using var provider = Create((r, _) => Task.FromResult(IsDefinition(r)
            ? ++definitions == 1 ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) } : Definition()
            : Json("ok")), o => o.DefinitionTimeout = TimeSpan.FromMilliseconds(150));
        var api = provider.GetRequiredService<IProbe>();
        await Assert.ThrowsAsync<TimeoutException>(() => api.GetAsync());
        Assert.True(stream.Disposed);
        Assert.Equal("ok", await api.GetAsync());
        Assert.Equal(2, definitions);
    }

    [Fact]
    public async Task OperationBudgetIsNotRestartedAfterDefinition()
    {
        using var provider = Create(async (r, ct) =>
        {
            await Task.Delay(250, ct);
            return IsDefinition(r) ? Definition() : Json("too late");
        }, o => o.OperationTimeout = TimeSpan.FromMilliseconds(400));
        await Assert.ThrowsAsync<TimeoutException>(() => provider.GetRequiredService<IProbe>().GetAsync());
    }

    [Fact]
    public async Task ReturnedStreamOutlivesOperationBudgetAndRemainsCallerOwned()
    {
        var stream = new BlockingStream();
        using var provider = Create((r, _) => Task.FromResult(IsDefinition(r) ? Definition() :
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) }),
            o => o.OperationTimeout = TimeSpan.FromMilliseconds(100));
        using var result = await provider.GetRequiredService<IProbe>().DownloadAsync();
        await Task.Delay(150);
        Assert.False(stream.Disposed);
        using var cts = new CancellationTokenSource();
        var read = result.GetStream().ReadAsync(new byte[1], cts.Token).AsTask();
        await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
        result.Dispose();
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task JsonParseFailureIsNotRetried()
    {
        int calls = 0;
        using var provider = Create((r, _) =>
        {
            if (IsDefinition(r)) return Task.FromResult(Definition());
            ++calls;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{", System.Text.Encoding.UTF8, "application/json") });
        });
        await Assert.ThrowsAsync<JsonException>(() => provider.GetRequiredService<IProbe>().GetAsync());
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ConflictingMissingAndDuplicateBindingsFail()
    {
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        services.AddBingDynamicApiClient(o => o.AddRemoteService("probe", r =>
        {
            r.BaseUrl = "http://service.test";
            r.MaxRetries = 1;
        }));
        services.AddBingDynamicApiClientResilience("PROBE");
        using (var p = services.BuildServiceProvider())
            Assert.Throws<OptionsValidationException>(() => p.GetRequiredService<IOptions<BingDynamicApiClientOptions>>().Value);
        Assert.Throws<InvalidOperationException>(() => services.AddBingDynamicApiClientResilience("probe"));

        var missing = new ServiceCollection();
        missing.AddBingDynamicApiClient(o => o.AddRemoteService("probe", r =>
        {
            r.BaseUrl = "http://service.test";
            r.RetryMode = BingDynamicApiRetryMode.HttpPipeline;
        }));
        using (var p = missing.BuildServiceProvider())
            Assert.Throws<OptionsValidationException>(() => p.GetRequiredService<IOptions<BingDynamicApiClientOptions>>().Value);
        var absent = new ServiceCollection();
        absent.AddBingDynamicApiClient(_ => { });
        absent.AddBingDynamicApiClientResilience("unknown");
        using (var p = absent.BuildServiceProvider())
            Assert.Throws<OptionsValidationException>(() => p.GetRequiredService<IOptions<BingDynamicApiClientOptions>>().Value);
    }

    [Fact]
    public void NeutralDefaultsAndInvalidBudgetsAreValidated()
    {
        var defaults = new BingDynamicApiRemoteOptions();
        Assert.Equal(BingDynamicApiRetryMode.Legacy, defaults.RetryMode);
        Assert.Null(defaults.OperationTimeout);
        Assert.Null(defaults.DefinitionTimeout);
        using var request = new HttpRequestMessage();
        Assert.False(BingDynamicApiRequestMetadata.TryGet(request, out _));
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        services.AddBingDynamicApiClient(o => o.AddRemoteService("probe", r => r.OperationTimeout = TimeSpan.Zero));
        using var p = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => p.GetRequiredService<IOptions<BingDynamicApiClientOptions>>().Value);
    }


    [Fact]
    public async Task BusinessExceptionIsNotReplacedWhenDeadlineExpires()
    {
        using var provider = Create((r, _) => Task.FromResult(IsDefinition(r) ? Definition() : Json("unused")),
            o => o.OperationTimeout = TimeSpan.FromMilliseconds(100),
            servicesSetup: s => s.AddSingleton<IBingDynamicApiRequestHandler>(new BusinessFailureHandler()));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetRequiredService<IProbe>().GetAsync());
        Assert.Equal("business failure", error.Message);
    }

    [Fact]
    public void AdapterDefaultsValidationAndRequestMetadataStayNeutral()
    {
        var defaults = new BingDynamicApiClientResilienceOptions();
        Assert.Equal(TimeSpan.FromSeconds(10), defaults.OperationTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), defaults.DefinitionTimeout);
        Assert.Throws<ArgumentNullException>(() =>
            BingDynamicApiClientResilienceExtensions.AddBingDynamicApiClientResilience(null!, "remote"));
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddBingDynamicApiClientResilience(" "));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ServiceCollection().AddBingDynamicApiClientResilience("remote", o => o.DefinitionTimeout = TimeSpan.Zero));
        Assert.Throws<ArgumentNullException>(() => BingDynamicApiRequestMetadata.TryGet(null!, out _));
    }

    private sealed class BusinessFailureHandler : IBingDynamicApiRequestHandler
    {
        public async ValueTask HandleAsync(string name, HttpRequestMessage request, CancellationToken token)
        {
            if (IsDefinition(request)) return;
            // 模拟完成时恰好超出预算的宿主业务检查；其业务异常仍应保留。
            await Task.Delay(150);
            Assert.True(token.IsCancellationRequested);
            throw new InvalidOperationException("business failure");
        }
    }

    private sealed class RequestHandler(Action<HttpRequestMessage> handle) : IBingDynamicApiRequestHandler
    {
        public ValueTask HandleAsync(string name, HttpRequestMessage request, CancellationToken ct) { handle(request); return ValueTask.CompletedTask; }
    }
    private sealed class RejectLegacyPolicy : IBingDynamicApiRetryPolicy
    {
        public bool ShouldRetry(string name, int attempt, HttpStatusCode? code, HttpRequestException exception, BingDynamicApiRemoteOptions options) =>
            throw new InvalidOperationException("新模式不应调用旧策略");
        public TimeSpan GetDelay(string name, int attempt, BingDynamicApiRemoteOptions options) => throw new InvalidOperationException();
    }
}
