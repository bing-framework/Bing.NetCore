using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Bing.Resilience;
using Bing.Resilience.Distributed;
using Bing.Resilience.Distributed.Redis;
using CSRedis;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

[CollectionDefinition("Redis distributed circuit integration", DisableParallelization = true)]
public sealed class RedisCircuitTestCollection
{
    public const string Name = "Redis distributed circuit integration";
}

[Collection(RedisCircuitTestCollection.Name)]
public sealed class RedisCircuitTests
{
    [RedisFact]
    public async Task IndependentClientsShareOpenStateAndDuplicateReportsCountOnce()
    {
        using var fixture = RedisFixture.Create();
        var configuration = Configuration(minimumThroughput: 2, failureRatio: 0.5);

        var first = await fixture.First.AdmitAsync(fixture.Key, configuration, "failure-first");
        Assert.True(first.Allowed);
        await fixture.First.ReportAsync(first, configuration, DistributedCircuitOutcome.Failure);
        await fixture.First.ReportAsync(first, configuration, DistributedCircuitOutcome.Failure);

        var afterDuplicate = await fixture.Second.GetSnapshotAsync(fixture.Key, configuration);
        Assert.Equal(ResilienceCircuitState.Closed, afterDuplicate.State);

        var second = await fixture.Second.AdmitAsync(fixture.Key, configuration, "failure-second");
        Assert.True(second.Allowed);
        await fixture.Second.ReportAsync(second, configuration, DistributedCircuitOutcome.Failure);

        var shared = await fixture.First.GetSnapshotAsync(fixture.Key, configuration);
        Assert.Equal(ResilienceCircuitState.Open, shared.State);
        Assert.Equal(1, shared.Epoch);
        Assert.Equal(configuration.Version, shared.ConfigVersion);
    }

    [RedisFact]
    public async Task StaleEpochSuccessCannotCloseOpenOrIsolatedCircuit()
    {
        using var fixture = RedisFixture.Create();
        var configuration = Configuration(minimumThroughput: 2, failureRatio: 0.5);
        var staleForOpen = await fixture.First.AdmitAsync(fixture.Key, configuration, "stale-open-success");
        var staleForIsolated = await fixture.Second.AdmitAsync(fixture.Key, configuration, "stale-isolated-success");
        var failureOne = await fixture.First.AdmitAsync(fixture.Key, configuration, "open-failure-one");
        var failureTwo = await fixture.Second.AdmitAsync(fixture.Key, configuration, "open-failure-two");
        Assert.All(new[] { staleForOpen, staleForIsolated, failureOne, failureTwo }, admission => Assert.True(admission.Allowed));

        await fixture.First.ReportAsync(failureOne, configuration, DistributedCircuitOutcome.Failure);
        await fixture.Second.ReportAsync(failureTwo, configuration, DistributedCircuitOutcome.Failure);
        var opened = await fixture.Second.GetSnapshotAsync(fixture.Key, configuration);
        Assert.Equal(ResilienceCircuitState.Open, opened.State);

        await fixture.First.ReportAsync(staleForOpen, configuration, DistributedCircuitOutcome.Success);
        var afterStaleOpenReport = await fixture.Second.GetSnapshotAsync(fixture.Key, configuration);
        Assert.Equal(ResilienceCircuitState.Open, afterStaleOpenReport.State);
        Assert.Equal(opened.Epoch, afterStaleOpenReport.Epoch);

        var isolated = await fixture.Second.ControlAsync(fixture.Key, configuration, opened.Epoch, isolate: true);
        Assert.Equal(ResilienceCircuitState.Isolated, isolated.State);
        await fixture.First.ReportAsync(staleForIsolated, configuration, DistributedCircuitOutcome.Success);

        var afterStaleIsolatedReport = await fixture.Second.GetSnapshotAsync(fixture.Key, configuration);
        Assert.Equal(ResilienceCircuitState.Isolated, afterStaleIsolatedReport.State);
        Assert.Equal(isolated.Epoch, afterStaleIsolatedReport.Epoch);
    }

    [RedisFact]
    public async Task HalfOpenProbeIsUniqueAndExpiredProbeCannotCompleteNewEpoch()
    {
        using var fixture = RedisFixture.Create();
        var configuration = Configuration(minimumThroughput: 2, failureRatio: 0.5,
            samplingMilliseconds: 2000, breakMilliseconds: 1500, probeLeaseMilliseconds: 1200);
        await OpenAsync(fixture, configuration, "probe-open");

        var blocked = await fixture.First.AdmitAsync(fixture.Key, configuration, "probe-wait");
        if (blocked.Allowed)
        {
            await fixture.First.ReportAsync(blocked, configuration, DistributedCircuitOutcome.Failure);
            blocked = await fixture.First.AdmitAsync(fixture.Key, configuration, "probe-wait-again");
        }
        Assert.False(blocked.Allowed);
        if (blocked.RetryAfter > TimeSpan.Zero)
            await Task.Delay(blocked.RetryAfter + TimeSpan.FromMilliseconds(100));

        var concurrent = await Task.WhenAll(
            fixture.First.AdmitAsync(fixture.Key, configuration, "probe-racer-a").AsTask(),
            fixture.Second.AdmitAsync(fixture.Key, configuration, "probe-racer-b").AsTask());
        var firstProbe = Assert.Single(concurrent, admission => admission.Allowed);
        Assert.Single(concurrent, admission => !admission.Allowed);
        Assert.NotNull(firstProbe.LeaseExpiresAt);

        var untilExpired = firstProbe.LeaseExpiresAt!.Value - DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(100);
        if (untilExpired > TimeSpan.Zero)
            await Task.Delay(untilExpired);

        var replacementProbe = await fixture.Second.AdmitAsync(fixture.Key, configuration, "probe-replacement");
        Assert.True(replacementProbe.Allowed);
        Assert.True(replacementProbe.Epoch > firstProbe.Epoch);

        var reportFromExpiredProbe = firstProbe;
        await fixture.First.ReportAsync(reportFromExpiredProbe, configuration, DistributedCircuitOutcome.Success);
        var stillHalfOpen = await fixture.Second.GetSnapshotAsync(fixture.Key, configuration);
        Assert.Equal(ResilienceCircuitState.HalfOpen, stillHalfOpen.State);
        Assert.Equal(replacementProbe.Epoch, stillHalfOpen.Epoch);

        await fixture.Second.ReportAsync(replacementProbe, configuration, DistributedCircuitOutcome.Success);
        var closed = await fixture.First.GetSnapshotAsync(fixture.Key, configuration);
        Assert.Equal(ResilienceCircuitState.Closed, closed.State);
        Assert.True(closed.Epoch > replacementProbe.Epoch);
    }

    [RedisFact]
    public async Task ConfigurationAndManualControlRejectStaleVersions()
    {
        using var fixture = RedisFixture.Create();
        var configuration = Configuration(minimumThroughput: 2, failureRatio: 0.5);
        var initial = await fixture.First.GetSnapshotAsync(fixture.Key, configuration);
        var changedConfiguration = configuration with { Version = "v2" };

        await Assert.ThrowsAsync<DistributedCircuitVersionException>(() =>
            fixture.Second.GetSnapshotAsync(fixture.Key, changedConfiguration).AsTask());
        await Assert.ThrowsAsync<DistributedCircuitVersionException>(() =>
            fixture.Second.ControlAsync(fixture.Key, configuration, initial.Epoch + 1, isolate: true).AsTask());

        var isolated = await fixture.First.ControlAsync(fixture.Key, configuration, initial.Epoch, isolate: true);
        Assert.Equal(ResilienceCircuitState.Isolated, isolated.State);
        await Assert.ThrowsAsync<DistributedCircuitVersionException>(() =>
            fixture.Second.ControlAsync(fixture.Key, configuration, initial.Epoch, isolate: false).AsTask());

        var current = await fixture.Second.GetSnapshotAsync(fixture.Key, configuration);
        Assert.Equal(ResilienceCircuitState.Isolated, current.State);
        Assert.Equal(isolated.Epoch, current.Epoch);
    }

    [RedisFact]
    public async Task CircuitOperationsRemainUsableAfterScriptFlush()
    {
        using var fixture = RedisFixture.Create();
        var configuration = Configuration(minimumThroughput: 2, failureRatio: 0.5);
        var beforeFlush = await fixture.First.GetSnapshotAsync(fixture.Key, configuration);
        Assert.Equal(ResilienceCircuitState.Closed, beforeFlush.State);

        await fixture.ClientOne.ScriptFlushAsync();

        var admission = await fixture.Second.AdmitAsync(fixture.Key, configuration, "after-script-flush");
        Assert.True(admission.Allowed);
        await fixture.Second.ReportAsync(admission, configuration, DistributedCircuitOutcome.Failure);
        var afterFlush = await fixture.First.GetSnapshotAsync(fixture.Key, configuration);
        Assert.Equal(ResilienceCircuitState.Closed, afterFlush.State);
    }

    [RedisFact]
    public async Task AbandonedAdmissionAuxiliaryKeysExpireWithoutAReport()
    {
        using var fixture = RedisFixture.Create();
        var configuration = Configuration(minimumThroughput: 2, failureRatio: 0.5,
            samplingMilliseconds: 500, breakMilliseconds: 500, probeLeaseMilliseconds: 100);

        var admission = await fixture.First.AdmitAsync(fixture.Key, configuration, "abandoned-admission");
        Assert.True(admission.Allowed);

        var redisKey = "bing:resilience:circuit:{" +
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fixture.Key))) + "}";
        Assert.True(await fixture.ClientOne.ExistsAsync(redisKey + ":admitted"));
        Assert.True(await fixture.ClientOne.ExistsAsync(redisKey + ":expiry"));

        await Task.Delay(TimeSpan.FromMilliseconds(1900));

        Assert.False(await fixture.ClientOne.ExistsAsync(redisKey + ":admitted"));
        Assert.False(await fixture.ClientOne.ExistsAsync(redisKey + ":expiry"));
    }

    private static async Task OpenAsync(RedisFixture fixture, DistributedCircuitConfiguration configuration, string prefix)
    {
        var first = await fixture.First.AdmitAsync(fixture.Key, configuration, prefix + "-a");
        var second = await fixture.Second.AdmitAsync(fixture.Key, configuration, prefix + "-b");
        Assert.True(first.Allowed);
        Assert.True(second.Allowed);
        await fixture.First.ReportAsync(first, configuration, DistributedCircuitOutcome.Failure);
        await fixture.Second.ReportAsync(second, configuration, DistributedCircuitOutcome.Failure);
        Assert.Equal(ResilienceCircuitState.Open,
            (await fixture.First.GetSnapshotAsync(fixture.Key, configuration)).State);
    }

    private static DistributedCircuitConfiguration Configuration(int minimumThroughput, double failureRatio,
        int samplingMilliseconds = 1000, int breakMilliseconds = 500, int probeLeaseMilliseconds = 200) =>
        new("v1", failureRatio, minimumThroughput, samplingMilliseconds, breakMilliseconds, probeLeaseMilliseconds);

    private sealed class RedisFixture : IDisposable
    {
        private RedisFixture(string connectionString)
        {
            ClientOne = new CSRedisClient(connectionString);
            ClientTwo = new CSRedisClient(connectionString);
            First = new RedisDistributedCircuitStateStore(ClientOne);
            Second = new RedisDistributedCircuitStateStore(ClientTwo);
            Key = "integration:" + Guid.NewGuid().ToString("N");
        }

        public CSRedisClient ClientOne { get; }
        public CSRedisClient ClientTwo { get; }
        public RedisDistributedCircuitStateStore First { get; }
        public RedisDistributedCircuitStateStore Second { get; }
        public string Key { get; }

        public static RedisFixture Create() => new(RedisTestSettings.RedisConnectionString);

        public void Dispose()
        {
            ClientOne.Dispose();
            ClientTwo.Dispose();
        }
    }
}

public sealed class RedisFactAttribute : FactAttribute
{
    public RedisFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(RedisTestSettings.ConnectionFileVariable)))
        {
            Skip = $"Set {RedisTestSettings.ConnectionFileVariable} to enable the local Redis integration tests.";
            return;
        }

        _ = RedisTestSettings.RedisConnectionString;
    }
}

internal static class RedisTestSettings
{
    public const string ConnectionFileVariable = "BING_RESILIENCE_TEST_CONNECTION_FILE";

    public static string RedisConnectionString
    {
        get
        {
            var path = Environment.GetEnvironmentVariable(ConnectionFileVariable);
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException($"{ConnectionFileVariable} is not set.");
            if (!File.Exists(path))
                throw new FileNotFoundException("The Redis integration connection file was not found.", path);

            using var document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
            if (!document.RootElement.TryGetProperty("redis", out var redis) ||
                !redis.TryGetProperty("connectionString", out var connectionString) ||
                string.IsNullOrWhiteSpace(connectionString.GetString()))
                throw new InvalidDataException("The Redis integration connection file has no redis.connectionString.");

            return connectionString.GetString()!;
        }
    }
}
