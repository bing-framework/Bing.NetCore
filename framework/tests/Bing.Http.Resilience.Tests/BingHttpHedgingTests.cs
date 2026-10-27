using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Bing.Http.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Http.Resilience.Tests;

public sealed class BingHttpHedgingTests
{
    private const string ClientName = "hedging";
    private static readonly Uri PrimaryOrigin = new("https://service.test/");
    private static readonly Uri BackupOrigin = new("https://backup.test/");

    [Fact]
    public async Task SlowPrimaryLosesToFastBackupAndLateResponseIsReleased()
    {
        var primaryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var primaryCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TrackingContent? lateContent = null;
        TrackingContent? backupContent = null;
        var requests = new ConcurrentBag<HttpRequestMessage>();
        using var services = CreateProvider((request, _) =>
        {
            requests.Add(request);
            if (request.RequestUri!.Host == "service.test")
            {
                return SlowPrimaryAsync();
            }

            backupContent = new TrackingContent();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = backupContent });

            async Task<HttpResponseMessage> SlowPrimaryAsync()
            {
                primaryStarted.TrySetResult();
                await Task.Delay(120);
                lateContent = new TrackingContent();
                primaryCompleted.TrySetResult();
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = lateContent };
            }
        });
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);
        using var request = Get("api/items?x=7");

        var response = await client.SendAsync(request).WaitAsync(TimeSpan.FromSeconds(5));
        await primaryStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await primaryCompleted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Same(backupContent, response.Content);
        Assert.Equal(1, Assert.IsType<TrackingContent>(lateContent).DisposeCount);
        Assert.Equal(0, Assert.IsType<TrackingContent>(backupContent).DisposeCount);
        var sends = requests.ToArray();
        Assert.Equal(2, sends.Length);
        Assert.NotSame(sends[0], sends[1]);
        Assert.All(sends, sent =>
        {
            Assert.Equal("/api/items", sent.RequestUri!.AbsolutePath);
            Assert.Equal("?x=7", sent.RequestUri.Query);
        });

        response.Dispose();
        Assert.Equal(1, backupContent.DisposeCount);
    }

    [Fact]
    public async Task AllFailedBranchesReturnPrimaryResponseAndReleaseSecondary()
    {
        HttpResponseMessage? primaryResponse = null;
        TrackingContent? backupContent = null;
        using var services = CreateProvider((request, _) =>
        {
            if (request.RequestUri!.Host == "service.test")
            {
                primaryResponse = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new TrackingContent()
                };
                return Task.FromResult(primaryResponse);
            }

            backupContent = new TrackingContent();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = backupContent });
        }, options => options.Hedging.Delay = TimeSpan.Zero);
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);
        using var request = Get();

        var response = await client.SendAsync(request).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Same(primaryResponse, response);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, Assert.IsType<TrackingContent>(backupContent).DisposeCount);
        Assert.Equal(0, Assert.IsType<TrackingContent>(primaryResponse!.Content).DisposeCount);
        response.Dispose();
        Assert.Equal(1, Assert.IsType<TrackingContent>(primaryResponse.Content).DisposeCount);
    }

    [Fact]
    public async Task BusinessClientErrorWinsWithoutStartingBackup()
    {
        var sends = new ConcurrentBag<string>();
        HttpResponseMessage? primaryResponse = null;
        using var services = CreateProvider((request, _) =>
        {
            sends.Add(request.RequestUri!.Host);
            primaryResponse = new HttpResponseMessage(HttpStatusCode.NotFound);
            return Task.FromResult(primaryResponse);
        });
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);
        using var request = Get();

        var response = await client.SendAsync(request).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Same(primaryResponse, response);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(new[] { "service.test" }, sends);
        response.Dispose();
    }

    [Fact]
    public async Task HedgeClonesRequestsForEachBranchAndRegeneratesSignatureHeaders()
    {
        var optionKey = new HttpRequestOptionsKey<List<string>>("hedging.mutable");
        var requests = new ConcurrentBag<HttpRequestMessage>();
        var signatures = new ConcurrentBag<string>();
        var primaryCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TrackingContent? lateContent = null;
        using var services = CreateProvider((request, _) =>
        {
            requests.Add(request);
            var signature = Assert.Single(request.Headers.GetValues("X-Signature"));
            Assert.NotEqual("original-signature", signature);
            signatures.Add(signature);
            Assert.Equal("forward-me", Assert.Single(request.Headers.GetValues("X-Forward")));
            Assert.False(request.Headers.Contains("Authorization"));
            Assert.False(request.Headers.Contains("Cookie"));
            Assert.True(request.Options.TryGetValue(optionKey, out var values));

            if (request.RequestUri!.Host == "service.test")
            {
                values.Add("primary");
                return SlowPrimaryAsync();
            }

            Assert.Equal(new[] { "original" }, values);
            values.Add("backup");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

            async Task<HttpResponseMessage> SlowPrimaryAsync()
            {
                await Task.Delay(100);
                lateContent = new TrackingContent();
                primaryCompleted.TrySetResult();
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = lateContent };
            }
        }, options =>
        {
            options.Hedging.ForwardHeaders.Add("X-Forward");
            options.Hedging.SignatureHeaders.Add("X-Signature");
            options.Hedging.RegisterOption(optionKey, source => new List<string>(source));
        }, () => new SignatureHandler());
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);
        using var request = Get();
        request.Headers.TryAddWithoutValidation("X-Forward", "forward-me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "do-not-copy");
        request.Headers.TryAddWithoutValidation("Cookie", "session=do-not-copy");
        request.Headers.TryAddWithoutValidation("X-Signature", "original-signature");
        var originalValues = new List<string> { "original" };
        request.Options.Set(optionKey, originalValues);

        var response = await client.SendAsync(request).WaitAsync(TimeSpan.FromSeconds(5));
        await primaryCompleted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        var sent = requests.ToArray();
        Assert.Equal(2, sent.Length);
        Assert.All(sent, sentRequest => Assert.NotSame(request, sentRequest));
        Assert.NotSame(sent[0], sent[1]);
        Assert.Equal(2, signatures.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(new[] { "original" }, originalValues);
        var primaryRequest = Assert.Single(sent, r => r.RequestUri!.Host == "service.test");
        var backupRequest = Assert.Single(sent, r => r.RequestUri!.Host == "backup.test");
        Assert.True(primaryRequest.Options.TryGetValue(optionKey, out var primaryValues));
        Assert.True(backupRequest.Options.TryGetValue(optionKey, out var backupValues));
        Assert.NotSame(originalValues, primaryValues);
        Assert.NotSame(originalValues, backupValues);
        Assert.NotSame(primaryValues, backupValues);
        Assert.Equal(new[] { "original", "primary" }, primaryValues);
        Assert.Equal(new[] { "original", "backup" }, backupValues);
        Assert.Equal(1, Assert.IsType<TrackingContent>(lateContent).DisposeCount);
        response.Dispose();
    }

    [Fact]
    public async Task MutableOptionsRequireCopierAndCopiedValuesAreBranchIsolated()
    {
        var key = new HttpRequestOptionsKey<List<string>>("hedging.unregistered");
        int sends = 0;
        using var services = CreateProvider((_, _) =>
        {
            Interlocked.Increment(ref sends);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);
        using var request = Get();
        request.Options.Set(key, new List<string> { "mutable" });

        await Assert.ThrowsAsync<ResilienceConfigurationException>(() => client.SendAsync(request));
        Assert.Equal(0, sends);
    }

    [Fact]
    public async Task EndpointCircuitBreakersAreIsolatedAndOpenEndpointIsSkipped()
    {
        int primarySends = 0;
        int backupSends = 0;
        using var services = CreateProvider((request, _) =>
        {
            if (request.RequestUri!.Host == "service.test")
            {
                Interlocked.Increment(ref primarySends);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }

            Interlocked.Increment(ref backupSends);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }, options =>
        {
            options.CircuitBreaker.Enabled = true;
            options.CircuitBreaker.MinimumThroughput = 2;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(3);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(5);
        });
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);

        for (var index = 0; index < 3; index++)
        {
            using var response = await client.SendAsync(Get()).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.Equal(2, primarySends);
        Assert.Equal(3, backupSends);
    }

    [Fact]
    public async Task RetryAfterDelaysNewHedgeUntilServerDeadline()
    {
        var backupStarted = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = Stopwatch.StartNew();
        using var services = CreateProvider((request, _) =>
        {
            if (request.RequestUri!.Host == "service.test")
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(320));
                return Task.FromResult(response);
            }

            backupStarted.TrySetResult(clock.Elapsed);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }, options => options.Hedging.Delay = TimeSpan.FromMilliseconds(40));
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);

        using var response = await client.SendAsync(Get()).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await backupStarted.Task.WaitAsync(TimeSpan.FromSeconds(3)) >= TimeSpan.FromMilliseconds(280));
    }

    [Fact]
    public async Task HedgingRejectsRequestOutsideConfiguredPrimaryOrigin()
    {
        int sends = 0;
        using var services = CreateProvider((_, _) =>
        {
            Interlocked.Increment(ref sends);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://outside.test/api/items");

        await Assert.ThrowsAsync<ResilienceConfigurationException>(() => client.SendAsync(request));
        Assert.Equal(0, sends);
    }

    [Fact]
    public void HedgingRequiresExplicitlySafeTransport()
    {
        var customServices = CreateServices();
        customServices.AddHttpClient(ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new DelegateHttpMessageHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))))
            .AddBingHttpResilience("unsafe-custom", options =>
            {
                ConfigureHedging(options);
                options.Hedging.ValidateCustomTransport = null;
            });
        using (var provider = customServices.BuildServiceProvider())
        {
            Assert.Throws<ResilienceConfigurationException>(() =>
                provider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName));
        }

        var socketsServices = CreateServices();
        socketsServices.AddHttpClient(ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                UseCookies = false
            })
            .AddBingHttpResilience("unsafe-sockets", options => ConfigureHedging(options));
        using (var provider = socketsServices.BuildServiceProvider())
        {
            Assert.Throws<ResilienceConfigurationException>(() =>
                provider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName));
        }

        var safeSocketsServices = CreateServices();
        safeSocketsServices.AddHttpClient(ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false
            })
            .AddBingHttpResilience("safe-sockets", options => ConfigureHedging(options));
        using var safeProvider = safeSocketsServices.BuildServiceProvider();
        using var safeClient = safeProvider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);
        Assert.NotNull(safeClient);
    }

    private static ServiceProvider CreateProvider(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback,
        Action<BingHttpResilienceOptions>? configure = null,
        Func<DelegatingHandler>? innerHandler = null)
    {
        var services = CreateServices();
        var builder = services.AddHttpClient(ClientName);
        builder.AddBingHttpResilience("hedging-tests", options =>
        {
            ResilienceTestHost.ConfigureDefaults(options);
            ConfigureHedging(options);
            configure?.Invoke(options);
        });
        if (innerHandler is not null)
            builder.AddHttpMessageHandler(innerHandler);
        builder.ConfigurePrimaryHttpMessageHandler(() => new DelegateHttpMessageHandler(callback));
        return services.BuildServiceProvider();
    }

    private static IServiceCollection CreateServices() =>
        new ServiceCollection().AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test").Services;

    private static void ConfigureHedging(BingHttpResilienceOptions options)
    {
        ResilienceTestHost.ConfigureDefaults(options);
        options.Mode = BingHttpResilienceMode.Hedging;
        options.CanHedgeRequest = _ => true;
        options.Hedging.MaxHedgedAttempts = 1;
        options.Hedging.Delay = TimeSpan.FromMilliseconds(20);
        options.Hedging.Endpoints.Add(new BingHttpHedgingEndpoint("primary", PrimaryOrigin));
        options.Hedging.Endpoints.Add(new BingHttpHedgingEndpoint("backup", BackupOrigin));
        options.Hedging.ValidateCustomTransport = handler => handler is DelegateHttpMessageHandler;
    }

    private static HttpRequestMessage Get(string path = "api/items?x=7") =>
        new(HttpMethod.Get, new Uri(PrimaryOrigin, path));

    private sealed class SignatureHandler : DelegatingHandler
    {
        private int _signature;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Remove("X-Signature");
            request.Headers.TryAddWithoutValidation("X-Signature", Interlocked.Increment(ref _signature).ToString());
            return base.SendAsync(request, cancellationToken);
        }
    }
}
