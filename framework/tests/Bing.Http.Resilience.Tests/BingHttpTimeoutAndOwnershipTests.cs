using System.Net;
using TimeoutRejectedException = Bing.Resilience.ResilienceTimeoutException;
using Xunit;

namespace Bing.Http.Resilience.Tests;

public sealed class BingHttpTimeoutAndOwnershipTests
{
    [Fact]
    public async Task Attempt_timeout_cancels_a_blocked_send()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var handler = new DelegateHttpMessageHandler(async (_, cancellationToken) =>
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ResilienceTestResponses.Create();
        });
        using var host = ResilienceTestHost.Create("attempt-timeout", handler, options =>
        {
            options.RequestTimeout = TimeSpan.FromSeconds(3);
            options.AttemptTimeout = TimeSpan.FromMilliseconds(400);
        });

        var send = host.Client.SendAsync(ResilienceTestResponses.Get());
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await Assert.ThrowsAsync<TimeoutRejectedException>(() => send);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retry_after_beyond_total_budget_does_not_start_an_early_attempt(bool useDateHeader)
    {
        var calls = 0;
        using var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            Interlocked.Increment(ref calls);
            var response = ResilienceTestResponses.Create(HttpStatusCode.ServiceUnavailable);
            RetryAfterHeaders.Set(response, useDateHeader);
            return Task.FromResult(response);
        });
        using var host = ResilienceTestHost.Create("retry-after-budget-" + useDateHeader, handler, options =>
        {
            options.RequestTimeout = TimeSpan.FromMilliseconds(600);
            options.AttemptTimeout = TimeSpan.FromMilliseconds(500);
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.Delay = TimeSpan.Zero;
            options.Retry.MaxDelay = TimeSpan.FromMilliseconds(100);
            options.Retry.UseJitter = false;
            options.CanRetryRequest = _ => true;
        });

        await Assert.ThrowsAsync<TimeoutRejectedException>(() => host.Client.SendAsync(ResilienceTestResponses.Get()));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Caller_cancellation_reaches_handler_and_prevents_retry()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var handler = new DelegateHttpMessageHandler(async (_, cancellationToken) =>
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ResilienceTestResponses.Create();
        });
        using var host = ResilienceTestHost.Create("caller-cancel", handler, options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.CanRetryRequest = _ => true;
        });
        using var cancellation = new CancellationTokenSource();

        var send = host.Client.SendAsync(ResilienceTestResponses.Get(), cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Dispose_retried_response_once_and_leave_final_response_owned_by_caller()
    {
        var calls = 0;
        var retriedContent = new TrackingContent();
        var finalContent = new TrackingContent();
        using var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = retriedContent
                });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = finalContent
            });
        });
        using var host = ResilienceTestHost.Create("response-ownership", handler, EnableRetry);

        var response = await host.Client.SendAsync(
            ResilienceTestResponses.Get(), HttpCompletionOption.ResponseHeadersRead);

        try
        {
            Assert.Equal(1, retriedContent.DisposeCount);
            Assert.Equal(0, finalContent.DisposeCount);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            response.Dispose();
        }

        Assert.Equal(1, finalContent.DisposeCount);
    }

    private static void EnableRetry(BingHttpResilienceOptions options)
    {
        options.Retry.MaxRetryAttempts = 1;
        options.Retry.Delay = TimeSpan.Zero;
        options.Retry.MaxDelay = TimeSpan.Zero;
        options.Retry.UseJitter = false;
        options.CanRetryRequest = _ => true;
    }
}
