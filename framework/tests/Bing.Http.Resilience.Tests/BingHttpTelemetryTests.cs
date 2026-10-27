using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using TimeoutRejectedException = Bing.Resilience.ResilienceTimeoutException;
using Xunit;

namespace Bing.Http.Resilience.Tests;

public sealed class BingHttpTelemetryTests
{
    [Fact]
    public async Task NeutralTelemetryReportsRetryAndDistinctTimeoutStrategiesWithoutRequestSecrets()
    {
        var integerMeasurements = new ConcurrentBag<(string InstrumentName, KeyValuePair<string, object?>[] Tags)>();
        var durationMeasurements = new ConcurrentBag<(string InstrumentName, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meter) =>
        {
            if (instrument.Meter.Name == ResilienceDiagnostics.SourceName) meter.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, values, _) =>
            integerMeasurements.Add((instrument.Name, values.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, _, values, _) =>
            durationMeasurements.Add((instrument.Name, values.ToArray())));
        listener.Start();

        var clientPrefix = $"telemetry-{Guid.NewGuid():N}";
        const string profileName = "resilience-profile";
        var retryClientName = $"{clientPrefix}-retry";
        var attemptTimeoutClientName = $"{clientPrefix}-attempt-timeout";
        var requestTimeoutClientName = $"{clientPrefix}-request-timeout";
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        var retryAttempts = 0;
        AddTelemetryClient(services, retryClientName, profileName,
            new DelegateHttpMessageHandler((_, _) =>
            {
                var attempt = Interlocked.Increment(ref retryAttempts);
                return Task.FromResult(ResilienceTestResponses.Create(
                    attempt == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
            }), options =>
            {
                options.CanRetryRequest = _ => true;
                options.Retry.MaxRetryAttempts = 1;
                options.Retry.Delay = TimeSpan.Zero;
                options.Retry.MaxDelay = TimeSpan.Zero;
                options.Retry.UseJitter = false;
            });

        AddTelemetryClient(services, attemptTimeoutClientName, profileName,
            new DelegateHttpMessageHandler(async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return ResilienceTestResponses.Create();
            }), options =>
            {
                options.RequestTimeout = TimeSpan.FromSeconds(2);
                options.AttemptTimeout = TimeSpan.FromMilliseconds(75);
            });

        var requestTimeoutAttempts = 0;
        AddTelemetryClient(services, requestTimeoutClientName, profileName,
            new DelegateHttpMessageHandler((_, _) =>
            {
                Interlocked.Increment(ref requestTimeoutAttempts);
                return Task.FromResult(ResilienceTestResponses.Create(HttpStatusCode.ServiceUnavailable));
            }), options =>
            {
                options.RequestTimeout = TimeSpan.FromMilliseconds(250);
                options.AttemptTimeout = TimeSpan.FromMilliseconds(100);
                options.CanRetryRequest = _ => true;
                options.Retry.MaxRetryAttempts = 1;
                options.Retry.Delay = TimeSpan.FromSeconds(1);
                options.Retry.MaxDelay = TimeSpan.FromSeconds(1);
                options.Retry.UseJitter = false;
            });

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var retryClient = factory.CreateClient(retryClientName);
        using var attemptTimeoutClient = factory.CreateClient(attemptTimeoutClientName);
        using var requestTimeoutClient = factory.CreateClient(requestTimeoutClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "http://service.test/customer-private/123?token=secret-query");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer secret-header");
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", "private-tenant");
        using var result = await retryClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        await Assert.ThrowsAsync<TimeoutRejectedException>(() =>
            attemptTimeoutClient.GetAsync("http://service.test/attempt-timeout"));
        await Assert.ThrowsAsync<TimeoutRejectedException>(() =>
            requestTimeoutClient.GetAsync("http://service.test/request-timeout"));

        Assert.Equal(2, retryAttempts);
        Assert.Equal(1, requestTimeoutAttempts);

        var ownIntegerMeasurements = integerMeasurements
            .Where(measurement => HasPipelinePrefix(measurement.Tags, clientPrefix))
            .ToArray();
        var ownDurationMeasurements = durationMeasurements
            .Where(measurement => HasPipelinePrefix(measurement.Tags, clientPrefix))
            .ToArray();
        Assert.Contains(ownIntegerMeasurements, measurement =>
            GetTagValue(measurement.Tags, "resilience.event") == "retry");
        Assert.Contains(ownIntegerMeasurements, measurement =>
            GetTagValue(measurement.Tags, "resilience.event") == "timeout.attempt");
        Assert.Contains(ownIntegerMeasurements, measurement =>
            GetTagValue(measurement.Tags, "resilience.event") == "timeout.execution");
        Assert.Equal(3, ownIntegerMeasurements.Count(m => m.InstrumentName == "bing.resilience.executions"));

        var ownMeasurements = ownIntegerMeasurements
            .Select(measurement => measurement.Tags)
            .Concat(ownDurationMeasurements.Select(measurement => measurement.Tags))
            .ToArray();
        Assert.All(new[] { retryClientName, attemptTimeoutClientName, requestTimeoutClientName }, clientName =>
            Assert.Contains(ownMeasurements.SelectMany(tags => tags), tag =>
                HasPipelineIdentity(tag, clientName, profileName)));
        Assert.DoesNotContain(ownMeasurements.SelectMany(tags => tags), tag =>
            new[] { "path", "query", "url", "authorization", "token", "tenant" }
                .Any(part => tag.Key.Contains(part, StringComparison.OrdinalIgnoreCase)) ||
            tag.Value?.ToString() is string value &&
            (value.Contains("secret-", StringComparison.Ordinal) ||
             value.Contains("customer-private", StringComparison.Ordinal) ||
             value.Contains("private-tenant", StringComparison.Ordinal)));
    }

    private static void AddTelemetryClient(
        IServiceCollection services,
        string clientName,
        string profileName,
        HttpMessageHandler handler,
        Action<BingHttpResilienceOptions> configure)
    {
        services.AddHttpClient(clientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddBingHttpResilience(profileName, configure);
    }

    private static bool HasPipelinePrefix(
        IEnumerable<KeyValuePair<string, object?>> tags,
        string clientPrefix) =>
        GetTagValue(tags, "resilience.pipeline")?.Contains(clientPrefix, StringComparison.Ordinal) == true;

    private static string? GetTagValue(IEnumerable<KeyValuePair<string, object?>> tags, string key) =>
        tags.FirstOrDefault(tag => tag.Key == key).Value?.ToString();

    private static bool HasPipelineIdentity(
        KeyValuePair<string, object?> tag,
        string clientName,
        string profileName)
    {
        if (tag.Key != "resilience.pipeline") return false;
        var name = tag.Value?.ToString();
        return name != null &&
            name == clientName;
    }
}
