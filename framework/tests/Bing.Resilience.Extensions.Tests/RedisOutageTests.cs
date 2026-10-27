using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Bing.Resilience.Distributed;
using Bing.Resilience.Distributed.Redis;
using CSRedis;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

[Collection(RedisCircuitTestCollection.Name)]
public sealed class RedisOutageTests
{
    [RedisFact]
    public async Task EndpointScopeUsesDistinctStableKeysAndReportsDoNotCrossEndpoints()
    {
        using var client = new CSRedisClient(RedisTestSettings.RedisConnectionString);
        var store = new RedisDistributedCircuitStateStore(client);
        var config = new DistributedCircuitConfiguration("v1", .5, 2, 30000, 15000, 1000);
        var observer = new DistributedCircuitObserver<int>(store, new()
        {
            CircuitKey = "endpoint-test:" + Guid.NewGuid().ToString("N"), Configuration = config,
            Scope = DistributedCircuitScope.Endpoint, FailureMode = DistributedCircuitFailureMode.FailClosed
        }, outcome => outcome.Exception is not null);
        var one = Context("one"); var two = Context("two");
        Assert.NotEqual(observer.GetCircuitKey(one), observer.GetCircuitKey(two));
        for (var i = 0; i < 2; i++)
        {
            await using var attempt = await observer.BeginAsync(one, default);
            await attempt.CompleteAsync(ResilienceOutcome<int>.FromException(new IOException()), default);
        }
        await Assert.ThrowsAsync<DistributedCircuitRejectedException>(() => observer.BeginAsync(one, default).AsTask());
        await using var allowed = await observer.BeginAsync(two, default);
        await allowed.CompleteAsync(ResilienceOutcome<int>.FromResult(1), default);
        Assert.Equal(ResilienceCircuitState.Open, (await store.GetSnapshotAsync(observer.GetCircuitKey(one), config)).State);
        Assert.Equal(ResilienceCircuitState.Closed, (await store.GetSnapshotAsync(observer.GetCircuitKey(two), config)).State);
    }

    [DockerRedisFaultFact]
    public async Task PausedRedisHonorsExplicitFailureModesAndRecoversWithoutLosingIsolation()
    {
        var container = "bing-resilience-ex-redis";
        var task = "20261027-resilience-extensions";
        var label = "task";
        var connectionFile = Environment.GetEnvironmentVariable(RedisTestSettings.ConnectionFileVariable);
        if (connectionFile is not null)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(connectionFile, Encoding.UTF8));
            if (document.RootElement.GetProperty("redis").TryGetProperty("dockerContainer", out var name))
            {
                container = name.GetString()!;
                task = document.RootElement.GetProperty("redis").GetProperty("taskId").GetString()!;
                label = "bing.task";
                Assert.StartsWith("bing-resilience-", container);
                Assert.StartsWith("20261027-resilience-", task);
            }
        }
        Assert.Equal(task, (await Docker("inspect", "--format", "{{index .Config.Labels \"" + label + "\"}}", container)).Trim());
        using var client = new CSRedisClient(RedisTestSettings.RedisConnectionString);
        var store = new RedisDistributedCircuitStateStore(client);
        var config = new DistributedCircuitConfiguration("v1", .5, 2, 30000, 15000, 1000);
        var key = "outage:" + Guid.NewGuid().ToString("N");
        var initial = await store.GetSnapshotAsync(key, config);
        var isolated = await store.ControlAsync(key, config, initial.Epoch, true);
        var closed = Observer(DistributedCircuitFailureMode.FailClosed, key);
        var opened = Observer(DistributedCircuitFailureMode.FailOpenWithLocalCircuit, key + ":local");
        var context = Context("one");
        await Docker("pause", container);
        try
        {
            await Assert.ThrowsAsync<ResilienceStateStoreUnavailableException>(() => closed.BeginAsync(context, default).AsTask());
            for (var i = 0; i < 2; i++)
            {
                await using var attempt = await opened.BeginAsync(context, default);
                await attempt.CompleteAsync(ResilienceOutcome<int>.FromException(new IOException()), default);
            }
            await Assert.ThrowsAsync<ResilienceCircuitOpenException>(() => opened.BeginAsync(context, default).AsTask());
            using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.AdmitAsync(key + ":unknown", config, "same-attempt", cancelled.Token).AsTask());
        }
        finally { await Docker("unpause", container); }
        // 被取消的客户端等待不能证明脚本未执行；同 AttemptId 协议安全接回该准入。
        var resumed = await store.AdmitAsync(key + ":unknown", config, "same-attempt");
        Assert.True(resumed.Allowed);
        await store.ReportAsync(resumed, config, DistributedCircuitOutcome.Failure);
        await store.ReportAsync(resumed, config, DistributedCircuitOutcome.Failure);
        Assert.Equal(ResilienceCircuitState.Closed, (await store.GetSnapshotAsync(key + ":unknown", config)).State);
        var after = await store.GetSnapshotAsync(key, config);
        Assert.Equal(ResilienceCircuitState.Isolated, after.State);
        Assert.Equal(isolated.Epoch, after.Epoch);
        await Assert.ThrowsAsync<DistributedCircuitRejectedException>(() => closed.BeginAsync(context, default).AsTask());

        DistributedCircuitObserver<int> Observer(DistributedCircuitFailureMode mode, string circuitKey) => new(store, new()
        {
            CircuitKey = circuitKey, Configuration = config, FailureMode = mode, StoreTimeout = TimeSpan.FromMilliseconds(150)
        }, outcome => outcome.Exception is not null);
    }
    private static ResilienceAttemptContext Context(string endpoint) => new(new("http.endpoint", "catalog", typeof(int), endpoint), new());
    private static async Task<string> Docker(params string[] arguments)
    {
        var start = new ProcessStartInfo("docker") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await process.WaitForExitAsync(timeout.Token);
        if (process.ExitCode != 0) throw new InvalidOperationException(await error);
        return await output;
    }
}
public sealed class DockerRedisFaultFactAttribute : FactAttribute
{
    public DockerRedisFaultFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("BING_RESILIENCE_ALLOW_DOCKER_FAULTS") != "1")
            Skip = "Task-owned Docker pause/unpause probe requires BING_RESILIENCE_ALLOW_DOCKER_FAULTS=1.";
    }
}
