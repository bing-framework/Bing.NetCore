using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Http.Resilience.Tests;

internal sealed class ResilienceTestHost : IDisposable
{
    private readonly ServiceProvider _services;

    private ResilienceTestHost(ServiceProvider services, HttpClient client)
    {
        _services = services;
        Client = client;
    }

    public HttpClient Client { get; }

    public static ResilienceTestHost Create(
        string clientName,
        HttpMessageHandler handler,
        Action<BingHttpResilienceOptions>? configure = null,
        TimeSpan? handlerLifetime = null)
    {
        return Create(clientName, () => handler, configure, handlerLifetime);
    }

    public static ResilienceTestHost Create(
        string clientName,
        Func<HttpMessageHandler> handlerFactory,
        Action<BingHttpResilienceOptions>? configure = null,
        TimeSpan? handlerLifetime = null)
    {
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        var builder = services.AddHttpClient(clientName)
            .ConfigurePrimaryHttpMessageHandler(handlerFactory);

        if (handlerLifetime.HasValue)
            builder.SetHandlerLifetime(handlerLifetime.Value);

        builder.AddBingHttpResilience($"test-{clientName}", options =>
        {
            ConfigureDefaults(options);
            configure?.Invoke(options);
        });

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);
        return new ResilienceTestHost(provider, client);
    }

    public static void ConfigureDefaults(BingHttpResilienceOptions options)
    {
        options.RequestTimeout = TimeSpan.FromSeconds(5);
        options.AttemptTimeout = TimeSpan.FromSeconds(2);
        options.Retry.MaxRetryAttempts = 0;
        options.Retry.Delay = TimeSpan.Zero;
        options.Retry.MaxDelay = TimeSpan.Zero;
        options.Retry.UseJitter = false;
        options.CircuitBreaker.Enabled = false;
        options.Concurrency.Enabled = false;
    }

    public void Dispose()
    {
        Client.Dispose();
        _services.Dispose();
    }
}

internal sealed class DelegateHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync;

    public DelegateHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
    {
        _sendAsync = sendAsync;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) => _sendAsync(request, cancellationToken);
}

internal static class ResilienceTestResponses
{
    public static HttpResponseMessage Create(HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode);

    public static HttpRequestMessage Get(string path = "resource") =>
        new(HttpMethod.Get, $"https://service.test/{path}");
}

internal sealed class TrackingContent : ByteArrayContent
{
    private int _disposeCount;

    public TrackingContent() : base(new byte[] { 1, 2, 3 })
    {
    }

    public int DisposeCount => Volatile.Read(ref _disposeCount);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Interlocked.Increment(ref _disposeCount);
        base.Dispose(disposing);
    }
}

internal static class RetryAfterHeaders
{
    public static void Set(HttpResponseMessage response, bool useDate)
    {
        response.Headers.RetryAfter = useDate
            ? new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(3))
            : new RetryConditionHeaderValue(TimeSpan.FromSeconds(3));
    }
}
