using Bing.Admin.Modules;
using Bing.Events.Cap;
using Bing.Tracing;
using DotNetCore.CAP;
using DotNetCore.CAP.Filter;
using DotNetCore.CAP.Internal;
using DotNetCore.CAP.Messages;
using DotNetCore.CAP.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Admin.EFCore.Tests;

/// <summary>
/// 经真实 CAP 8.0.1 调用链验证 Admin 订阅器的跟踪上下文。
/// </summary>
public sealed class AdminCapTraceInvocationTest
{
    private readonly ITestOutputHelper _output;

    public AdminCapTraceInvocationTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task InvokeAsync_WithTrace_ShouldKeepTargetTraceAndParent()
    {
        var consumer = new AdminTraceConsumer();
        var filter = new TraceRecordingFilter();
        using var provider = CreateProvider(consumer, filter);
        var invoker = provider.GetRequiredService<ISubscribeInvoker>();
        Assert.IsType<CapSubscribeInvoker>(invoker);

        var parent = CreateParent();
        TraceIdContext.Current = parent;
        try
        {
            var result = await invoker.InvokeAsync(CreateContext(nameof(AdminTraceConsumer.Succeed),
                "admin-trace"), CancellationToken.None);

            Assert.Equal("admin-result", result.Result);
            Assert.Equal(1, consumer.Calls);
            var targetObservation = Assert.Single(consumer.Observations);
            var filterObservation = Assert.Single(filter.ExecutingObservations);
            Assert.Equal("admin-trace", targetObservation.TraceId);
            Assert.NotSame(parent, targetObservation.Context);
            Assert.Equal("admin-trace", filterObservation.TraceId);
            Assert.Same(targetObservation.Context, filterObservation.Context);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("empty")]
    public async Task InvokeAsync_WithoutNonEmptyTraceHeader_ShouldKeepParent(string headerKind)
    {
        var consumer = new AdminTraceConsumer();
        var filter = new TraceRecordingFilter();
        using var provider = CreateProvider(consumer, filter);
        var invoker = provider.GetRequiredService<ISubscribeInvoker>();
        var parent = CreateParent();
        TraceIdContext.Current = parent;

        try
        {
            var result = await invoker.InvokeAsync(CreateContext(nameof(AdminTraceConsumer.Succeed), headerKind,
                null), CancellationToken.None);

            Assert.Equal("admin-result", result.Result);
            Assert.Same(parent, Assert.Single(consumer.Observations).Context);
            Assert.Same(parent, Assert.Single(filter.ExecutingObservations).Context);
            Assert.Equal(1, consumer.Calls);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    [Fact]
    public async Task InvokeAsync_WithoutTraceHeaderAndParent_ShouldKeepNull()
    {
        var consumer = new AdminTraceConsumer();
        var filter = new TraceRecordingFilter();
        using var provider = CreateProvider(consumer, filter);
        var invoker = provider.GetRequiredService<ISubscribeInvoker>();
        TraceIdContext.Current = null;

        var result = await invoker.InvokeAsync(CreateContext(nameof(AdminTraceConsumer.Succeed), "missing", null),
            CancellationToken.None);

        Assert.Equal("admin-result", result.Result);
        Assert.Null(Assert.Single(consumer.Observations).Context);
        Assert.Null(Assert.Single(filter.ExecutingObservations).Context);
        Assert.Null(TraceIdContext.Current);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("parent-trace")]
    public async Task InvokeAsync_WithNonEmptyTraceHeader_ShouldInstallIndependentContext(string traceId)
    {
        var consumer = new AdminTraceConsumer();
        var filter = new TraceRecordingFilter();
        using var provider = CreateProvider(consumer, filter);
        var invoker = provider.GetRequiredService<ISubscribeInvoker>();
        var parent = CreateParent();
        TraceIdContext.Current = parent;

        try
        {
            await invoker.InvokeAsync(CreateContext(nameof(AdminTraceConsumer.Succeed), traceId),
                CancellationToken.None);

            var targetObservation = Assert.Single(consumer.Observations);
            var filterObservation = Assert.Single(filter.ExecutingObservations);
            Assert.Equal(traceId, targetObservation.TraceId);
            Assert.NotSame(parent, targetObservation.Context);
            Assert.Same(targetObservation.Context, filterObservation.Context);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    [Fact]
    public async Task InvokeAsync_WhileTargetIsSuspended_ShouldKeepTraceUntilCompletion()
    {
        var consumer = new AdminTraceConsumer();
        var filter = new TraceRecordingFilter();
        var gate = new AdminTraceGate();
        consumer.Gates["admin-suspended"] = gate;
        using var provider = CreateProvider(consumer, filter);
        var invoker = provider.GetRequiredService<ISubscribeInvoker>();
        var parent = CreateParent();
        TraceIdContext.Current = parent;

        try
        {
            var invocation = invoker.InvokeAsync(CreateContext(nameof(AdminTraceConsumer.Suspend),
                "admin-suspended"), CancellationToken.None);
            var entered = await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal("admin-suspended", entered.TraceId);
            Assert.NotSame(parent, entered.Context);
            Assert.Equal("admin-suspended", Assert.Single(filter.ExecutingObservations).TraceId);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);

            gate.Release.TrySetResult(true);
            var result = await invocation.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal("admin-result", result.Result);
            Assert.Equal(2, consumer.Observations.Count);
            Assert.All(consumer.Observations, observation =>
            {
                Assert.Equal("admin-suspended", observation.TraceId);
                Assert.NotSame(parent, observation.Context);
            });
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            gate.Release.TrySetResult(true);
            TraceIdContext.Current = null;
        }
    }

    [Fact]
    public async Task InvokeAsync_WhenTargetThrows_ShouldPreserveExceptionAndRestoreParent()
    {
        var expected = new AdminTraceException("admin consumer failed");
        var consumer = new AdminTraceConsumer { Failure = expected };
        var filter = new TraceRecordingFilter();
        using var provider = CreateProvider(consumer, filter);
        var invoker = provider.GetRequiredService<ISubscribeInvoker>();
        var parent = CreateParent();
        TraceIdContext.Current = parent;

        try
        {
            var actual = await Assert.ThrowsAsync<AdminTraceException>(() => invoker.InvokeAsync(
                CreateContext(nameof(AdminTraceConsumer.Throw), "admin-failure"), CancellationToken.None));

            Assert.Same(expected, actual);
            Assert.Equal(1, consumer.Calls);
            Assert.Same(expected, Assert.Single(filter.Exceptions).Exception);
            Assert.Equal("admin-failure", Assert.Single(consumer.Observations).TraceId);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    [Fact]
    public async Task InvokeAsync_WhenTargetCancels_ShouldPreserveCancellationAndRestoreParent()
    {
        using var businessCancellation = new CancellationTokenSource();
        businessCancellation.Cancel();
        var original = new OperationCanceledException("admin cancellation", businessCancellation.Token);
        var consumer = new AdminTraceConsumer { Failure = original };
        var filter = new TraceRecordingFilter();
        using var provider = CreateProvider(consumer, filter);
        var invoker = provider.GetRequiredService<ISubscribeInvoker>();
        var parent = CreateParent();
        TraceIdContext.Current = parent;

        try
        {
            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => invoker.InvokeAsync(
                CreateContext(nameof(AdminTraceConsumer.Cancel), "admin-cancel"), CancellationToken.None));

            Assert.Same(original, actual.InnerException);
            Assert.Equal(businessCancellation.Token,
                Assert.IsType<OperationCanceledException>(actual.InnerException).CancellationToken);
            Assert.True(Assert.IsType<bool>(actual.Data["Bing.CapConsumerCancellation"]));
            Assert.Equal("admin-cancel", Assert.Single(consumer.Observations).TraceId);
            Assert.Same(original, Assert.Single(filter.Exceptions).Exception);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    [Fact]
    public async Task InvokeAsync_WhenCallsAreSequential_ShouldRestoreParentBetweenMessages()
    {
        var consumer = new AdminTraceConsumer();
        var filter = new TraceRecordingFilter();
        using var provider = CreateProvider(consumer, filter);
        var invoker = provider.GetRequiredService<ISubscribeInvoker>();
        var parent = CreateParent();
        TraceIdContext.Current = parent;

        try
        {
            await invoker.InvokeAsync(CreateContext(nameof(AdminTraceConsumer.Succeed), "first-message"),
                CancellationToken.None);
            Assert.Same(parent, TraceIdContext.Current);

            await invoker.InvokeAsync(CreateContext(nameof(AdminTraceConsumer.Succeed), "missing", null),
                CancellationToken.None);

            var targetObservations = consumer.Observations.ToArray();
            var filterObservations = filter.ExecutingObservations.ToArray();
            Assert.Equal(2, targetObservations.Length);
            Assert.Equal("first-message", targetObservations[0].TraceId);
            Assert.Same(parent, targetObservations[1].Context);
            Assert.Equal("first-message", filterObservations[0].TraceId);
            Assert.Same(parent, filterObservations[1].Context);
            Assert.Equal(2, consumer.Calls);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    [Fact]
    public async Task InvokeAsync_WhenCallsOverlap_ShouldIsolateAndRestoreEachContext()
    {
        var consumer = new AdminTraceConsumer();
        var filter = new TraceRecordingFilter();
        var gateA = new AdminTraceGate();
        var gateB = new AdminTraceGate();
        consumer.Gates["admin-a"] = gateA;
        consumer.Gates["admin-b"] = gateB;
        using var provider = CreateProvider(consumer, filter);
        var invoker = provider.GetRequiredService<ISubscribeInvoker>();
        var parent = CreateParent();
        TraceIdContext.Current = parent;

        try
        {
            var invocationA = invoker.InvokeAsync(CreateContext(nameof(AdminTraceConsumer.Suspend), "admin-a"),
                CancellationToken.None);
            var invocationB = invoker.InvokeAsync(CreateContext(nameof(AdminTraceConsumer.Suspend), "admin-b"),
                CancellationToken.None);
            var entered = await Task.WhenAll(gateA.Entered.Task, gateB.Entered.Task)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(new[] { "admin-a", "admin-b" }, entered.Select(observation => observation.TraceId)
                .OrderBy(traceId => traceId));
            Assert.NotSame(entered[0].Context, entered[1].Context);
            Assert.Equal(2, filter.ExecutingObservations.Count);
            Assert.All(filter.ExecutingObservations, observation => Assert.NotSame(parent, observation.Context));
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);

            gateA.Release.TrySetResult(true);
            gateB.Release.TrySetResult(true);
            await Task.WhenAll(invocationA, invocationB).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(4, consumer.Observations.Count);
            Assert.All(consumer.Observations, observation => Assert.NotSame(parent, observation.Context));
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            gateA.Release.TrySetResult(true);
            gateB.Release.TrySetResult(true);
            TraceIdContext.Current = null;
        }
    }

    [AdminCapTraceProbeFact]
    [Trait("Category", "Probe")]
    public async Task TraceProbe_ShouldObserveBoundedSynchronousConsumerScenarios()
    {
        const int warmupCount = 100;
        const int batchCount = 5;
        const int operationsPerBatch = 1000;
        var elapsedWindow = Stopwatch.StartNew();
        var scenarios = new List<AdminProbeScenario>();

        foreach (var (name, headerKind, traceId) in new[]
                 {
                     ("no-header", "missing", (string)null),
                     ("valid-trace", "value", "admin-probe-trace")
                 })
        {
            var parent = CreateParent();
            var consumer = new AdminTraceProbeConsumer(parent,
                name == "no-header" ? parent.TraceId : traceId,
                name == "no-header");
            var filter = new ProbeNoOpSubscribeFilter();
            using var provider = CreateProvider(consumer, filter);
            var invoker = provider.GetRequiredService<ISubscribeInvoker>();
            var context = CreateContext(typeof(AdminTraceProbeConsumer),
                nameof(AdminTraceProbeConsumer.Succeed), headerKind, traceId);
            var threadId = Environment.CurrentManagedThreadId;
            var threadTracker = new AdminProbeThreadTracker(threadId);
            TraceIdContext.Current = parent;

            try
            {
                for (var index = 0; index < warmupCount; index++)
                    await InvokeAndValidateProbeAsync(invoker, context, consumer, parent, threadTracker);

                var batches = new List<AdminProbeBatch>(batchCount);
                for (var batch = 0; batch < batchCount; batch++)
                {
                    var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                    var startedAt = Stopwatch.GetTimestamp();
                    for (var operation = 0; operation < operationsPerBatch; operation++)
                        await InvokeAndValidateProbeAsync(invoker, context, consumer, parent, threadTracker);

                    var elapsedTicks = Stopwatch.GetTimestamp() - startedAt;
                    var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                    batches.Add(new AdminProbeBatch(batch + 1, operationsPerBatch, elapsedTicks,
                        threadTracker.AllCallsStayedOnCallingThread ? allocatedBytes : null));
                    if (elapsedWindow.Elapsed > TimeSpan.FromSeconds(30))
                        throw new TimeoutException("The bounded Admin CAP trace observation exceeded 30 seconds.");
                }

                scenarios.Add(new AdminProbeScenario(name, traceId, batches));
            }
            finally
            {
                TraceIdContext.Current = null;
            }
        }

        var report = new AdminProbeReport(RuntimeInformation.FrameworkDescription, Environment.Version.ToString(),
            Environment.OSVersion.ToString(), warmupCount, batchCount, operationsPerBatch, 1, 1, scenarios);
        _output.WriteLine("ADMIN_CAPTRACE_PROBE_JSON=" + JsonSerializer.Serialize(report));
    }

    private async Task InvokeAndValidateProbeAsync(ISubscribeInvoker invoker, ConsumerContext context,
        AdminTraceProbeConsumer consumer, TraceIdContext parent, AdminProbeThreadTracker threadTracker)
    {
        var callsBefore = consumer.Calls;
        threadTracker.Record(Environment.CurrentManagedThreadId);
        var result = await invoker.InvokeAsync(context, CancellationToken.None);
        threadTracker.Record(Environment.CurrentManagedThreadId);
        if (!string.Equals(result.Result as string, "admin-probe-result", StringComparison.Ordinal))
            throw new InvalidOperationException("The real CAP invocation returned an unexpected result.");
        if (consumer.Calls != callsBefore + 1 || consumer.Mismatches != 0)
            throw new InvalidOperationException("The real CAP target did not observe the expected trace exactly once.");
        if (!ReferenceEquals(parent, TraceIdContext.Current))
            throw new InvalidOperationException("The real CAP invocation did not restore its parent context.");
    }

    private static ServiceProvider CreateProvider<TConsumer>(TConsumer consumer, ISubscribeFilter filter)
        where TConsumer : class
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(consumer);
        services.AddSingleton<ISubscribeFilter>(filter);
        services.AddSingleton<ISerializer>(new JsonUtf8Serializer(
            Microsoft.Extensions.Options.Options.Create(new CapOptions())));
        services.AddSingleton<ISubscribeInvoker, CapSubscribeInvoker>();
        return services.BuildServiceProvider();
    }

    private static ConsumerContext CreateContext(string methodName, string traceId)
        => CreateContext(methodName, traceId == null ? "missing" : "value", traceId);

    private static ConsumerContext CreateContext(string methodName, string headerKind, string traceId)
        => CreateContext(typeof(AdminTraceConsumer), methodName, headerKind, traceId);

    private static ConsumerContext CreateContext(Type consumerType, string methodName, string headerKind,
        string traceId)
    {
        var method = consumerType.GetMethod(methodName);
        Assert.NotNull(method);
        var descriptor = new ConsumerExecutorDescriptor
        {
            ImplTypeInfo = consumerType.GetTypeInfo(),
            ServiceTypeInfo = consumerType.GetTypeInfo(),
            MethodInfo = method,
            Attribute = new CapSubscribeAttribute("bing.tests.admin-trace", false),
            Parameters = method.GetParameters().Select(parameter => new ParameterDescriptor
            {
                Name = parameter.Name,
                ParameterType = parameter.ParameterType,
                IsFromCap = parameter.GetCustomAttribute<FromCapAttribute>() != null
            }).ToList()
        };
        var headers = new Dictionary<string, string>
        {
            [DotNetCore.CAP.Messages.Headers.MessageId] = Guid.NewGuid().ToString("N"),
            [DotNetCore.CAP.Messages.Headers.MessageName] = "bing.tests.admin-trace",
            [DotNetCore.CAP.Messages.Headers.CallbackName] = "bing.tests.admin-trace.callback"
        };
        if (headerKind == "empty")
            traceId = string.Empty;
        else if (headerKind == "null")
            traceId = null;

        if (headerKind != "missing")
            headers["bing-trace-id"] = traceId;
        return new ConsumerContext(descriptor, new Message(headers, null));
    }

    private static TraceIdContext CreateParent() =>
        new("parent-trace", "root-id", "parent-id", "child-id");

    private static void AssertParentUnchanged(TraceIdContext parent)
    {
        Assert.Equal("parent-trace", parent.TraceId);
        Assert.Equal("root-id", parent.RootId);
        Assert.Equal("parent-id", parent.ParentId);
        Assert.Equal("child-id", parent.ChildId);
    }

    public sealed class AdminTraceConsumer
    {
        private int _calls;

        public ConcurrentDictionary<string, AdminTraceGate> Gates { get; } = new();
        public ConcurrentQueue<TraceObservation> Observations { get; } = new();
        public Exception Failure { get; set; }
        public int Calls => Volatile.Read(ref _calls);

        public Task<string> Succeed()
        {
            RecordObservation();
            return Task.FromResult("admin-result");
        }

        public async Task<string> Suspend()
        {
            var observation = RecordObservation();
            if (!Gates.TryGetValue(observation.TraceId, out var gate))
                throw new InvalidOperationException($"No test gate is configured for '{observation.TraceId}'.");

            gate.Entered.TrySetResult(observation);
            await gate.Release.Task.ConfigureAwait(false);
            RecordObservation();
            return "admin-result";
        }

        public Task<string> Throw()
        {
            RecordObservation();
            return Task.FromException<string>(Failure ?? new InvalidOperationException("Failure was not configured."));
        }

        public Task<string> Cancel()
        {
            RecordObservation();
            var exception = Failure as OperationCanceledException ?? new OperationCanceledException();
            return Task.FromException<string>(exception);
        }

        private TraceObservation RecordObservation()
        {
            Interlocked.Increment(ref _calls);
            var context = TraceIdContext.Current;
            var observation = new TraceObservation(context, context?.TraceId, context?.RootId,
                context?.ParentId, context?.ChildId);
            Observations.Enqueue(observation);
            return observation;
        }
    }

    public sealed class AdminTraceProbeConsumer
    {
        private readonly TraceIdContext _parent;
        private readonly string _expectedTraceId;
        private readonly bool _expectParentContext;
        private int _calls;
        private int _mismatches;

        public AdminTraceProbeConsumer(TraceIdContext parent, string expectedTraceId, bool expectParentContext)
        {
            _parent = parent;
            _expectedTraceId = expectedTraceId;
            _expectParentContext = expectParentContext;
        }

        public int Calls => Volatile.Read(ref _calls);
        public int Mismatches => Volatile.Read(ref _mismatches);

        public Task<string> Succeed()
        {
            var context = TraceIdContext.Current;
            Interlocked.Increment(ref _calls);
            if (!string.Equals(context?.TraceId, _expectedTraceId, StringComparison.Ordinal) ||
                _expectParentContext != ReferenceEquals(_parent, context))
                Interlocked.Increment(ref _mismatches);
            return Task.FromResult("admin-probe-result");
        }
    }

    private sealed class ProbeNoOpSubscribeFilter : SubscribeFilter
    {
        public override Task OnSubscribeExecutingAsync(ExecutingContext context) => Task.CompletedTask;
    }

    private sealed class AdminProbeThreadTracker
    {
        private readonly int _callingThreadId;
        private int _sameThread = 1;

        public AdminProbeThreadTracker(int callingThreadId) => _callingThreadId = callingThreadId;

        public bool AllCallsStayedOnCallingThread => Volatile.Read(ref _sameThread) == 1;

        public void Record(int threadId)
        {
            if (threadId != _callingThreadId)
                Interlocked.Exchange(ref _sameThread, 0);
        }
    }

    private sealed record AdminProbeReport(string Framework, string Runtime, string OperatingSystem,
        int WarmupOperationsPerScenario, int BatchCount, int OperationsPerBatch, int RequestedConcurrency,
        int MaxActualParallelism, IReadOnlyList<AdminProbeScenario> Scenarios);

    private sealed record AdminProbeScenario(string Name, string TraceId, IReadOnlyList<AdminProbeBatch> Batches);

    private sealed record AdminProbeBatch(int Batch, int Operations, long ElapsedTicks,
        long? CurrentThreadAllocatedBytes);

    private sealed class TraceRecordingFilter : SubscribeFilter
    {
        public ConcurrentQueue<TraceObservation> ExecutingObservations { get; } = new();
        public ConcurrentQueue<FilterExceptionObservation> Exceptions { get; } = new();

        public override Task OnSubscribeExecutingAsync(ExecutingContext context)
        {
            var trace = TraceIdContext.Current;
            ExecutingObservations.Enqueue(new TraceObservation(trace, trace?.TraceId, trace?.RootId,
                trace?.ParentId, trace?.ChildId));
            return Task.CompletedTask;
        }

        public override Task OnSubscribeExceptionAsync(ExceptionContext context)
        {
            var trace = TraceIdContext.Current;
            Exceptions.Enqueue(new FilterExceptionObservation(context.Exception, trace, trace?.TraceId));
            return Task.CompletedTask;
        }
    }

    public sealed class AdminTraceGate
    {
        public TaskCompletionSource<TraceObservation> Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public sealed record TraceObservation(TraceIdContext Context, string TraceId, string RootId,
        string ParentId, string ChildId);

    private sealed record FilterExceptionObservation(Exception Exception, TraceIdContext Context, string TraceId);

    private sealed class AdminTraceException : Exception
    {
        public AdminTraceException(string message) : base(message)
        {
        }
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class AdminCapTraceProbeFactAttribute : FactAttribute
{
    public AdminCapTraceProbeFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_CAP_TRACE_PROBE"), "1",
                StringComparison.Ordinal))
            Skip = "Set RUN_CAP_TRACE_PROBE=1 to run the bounded CAP trace observation.";
    }
}
