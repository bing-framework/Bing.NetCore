using AspectCore.DynamicProxy;
using AspectCore.Extensions.Hosting;
using Bing.DependencyInjection;
using Bing.Events.Cap;
using Bing.Tracing;
using DotNetCore.CAP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using Xunit;

namespace Bing.EventBus.Tests.Cap;

/// <summary>
/// 通过真实 AspectCore 接口代理验证 CAP 跟踪拦截器。
/// </summary>
public sealed class CapTraceAttributeTest
{
    [Fact]
    public void Invoke_WithoutFromCap_ShouldCallTargetOnce()
    {
        using var host = CreateHost();
        var probe = host.Services.GetRequiredService<ICapTraceProbe>();
        AssertProxyMetadata(probe);
        var parent = CreateParent();
        TraceIdContext.Current = parent;

        try
        {
            var result = probe.WithoutFromCap(1);

            Assert.Equal(1, probe.Calls);
            Assert.Same(parent, result.Context);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    [Fact]
    public void Invoke_WithNullFromCap_ShouldCallTargetOnce()
    {
        using var host = CreateHost();
        var probe = host.Services.GetRequiredService<ICapTraceProbe>();
        var parent = CreateParent();
        TraceIdContext.Current = parent;

        try
        {
            var result = probe.WithObject(null);

            Assert.Equal(1, probe.Calls);
            Assert.Same(parent, result.Context);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    [Fact]
    public void Invoke_WithNonHeaderFromCap_ShouldCallTargetOnce()
    {
        using var host = CreateHost();
        var probe = host.Services.GetRequiredService<ICapTraceProbe>();
        var parent = CreateParent();
        TraceIdContext.Current = parent;

        try
        {
            var result = probe.WithObject(new object());

            Assert.Equal(1, probe.Calls);
            Assert.Same(parent, result.Context);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Invoke_WithNonCapHeaderFromCap_ShouldCallNextOnce(bool useNull)
    {
        var method = typeof(ICapTraceProbe).GetMethod(nameof(ICapTraceProbe.WithObject));
        Assert.NotNull(method);
        var value = useNull ? null : new object();
        var context = new TestAspectContext(method, new[] { value });
        var nextCalls = 0;

        await new CapTraceAttribute().Invoke(context, _ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        });

        Assert.Equal(1, nextCalls);
    }

    [Fact]
    public async Task Invoke_WhenCanceled_ShouldFaultReturnedPipelineTask()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var expected = new OperationCanceledException(cancellation.Token);
        var method = typeof(ICapTraceProbe).GetMethod(nameof(ICapTraceProbe.Cancel));
        Assert.NotNull(method);
        var context = new TestAspectContext(method, new object[] { null, cancellation.Token });
        var parent = CreateParent();
        TraceIdContext.Current = parent;

        try
        {
            var pipeline = new CapTraceAttribute().Invoke(context, _ => Task.FromException(expected));
            var actual = await Assert.ThrowsAsync<OperationCanceledException>(() => pipeline);

            Assert.True(pipeline.IsFaulted);
            Assert.Same(expected, actual);
            Assert.Equal(cancellation.Token, actual.CancellationToken);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    [Fact]
    public void Invoke_WithFromCapCancellationToken_ShouldCallTargetOnce()
    {
        using var host = CreateHost();
        var probe = host.Services.GetRequiredService<ICapTraceProbe>();
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;

        var result = probe.WithCancellationToken(token);

        Assert.Equal(token, result);
        Assert.Equal(1, probe.Calls);
    }

    [Fact]
    public void Invoke_WithTraceId_ShouldUseIndependentContextAndRestoreParent()
    {
        using var host = CreateHost();
        var probe = host.Services.GetRequiredService<ICapTraceProbe>();
        var parent = CreateParent();
        TraceIdContext.Current = parent;

        try
        {
            var result = probe.WithObject(CreateHeader("cap-trace"));

            Assert.Equal(1, probe.Calls);
            Assert.Equal("cap-trace", result.TraceId);
            Assert.NotSame(parent, result.Context);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    [Theory]
    [InlineData("missing", null, true)]
    [InlineData("null", null, true)]
    [InlineData("empty", "", true)]
    [InlineData("missing", null, false)]
    [InlineData("null", null, false)]
    [InlineData("empty", "", false)]
    public void Invoke_WithoutNonEmptyTraceId_ShouldKeepParent(string headerKind, string traceId,
        bool hasParent)
    {
        using var host = CreateHost();
        var probe = host.Services.GetRequiredService<ICapTraceProbe>();
        var parent = hasParent ? CreateParent() : null;
        var header = headerKind == "missing" ? CreateHeaderWithoutTraceId() : CreateHeader(traceId);
        TraceIdContext.Current = parent;

        try
        {
            var result = probe.WithObject(header);

            Assert.Equal(1, probe.Calls);
            Assert.Same(parent, result.Context);
            Assert.Equal(parent?.TraceId, result.TraceId);
            if (parent != null)
                AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("parent-trace")]
    public void Invoke_WithWhitespaceOrSameTraceId_ShouldUseIndependentContext(string traceId)
    {
        using var host = CreateHost();
        var probe = host.Services.GetRequiredService<ICapTraceProbe>();
        var parent = CreateParent();
        TraceIdContext.Current = parent;

        try
        {
            var result = probe.WithObject(CreateHeader(traceId));

            Assert.Equal(1, probe.Calls);
            Assert.Equal(traceId, result.TraceId);
            Assert.NotSame(parent, result.Context);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    [Fact]
    public async Task Invoke_WithAllReturnKinds_ShouldPreserveResultsAndCallOnce()
    {
        using var host = CreateHost();
        var probe = host.Services.GetRequiredService<ICapTraceProbe>();
        var parent = CreateParent();
        TraceIdContext.Current = parent;

        try
        {
            var sync = probe.WithObject(CreateHeader("sync"));
            var taskResult = await probe.WithTask(CreateHeader("task"));
            var valueTaskResult = await probe.WithValueTask(CreateHeader("value-task"));
            await probe.WithTaskVoid(CreateHeader("task-void"));
            var taskVoidTraceId = probe.LastObservation.TraceId;
            await probe.WithValueTaskVoid(CreateHeader("value-task-void"));
            var valueTaskVoidTraceId = probe.LastObservation.TraceId;

            Assert.Equal(new[] { "sync", "task", "value-task", "task-void", "value-task-void" },
                new[] { sync.TraceId, taskResult.TraceId, valueTaskResult.TraceId,
                    taskVoidTraceId, valueTaskVoidTraceId });
            Assert.Equal(5, probe.Calls);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    [Fact]
    public async Task Invoke_WithAsyncTarget_ShouldKeepTraceUntilCompletion()
    {
        using var host = CreateHost();
        var probe = host.Services.GetRequiredService<ICapTraceProbe>();
        var parent = CreateParent();
        var entered = new TaskCompletionSource<TraceObservation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TraceIdContext.Current = parent;

        try
        {
            var operation = probe.WaitAsync(CreateHeader("async-trace"), entered, release.Task);
            var started = await entered.Task;

            Assert.Equal("async-trace", started.TraceId);
            Assert.NotSame(parent, started.Context);
            release.TrySetResult();
            var completed = await operation;

            Assert.Equal("async-trace", completed.TraceId);
            Assert.NotSame(parent, completed.Context);
            Assert.Equal(1, probe.Calls);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            release.TrySetResult();
            TraceIdContext.Current = null;
        }
    }

    [Fact]
    public void Invoke_WhenTargetThrows_ShouldRestoreParentAndPreserveException()
    {
        using var host = CreateHost();
        var probe = host.Services.GetRequiredService<ICapTraceProbe>();
        var parent = CreateParent();
        var expected = new TraceProbeException("sync failure");
        TraceIdContext.Current = parent;

        try
        {
            var actual = Assert.Throws<TraceProbeException>(() =>
                probe.Throw(CreateHeader("throw-trace"), expected));

            Assert.Same(expected, actual);
            Assert.Equal(1, probe.Calls);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    [Fact]
    public async Task Invoke_WhenTargetCancels_ShouldRestoreParentAndPreserveToken()
    {
        using var host = CreateHost();
        var probe = host.Services.GetRequiredService<ICapTraceProbe>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var parent = CreateParent();
        TraceIdContext.Current = parent;

        try
        {
            var operation = probe.Cancel(CreateHeader("cancel-trace"), cancellation.Token);
            Assert.Equal(1, probe.Calls);
            var actual = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);

            Assert.Equal(cancellation.Token, actual.CancellationToken);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    [Fact]
    public async Task Invoke_WhenSuspendedTargetCancels_ShouldRestoreParentAndPreserveToken()
    {
        using var host = CreateHost();
        var probe = host.Services.GetRequiredService<ICapTraceProbe>();
        using var cancellation = new CancellationTokenSource();
        var parent = CreateParent();
        var entered = new TaskCompletionSource<TraceObservation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TraceIdContext.Current = parent;

        try
        {
            var operation = probe.CancelAfterRelease(CreateHeader("cancel-after-await"), entered,
                release.Task, cancellation.Token);
            var started = await entered.Task;
            Assert.Equal("cancel-after-await", started.TraceId);
            cancellation.Cancel();
            var actual = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                operation.WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.Equal(cancellation.Token, actual.CancellationToken);
            Assert.Equal(1, probe.Calls);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            release.TrySetResult();
            TraceIdContext.Current = null;
        }
    }

    [Fact]
    public void Invoke_WhenNested_ShouldRestoreEachParent()
    {
        using var host = CreateHost();
        var probe = host.Services.GetRequiredService<ICapTraceProbe>();
        var parent = CreateParent();
        TraceIdContext.Current = parent;

        try
        {
            var result = probe.WithNested(CreateHeader("outer-trace"), CreateHeader("inner-trace"), probe);

            Assert.Equal("outer-trace", result.OuterBefore.TraceId);
            Assert.Equal("inner-trace", result.Inner.TraceId);
            Assert.Equal("outer-trace", result.OuterAfter.TraceId);
            Assert.Same(result.OuterBefore.Context, result.OuterAfter.Context);
            Assert.NotSame(result.OuterBefore.Context, result.Inner.Context);
            Assert.Equal(2, probe.Calls);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    [Fact]
    public void Invoke_WhenNestedTargetThrows_ShouldRestoreOuterAndRootContexts()
    {
        using var host = CreateHost();
        var probe = host.Services.GetRequiredService<ICapTraceProbe>();
        var parent = CreateParent();
        var expected = new TraceProbeException("nested failure");
        TraceIdContext.Current = parent;

        try
        {
            var result = probe.WithNestedFailure(CreateHeader("outer-trace"), CreateHeader("inner-trace"),
                probe, expected);

            Assert.Equal("outer-trace", result.TraceId);
            Assert.NotSame(parent, result.Context);
            Assert.Equal(2, probe.Calls);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    [Fact]
    public async Task Invoke_WhenOverlapped_ShouldIsolateContexts()
    {
        using var host = CreateHost();
        var probe = host.Services.GetRequiredService<ICapTraceProbe>();
        var parent = CreateParent();
        var enteredA = new TaskCompletionSource<TraceObservation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var enteredB = new TaskCompletionSource<TraceObservation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TraceIdContext.Current = parent;

        try
        {
            var operationA = probe.WaitAsync(CreateHeader("trace-a"), enteredA, releaseA.Task);
            var operationB = probe.WaitAsync(CreateHeader("trace-b"), enteredB, releaseB.Task);
            var startedA = await enteredA.Task;
            var startedB = await enteredB.Task;

            Assert.Equal("trace-a", startedA.TraceId);
            Assert.Equal("trace-b", startedB.TraceId);
            Assert.NotSame(startedA.Context, startedB.Context);
            releaseA.TrySetResult();
            releaseB.TrySetResult();
            var completed = await Task.WhenAll(operationA, operationB);

            Assert.Equal(new[] { "trace-a", "trace-b" }, completed.Select(x => x.TraceId));
            Assert.Equal(2, probe.Calls);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            releaseA.TrySetResult();
            releaseB.TrySetResult();
            TraceIdContext.Current = null;
        }
    }

    private static IHost CreateHost()
    {
        return new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.EnableAop();
                services.AddScoped<ICapTraceProbe, CapTraceProbe>();
            })
            .Build();
    }

    private static void AssertProxyMetadata(ICapTraceProbe probe)
    {
        Assert.NotEqual(typeof(CapTraceProbe), probe.GetType());
        Assert.NotNull(typeof(ICapTraceProbe).GetMethod(nameof(ICapTraceProbe.WithObject))
            ?.GetCustomAttribute<CapTraceAttribute>());
        Assert.NotNull(typeof(ICapTraceProbe).GetMethod(nameof(ICapTraceProbe.WithObject))
            ?.GetParameters()[0].GetCustomAttribute<FromCapAttribute>());
    }

    private static CapHeader CreateHeader(string traceId) => new(new Dictionary<string, string>
    {
        ["bing-trace-id"] = traceId
    });

    private static CapHeader CreateHeaderWithoutTraceId() => new(new Dictionary<string, string>());

    private static TraceIdContext CreateParent() =>
        new("parent-trace", "root-id", "parent-id", "child-id");

    private static void AssertParentUnchanged(TraceIdContext parent)
    {
        Assert.Equal("parent-trace", parent.TraceId);
        Assert.Equal("root-id", parent.RootId);
        Assert.Equal("parent-id", parent.ParentId);
        Assert.Equal("child-id", parent.ChildId);
    }

    private sealed class TestAspectContext : AspectContext
    {
        private readonly MethodInfo _method;

        public TestAspectContext(MethodInfo method, object[] parameters)
        {
            _method = method;
            Parameters = parameters;
        }

        public override IDictionary<string, object> AdditionalData { get; } = new Dictionary<string, object>();
        public override object ReturnValue { get; set; }
        public override IServiceProvider ServiceProvider => null;
        public override MethodInfo ServiceMethod => _method;
        public override object Implementation => null;
        public override MethodInfo ImplementationMethod => _method;
        public override object[] Parameters { get; }
        public override MethodInfo ProxyMethod => _method;
        public override MethodInfo PredicateMethod => _method;
        public override object Proxy => null;
        public override Task Break() => Task.CompletedTask;
        public override Task Invoke(AspectDelegate next) => next(this);
        public override Task Complete() => Task.CompletedTask;
    }

    public sealed record TraceObservation(TraceIdContext Context, string TraceId,
        string RootId, string ParentId, string ChildId);

    public sealed record NestedTraceObservation(TraceObservation OuterBefore, TraceObservation Inner,
        TraceObservation OuterAfter);

    public interface ICapTraceProbe
    {
        [CapTrace]
        TraceObservation WithoutFromCap(int value);

        [CapTrace]
        TraceObservation WithObject([FromCap] object value);

        [CapTrace]
        CancellationToken WithCancellationToken([FromCap] CancellationToken token);

        [CapTrace]
        Task<TraceObservation> WithTask([FromCap] object value);

        [CapTrace]
        ValueTask<TraceObservation> WithValueTask([FromCap] object value);

        [CapTrace]
        Task WithTaskVoid([FromCap] object value);

        [CapTrace]
        ValueTask WithValueTaskVoid([FromCap] object value);

        [CapTrace]
        Task<TraceObservation> WaitAsync([FromCap] object value,
            TaskCompletionSource<TraceObservation> entered, Task release);

        [CapTrace]
        TraceObservation Throw([FromCap] object value, Exception exception);

        [CapTrace]
        Task<TraceObservation> Cancel([FromCap] object value, CancellationToken cancellationToken);

        [CapTrace]
        Task<TraceObservation> CancelAfterRelease([FromCap] object value,
            TaskCompletionSource<TraceObservation> entered, Task release, CancellationToken cancellationToken);

        [CapTrace]
        NestedTraceObservation WithNested([FromCap] object value, object nestedValue, ICapTraceProbe nested);

        [CapTrace]
        TraceObservation WithNestedFailure([FromCap] object value, object nestedValue, ICapTraceProbe nested,
            Exception exception);

        TraceObservation LastObservation { get; }
        int Calls { get; }
    }

    public sealed class CapTraceProbe : ICapTraceProbe
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);
        public TraceObservation LastObservation { get; private set; }

        public TraceObservation WithoutFromCap(int value)
        {
            Interlocked.Increment(ref _calls);
            return ObserveAndRecord();
        }

        public TraceObservation WithObject(object value)
        {
            Interlocked.Increment(ref _calls);
            return ObserveAndRecord();
        }

        public CancellationToken WithCancellationToken(CancellationToken token)
        {
            Interlocked.Increment(ref _calls);
            ObserveAndRecord();
            return token;
        }

        public Task<TraceObservation> WithTask(object value)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(ObserveAndRecord());
        }

        public ValueTask<TraceObservation> WithValueTask(object value)
        {
            Interlocked.Increment(ref _calls);
            return ValueTask.FromResult(ObserveAndRecord());
        }

        public Task WithTaskVoid(object value)
        {
            Interlocked.Increment(ref _calls);
            ObserveAndRecord();
            return Task.CompletedTask;
        }

        public ValueTask WithValueTaskVoid(object value)
        {
            Interlocked.Increment(ref _calls);
            ObserveAndRecord();
            return ValueTask.CompletedTask;
        }

        public async Task<TraceObservation> WaitAsync(object value, TaskCompletionSource<TraceObservation> entered,
            Task release)
        {
            Interlocked.Increment(ref _calls);
            entered.TrySetResult(ObserveAndRecord());
            await release.ConfigureAwait(false);
            return ObserveAndRecord();
        }

        public TraceObservation Throw(object value, Exception exception)
        {
            Interlocked.Increment(ref _calls);
            throw exception;
        }

        public Task<TraceObservation> Cancel(object value, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            ObserveAndRecord();
            return Task.FromCanceled<TraceObservation>(cancellationToken);
        }

        public async Task<TraceObservation> CancelAfterRelease(object value,
            TaskCompletionSource<TraceObservation> entered, Task release, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            entered.TrySetResult(ObserveAndRecord());
            await release.WaitAsync(cancellationToken).ConfigureAwait(false);
            return ObserveAndRecord();
        }

        public NestedTraceObservation WithNested(object value, object nestedValue, ICapTraceProbe nested)
        {
            Interlocked.Increment(ref _calls);
            var outerBefore = ObserveAndRecord();
            var inner = nested.WithObject(nestedValue);
            var outerAfter = ObserveAndRecord();
            return new NestedTraceObservation(outerBefore, inner, outerAfter);
        }

        public TraceObservation WithNestedFailure(object value, object nestedValue, ICapTraceProbe nested,
            Exception exception)
        {
            Interlocked.Increment(ref _calls);
            try
            {
                nested.Throw(nestedValue, exception);
            }
            catch (Exception actual) when (ReferenceEquals(actual, exception))
            {
                return ObserveAndRecord();
            }

            throw new InvalidOperationException("Expected nested call to throw.");
        }

        private TraceObservation ObserveAndRecord()
        {
            var context = TraceIdContext.Current;
            return LastObservation = new TraceObservation(context, context?.TraceId, context?.RootId,
                context?.ParentId, context?.ChildId);
        }
    }

    private sealed class TraceProbeException : Exception
    {
        public TraceProbeException(string message) : base(message)
        {
        }
    }
}
