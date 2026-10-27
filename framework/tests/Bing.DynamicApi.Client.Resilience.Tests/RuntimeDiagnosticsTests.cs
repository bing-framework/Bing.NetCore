using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Bing.DynamicApi.Client;
using Bing.DynamicApi.Client.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Bing.DynamicApi.Client.Resilience.Tests.TestSupport;

namespace Bing.DynamicApi.Client.Resilience.Tests;

public sealed class RuntimeDiagnosticsTests
{
    private const string ActivitySourceName = "Bing.DynamicApi.Client";

    [Fact]
    public async Task Successful_pipeline_call_stops_invocation_and_definition_activities()
    {
        var activities = new ConcurrentQueue<RecordedActivity>();
        using var listener = Listen(activities);
        var remoteName = NewRemoteName();
        using var provider = CreateProvider(remoteName, (request, _) => Task.FromResult(
            IsDefinition(request) ? Definition() : Json("ok")));

        Assert.Equal("ok", await provider.GetRequiredService<IProbe>().GetAsync());

        var remoteActivities = ForRemote(activities, remoteName);
        Assert.Equal(2, remoteActivities.Length);
        var invocation = Assert.Single(remoteActivities, activity => activity.OperationName == "dynamic_api.invocation");
        var definition = Assert.Single(remoteActivities, activity => activity.OperationName == "dynamic_api.definition");
        Assert.Equal("Invocation", invocation.GetTagValue("bing.request.kind"));
        Assert.Equal("Definition", definition.GetTagValue("bing.request.kind"));
        Assert.Equal(remoteName, invocation.GetTagValue("bing.remote.name"));
        Assert.Equal(remoteName, definition.GetTagValue("bing.remote.name"));
        AssertNoSensitiveTags(remoteActivities);
    }

    [Fact]
    public async Task Business_body_budget_timeout_records_operation_timeout_on_invocation()
    {
        var activities = new ConcurrentQueue<RecordedActivity>();
        using var listener = Listen(activities);
        var remoteName = NewRemoteName();
        var stream = new BlockingStream();
        using var provider = CreateProvider(remoteName, (request, _) => Task.FromResult(IsDefinition(request)
            ? Definition()
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) }),
            options => options.OperationTimeout = TimeSpan.FromMilliseconds(500));

        var call = provider.GetRequiredService<IProbe>().GetAsync();
        await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var exception = await Assert.ThrowsAsync<TimeoutException>(() => call);

        Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
        Assert.True(stream.Disposed);
        var remoteActivities = ForRemote(activities, remoteName);
        var invocation = Assert.Single(remoteActivities, activity => activity.OperationName == "dynamic_api.invocation");
        Assert.Contains(invocation.Events, activityEvent => activityEvent.Name == "operation_timeout");
        AssertNoSensitiveTags(remoteActivities);
    }

    [Fact]
    public async Task Definition_body_budget_timeout_records_definition_timeout()
    {
        var activities = new ConcurrentQueue<RecordedActivity>();
        using var listener = Listen(activities);
        var remoteName = NewRemoteName();
        var stream = new BlockingStream();
        using var provider = CreateProvider(remoteName, (request, _) => Task.FromResult(IsDefinition(request)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) }
            : Json("unused")),
            options => options.DefinitionTimeout = TimeSpan.FromMilliseconds(500));

        var call = provider.GetRequiredService<IProbe>().GetAsync();
        await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var exception = await Assert.ThrowsAsync<TimeoutException>(() => call);

        Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
        Assert.True(stream.Disposed);
        var remoteActivities = ForRemote(activities, remoteName);
        var definition = Assert.Single(remoteActivities, activity => activity.OperationName == "dynamic_api.definition");
        Assert.Contains(definition.Events, activityEvent => activityEvent.Name == "definition_timeout");
        Assert.DoesNotContain(remoteActivities.SelectMany(activity => activity.Events),
            activityEvent => activityEvent.Name == "operation_timeout");
        AssertNoSensitiveTags(remoteActivities);
    }

    [Fact]
    public async Task Caller_cancelled_body_records_caller_cancelled_and_preserves_cancellation()
    {
        var activities = new ConcurrentQueue<RecordedActivity>();
        using var listener = Listen(activities);
        var remoteName = NewRemoteName();
        var stream = new BlockingStream();
        using var provider = CreateProvider(remoteName, (request, _) => Task.FromResult(IsDefinition(request)
            ? Definition()
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) }));
        using var cancellation = new CancellationTokenSource();

        var call = provider.GetRequiredService<IProbe>().GetAsync(cancellation.Token);
        await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);

        Assert.True(stream.Disposed);
        var remoteActivities = ForRemote(activities, remoteName);
        var invocation = Assert.Single(remoteActivities, activity => activity.OperationName == "dynamic_api.invocation");
        Assert.Contains(invocation.Events, activityEvent => activityEvent.Name == "caller_cancelled");
        AssertNoSensitiveTags(remoteActivities);
    }

    private static ServiceProvider CreateProvider(
        string remoteName,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback,
        Action<BingDynamicApiClientResilienceOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        services.AddSingleton<IBingDynamicApiRequestContext>(new SensitiveRequestContext());
        services.AddBingDynamicApiClient(options => options.AddRemoteService(remoteName, remote =>
        {
            remote.BaseUrl = "http://service.test/customer-private";
            remote.Headers["X-Api-Key"] = "secret-header";
        }));
        services.AddBingDynamicApiClient<IProbe>(remoteName);
        services.AddBingDynamicApiClientResilience(remoteName, options =>
        {
            options.Http.RequestTimeout = TimeSpan.FromSeconds(10);
            options.Http.AttemptTimeout = TimeSpan.FromSeconds(5);
            options.Http.Retry.MaxRetryAttempts = 0;
            options.Http.CircuitBreaker.Enabled = false;
            options.Http.Concurrency.Enabled = false;
            options.OperationTimeout = TimeSpan.FromSeconds(5);
            options.DefinitionTimeout = TimeSpan.FromSeconds(5);
            configure?.Invoke(options);
        }).ConfigurePrimaryHttpMessageHandler(() => new CallbackHandler((request, cancellationToken) =>
        {
            AssertSensitiveRequestData(request);
            return callback(request, cancellationToken);
        }));
        return services.BuildServiceProvider();
    }

    private static void AssertSensitiveRequestData(HttpRequestMessage request)
    {
        Assert.Contains("customer-private", request.RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal("secret-header", Assert.Single(request.Headers.GetValues("X-Api-Key")));
        Assert.Equal("private-tenant", Assert.Single(request.Headers.GetValues("X-Tenant-Id")));
    }

    private static ActivityListener Listen(ConcurrentQueue<RecordedActivity> activities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activities.Enqueue(new RecordedActivity(
                activity.OperationName,
                activity.TagObjects.ToArray(),
                activity.Events.Select(activityEvent => new RecordedActivityEvent(
                    activityEvent.Name,
                    activityEvent.Tags.ToArray())).ToArray()))
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static RecordedActivity[] ForRemote(IEnumerable<RecordedActivity> activities, string remoteName) =>
        activities.Where(activity => activity.GetTagValue("bing.remote.name") == remoteName).ToArray();

    private static void AssertNoSensitiveTags(IEnumerable<RecordedActivity> activities)
    {
        var tags = activities.SelectMany(activity => activity.Tags.Concat(
            activity.Events.SelectMany(activityEvent => activityEvent.Tags)));
        Assert.DoesNotContain(tags, tag =>
            new[] { "path", "query", "url", "authorization", "api-key", "token", "tenant", "correlation" }
                .Any(part => tag.Key.Contains(part, StringComparison.OrdinalIgnoreCase)) ||
            tag.Value?.ToString() is string value &&
            (value.Contains("customer-private", StringComparison.Ordinal) ||
             value.Contains("secret-header", StringComparison.Ordinal) ||
             value.Contains("private-tenant", StringComparison.Ordinal) ||
             value.Contains("private-correlation", StringComparison.Ordinal) ||
             value.Contains("private-language", StringComparison.Ordinal)));
    }

    private static string NewRemoteName() => $"diagnostics-{Guid.NewGuid():N}";

    private sealed class SensitiveRequestContext : IBingDynamicApiRequestContext
    {
        public string TenantId => "private-tenant";
        public string CorrelationId => "private-correlation";
        public string Language => "private-language";
    }

    private sealed record RecordedActivity(
        string OperationName,
        KeyValuePair<string, object?>[] Tags,
        RecordedActivityEvent[] Events)
    {
        public string? GetTagValue(string key) =>
            Tags.FirstOrDefault(tag => tag.Key == key).Value?.ToString();
    }

    private sealed record RecordedActivityEvent(string Name, KeyValuePair<string, object?>[] Tags);
}
