using System.Net;
using System.Security.Authentication;
using TimeoutRejectedException = Bing.Resilience.ResilienceTimeoutException;
using Xunit;

namespace Bing.Http.Resilience.Tests;

public sealed class BingHttpRetryTests
{
    [Fact]
    public async Task Retry_permitted_get_after_503_and_return_the_success_response()
    {
        var calls = 0;
        using var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            var attempt = Interlocked.Increment(ref calls);
            return Task.FromResult(ResilienceTestResponses.Create(
                attempt == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
        });
        using var host = ResilienceTestHost.Create("retry-success", handler, EnableRetry);

        using var response = await host.Client.SendAsync(ResilienceTestResponses.Get());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Stop_after_configured_retry_count_when_503_continues()
    {
        var calls = 0;
        using var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(ResilienceTestResponses.Create(HttpStatusCode.ServiceUnavailable));
        });
        using var host = ResilienceTestHost.Create("retry-exhausted", handler, options =>
        {
            EnableRetry(options);
            options.Retry.MaxRetryAttempts = 2;
        });

        using var response = await host.Client.SendAsync(ResilienceTestResponses.Get());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task Do_not_retry_when_business_replay_permission_is_missing()
    {
        var calls = 0;
        using var handler = AlwaysUnavailable(() => Interlocked.Increment(ref calls));
        using var host = ResilienceTestHost.Create("retry-no-permission", handler, options =>
        {
            options.Retry.MaxRetryAttempts = 2;
        });

        using var response = await host.Client.SendAsync(ResilienceTestResponses.Get());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Do_not_retry_get_requests_with_a_body()
    {
        var calls = 0;
        using var handler = AlwaysUnavailable(() => Interlocked.Increment(ref calls));
        using var host = ResilienceTestHost.Create("retry-get-body", handler, EnableRetry);
        using var request = ResilienceTestResponses.Get();
        request.Content = new StringContent("payload");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Do_not_retry_write_requests_even_when_business_permission_is_granted()
    {
        var calls = 0;
        using var handler = AlwaysUnavailable(() => Interlocked.Increment(ref calls));
        using var host = ResilienceTestHost.Create("retry-post", handler, EnableRetry);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://service.test/resource");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Retry_bodyless_head_requests_when_business_permission_is_granted()
    {
        var calls = 0;
        using var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            var attempt = Interlocked.Increment(ref calls);
            return Task.FromResult(ResilienceTestResponses.Create(
                attempt == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
        });
        using var host = ResilienceTestHost.Create("retry-head", handler, EnableRetry);
        using var request = new HttpRequestMessage(HttpMethod.Head, "https://service.test/resource");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout, 2)]
    [InlineData(HttpStatusCode.InternalServerError, 2)]
    [InlineData(HttpStatusCode.BadGateway, 2)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 2)]
    [InlineData(HttpStatusCode.GatewayTimeout, 2)]
    [InlineData(HttpStatusCode.TooManyRequests, 2)]
    [InlineData(HttpStatusCode.BadRequest, 1)]
    [InlineData(HttpStatusCode.Unauthorized, 1)]
    [InlineData(HttpStatusCode.Forbidden, 1)]
    [InlineData(HttpStatusCode.NotFound, 1)]
    [InlineData(HttpStatusCode.NotImplemented, 1)]
    public async Task Retry_only_configured_transient_http_statuses(HttpStatusCode firstStatus, int expectedCalls)
    {
        var calls = 0;
        using var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            var attempt = Interlocked.Increment(ref calls);
            return Task.FromResult(ResilienceTestResponses.Create(
                attempt == 1 ? firstStatus : HttpStatusCode.OK));
        });
        using var host = ResilienceTestHost.Create("retry-status-" + (int)firstStatus, handler, EnableRetry);

        using var response = await host.Client.SendAsync(ResilienceTestResponses.Get());

        Assert.Equal(expectedCalls == 1 ? firstStatus : HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedCalls, calls);
    }

    [Fact]
    public async Task Retry_plain_http_request_exception_without_status()
    {
        var calls = 0;
        using var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
                return Task.FromException<HttpResponseMessage>(new HttpRequestException("temporary transport failure"));
            return Task.FromResult(ResilienceTestResponses.Create());
        });
        using var host = ResilienceTestHost.Create("retry-transport-exception", handler, EnableRetry);

        using var response = await host.Client.SendAsync(ResilienceTestResponses.Get());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Do_not_retry_http_request_exception_with_permanent_status()
    {
        var calls = 0;
        using var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromException<HttpResponseMessage>(
                new HttpRequestException("not found", null, HttpStatusCode.NotFound));
        });
        using var host = ResilienceTestHost.Create("retry-permanent-exception", handler, EnableRetry);

        await Assert.ThrowsAsync<HttpRequestException>(() => host.Client.SendAsync(ResilienceTestResponses.Get()));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Do_not_retry_authentication_exception()
    {
        var calls = 0;
        using var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromException<HttpResponseMessage>(
                new HttpRequestException("authentication failed", new AuthenticationException("rejected")));
        });
        using var host = ResilienceTestHost.Create("retry-authentication-exception", handler, EnableRetry);

        await Assert.ThrowsAsync<HttpRequestException>(() => host.Client.SendAsync(ResilienceTestResponses.Get()));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Retry_timeout_rejected_exception_when_business_permission_is_granted()
    {
        var calls = 0;
        using var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
                return Task.FromException<HttpResponseMessage>(new TimeoutRejectedException(ResilienceTimeoutKind.Attempt, new("test", "attempt", typeof(HttpResponseMessage))));
            return Task.FromResult(ResilienceTestResponses.Create());
        });
        using var host = ResilienceTestHost.Create("retry-timeout-exception", handler, EnableRetry);

        using var response = await host.Client.SendAsync(ResilienceTestResponses.Get());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, calls);
    }

    private static void EnableRetry(BingHttpResilienceOptions options)
    {
        options.Retry.MaxRetryAttempts = 1;
        options.Retry.Delay = TimeSpan.Zero;
        options.Retry.MaxDelay = TimeSpan.Zero;
        options.Retry.UseJitter = false;
        options.CanRetryRequest = _ => true;
    }

    private static DelegateHttpMessageHandler AlwaysUnavailable(Action onSend) =>
        new((_, _) =>
        {
            onSend();
            return Task.FromResult(ResilienceTestResponses.Create(HttpStatusCode.ServiceUnavailable));
        });
}
