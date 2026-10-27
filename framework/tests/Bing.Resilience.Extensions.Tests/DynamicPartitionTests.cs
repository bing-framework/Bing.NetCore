using Bing.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

public sealed class DynamicPartitionTests
{
    [Fact]
    public void SamePartitionIsReusedAndLeasePreventsCollectionUntilRetentionExpires()
    {
        var clock = new ManualTimeProvider();
        var services = CreateServices(clock);
        services.AddBingResiliencePipeline<int>("partitioned", _ => { });
        using var host = services.BuildServiceProvider();
        var partitions = host.GetRequiredService<IResiliencePartitionedPipelineProvider>();

        using var first = partitions.AcquirePartition<int>("partitioned", "generic", "tenant-a", maximumPartitions: 2,
            retention: TimeSpan.FromSeconds(10));
        using var second = partitions.AcquirePartition<int>("partitioned", "generic", "tenant-a", maximumPartitions: 2,
            retention: TimeSpan.FromSeconds(10));

        Assert.Equal(1, partitions.PartitionCount);
        Assert.Same(first.Pipeline, second.Pipeline);

        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(0, partitions.CollectPartitions());
        first.Dispose();
        Assert.Equal(0, partitions.CollectPartitions());
        second.Dispose();
        Assert.Equal(0, partitions.CollectPartitions());

        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(1, partitions.CollectPartitions());
        Assert.Equal(0, partitions.PartitionCount);
    }

    [Fact]
    public void PartitionLimitRejectsAdditionalPartitionWhileExistingLeasesAreHeld()
    {
        var clock = new ManualTimeProvider();
        var services = CreateServices(clock);
        services.AddBingResiliencePipeline<int>("bounded", _ => { });
        using var host = services.BuildServiceProvider();
        var partitions = host.GetRequiredService<IResiliencePartitionedPipelineProvider>();

        using var first = partitions.AcquirePartition<int>("bounded", "generic", "tenant-a", maximumPartitions: 2,
            retention: TimeSpan.Zero);
        using var second = partitions.AcquirePartition<int>("bounded", "generic", "tenant-b", maximumPartitions: 2,
            retention: TimeSpan.Zero);

        Assert.Throws<ResilienceConfigurationException>(() =>
            partitions.AcquirePartition<int>("bounded", "generic", "tenant-c", maximumPartitions: 2,
                retention: TimeSpan.Zero));
        Assert.Equal(2, partitions.PartitionCount);
    }

    [Fact]
    public void RetentionCannotBeShorterThanCircuitSamplingOrBreakWindow()
    {
        var clock = new ManualTimeProvider();
        var services = CreateServices(clock);
        services.AddBingResiliencePipeline<int>("protected", options =>
        {
            options.CircuitBreaker.Enabled = true;
            options.CircuitBreaker.MinimumThroughput = 2;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(4);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(6);
            options.CircuitBreaker.ShouldHandle = _ => true;
        });
        using var host = services.BuildServiceProvider();
        var partitions = host.GetRequiredService<IResiliencePartitionedPipelineProvider>();

        Assert.Throws<ResilienceConfigurationException>(() =>
            partitions.AcquirePartition<int>("protected", "generic", "tenant-a", retention: TimeSpan.FromSeconds(5)));
        Assert.Equal(0, partitions.PartitionCount);

        using var accepted = partitions.AcquirePartition<int>("protected", "generic", "tenant-a",
            retention: TimeSpan.FromSeconds(6));
        Assert.Equal(1, partitions.PartitionCount);
    }

    [Fact]
    public async Task CollectingPartitionReleasesRateLimitAndCircuitState()
    {
        var clock = new ManualTimeProvider();
        var services = CreateServices(clock);
        services.AddBingResiliencePipeline<int>("stateful", options =>
        {
            options.RateLimit.Enabled = true;
            options.RateLimit.TokenLimit = 1;
            options.RateLimit.TokensPerPeriod = 1;
            options.RateLimit.ReplenishmentPeriod = TimeSpan.FromHours(1);
            options.CircuitBreaker.Enabled = true;
            options.CircuitBreaker.EnableManualControl = true;
            options.CircuitBreaker.MinimumThroughput = 2;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(2);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(2);
            options.CircuitBreaker.ShouldHandle = args => args.Outcome.Exception is IOException;
        });
        using var host = services.BuildServiceProvider();
        var partitions = host.GetRequiredService<IResiliencePartitionedPipelineProvider>();
        var controller = host.GetRequiredService<IResilienceCircuitController>();
        const string partitionName = "tenant-a";
        var identity = new ResiliencePipelineIdentity("generic", "stateful", typeof(int), partitionName);
        var lease = partitions.AcquirePartition<int>("stateful", "generic", partitionName,
            retention: TimeSpan.FromSeconds(2));

        Assert.Equal(1, await lease.Pipeline.ExecuteAsync(_ => ValueTask.FromResult(1)));
        await Assert.ThrowsAsync<ResilienceRejectedException>(() =>
            lease.Pipeline.ExecuteAsync(_ => ValueTask.FromResult(2)).AsTask());
        await controller.IsolateAsync(identity);
        Assert.Equal(ResilienceCircuitState.Isolated, controller.GetState(identity));

        lease.Dispose();
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(1, partitions.CollectPartitions());
        Assert.Equal(0, partitions.PartitionCount);
        Assert.Throws<ResilienceConfigurationException>(() => controller.GetState(identity));

        using var recreated = partitions.AcquirePartition<int>("stateful", "generic", partitionName,
            retention: TimeSpan.FromSeconds(2));
        Assert.Equal(3, await recreated.Pipeline.ExecuteAsync(_ => ValueTask.FromResult(3)));
        Assert.Equal(ResilienceCircuitState.Closed, controller.GetState(identity));
    }

    [Fact]
    public async Task DynamicPartitionCanBeControlledBeforeFirstExecution()
    {
        var clock = new ManualTimeProvider();
        var services = CreateServices(clock);
        services.AddBingResiliencePipeline<int>("precontrolled", options =>
        {
            options.CircuitBreaker.Enabled = true;
            options.CircuitBreaker.EnableManualControl = true;
            options.CircuitBreaker.MinimumThroughput = 2;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(2);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(2);
            options.CircuitBreaker.ShouldHandle = _ => true;
        });
        using var host = services.BuildServiceProvider();
        var partitions = host.GetRequiredService<IResiliencePartitionedPipelineProvider>();
        var controller = host.GetRequiredService<IResilienceCircuitController>();
        using var partition = partitions.AcquirePartition<int>("precontrolled", "generic", "tenant-a", retention: TimeSpan.FromSeconds(2));
        var identity = new ResiliencePipelineIdentity("generic", "precontrolled", typeof(int), "tenant-a");

        Assert.Equal(ResilienceCircuitState.Closed, controller.GetState(identity));
        await controller.IsolateAsync(identity);
        Assert.Equal(ResilienceCircuitState.Isolated, controller.GetState(identity));
        await controller.CloseAsync(identity);
        Assert.Equal(ResilienceCircuitState.Closed, controller.GetState(identity));
    }

    [Fact]
    public async Task DifferentPartitionsHaveIndependentConcurrencyLimits()
    {
        var clock = new ManualTimeProvider();
        var services = CreateServices(clock);
        services.AddBingResiliencePipeline<int>("concurrent", options =>
        {
            options.Concurrency.Enabled = true;
            options.Concurrency.PermitLimit = 1;
        });
        using var host = services.BuildServiceProvider();
        var partitions = host.GetRequiredService<IResiliencePartitionedPipelineProvider>();
        using var left = partitions.AcquirePartition<int>("concurrent", "generic", "left", retention: TimeSpan.Zero);
        using var right = partitions.AcquirePartition<int>("concurrent", "generic", "right", retention: TimeSpan.Zero);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = left.Pipeline.ExecuteAsync(async token =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return 1;
        }).AsTask();

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        try
        {
            await Assert.ThrowsAsync<ResilienceRejectedException>(() =>
                left.Pipeline.ExecuteAsync(_ => ValueTask.FromResult(2)).AsTask());
            Assert.Equal(3, await right.Pipeline.ExecuteAsync(_ => ValueTask.FromResult(3)));
        }
        finally
        {
            release.TrySetResult();
        }

        Assert.Equal(1, await running);
    }

    [Fact]
    public async Task DynamicPartitionUsesNewProviderGenerationWhileInflightCallStaysPinned()
    {
        var services = new ServiceCollection();
        services.AddBingResilience()
            .AddBuiltInProvider("builtin")
            .AddFencesProvider("fences")
            .AddPollyProvider("polly")
            .UseDefaultProvider("builtin")
            .AddRuntimeSwitching(options => options.DrainTimeout = TimeSpan.FromMilliseconds(50));
        services.AddBingResiliencePipeline<int>("switchable", _ => { });
        using var host = services.BuildServiceProvider();
        var partitions = host.GetRequiredService<IResiliencePartitionedPipelineProvider>();
        var runtime = host.GetRequiredService<IResilienceRuntimeManager>();
        var runtimeLeases = host.GetRequiredService<IResilienceRuntimeLeaseProvider>();
        const string group = "generic:switchable";
        using var partition = partitions.AcquirePartition<int>("switchable", "generic", "tenant-a", retention: TimeSpan.Zero);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueOldCall = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldCall = partition.Pipeline.ExecuteAsync(async (context, token) =>
        {
            using var beforeSwitch = runtimeLeases.AcquireLease(group, context);
            entered.TrySetResult();
            await continueOldCall.Task.WaitAsync(token);
            using var afterSwitch = runtimeLeases.AcquireLease(group, context);
            return checked((int)(beforeSwitch.Version * 10 + afterSwitch.Version));
        }, new()).AsTask();

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        try
        {
            var switched = await runtime.SwitchAsync(group, "fences", 0);
            Assert.False(switched.PreviousGenerationDrained);
            Assert.Equal("fences", switched.Snapshot.ProviderName);

            var newVersion = await partition.Pipeline.ExecuteAsync((context, _) =>
            {
                using var lease = runtimeLeases.AcquireLease(group, context);
                return ValueTask.FromResult(checked((int)lease.Version));
            }, new());
            Assert.Equal(1, newVersion);
            Assert.Equal(1, partitions.PartitionCount);
        }
        finally
        {
            continueOldCall.TrySetResult();
        }

        Assert.Equal(0, await oldCall);
        Assert.Equal(0, runtime.GetSnapshot(group).RetiredGenerations);
    }

    private static ServiceCollection CreateServices(ManualTimeProvider clock)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(clock);
        services.AddBingResilience()
            .AddBuiltInProvider("builtin")
            .AddFencesProvider("fences")
            .AddPollyProvider("polly")
            .UseDefaultProvider("builtin");
        return services;
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _origin = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Volatile.Read(ref _ticks);
        public override DateTimeOffset GetUtcNow() => _origin.AddTicks(Volatile.Read(ref _ticks));
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new NoOpTimer();
        public void Advance(TimeSpan amount) => Interlocked.Add(ref _ticks, amount.Ticks);

        private sealed class NoOpTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => default;
        }
    }
}
