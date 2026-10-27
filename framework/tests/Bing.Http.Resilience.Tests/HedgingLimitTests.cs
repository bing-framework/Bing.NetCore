using System.Net;
using Bing.Http.Resilience;
using Bing.Resilience;
using Xunit;

namespace Bing.Http.Resilience.Tests;

public sealed class HedgingLimitTests
{
    private static readonly Uri PrimaryOrigin = new("https://primary.test/");
    private static readonly Uri BackupOrigin = new("https://backup.test/");

    [Fact]
    public async Task EndpointPermitLimitRoutesOverlappingCallToBackupAndCapsPrimaryConcurrency()
    {
        var primaryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePrimary = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondBackupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var primarySends = 0;
        var backupSends = 0;
        var primaryActive = 0;
        var primaryPeak = 0;
        var backupActive = 0;
        var backupPeak = 0;

        using var host = CreateHost(async (request, token) =>
        {
            if (request.RequestUri!.Host == PrimaryOrigin.Host)
            {
                var active = Interlocked.Increment(ref primaryActive);
                UpdatePeak(ref primaryPeak, active);
                try
                {
                    if (Interlocked.Increment(ref primarySends) == 1)
                    {
                        primaryStarted.TrySetResult();
                        return await releasePrimary.Task.WaitAsync(token);
                    }
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                }
                finally { Interlocked.Decrement(ref primaryActive); }
            }

            Interlocked.Increment(ref backupSends);
            var backupNowActive = Interlocked.Increment(ref backupActive);
            UpdatePeak(ref backupPeak, backupNowActive);
            try
            {
                if (request.RequestUri.Query.Contains("request=second", StringComparison.Ordinal))
                    secondBackupStarted.TrySetResult();
                var status = request.RequestUri.Query.Contains("request=second", StringComparison.Ordinal)
                    ? HttpStatusCode.Created
                    : HttpStatusCode.ServiceUnavailable;
                return new HttpResponseMessage(status);
            }
            finally { Interlocked.Decrement(ref backupActive); }
        }, options => options.Hedging.Delay = TimeSpan.FromMilliseconds(30));

        using var firstRequest = Request("first");
        using var secondRequest = Request("second");
        var firstSend = host.Client.SendAsync(firstRequest);
        Task<HttpResponseMessage>? secondSend = null;
        try
        {
            await primaryStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            secondSend = host.Client.SendAsync(secondRequest);
            using var secondResponse = await secondSend.WaitAsync(TimeSpan.FromSeconds(3));
            await secondBackupStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
            Assert.Equal(1, Volatile.Read(ref primarySends));
            Assert.True(Volatile.Read(ref primaryPeak) <= 1);
            Assert.True(Volatile.Read(ref backupPeak) <= 1);
            Assert.True(Volatile.Read(ref backupSends) >= 1);
        }
        finally
        {
            releasePrimary.TrySetResult(new HttpResponseMessage(HttpStatusCode.OK));
        }

        using var firstResponse = await firstSend.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
    }

    [Fact]
    public async Task GlobalRateLimitRejectionStopsHedgingBeforeBackupSend()
    {
        var primarySends = 0;
        var backupSends = 0;
        using var host = CreateHost((request, _) =>
        {
            if (request.RequestUri!.Host == PrimaryOrigin.Host)
            {
                Interlocked.Increment(ref primarySends);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }

            Interlocked.Increment(ref backupSends);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
        }, options =>
        {
            options.Hedging.Delay = TimeSpan.Zero;
            options.RateLimit.Enabled = true;
            options.RateLimit.TokenLimit = 1;
            options.RateLimit.TokensPerPeriod = 1;
            options.RateLimit.ReplenishmentPeriod = TimeSpan.FromHours(1);
            options.RateLimit.QueueLimit = 0;
        });

        using var request = Request("rate-limit");
        var error = await Assert.ThrowsAsync<ResilienceRejectedException>(() => host.Client.SendAsync(request));

        Assert.Equal(ResilienceRejectionReason.RateLimit, error.Reason);
        Assert.Equal(1, Volatile.Read(ref primarySends));
        Assert.Equal(0, Volatile.Read(ref backupSends));
    }

    private static HttpRequestMessage Request(string name) =>
        new(HttpMethod.Get, new Uri(PrimaryOrigin, $"resource?request={name}"));

    private static ResilienceTestHost CreateHost(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
        Action<BingHttpResilienceOptions>? configure = null) =>
        ResilienceTestHost.Create("hedging-limits", new DelegateHttpMessageHandler(send), options =>
        {
            options.Mode = BingHttpResilienceMode.Hedging;
            options.CanHedgeRequest = _ => true;
            options.Retry.MaxRetryAttempts = 0;
            options.Hedging.MaxHedgedAttempts = 1;
            options.Hedging.Delay = TimeSpan.FromMilliseconds(30);
            options.Hedging.EndpointPermitLimit = 1;
            options.Hedging.EndpointQueueLimit = 0;
            options.Hedging.Endpoints.Add(new("primary", PrimaryOrigin));
            options.Hedging.Endpoints.Add(new("backup", BackupOrigin));
            options.Hedging.ValidateCustomTransport = handler => handler is DelegateHttpMessageHandler;
            configure?.Invoke(options);
        });

    private static void UpdatePeak(ref int peak, int current)
    {
        var observed = Volatile.Read(ref peak);
        while (current > observed)
        {
            var previous = Interlocked.CompareExchange(ref peak, current, observed);
            if (previous == observed) return;
            observed = previous;
        }
    }
}
