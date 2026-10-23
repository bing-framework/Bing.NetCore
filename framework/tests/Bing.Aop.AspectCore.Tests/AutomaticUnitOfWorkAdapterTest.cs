#nullable enable

using System.Collections.Concurrent;
using System.Threading.Tasks.Sources;
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
/// 通过真实代理验证自动工作单元 owner、participant 与取消传播。
/// </summary>
public sealed class AutomaticUnitOfWorkAdapterTest
{
    [Fact]
    public async Task Success_ShouldCommitOwnerAndRunCommitAfterOnce()
    {
        using var host = CreateHost();
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomaticOperationProbe>();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var state = scope.ServiceProvider.GetRequiredService<AutomaticOperationState>();
        var outer = scope.ServiceProvider.GetRequiredService<AutomaticOuterSuccessRecorder>();
        state.SetOperationManager(manager);

        var result = await ObserveOuterSuccess(
            service.SucceedAsync(CreateHeader(), CancellationToken.None), outer);
        await outer.SuccessObserved.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(42, result);
        Assert.Equal(1, service.CommitAfterCount);
        Assert.Equal(1, scope.ServiceProvider.GetRequiredService<AutomaticOperationState>().ResourceCommitCount);
        Assert.Equal(1, scope.ServiceProvider.GetRequiredService<AutomaticOperationState>().TargetCount);
        Assert.Equal(1, outer.EntryCount);
        Assert.Equal(1, outer.SuccessCount);
        Assert.Null(manager.Current);
        Assert.Equal(TimeSpan.FromSeconds(20),
            scope.ServiceProvider.GetRequiredService<AutomaticOperationState>().DefaultTimeout);
    }

    [Fact]
    public async Task RepeatedAutomaticParticipant_ShouldRegisterCommitAfterOnce()
    {
        using var host = CreateHost();
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomaticOperationProbe>();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var state = scope.ServiceProvider.GetRequiredService<AutomaticOperationState>();
        state.SetOperationManager(manager);
        var owner = manager.BeginOperation();

        await service.SucceedAsync(CreateHeader(), CancellationToken.None);
        await service.SucceedAsync(CreateHeader(), CancellationToken.None);

        Assert.Equal(2, state.TargetCount);
        Assert.Equal(0, state.ResourceCommitCount);
        Assert.Equal(0, service.CommitAfterCount);
        await owner.CompleteAsync();
        await owner.DisposeAsync();

        Assert.Equal(1, state.ResourceCommitCount);
        Assert.Equal(1, service.CommitAfterCount);
        Assert.Null(manager.Current);
    }

    [Fact]
    public async Task NestedAutomaticParticipant_ShouldCompleteAtOwnerBoundary()
    {
        using var host = CreateHost();
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomaticOperationProbe>();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var state = scope.ServiceProvider.GetRequiredService<AutomaticOperationState>();
        state.SetOperationManager(manager);

        await service.ExecuteNestedAsync(CreateHeader(), CancellationToken.None);

        Assert.Equal(2, state.TargetCount);
        Assert.Equal(1, state.ResourceCommitCount);
        Assert.Equal(0, state.ResourceRollbackCount);
        Assert.Equal(1, service.CommitAfterCount);
        Assert.Null(manager.Current);
    }

    [Fact]
    public void SynchronousVirtualClassProxy_ShouldCommitAutomaticOwner()
    {
        using var host = CreateHost();
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ClassAutomaticOperationProbe>();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var state = scope.ServiceProvider.GetRequiredService<AutomaticOperationState>();
        state.SetOperationManager(manager);
        Assert.NotEqual(typeof(ClassAutomaticOperationProbe), service.GetType());

        service.ExecuteSynchronously();

        Assert.Equal(1, state.TargetCount);
        Assert.Equal(1, state.ResourceCommitCount);
        Assert.Equal(0, state.ResourceRollbackCount);
        Assert.Equal(1, service.CommitAfterCount);
        Assert.Null(manager.Current);
    }

    [Fact]
    public void AutomaticProxy_SynchronousOriginalCancellation_ShouldSkipCommitAndOuterSuccess()
    {
        using var host = CreateHost();
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomaticOperationProbe>();
        var state = scope.ServiceProvider.GetRequiredService<AutomaticOperationState>();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var outer = scope.ServiceProvider.GetRequiredService<AutomaticOuterSuccessRecorder>();
        state.SetOperationManager(manager);
        using var caller = new CancellationTokenSource();
        state.ExpectedCancellation = new OperationCanceledException("original synchronous cancellation", caller.Token);

        var exception = Record.Exception(() => InvokeWithOuterSuccess(
            () => service.CancelSynchronously(CreateHeader(), caller.Token), outer));

        Assert.Same(state.ExpectedCancellation, exception);
        Assert.Equal(caller.Token, Assert.IsType<OperationCanceledException>(exception).CancellationToken);
        AssertAutomaticFailure(state, service, manager, outer);
    }

    [Theory]
    [InlineData(AsyncReturnKind.Task)]
    [InlineData(AsyncReturnKind.TaskOfT)]
    public async Task AutomaticProxy_TaskPreCanceled_ShouldReturnCanceledTaskAndRollback(
        AsyncReturnKind returnKind)
    {
        using var host = CreateHost();
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomaticOperationProbe>();
        var state = scope.ServiceProvider.GetRequiredService<AutomaticOperationState>();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var outer = scope.ServiceProvider.GetRequiredService<AutomaticOuterSuccessRecorder>();
        state.SetOperationManager(manager);
        using var caller = new CancellationTokenSource();
        caller.Cancel();

        var invocation = ObserveOuterSuccess(InvokeCancellableTask(service, returnKind, caller.Token), outer);
        Assert.Equal(TaskStatus.Canceled, invocation.Status);
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invocation);

        Assert.Equal(state.TargetCancellationToken, exception.CancellationToken);
        Assert.True(state.TargetCancellationToken.IsCancellationRequested);
        AssertAutomaticFailure(state, service, manager, outer);
    }

    [Theory]
    [InlineData(AsyncReturnKind.Task)]
    [InlineData(AsyncReturnKind.TaskOfT)]
    public async Task AutomaticProxy_TaskCanceledAfterTargetEntry_ShouldReturnCanceledTaskAndRollback(
        AsyncReturnKind returnKind)
    {
        using var host = CreateHost();
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomaticOperationProbe>();
        var state = scope.ServiceProvider.GetRequiredService<AutomaticOperationState>();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var outer = scope.ServiceProvider.GetRequiredService<AutomaticOuterSuccessRecorder>();
        state.SetOperationManager(manager);
        state.WaitForTaskCancellation = true;
        using var caller = new CancellationTokenSource();

        var invocation = ObserveOuterSuccess(InvokeCancellableTask(service, returnKind, caller.Token), outer);
        await state.TargetEntered.WaitAsync(TimeSpan.FromSeconds(5));
        caller.Cancel();
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => invocation.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal(TaskStatus.Canceled, invocation.Status);
        Assert.Equal(state.TargetCancellationToken, exception.CancellationToken);
        Assert.True(exception.CancellationToken.IsCancellationRequested);
        AssertAutomaticFailure(state, service, manager, outer);
    }

    [Fact]
    public async Task ExplicitInterfaceProxy_ShouldCommitAutomaticOwner()
    {
        using var host = CreateHost();
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IExplicitAutomaticOperationProbe>();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var state = scope.ServiceProvider.GetRequiredService<AutomaticOperationState>();
        state.SetOperationManager(manager);
        Assert.NotEqual(typeof(ExplicitAutomaticOperationProbe), service.GetType());

        Assert.Equal(42, await service.ExecuteAsync());

        Assert.Equal(1, state.TargetCount);
        Assert.Equal(1, state.ResourceCommitCount);
        Assert.Equal(1, service.CommitAfterCount);
        Assert.Null(manager.Current);
    }

    [Theory]
    [InlineData(SourceBackedReturnKind.ValueTask)]
    [InlineData(SourceBackedReturnKind.ValueTaskOfT)]
    public async Task SourceBackedValueTask_PreCanceledOwner_ShouldPreserveCancellationAndRollbackOnce(
        SourceBackedReturnKind returnKind)
    {
        using var host = CreateHost();
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomaticOperationProbe>();
        var state = scope.ServiceProvider.GetRequiredService<AutomaticOperationState>();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var outer = scope.ServiceProvider.GetRequiredService<AutomaticOuterSuccessRecorder>();
        state.SetOperationManager(manager);
        using var caller = new CancellationTokenSource();
        caller.Cancel();

        var invocation = InvokeSourceBacked(service, returnKind, caller.Token, outer);
        Assert.Equal(TaskStatus.Canceled, invocation.Status());
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            invocation.AsTask().WaitAsync(TimeSpan.FromSeconds(5)));

        using var source = Assert.IsType<SingleConsumptionValueTaskSource>(state.LastValueTaskSource);
        Assert.Equal(TaskStatus.Canceled, invocation.Status());
        Assert.Same(source.ExpectedCancellation, exception);
        Assert.Equal(state.TargetCancellationToken, exception.CancellationToken);
        Assert.True(caller.IsCancellationRequested);
        Assert.True(state.TargetCancellationToken.IsCancellationRequested);
        Assert.Equal(1, state.TargetCount);
        Assert.Equal(1, source.GetResultCount);
        Assert.Equal(ValueTaskSourceStatus.Canceled, source.Status);
        Assert.Equal(0, state.ResourceCommitCount);
        Assert.Equal(1, state.ResourceRollbackCount);
        Assert.Equal(1, state.ResourceDisposeCount);
        Assert.Equal(0, service.CommitAfterCount);
        Assert.Equal(1, outer.EntryCount);
        Assert.Equal(0, outer.SuccessCount);
        Assert.Null(manager.Current);
    }

    [Theory]
    [InlineData(SourceBackedReturnKind.ValueTask)]
    [InlineData(SourceBackedReturnKind.ValueTaskOfT)]
    public async Task SourceBackedValueTask_SignalledParticipantCancellation_ShouldRollbackOwnerOnce(
        SourceBackedReturnKind returnKind)
    {
        using var host = CreateHost();
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomaticOperationProbe>();
        var state = scope.ServiceProvider.GetRequiredService<AutomaticOperationState>();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var outer = scope.ServiceProvider.GetRequiredService<AutomaticOuterSuccessRecorder>();
        state.SetOperationManager(manager);
        state.WaitForSourceCancellation = true;
        var owner = manager.BeginOperation();
        using var caller = new CancellationTokenSource();
        var invocation = InvokeSourceBacked(service, returnKind, caller.Token, outer);
        Assert.Equal(TaskStatus.WaitingForActivation, invocation.Status());
        var invocationTask = invocation.AsTask();

        using var source = Assert.IsType<SingleConsumptionValueTaskSource>(state.LastValueTaskSource);
        await source.Started.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(owner, manager.Current);
        caller.Cancel();
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => invocationTask.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal(TaskStatus.Canceled, invocation.Status());
        Assert.Equal(TaskStatus.Canceled, invocationTask.Status);
        Assert.Equal(ValueTaskSourceStatus.Canceled, source.Status);
        Assert.Same(source.ExpectedCancellation, exception);
        Assert.Equal(state.TargetCancellationToken, exception.CancellationToken);
        Assert.True(exception.CancellationToken.IsCancellationRequested);
        Assert.Equal(1, state.TargetCount);
        Assert.Equal(1, source.GetResultCount);
        Assert.True(owner.IsRollbackOnly);
        Assert.Equal(0, state.ResourceCommitCount);
        Assert.Equal(0, state.ResourceRollbackCount);
        Assert.Equal(0, service.CommitAfterCount);
        Assert.Equal(1, outer.EntryCount);
        Assert.Equal(0, outer.SuccessCount);

        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CompleteAsync());
        await owner.DisposeAsync();

        Assert.Equal(0, state.ResourceCommitCount);
        Assert.Equal(1, state.ResourceRollbackCount);
        Assert.Equal(1, state.ResourceDisposeCount);
        Assert.Equal(0, service.CommitAfterCount);
        Assert.Equal(1, outer.EntryCount);
        Assert.Equal(0, outer.SuccessCount);
        Assert.Null(manager.Current);
    }

    [Theory]
    [InlineData(SourceBackedReturnKind.ValueTask)]
    [InlineData(SourceBackedReturnKind.ValueTaskOfT)]
    public async Task SourceBackedValueTask_SuccessfulParticipant_ShouldCommitAtOwnerOnce(
        SourceBackedReturnKind returnKind)
    {
        using var host = CreateHost();
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomaticOperationProbe>();
        var state = scope.ServiceProvider.GetRequiredService<AutomaticOperationState>();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var outer = scope.ServiceProvider.GetRequiredService<AutomaticOuterSuccessRecorder>();
        state.SetOperationManager(manager);
        var owner = manager.BeginOperation();

        var invocation = InvokeSourceBacked(service, returnKind, CancellationToken.None, outer);
        Assert.Equal(TaskStatus.RanToCompletion, invocation.Status());
        await invocation.AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await outer.SuccessObserved.WaitAsync(TimeSpan.FromSeconds(5));

        using var source = Assert.IsType<SingleConsumptionValueTaskSource>(state.LastValueTaskSource);
        Assert.Equal(1, state.TargetCount);
        Assert.Equal(1, source.GetResultCount);
        Assert.Equal(ValueTaskSourceStatus.Succeeded, source.Status);
        Assert.Equal(0, state.ResourceCommitCount);
        Assert.Equal(0, state.ResourceRollbackCount);
        Assert.Equal(0, service.CommitAfterCount);
        Assert.Equal(1, outer.EntryCount);
        Assert.Equal(1, outer.SuccessCount);
        Assert.Same(owner, manager.Current);

        await owner.CompleteAsync();
        await owner.DisposeAsync();

        Assert.Equal(1, state.ResourceCommitCount);
        Assert.Equal(0, state.ResourceRollbackCount);
        Assert.Equal(1, state.ResourceDisposeCount);
        Assert.Equal(1, service.CommitAfterCount);
        Assert.Null(manager.Current);
    }

    [Theory]
    [InlineData(SourceBackedReturnKind.ValueTask)]
    [InlineData(SourceBackedReturnKind.ValueTaskOfT)]
    public async Task SourceBackedValueTask_AutomaticOwner_ShouldObserveSuccessAndCommitOnce(
        SourceBackedReturnKind returnKind)
    {
        using var host = CreateHost();
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomaticOperationProbe>();
        var state = scope.ServiceProvider.GetRequiredService<AutomaticOperationState>();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var outer = scope.ServiceProvider.GetRequiredService<AutomaticOuterSuccessRecorder>();
        state.SetOperationManager(manager);

        var invocation = InvokeSourceBacked(service, returnKind, CancellationToken.None, outer);
        Assert.Equal(TaskStatus.RanToCompletion, invocation.Status());
        await invocation.AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await outer.SuccessObserved.WaitAsync(TimeSpan.FromSeconds(5));

        using var source = Assert.IsType<SingleConsumptionValueTaskSource>(state.LastValueTaskSource);
        Assert.Equal(1, state.TargetCount);
        Assert.Equal(1, source.GetResultCount);
        Assert.Equal(1, state.ResourceCommitCount);
        Assert.Equal(0, state.ResourceRollbackCount);
        Assert.Equal(1, state.ResourceDisposeCount);
        Assert.Equal(1, service.CommitAfterCount);
        Assert.Equal(1, outer.EntryCount);
        Assert.Equal(1, outer.SuccessCount);
        Assert.Null(manager.Current);
    }

    [Theory]
    [InlineData(AsyncReturnKind.Task)]
    [InlineData(AsyncReturnKind.TaskOfT)]
    [InlineData(AsyncReturnKind.ValueTask)]
    [InlineData(AsyncReturnKind.ValueTaskOfT)]
    public async Task CapTraceAndAutomaticBoundary_ShouldPreserveTargetFaultedCancellationAndOriginalException(
        AsyncReturnKind returnKind)
    {
        using var host = CreateHost();
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomaticOperationProbe>();
        var state = scope.ServiceProvider.GetRequiredService<AutomaticOperationState>();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var outer = scope.ServiceProvider.GetRequiredService<AutomaticOuterSuccessRecorder>();
        state.SetOperationManager(manager);
        using var caller = new CancellationTokenSource();
        state.ExpectedCancellation = new OperationCanceledException("original cancellation", caller.Token);
        TraceIdContext.Current = new TraceIdContext("parent", "root", "parent", "child");

        try
        {
            var invocation = InvokeFaultedOce(service, returnKind, caller.Token);
            invocation.Observe(outer);
            Assert.Equal(TaskStatus.Canceled, invocation.Task.Status);
            if (invocation.ValueTaskStatus != null)
                Assert.Equal(TaskStatus.Canceled, invocation.ValueTaskStatus());
            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invocation.Task);

            Assert.Equal(TaskStatus.Faulted, state.TargetAsyncStatus);
            Assert.Equal(TaskStatus.Canceled, invocation.Task.Status);
            Assert.Same(state.ExpectedCancellation, exception);
            Assert.Equal(caller.Token, exception.CancellationToken);
            Assert.Equal(1, state.TargetCount);
            Assert.Equal(0, state.ResourceCommitCount);
            Assert.Equal(1, state.ResourceRollbackCount);
            Assert.Equal(0, service.CommitAfterCount);
            Assert.NotEqual(caller.Token, state.TargetCancellationToken);
            Assert.Equal("cap-trace", state.TargetTraceId);
            Assert.Equal(1, outer.EntryCount);
            Assert.Equal(0, outer.SuccessCount);
            Assert.Equal("parent", TraceIdContext.Current?.TraceId);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    [Fact]
    public async Task CaughtParticipantFailure_ShouldKeepOwnerRollbackOnly()
    {
        using var host = CreateHost();
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomaticOperationProbe>();
        var state = scope.ServiceProvider.GetRequiredService<AutomaticOperationState>();
        state.SetOperationManager(scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CatchInnerFailureAsync());

        Assert.Equal(1, state.TargetCount);
        Assert.True(state.InnerFailureCaught);
        Assert.Equal(0, state.ResourceCommitCount);
        Assert.Equal(1, state.ResourceRollbackCount);
        Assert.Equal(0, service.CommitAfterCount);
    }

    private static IHost CreateHost()
    {
        return new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.AddSingleton<AutomaticOperationState>();
                services.AddScoped<IInnerAutomaticOperationProbe, InnerAutomaticOperationProbe>();
                services.AddScoped<IAutomaticOperationProbe, AutomaticOperationProbe>();
                services.AddScoped<ClassAutomaticOperationProbe>();
                services.AddScoped<IExplicitAutomaticOperationProbe, ExplicitAutomaticOperationProbe>();
                services.AddSingleton<AutomaticOuterSuccessRecorder>();
                services.EnableAop();
                services.EnableBingAutomaticUnitOfWork(options =>
                    options.DefaultOptions.Timeout = TimeSpan.FromSeconds(10));
                services.EnableBingAutomaticUnitOfWork(options =>
                    options.DefaultOptions.Timeout = TimeSpan.FromSeconds(20));
            })
            .Build();
    }

    private static Task InvokeCancellableTask(IAutomaticOperationProbe service, AsyncReturnKind returnKind,
        CancellationToken cancellationToken)
    {
        var header = CreateHeader();
        return returnKind switch
        {
            AsyncReturnKind.Task => service.CancelOnTokenTaskAsync(header, cancellationToken),
            AsyncReturnKind.TaskOfT => service.CancelOnTokenTaskOfTAsync(header, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(returnKind), returnKind, null)
        };
    }

    private static AutomaticInvocation InvokeFaultedOce(IAutomaticOperationProbe service,
        AsyncReturnKind returnKind, CancellationToken cancellationToken)
    {
        var header = CreateHeader();
        return returnKind switch
        {
            AsyncReturnKind.Task => new AutomaticInvocation(
                service.CancelTaskAsync(header, cancellationToken)),
            AsyncReturnKind.TaskOfT => new AutomaticInvocation(
                service.CancelTaskOfTAsync(header, cancellationToken)),
            AsyncReturnKind.ValueTask => CreateValueTaskInvocation(
                service.CancelValueTaskAsync(header, cancellationToken)),
            AsyncReturnKind.ValueTaskOfT => CreateValueTaskInvocation(
                service.CancelValueTaskOfTAsync(header, cancellationToken)),
            _ => throw new ArgumentOutOfRangeException(nameof(returnKind), returnKind, null)
        };
    }

    private static SourceBackedInvocation InvokeSourceBacked(IAutomaticOperationProbe service,
        SourceBackedReturnKind returnKind, CancellationToken cancellationToken,
        AutomaticOuterSuccessRecorder outer)
    {
        var header = CreateHeader();
        if (returnKind == SourceBackedReturnKind.ValueTask)
        {
            var valueTask = service.SourceBackedValueTaskAsync(header, cancellationToken);
            return new SourceBackedInvocation(() => valueTask.AsTask(), () => GetValueTaskStatus(valueTask), outer);
        }

        var genericValueTask = service.SourceBackedValueTaskOfTAsync(header, cancellationToken);
        return new SourceBackedInvocation(() => genericValueTask.AsTask(),
            () => GetValueTaskStatus(genericValueTask), outer);
    }

    private static AutomaticInvocation CreateValueTaskInvocation(ValueTask valueTask) =>
        new(valueTask.AsTask(), () => GetValueTaskStatus(valueTask));

    private static AutomaticInvocation CreateValueTaskInvocation<T>(ValueTask<T> valueTask) =>
        new(valueTask.AsTask(), () => GetValueTaskStatus(valueTask));

    private static TaskStatus GetValueTaskStatus(ValueTask valueTask) =>
        valueTask.IsCanceled ? TaskStatus.Canceled : valueTask.IsFaulted ? TaskStatus.Faulted :
        valueTask.IsCompletedSuccessfully ? TaskStatus.RanToCompletion : TaskStatus.WaitingForActivation;

    private static TaskStatus GetValueTaskStatus<T>(ValueTask<T> valueTask) =>
        valueTask.IsCanceled ? TaskStatus.Canceled : valueTask.IsFaulted ? TaskStatus.Faulted :
        valueTask.IsCompletedSuccessfully ? TaskStatus.RanToCompletion : TaskStatus.WaitingForActivation;

    private static Task ObserveOuterSuccess(Task task, AutomaticOuterSuccessRecorder outer)
    {
        outer.Enter();
        _ = task.ContinueWith(_ => outer.Succeed(), CancellationToken.None,
            TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return task;
    }

    private static Task<T> ObserveOuterSuccess<T>(Task<T> task, AutomaticOuterSuccessRecorder outer)
    {
        outer.Enter();
        _ = task.ContinueWith(_ => outer.Succeed(), CancellationToken.None,
            TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return task;
    }

    private static void InvokeWithOuterSuccess(Action invocation, AutomaticOuterSuccessRecorder outer)
    {
        outer.Enter();
        invocation();
        outer.Succeed();
    }

    private static void AssertAutomaticFailure(AutomaticOperationState state, IAutomaticOperationProbe service,
        IUnitOfWorkOperationManager manager, AutomaticOuterSuccessRecorder outer)
    {
        Assert.Equal(1, state.TargetCount);
        Assert.Equal(0, state.ResourceCommitCount);
        Assert.Equal(1, state.ResourceRollbackCount);
        Assert.Equal(1, state.ResourceDisposeCount);
        Assert.Equal(0, service.CommitAfterCount);
        Assert.Equal(1, outer.EntryCount);
        Assert.Equal(0, outer.SuccessCount);
        Assert.Null(manager.Current);
    }

    private sealed class AutomaticInvocation
    {
        public AutomaticInvocation(Task task, Func<TaskStatus>? valueTaskStatus = null)
        {
            Task = task;
            ValueTaskStatus = valueTaskStatus;
        }

        public Task Task { get; }
        public TaskStatus Status => Task.Status;
        public Func<TaskStatus>? ValueTaskStatus { get; }

        public void Observe(AutomaticOuterSuccessRecorder outer) => ObserveOuterSuccess(Task, outer);
    }

    private sealed class SourceBackedInvocation
    {
        private readonly Func<Task> _asTask;
        private readonly AutomaticOuterSuccessRecorder _outer;
        private Task? _task;

        public SourceBackedInvocation(Func<Task> asTask, Func<TaskStatus> status,
            AutomaticOuterSuccessRecorder outer)
        {
            _asTask = asTask;
            Status = status;
            _outer = outer;
        }

        public Func<TaskStatus> Status { get; }
        public Task AsTask() => _task ??= ObserveOuterSuccess(_asTask(), _outer);
    }

    private static void RecordOperationTarget(AutomaticOperationState state, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref state.TargetCount);
        state.TargetCancellationToken = cancellationToken;
        var current = state.OperationManager.Current as IUnitOfWorkExecutionScope
            ?? throw new InvalidOperationException("自动 owner 必须先于目标调用创建。");
        current.GetOrAddResource("automatic-operation", () => new CountingResource(state));
    }

    private static CapHeader CreateHeader() => new(new Dictionary<string, string?>
    {
        ["bing-trace-id"] = "cap-trace"
    });

    public enum AsyncReturnKind
    {
        Task,
        TaskOfT,
        ValueTask,
        ValueTaskOfT
    }

    public enum SourceBackedReturnKind
    {
        ValueTask,
        ValueTaskOfT
    }

    public interface IAutomaticOperationProbe
    {
        int CommitAfterCount { get; }

        [CapTrace]
        [UnitOfWork]
        void CancelSynchronously([FromCap] CapHeader header, CancellationToken cancellationToken);

        [CapTrace]
        [UnitOfWork]
        Task<int> SucceedAsync([FromCap] CapHeader header, CancellationToken cancellationToken);

        [CapTrace]
        [UnitOfWork]
        Task CancelTaskAsync([FromCap] CapHeader header, CancellationToken cancellationToken);

        [CapTrace]
        [UnitOfWork]
        Task<int> CancelTaskOfTAsync([FromCap] CapHeader header, CancellationToken cancellationToken);

        [CapTrace]
        [UnitOfWork]
        Task CancelOnTokenTaskAsync([FromCap] CapHeader header, CancellationToken cancellationToken);

        [CapTrace]
        [UnitOfWork]
        Task<int> CancelOnTokenTaskOfTAsync([FromCap] CapHeader header, CancellationToken cancellationToken);

        [CapTrace]
        [UnitOfWork]
        ValueTask CancelValueTaskAsync([FromCap] CapHeader header, CancellationToken cancellationToken);

        [CapTrace]
        [UnitOfWork]
        ValueTask<int> CancelValueTaskOfTAsync([FromCap] CapHeader header, CancellationToken cancellationToken);

        [UnitOfWork]
        [CapTrace]
        ValueTask SourceBackedValueTaskAsync([FromCap] CapHeader header, CancellationToken cancellationToken);

        [UnitOfWork]
        [CapTrace]
        ValueTask<int> SourceBackedValueTaskOfTAsync([FromCap] CapHeader header, CancellationToken cancellationToken);

        [CapTrace]
        [UnitOfWork]
        Task ExecuteNestedAsync([FromCap] CapHeader header, CancellationToken cancellationToken);

        [UnitOfWork]
        Task CatchInnerFailureAsync();
    }

    public interface IInnerAutomaticOperationProbe
    {
        [UnitOfWork]
        Task ThrowAsync();

        [UnitOfWork]
        Task SucceedAsync(CancellationToken cancellationToken);
    }

    public interface IExplicitAutomaticOperationProbe
    {
        int CommitAfterCount { get; }

        [UnitOfWork]
        Task<int> ExecuteAsync();
    }

    public sealed class AutomaticOperationProbe : IAutomaticOperationProbe, ICommitAfter
    {
        private readonly AutomaticOperationState _state;
        private readonly IInnerAutomaticOperationProbe _inner;
        private readonly IUnitOfWorkOperationManager _operationManager;
        private int _commitAfterCount;

        public AutomaticOperationProbe(AutomaticOperationState state, IInnerAutomaticOperationProbe inner,
            IUnitOfWorkOperationManager operationManager)
        {
            _state = state;
            _inner = inner;
            _operationManager = operationManager;
        }

        public int CommitAfterCount => Volatile.Read(ref _commitAfterCount);

        public void CancelSynchronously(CapHeader header, CancellationToken cancellationToken)
        {
            RecordTarget(cancellationToken);
            throw _state.ExpectedCancellation!;
        }

        public Task<int> SucceedAsync(CapHeader header, CancellationToken cancellationToken)
        {
            RecordTarget(cancellationToken);
            _state.DefaultTimeout = _operationManager.Current!.Options.Timeout;
            return Task.FromResult(42);
        }

        public Task CancelTaskAsync(CapHeader header, CancellationToken cancellationToken)
        {
            RecordTarget(cancellationToken);
            var task = Task.FromException(_state.ExpectedCancellation!);
            _state.TargetAsyncStatus = task.Status;
            return task;
        }

        public Task<int> CancelTaskOfTAsync(CapHeader header, CancellationToken cancellationToken)
        {
            RecordTarget(cancellationToken);
            var task = Task.FromException<int>(_state.ExpectedCancellation!);
            _state.TargetAsyncStatus = task.Status;
            return task;
        }

        public Task CancelOnTokenTaskAsync(CapHeader header, CancellationToken cancellationToken)
        {
            RecordTarget(cancellationToken);
            return _state.WaitForTaskCancellation
                ? WaitForCancellationAsync(cancellationToken)
                : Task.FromCanceled(cancellationToken);
        }

        public Task<int> CancelOnTokenTaskOfTAsync(CapHeader header, CancellationToken cancellationToken)
        {
            RecordTarget(cancellationToken);
            return _state.WaitForTaskCancellation
                ? WaitForCancellationAsync<int>(cancellationToken)
                : Task.FromCanceled<int>(cancellationToken);
        }

        public ValueTask CancelValueTaskAsync(CapHeader header, CancellationToken cancellationToken)
        {
            RecordTarget(cancellationToken);
            var task = Task.FromException(_state.ExpectedCancellation!);
            _state.TargetAsyncStatus = task.Status;
            return new ValueTask(task);
        }

        public ValueTask<int> CancelValueTaskOfTAsync(CapHeader header, CancellationToken cancellationToken)
        {
            RecordTarget(cancellationToken);
            var task = Task.FromException<int>(_state.ExpectedCancellation!);
            _state.TargetAsyncStatus = task.Status;
            return new ValueTask<int>(task);
        }

        public ValueTask SourceBackedValueTaskAsync(CapHeader header, CancellationToken cancellationToken)
        {
            var source = PrepareSourceBackedValueTask(cancellationToken);
            return source.AsValueTask();
        }

        public ValueTask<int> SourceBackedValueTaskOfTAsync(CapHeader header, CancellationToken cancellationToken)
        {
            var source = PrepareSourceBackedValueTask(cancellationToken);
            return source.AsValueTaskOfT();
        }

        public async Task ExecuteNestedAsync(CapHeader header, CancellationToken cancellationToken)
        {
            RecordTarget(cancellationToken);
            await _inner.SucceedAsync(cancellationToken);
        }

        public async Task CatchInnerFailureAsync()
        {
            try
            {
                await _inner.ThrowAsync();
            }
            catch (InvalidOperationException)
            {
                _state.InnerFailureCaught = true;
            }
        }

        public void CommitAfter() => Interlocked.Increment(ref _commitAfterCount);

        private void RecordTarget(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _state.TargetCount);
            _state.TargetCancellationToken = cancellationToken;
            _state.TargetTraceId = TraceIdContext.Current?.TraceId;
            var current = _operationManager.Current as IUnitOfWorkExecutionScope
                ?? throw new InvalidOperationException("自动 owner 必须先于目标调用创建。");
            current.GetOrAddResource("automatic-operation", () => new CountingResource(_state));
            _state.SignalTargetEntered();
        }

        private static async Task WaitForCancellationAsync(CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var registration = cancellationToken.Register(() => cancelled.TrySetResult(true));
                if (!cancellationToken.IsCancellationRequested)
                    await cancelled.Task.ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
        }

        private static async Task<T> WaitForCancellationAsync<T>(CancellationToken cancellationToken)
        {
            await WaitForCancellationAsync(cancellationToken).ConfigureAwait(false);
            return default!;
        }

        private SingleConsumptionValueTaskSource PrepareSourceBackedValueTask(CancellationToken cancellationToken)
        {
            RecordTarget(cancellationToken);
            var source = new SingleConsumptionValueTaskSource();
            _state.LastValueTaskSource = source;
            if (cancellationToken.IsCancellationRequested)
                source.SetCanceled(cancellationToken);
            else if (_state.WaitForSourceCancellation)
                source.WaitForCancellation(cancellationToken);
            else
                source.SetResult(42);

            return source;
        }
    }

    public sealed class InnerAutomaticOperationProbe : IInnerAutomaticOperationProbe
    {
        private readonly AutomaticOperationState _state;

        public InnerAutomaticOperationProbe(AutomaticOperationState state) => _state = state;

        public Task ThrowAsync()
        {
            Interlocked.Increment(ref _state.TargetCount);
            var current = _state.OperationManager.Current
                ?? throw new InvalidOperationException("内层 participant 必须共享 owner。");
            current.GetOrAddResource("automatic-operation", () => new CountingResource(_state));
            throw new InvalidOperationException("inner failure");
        }

        public Task SucceedAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _state.TargetCount);
            _state.TargetCancellationToken = cancellationToken;
            var current = _state.OperationManager.Current
                ?? throw new InvalidOperationException("内层 participant 必须共享 owner。");
            current.GetOrAddResource("automatic-operation", () => new CountingResource(_state));
            return Task.CompletedTask;
        }
    }

    public class ClassAutomaticOperationProbe : ICommitAfter
    {
        private readonly AutomaticOperationState _state;
        private int _commitAfterCount;

        public ClassAutomaticOperationProbe(AutomaticOperationState state) => _state = state;

        public int CommitAfterCount => Volatile.Read(ref _commitAfterCount);

        [UnitOfWork]
        public virtual void ExecuteSynchronously() => RecordOperationTarget(_state, CancellationToken.None);

        public void CommitAfter() => Interlocked.Increment(ref _commitAfterCount);
    }

    public sealed class ExplicitAutomaticOperationProbe : IExplicitAutomaticOperationProbe, ICommitAfter
    {
        private readonly AutomaticOperationState _state;
        private int _commitAfterCount;

        public ExplicitAutomaticOperationProbe(AutomaticOperationState state) => _state = state;

        public int CommitAfterCount => Volatile.Read(ref _commitAfterCount);

        Task<int> IExplicitAutomaticOperationProbe.ExecuteAsync()
        {
            RecordOperationTarget(_state, CancellationToken.None);
            return Task.FromResult(42);
        }

        public void CommitAfter() => Interlocked.Increment(ref _commitAfterCount);
    }

    public sealed class AutomaticOperationState
    {
        private readonly ConcurrentQueue<string> _events = new();
        private IUnitOfWorkOperationManager? _operationManager;
        private readonly TaskCompletionSource _targetEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public OperationCanceledException? ExpectedCancellation { get; set; }
        public TaskStatus? TargetAsyncStatus { get; set; }
        public CancellationToken TargetCancellationToken { get; set; }
        public string? TargetTraceId { get; set; }
        public TimeSpan? DefaultTimeout { get; set; }
        public bool InnerFailureCaught { get; set; }
        public bool WaitForSourceCancellation { get; set; }
        public bool WaitForTaskCancellation { get; set; }
        public SingleConsumptionValueTaskSource? LastValueTaskSource { get; set; }
        public int TargetCount;
        public int ResourceCommitCount;
        public int ResourceRollbackCount;
        public int ResourceDisposeCount;

        public IUnitOfWorkOperationManager OperationManager => _operationManager
            ?? throw new InvalidOperationException("操作管理器尚未设置。");

        public Task TargetEntered => _targetEntered.Task;

        public void SetOperationManager(IUnitOfWorkOperationManager manager) => _operationManager = manager;

        public void SignalTargetEntered() => _targetEntered.TrySetResult();

        public IReadOnlyCollection<string> Events => _events.ToArray();
        public void Record(string value) => _events.Enqueue(value);
    }

    public sealed class AutomaticOuterSuccessRecorder
    {
        private readonly TaskCompletionSource _successObserved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _entryCount;
        private int _successCount;

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

    public sealed class CountingResource : IUnitOfWorkResource
    {
        private readonly AutomaticOperationState _state;

        public CountingResource(AutomaticOperationState state) => _state = state;
        public void Commit() => Interlocked.Increment(ref _state.ResourceCommitCount);
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commit();
            return Task.CompletedTask;
        }
        public void Rollback() => Interlocked.Increment(ref _state.ResourceRollbackCount);
        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Rollback();
            return Task.CompletedTask;
        }
        public void Dispose() => Interlocked.Increment(ref _state.ResourceDisposeCount);
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    public sealed class SingleConsumptionValueTaskSource : IValueTaskSource, IValueTaskSource<int>, IDisposable
    {
        private ManualResetValueTaskSourceCore<int> _core;
        private readonly TaskCompletionSource<bool> _started = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenRegistration _cancellationRegistration;
        private int _getResultCount;
        private int _completionState;

        public SingleConsumptionValueTaskSource()
        {
            _core.RunContinuationsAsynchronously = true;
        }

        public Task Started => _started.Task;

        public int GetResultCount => Volatile.Read(ref _getResultCount);

        public ValueTaskSourceStatus Status => _core.GetStatus(_core.Version);

        public OperationCanceledException? ExpectedCancellation { get; private set; }

        public void SetResult(int result)
        {
            if (Interlocked.CompareExchange(ref _completionState, 1, 0) != 0)
                return;

            _started.TrySetResult(true);
            _core.SetResult(result);
        }

        public void SetCanceled(CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref _completionState, 1, 0) != 0)
                return;

            ExpectedCancellation = new OperationCanceledException("Source-backed operation canceled.",
                cancellationToken);
            _started.TrySetResult(true);
            _core.SetException(ExpectedCancellation);
        }

        public void WaitForCancellation(CancellationToken cancellationToken)
        {
            _cancellationRegistration = cancellationToken.Register(() => SetCanceled(cancellationToken));
            _started.TrySetResult(true);
        }

        public ValueTask AsValueTask() => new(this, _core.Version);

        public ValueTask<int> AsValueTaskOfT() => new(this, _core.Version);

        void IValueTaskSource.GetResult(short token)
        {
            EnsureSingleConsumption();
            _ = _core.GetResult(token);
        }

        int IValueTaskSource<int>.GetResult(short token)
        {
            EnsureSingleConsumption();
            return _core.GetResult(token);
        }

        public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

        public void OnCompleted(Action<object?> continuation, object? state, short token,
            ValueTaskSourceOnCompletedFlags flags) => _core.OnCompleted(continuation, state, token, flags);

        public void Dispose() => _cancellationRegistration.Dispose();

        private void EnsureSingleConsumption()
        {
            if (Interlocked.Increment(ref _getResultCount) != 1)
                throw new InvalidOperationException("The source-backed ValueTask result was consumed more than once.");
        }
    }
}
