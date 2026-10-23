using AspectCore.Extensions.Hosting;
using Bing.DependencyInjection;
using Bing.Events.Cap;
using Bing.Tracing;
using DotNetCore.CAP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Bing.EventBus.Tests.Cap;

/// <summary>
/// 有界观察真实 CAP 跟踪代理的同步热路径，不定义性能通过阈值。
/// </summary>
public sealed class CapTraceProbeTest
{
    private const int WarmupCount = 100;
    private const int BatchCount = 5;
    private const int OperationsPerBatch = 1000;

    private readonly ITestOutputHelper _output;

    public CapTraceProbeTest(ITestOutputHelper output) => _output = output;

    [CapTraceProbeFact]
    [Trait("Category", "Probe")]
    public void TraceProbe_ShouldObserveBoundedSynchronousScenarios()
    {
        using var host = new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.EnableAop();
                services.AddScoped<CapTraceAttributeTest.ICapTraceProbe,
                    CapTraceAttributeTest.CapTraceProbe>();
            })
            .Build();
        var probe = host.Services.GetRequiredService<CapTraceAttributeTest.ICapTraceProbe>();
        var parent = new TraceIdContext("probe-parent", "probe-root", "probe-parent-id", "probe-child-id");
        var elapsedWindow = Stopwatch.StartNew();
        var measurements = new List<ProbeScenario>();
        TraceIdContext.Current = parent;

        try
        {
            measurements.Add(MeasureScenario(probe, parent, "no-header", null, null, elapsedWindow));
            measurements.Add(MeasureScenario(probe, parent, "valid-trace", "probe-trace",
                CreateHeader("probe-trace"), elapsedWindow));
        }
        finally
        {
            TraceIdContext.Current = null;
        }

        var report = new ProbeReport(RuntimeInformation.FrameworkDescription, Environment.Version.ToString(),
            Environment.OSVersion.ToString(), WarmupCount, BatchCount, OperationsPerBatch, 1, 1,
            measurements);
        _output.WriteLine("CAPTRACE_PROBE_JSON=" + JsonSerializer.Serialize(report));
    }

    private static ProbeScenario MeasureScenario(CapTraceAttributeTest.ICapTraceProbe probe,
        TraceIdContext parent, string scenario, string traceId, object argument, Stopwatch elapsedWindow)
    {
        var expectedTraceId = scenario == "no-header" ? parent.TraceId : traceId;
        for (var index = 0; index < WarmupCount; index++)
            InvokeAndValidate(probe, parent, scenario, expectedTraceId, argument);

        var batches = new List<ProbeBatch>(BatchCount);
        for (var batch = 0; batch < BatchCount; batch++)
        {
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var startedAt = Stopwatch.GetTimestamp();
            for (var operation = 0; operation < OperationsPerBatch; operation++)
                InvokeAndValidate(probe, parent, scenario, expectedTraceId, argument);

            var elapsedTicks = Stopwatch.GetTimestamp() - startedAt;
            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            batches.Add(new ProbeBatch(batch + 1, OperationsPerBatch, elapsedTicks, allocatedBytes));
            if (elapsedWindow.Elapsed > TimeSpan.FromSeconds(30))
                throw new TimeoutException("The bounded CAP trace observation exceeded 30 seconds.");
        }

        return new ProbeScenario(scenario, traceId, batches);
    }

    private static void InvokeAndValidate(CapTraceAttributeTest.ICapTraceProbe probe,
        TraceIdContext parent, string scenario, string expectedTraceId, object argument)
    {
        var callsBefore = probe.Calls;
        var observation = probe.WithObject(argument);
        if (probe.Calls != callsBefore + 1)
            throw new InvalidOperationException("The proxied CAP target did not run exactly once.");

        if (!string.Equals(expectedTraceId, observation.TraceId, StringComparison.Ordinal))
            throw new InvalidOperationException($"Unexpected trace value in the {scenario} scenario.");

        if (scenario == "no-header" && !ReferenceEquals(parent, observation.Context))
            throw new InvalidOperationException("The no-header scenario did not retain the parent context.");

        if (scenario == "valid-trace" && ReferenceEquals(parent, observation.Context))
            throw new InvalidOperationException("The valid-trace scenario reused the parent context.");

        if (!ReferenceEquals(parent, TraceIdContext.Current))
            throw new InvalidOperationException("The proxy did not restore the caller context.");
    }

    private static CapHeader CreateHeader(string traceId) => new(new Dictionary<string, string>
    {
        ["bing-trace-id"] = traceId
    });

    private sealed record ProbeReport(string Framework, string Runtime, string OperatingSystem,
        int WarmupOperationsPerScenario, int BatchCount, int OperationsPerBatch, int RequestedConcurrency,
        int MaxActualParallelism, IReadOnlyList<ProbeScenario> Scenarios);

    private sealed record ProbeScenario(string Name, string TraceId, IReadOnlyList<ProbeBatch> Batches);

    private sealed record ProbeBatch(int Batch, int Operations, long ElapsedTicks, long CurrentThreadAllocatedBytes);
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class CapTraceProbeFactAttribute : FactAttribute
{
    public CapTraceProbeFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_CAP_TRACE_PROBE"), "1",
                StringComparison.Ordinal))
            Skip = "Set RUN_CAP_TRACE_PROBE=1 to run the bounded CAP trace observation.";
    }
}
