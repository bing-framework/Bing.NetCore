#nullable enable

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Aop.AspectCore.Tests;

public sealed class AutomaticUnitOfWorkProbeTest
{
    private static readonly TimeSpan DiagnosticWindow = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FormalWindow = TimeSpan.FromSeconds(30);
    private static readonly string[] WorkloadNames = ["empty", "nested", "independent"];
    [Fact]
    public async Task MeasureBoundedAutomaticOperationWorkloads()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_BING_OPERATION_PROBE"), "1",
                StringComparison.Ordinal))
            return;

        var metrics = new OperationProbeMetrics();
        var services = new ServiceCollection();
        services.AddBingOperations();
        services.AddSingleton(metrics);
        services.AddScoped<OperationProbeService>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        var executor = provider.GetRequiredService<IBingOperationExecutor>();
        var diagnostic = await RunWindowAsync(executor, metrics, DiagnosticWindow, maximumRounds: 1);
        var formal = await RunWindowAsync(executor, metrics, FormalWindow, maximumRounds: 5);
        var report = new OperationProbeReport(
            Environment.GetEnvironmentVariable("BING_OPERATION_PROBE_CANDIDATE") ?? "current-worktree",
            RuntimeInformation.FrameworkDescription,
            ".NET 8.0",
            "CURRENT_ONLY_NO_EQUIVALENT_BEFORE",
            "BLOCKED_APPROVAL",
            1,
            1,
            0,
            diagnostic,
            formal,
            metrics.Snapshot());
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        var outputPath = Environment.GetEnvironmentVariable("BING_OPERATION_PROBE_OUTPUT");
        if (string.IsNullOrWhiteSpace(outputPath))
            outputPath = Path.Combine(Path.GetTempPath(), $"bing-operation-probe-{Guid.NewGuid():N}.json");
        outputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Console.WriteLine($"probe-output={outputPath}");
        Console.WriteLine(json);
    }

    private static async Task<OperationProbeWindow> RunWindowAsync(IBingOperationExecutor executor,
        OperationProbeMetrics metrics, TimeSpan window, int maximumRounds)
    {
        foreach (var workload in WorkloadNames)
            for (var index = 0; index < 100; index++)
                await InvokeAsync(executor, workload);

        var baseline = metrics.Snapshot();
        var allocatedStart = GC.GetTotalAllocatedBytes(precise: true);
        var gc0Start = GC.CollectionCount(0);
        var gc1Start = GC.CollectionCount(1);
        var gc2Start = GC.CollectionCount(2);
        var timer = Stopwatch.StartNew();
        var completed = WorkloadNames.ToDictionary(name => name, _ => 0, StringComparer.Ordinal);
        var roundsCompleted = 0;

        for (var round = 0; round < maximumRounds && timer.Elapsed < window; round++)
        {
            foreach (var workload in WorkloadNames)
            {
                for (var index = 0; index < 1000 && timer.Elapsed < window; index++)
                {
                    await InvokeAsync(executor, workload);
                    completed[workload]++;
                }
            }
            roundsCompleted++;
        }

        timer.Stop();
        var after = metrics.Snapshot();
        return new OperationProbeWindow(
            window.TotalSeconds,
            timer.Elapsed.TotalMilliseconds,
            100,
            maximumRounds,
            roundsCompleted,
            completed,
            GC.GetTotalAllocatedBytes(precise: true) - allocatedStart,
            GC.CollectionCount(0) - gc0Start,
            GC.CollectionCount(1) - gc1Start,
            GC.CollectionCount(2) - gc2Start,
            after.OperationScopesOpened - baseline.OperationScopesOpened,
            after.OperationScopesDisposed - baseline.OperationScopesDisposed,
            after.RootCommits - baseline.RootCommits,
            after.OpenConnections - baseline.OpenConnections,
            after.ClosedConnections - baseline.ClosedConnections);
    }

    private static Task InvokeAsync(IBingOperationExecutor executor, string workload) => workload switch
    {
        "empty" => executor.ExecuteAsync<OperationProbeService>((service, token) => service.EmptyAsync(token)),
        "nested" => executor.ExecuteAsync<OperationProbeService>((service, token) => service.NestedAsync(token)),
        "independent" => executor.ExecuteAsync<OperationProbeService>(
            (service, token) => service.IndependentAsync(token)),
        _ => throw new ArgumentOutOfRangeException(nameof(workload))
    };

    private sealed class OperationProbeService : IDisposable
    {
        private readonly IUnitOfWorkScopeManager _scopeManager;
        private readonly IBingOperationExecutor _executor;
        private readonly OperationProbeMetrics _metrics;

        public OperationProbeService(IUnitOfWorkScopeManager scopeManager, IBingOperationExecutor executor,
            OperationProbeMetrics metrics)
        {
            _scopeManager = scopeManager;
            _executor = executor;
            _metrics = metrics;
            _metrics.ScopeOpened();
            var scope = _scopeManager.Current ??
                        throw new InvalidOperationException("Probe service resolved before its operation root.");
            scope.OnCompleted(() =>
            {
                _metrics.RootCommitted();
                return Task.CompletedTask;
            });
        }

        public Task EmptyAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public async Task NestedAsync(CancellationToken cancellationToken)
        {
            await using var participant = ((IUnitOfWorkOperationManager)_scopeManager)
                .BeginOperation(cancellationToken: cancellationToken);
            await participant.CompleteAsync(cancellationToken);
        }

        public Task IndependentAsync(CancellationToken cancellationToken) =>
            _executor.ExecuteAsync<OperationProbeService>(
                (service, token) => service.EmptyAsync(token), cancellationToken: cancellationToken);

        public void Dispose() => _metrics.ScopeDisposed();
    }

    private sealed class OperationProbeMetrics
    {
        private long _operationScopesOpened;
        private long _operationScopesDisposed;
        private long _rootCommits;

        public void ScopeOpened() => Interlocked.Increment(ref _operationScopesOpened);
        public void ScopeDisposed() => Interlocked.Increment(ref _operationScopesDisposed);
        public void RootCommitted() => Interlocked.Increment(ref _rootCommits);

        public OperationProbeMetricsSnapshot Snapshot() => new(
            Interlocked.Read(ref _operationScopesOpened),
            Interlocked.Read(ref _operationScopesDisposed),
            Interlocked.Read(ref _rootCommits),
            0,
            0);
    }

    private sealed record OperationProbeMetricsSnapshot(long OperationScopesOpened,
        long OperationScopesDisposed, long RootCommits, long OpenConnections, long ClosedConnections);

    private sealed record OperationProbeWindow(double TimeBudgetSeconds, double ElapsedMilliseconds,
        int WarmupPerWorkload, int MaximumRounds, int CompletedRounds, IReadOnlyDictionary<string, int> Completed,
        long AllocatedBytes, int Gen0Collections, int Gen1Collections, int Gen2Collections,
        long OperationScopesOpened, long OperationScopesDisposed, long RootCommits,
        long OpenConnections, long ClosedConnections);

    private sealed record OperationProbeReport(string Candidate, string Runtime, string TargetFramework,
        string ComparisonStatus, string ThresholdStatus, int RequestedConcurrency, int MaximumActualParallelism,
        int MaximumQueuedRequests, OperationProbeWindow Diagnostic, OperationProbeWindow Formal,
        OperationProbeMetricsSnapshot FinalCounters);
}
