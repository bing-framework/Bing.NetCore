#nullable enable

using System.Threading.Tasks.Sources;
using AspectCore.Configuration;
using AspectCore.DynamicProxy;
using AspectCore.Extensions.Hosting;
using Bing.Application.Aspects;
using Bing.DependencyInjection;
using Bing.Events.Cap;
using Bing.Tracing;
using Bing.Uow;
using DotNetCore.CAP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bing.Aop.AspectCore;

/// <summary>
/// 通过真实 AspectCore 代理验证 CapTrace 与工作单元拦截器的取消管道。
/// </summary>
public sealed class CapTraceUnitOfWorkCancellationTest
{
    [Fact]
    public void Proxy_SynchronousCancellation_ShouldPreserveExceptionAndSkipSuccessPath()
    {
        using var host = CreateHost();
        using var scope = host.Services.CreateScope();
        var probe = scope.ServiceProvider.GetRequiredService<ICapTraceCancellationProbe>();
        var state = scope.ServiceProvider.GetRequiredService<CancellationProbeState>();
        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        var outer = host.Services.GetRequiredService<OuterSuccessPathRecorder>();
        var parent = CreateParent();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        state.Configure(cancellation.Token);
        TraceIdContext.Current = parent;

        try
        {
            var observed = Record.Exception(() => probe.CancelSync(CreateHeader(), cancellation.Token));
            Assert.True(observed is OperationCanceledException,
                $"Observed {observed?.GetType().FullName ?? "no exception"}; target={state.TargetCount}, " +
                $"outerEntered={outer.EntryCount}, outerSucceeded={outer.SuccessCount}, commits={manager.CommitCount}.");
            var actual = (OperationCanceledException)observed!;

            AssertCancellationOutcome(state, manager, outer, parent, actual, cancellation.Token);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    [Theory]
    [InlineData(CancellationReturnKind.Task)]
    [InlineData(CancellationReturnKind.TaskOfT)]
    [InlineData(CancellationReturnKind.ValueTask)]
    [InlineData(CancellationReturnKind.ValueTaskOfT)]
    public Task Proxy_PreCanceledAsyncReturn_ShouldPreserveExceptionAndSkipSuccessPath(
        CancellationReturnKind returnKind) => AssertAsyncCancellationAsync(returnKind, cancelAfterEntry: false);

    [Theory]
    [InlineData(CancellationReturnKind.Task)]
    [InlineData(CancellationReturnKind.TaskOfT)]
    [InlineData(CancellationReturnKind.ValueTask)]
    [InlineData(CancellationReturnKind.ValueTaskOfT)]
    public Task Proxy_SuspendedAsyncReturn_ShouldPreserveExceptionAndSkipSuccessPath(
        CancellationReturnKind returnKind) => AssertAsyncCancellationAsync(returnKind, cancelAfterEntry: true);

    [Fact]
    public async Task Proxy_Success_ShouldRunOuterPathAndCommitWorkUnit()
    {
        using var host = CreateHost();
        using var scope = host.Services.CreateScope();
        var probe = scope.ServiceProvider.GetRequiredService<ICapTraceCancellationProbe>();
        var state = scope.ServiceProvider.GetRequiredService<CancellationProbeState>();
        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        var outer = host.Services.GetRequiredService<OuterSuccessPathRecorder>();
        var parent = CreateParent();
        TraceIdContext.Current = parent;

        try
        {
            Assert.Equal(42, await probe.SucceedAsync(CreateHeader()));
            await outer.SuccessObserved.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(1, state.TargetCount);
            Assert.NotSame(parent, state.TargetTraceContext);
            Assert.Equal("cap-trace", state.TargetTraceContext!.TraceId);
            Assert.Equal(1, outer.EntryCount);
            Assert.Equal(1, outer.SuccessCount);
            Assert.Equal(1, manager.CommitCount);
            Assert.Equal(1, state.CommitAfterCount);
            AssertParentUnchanged(parent);
            Assert.Same(parent, TraceIdContext.Current);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    private static async Task AssertAsyncCancellationAsync(CancellationReturnKind returnKind,
        bool cancelAfterEntry)
    {
        using var host = CreateHost();
        using var scope = host.Services.CreateScope();
        var probe = scope.ServiceProvider.GetRequiredService<ICapTraceCancellationProbe>();
        var state = scope.ServiceProvider.GetRequiredService<CancellationProbeState>();
        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        var outer = host.Services.GetRequiredService<OuterSuccessPathRecorder>();
        var parent = CreateParent();
        using var cancellation = new CancellationTokenSource();
        if (!cancelAfterEntry)
            cancellation.Cancel();
        state.Configure(cancellation.Token);
        TraceIdContext.Current = parent;

        try
        {
            var operation = InvokeAsync(probe, returnKind, cancellation.Token);
            if (cancelAfterEntry)
            {
                await state.TargetEntered.WaitAsync(TimeSpan.FromSeconds(5));
                cancellation.Cancel();
            }

            var observed = await Record.ExceptionAsync(() => operation.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(observed is OperationCanceledException,
                $"Observed {observed?.GetType().FullName ?? "no exception"}; target={state.TargetCount}, " +
                $"outerEntered={outer.EntryCount}, outerSucceeded={outer.SuccessCount}, commits={manager.CommitCount}.");
            var actual = (OperationCanceledException)observed!;

            AssertCancellationOutcome(state, manager, outer, parent, actual, cancellation.Token);
            if (returnKind is CancellationReturnKind.ValueTask or CancellationReturnKind.ValueTaskOfT)
                Assert.Equal(1, state.ValueTaskSource!.GetResultCount);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    private static void AssertCancellationOutcome(CancellationProbeState state,
        RecordingUnitOfWorkManager manager, OuterSuccessPathRecorder outer, TraceIdContext parent,
        OperationCanceledException actual, CancellationToken token)
    {
        Assert.Same(state.ExpectedCancellation, actual);
        Assert.Equal(token, actual.CancellationToken);
        Assert.Equal(1, state.TargetCount);
        Assert.NotSame(parent, state.TargetTraceContext);
        Assert.Equal("cap-trace", state.TargetTraceContext!.TraceId);
        Assert.Equal(1, outer.EntryCount);
        Assert.Equal(0, outer.SuccessCount);
        Assert.Equal(0, manager.CommitCount);
        Assert.Equal(0, state.CommitAfterCount);
        AssertParentUnchanged(parent);
        Assert.Same(parent, TraceIdContext.Current);
    }

    private static Task InvokeAsync(ICapTraceCancellationProbe probe, CancellationReturnKind returnKind,
        CancellationToken cancellationToken)
    {
        var header = CreateHeader();
        return returnKind switch
        {
            CancellationReturnKind.Task => probe.CancelTask(header, cancellationToken),
            CancellationReturnKind.TaskOfT => probe.CancelTaskOfT(header, cancellationToken),
            CancellationReturnKind.ValueTask => probe.CancelValueTask(header, cancellationToken).AsTask(),
            CancellationReturnKind.ValueTaskOfT => probe.CancelValueTaskOfT(header, cancellationToken).AsTask(),
            _ => throw new ArgumentOutOfRangeException(nameof(returnKind), returnKind, null)
        };
    }

    private static IHost CreateHost() => new HostBuilder()
        .UseServiceContext()
        .ConfigureServices(services =>
        {
            services.AddSingleton<CancellationProbeState>();
            services.AddSingleton<RecordingUnitOfWorkManager>();
            services.AddScoped<IUnitOfWorkManager>(provider =>
                provider.GetRequiredService<RecordingUnitOfWorkManager>());
            services.AddSingleton<OuterSuccessPathRecorder>();
            services.AddUnitOfWorkScopes();
            services.AddScoped<ICapTraceCancellationProbe, CapTraceCancellationProbe>();
            services.EnableAop(configuration =>
                configuration.Interceptors.AddTyped<OuterSuccessPathInterceptor>());
            services.EnableBingUnitOfWork();
        })
        .Build();

    private static CapHeader CreateHeader() => new(new Dictionary<string, string?>
    {
        ["bing-trace-id"] = "cap-trace"
    });

    private static TraceIdContext CreateParent() =>
        new("parent-trace", "root-id", "parent-id", "child-id");

    private static void AssertParentUnchanged(TraceIdContext parent)
    {
        Assert.Equal("parent-trace", parent.TraceId);
        Assert.Equal("root-id", parent.RootId);
        Assert.Equal("parent-id", parent.ParentId);
        Assert.Equal("child-id", parent.ChildId);
    }

    public enum CancellationReturnKind
    {
        Task,
        TaskOfT,
        ValueTask,
        ValueTaskOfT
    }

    public interface ICapTraceCancellationProbe
    {
        [CapTrace]
        [UnitOfWork]
        int CancelSync([FromCap] CapHeader header, CancellationToken cancellationToken);

        [CapTrace]
        [UnitOfWork]
        Task CancelTask([FromCap] CapHeader header, CancellationToken cancellationToken);

        [CapTrace]
        [UnitOfWork]
        Task<int> CancelTaskOfT([FromCap] CapHeader header, CancellationToken cancellationToken);

        [CapTrace]
        [UnitOfWork]
        ValueTask CancelValueTask([FromCap] CapHeader header, CancellationToken cancellationToken);

        [CapTrace]
        [UnitOfWork]
        ValueTask<int> CancelValueTaskOfT([FromCap] CapHeader header, CancellationToken cancellationToken);

        [CapTrace]
        [UnitOfWork]
        Task<int> SucceedAsync([FromCap] CapHeader header);
    }

    public sealed class CapTraceCancellationProbe : ICapTraceCancellationProbe, ICommitAfter
    {
        private readonly CancellationProbeState _state;

        public CapTraceCancellationProbe(CancellationProbeState state) => _state = state;

        public int CancelSync(CapHeader header, CancellationToken cancellationToken)
        {
            _state.RecordTarget();
            throw _state.ExpectedCancellation;
        }

        public Task CancelTask(CapHeader header, CancellationToken cancellationToken)
        {
            _state.RecordTarget();
            return _state.CreateTask(cancellationToken);
        }

        public Task<int> CancelTaskOfT(CapHeader header, CancellationToken cancellationToken)
        {
            _state.RecordTarget();
            return _state.CreateTaskOfT(cancellationToken);
        }

        public ValueTask CancelValueTask(CapHeader header, CancellationToken cancellationToken)
        {
            _state.RecordTarget();
            return _state.CreateValueTask(cancellationToken);
        }

        public ValueTask<int> CancelValueTaskOfT(CapHeader header, CancellationToken cancellationToken)
        {
            _state.RecordTarget();
            return _state.CreateValueTaskOfT(cancellationToken);
        }

        public Task<int> SucceedAsync(CapHeader header)
        {
            _state.RecordTarget();
            return Task.FromResult(42);
        }

        public void CommitAfter() => _state.RecordCommitAfter();
    }

    public sealed class CancellationProbeState
    {
        private TaskCompletionSource _targetEntered = NewCompletionSource();
        private OperationCanceledException? _expectedCancellation;
        private TraceIdContext? _targetTraceContext;
        private int _targetCount;
        private int _commitAfterCount;

        public OperationCanceledException ExpectedCancellation =>
            _expectedCancellation ?? throw new InvalidOperationException("Cancellation was not configured.");

        public TraceIdContext? TargetTraceContext => _targetTraceContext;

        public int TargetCount => Volatile.Read(ref _targetCount);

        public int CommitAfterCount => Volatile.Read(ref _commitAfterCount);

        public Task TargetEntered => _targetEntered.Task;

        public SingleConsumptionValueTaskSource? ValueTaskSource { get; private set; }

        public void Configure(CancellationToken cancellationToken)
        {
            _expectedCancellation = new OperationCanceledException(cancellationToken);
            _targetEntered = NewCompletionSource();
            _targetTraceContext = null;
            _targetCount = 0;
            _commitAfterCount = 0;
            ValueTaskSource = null;
        }

        public void RecordTarget()
        {
            Interlocked.Increment(ref _targetCount);
            _targetTraceContext = TraceIdContext.Current;
        }

        public void RecordCommitAfter() => Interlocked.Increment(ref _commitAfterCount);

        public Task CreateTask(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromException(ExpectedCancellation);

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => completion.TrySetException(ExpectedCancellation));
            _targetEntered.TrySetResult();
            return completion.Task;
        }

        public Task<int> CreateTaskOfT(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromException<int>(ExpectedCancellation);

            var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => completion.TrySetException(ExpectedCancellation));
            _targetEntered.TrySetResult();
            return completion.Task;
        }

        public ValueTask CreateValueTask(CancellationToken cancellationToken)
        {
            ValueTaskSource = new SingleConsumptionValueTaskSource();
            if (cancellationToken.IsCancellationRequested)
                ValueTaskSource.SetException(ExpectedCancellation);
            else
            {
                cancellationToken.Register(() => ValueTaskSource.SetException(ExpectedCancellation));
                _targetEntered.TrySetResult();
            }

            return ValueTaskSource.AsValueTask();
        }

        public ValueTask<int> CreateValueTaskOfT(CancellationToken cancellationToken)
        {
            ValueTaskSource = new SingleConsumptionValueTaskSource();
            if (cancellationToken.IsCancellationRequested)
                ValueTaskSource.SetException(ExpectedCancellation);
            else
            {
                cancellationToken.Register(() => ValueTaskSource.SetException(ExpectedCancellation));
                _targetEntered.TrySetResult();
            }

            return ValueTaskSource.AsValueTaskOfT();
        }

        private static TaskCompletionSource NewCompletionSource() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public sealed class SingleConsumptionValueTaskSource : IValueTaskSource, IValueTaskSource<int>
    {
        private ManualResetValueTaskSourceCore<int> _core;
        private int _getResultCount;

        public SingleConsumptionValueTaskSource() => _core.RunContinuationsAsynchronously = true;

        public int GetResultCount => Volatile.Read(ref _getResultCount);

        public ValueTask AsValueTask() => new(this, _core.Version);

        public ValueTask<int> AsValueTaskOfT() => new(this, _core.Version);

        public void SetException(Exception exception) => _core.SetException(exception);

        void IValueTaskSource.GetResult(short token)
        {
            Interlocked.Increment(ref _getResultCount);
            _core.GetResult(token);
        }

        int IValueTaskSource<int>.GetResult(short token)
        {
            Interlocked.Increment(ref _getResultCount);
            return _core.GetResult(token);
        }

        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) => _core.GetStatus(token);

        ValueTaskSourceStatus IValueTaskSource<int>.GetStatus(short token) => _core.GetStatus(token);

        void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token,
            ValueTaskSourceOnCompletedFlags flags) => _core.OnCompleted(continuation, state, token, flags);

        void IValueTaskSource<int>.OnCompleted(Action<object?> continuation, object? state, short token,
            ValueTaskSourceOnCompletedFlags flags) => _core.OnCompleted(continuation, state, token, flags);
    }

    public sealed class RecordingUnitOfWorkManager : IUnitOfWorkManager
    {
        private int _commitCount;

        public int CommitCount => Volatile.Read(ref _commitCount);

        public void Commit() => Interlocked.Increment(ref _commitCount);

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _commitCount);
            return Task.CompletedTask;
        }

        public void Register(IUnitOfWork unitOfWork) { }

        public IReadOnlyCollection<IUnitOfWork> GetUnitOfWorks() => Array.Empty<IUnitOfWork>();
    }

    public sealed class OuterSuccessPathRecorder
    {
        private int _entryCount;
        private int _successCount;
        private readonly TaskCompletionSource _successObserved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int EntryCount => Volatile.Read(ref _entryCount);

        public int SuccessCount => Volatile.Read(ref _successCount);

        public Task SuccessObserved => _successObserved.Task;

        public void Enter() => Interlocked.Increment(ref _entryCount);

        public void Succeed()
        {
            Interlocked.Increment(ref _successCount);
            _successObserved.TrySetResult();
        }
    }

    public sealed class OuterSuccessPathInterceptor : AbstractInterceptorAttribute
    {
        public OuterSuccessPathInterceptor() => Order = -1000;

        public override Task Invoke(AspectContext context, AspectDelegate next)
        {
            if (context.Implementation is not ICapTraceCancellationProbe)
                return next(context);

            var recorder = context.ServiceProvider.GetRequiredService<OuterSuccessPathRecorder>();
            recorder.Enter();
            var nextTask = next(context);
            _ = nextTask.ContinueWith(_ => recorder.Succeed(), CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            return nextTask;
        }
    }
}
