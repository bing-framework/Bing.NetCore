using System.Collections.Concurrent;
using System.Threading.Tasks.Sources;
using AspectCore.DynamicProxy;
using AspectCore.Extensions.Hosting;
using Bing.Application.Aspects;
using Bing.DependencyInjection;
using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bing.Aop.AspectCore;

/// <summary>
/// 验证中立工作单元标记的 AspectCore 适配行为。
/// </summary>
public sealed class UnitOfWorkAdapterTest
{
    /// <summary>
    /// 成功调用后提交一次并执行提交后回调。
    /// </summary>
    [Fact]
    public async Task Success_ShouldCommitThenRunCommitAfter()
    {
        using var host = CreateHost();
        await host.StartAsync();
        var service = host.Services.GetRequiredService<IUnitOfWorkService>();
        Assert.NotEqual(typeof(UnitOfWorkService), service.GetType());

        await service.ExecuteAsync();

        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        Assert.Equal(new[] { "Target", "Commit", "CommitAfter" }, manager.Events.ToArray());
        Assert.Equal(1, manager.CommitCount);
        Assert.Equal(1, service.CommitAfterCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 验证同步调用也会在目标完成后提交并执行提交后回调。
    /// </summary>
    [Fact]
    public async Task SyncSuccess_ShouldCommitThenRunCommitAfter()
    {
        using var host = CreateHost();
        await host.StartAsync();
        var service = host.Services.GetRequiredService<IUnitOfWorkService>();

        service.ExecuteSync();

        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        Assert.Equal(new[] { "Target", "Commit", "CommitAfter" }, manager.Events.ToArray());
        Assert.Equal(1, manager.CommitCount);
        Assert.Equal(1, service.CommitAfterCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 目标异常时不提交工作单元。
    /// </summary>
    [Fact]
    public async Task Failure_ShouldNotCommit()
    {
        using var host = CreateHost();
        await host.StartAsync();
        var service = host.Services.GetRequiredService<IUnitOfWorkService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ThrowAsync());

        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        Assert.Equal(0, manager.CommitCount);
        Assert.Equal(0, service.CommitAfterCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 默认 Aspect 范围下嵌套调用只提交一次。
    /// </summary>
    [Fact]
    public async Task Nested_ShouldCommitOnlyAtOuterBoundary()
    {
        using var host = CreateHost();
        await host.StartAsync();
        var service = host.Services.GetRequiredService<IUnitOfWorkService>();

        await service.ExecuteNestedAsync();

        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        Assert.Equal(1, manager.CommitCount);
        Assert.Equal(1, service.CommitAfterCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 验证调用取消时不会提交工作单元。
    /// </summary>
    [Fact]
    public async Task Cancellation_ShouldNotCommit()
    {
        using var host = CreateHost();
        await host.StartAsync();
        var service = host.Services.GetRequiredService<IUnitOfWorkService>();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CancelAsync().WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal(default, exception.CancellationToken);
        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        Assert.Equal(new[] { "Target" }, manager.Events.ToArray());
        Assert.Equal(0, manager.CommitCount);
        Assert.Equal(0, service.CommitAfterCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 调用前取消的 Task 应保留取消令牌，且不提交或执行回调。
    /// </summary>
    [Fact]
    public Task PreCancelled_Task_ShouldPreserveTokenAndSkipCommit() =>
        AssertCancellationAsync((service, token) => service.CancelTaskAsync(token), cancelBeforeInvocation: true);

    /// <summary>
    /// 调用前取消的 Task<TResult> 应保留取消令牌，且不提交或执行回调。
    /// </summary>
    [Fact]
    public Task PreCancelled_TaskOfT_ShouldPreserveTokenAndSkipCommit() =>
        AssertCancellationAsync((service, token) => service.CancelTaskOfTAsync(token), cancelBeforeInvocation: true);

    /// <summary>
    /// 调用前取消的 ValueTask 应保留取消令牌，且不提交或执行回调。
    /// </summary>
    [Fact]
    public Task PreCancelled_ValueTask_ShouldPreserveTokenAndSkipCommit() =>
        AssertCancellationAsync((service, token) => service.CancelValueTaskAsync(token).AsTask(),
            cancelBeforeInvocation: true);

    /// <summary>
    /// 调用前取消的 ValueTask<TResult> 应保留取消令牌，且不提交或执行回调。
    /// </summary>
    [Fact]
    public Task PreCancelled_ValueTaskOfT_ShouldPreserveTokenAndSkipCommit() =>
        AssertCancellationAsync((service, token) => service.CancelValueTaskOfTAsync(token).AsTask(),
            cancelBeforeInvocation: true);

    /// <summary>
    /// 已取消的无 token Task 应按目标结果传播取消且不提交。
    /// </summary>
    [Fact]
    public Task NoTokenCanceled_Task_ShouldPropagateCancellation() =>
        AssertNoTokenCancellationAsync(service => service.CancelNoTokenTaskAsync());

    /// <summary>
    /// 已取消的无 token Task<TResult> 应按目标结果传播取消且不提交。
    /// </summary>
    [Fact]
    public Task NoTokenCanceled_TaskOfT_ShouldPropagateCancellation() =>
        AssertNoTokenCancellationAsync(service => service.CancelNoTokenTaskOfTAsync());

    /// <summary>
    /// 已取消的无 token ValueTask 应按目标结果传播取消且不提交。
    /// </summary>
    [Fact]
    public Task NoTokenCanceled_ValueTask_ShouldPropagateCancellation() =>
        AssertNoTokenCancellationAsync(service => service.CancelNoTokenValueTaskAsync().AsTask());

    /// <summary>
    /// 已取消的无 token ValueTask<TResult> 应按目标结果传播取消且不提交。
    /// </summary>
    [Fact]
    public Task NoTokenCanceled_ValueTaskOfT_ShouldPropagateCancellation() =>
        AssertNoTokenCancellationAsync(service => service.CancelNoTokenValueTaskOfTAsync().AsTask());

    /// <summary>
    /// 目标内部取消应保留其令牌，且不提交或执行回调。
    /// </summary>
    [Fact]
    public async Task CancelInsideTarget_ShouldPropagateCancellation()
    {
        using var host = CreateHost();
        await host.StartAsync();
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUnitOfWorkService>();
        var signal = scope.ServiceProvider.GetRequiredService<CancellationSignal>();
        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        using var cancellation = new CancellationTokenSource();
        signal.TargetCancellationSource = cancellation;

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CancelInsideTargetAsync().WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(new[] { "Target" }, manager.Events.ToArray());
        Assert.Equal(0, manager.CommitCount);
        Assert.Equal(0, service.CommitAfterCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 目标使用 IValueTaskSource 返回取消时，管线只消费一次结果。
    /// </summary>
    [Fact]
    public async Task SourceBackedValueTaskCancellation_ShouldConsumeResultOnce()
    {
        using var host = CreateHost();
        await host.StartAsync();
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUnitOfWorkService>();
        var signal = scope.ServiceProvider.GetRequiredService<CancellationSignal>();
        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        signal.NoTokenCancellationToken = cancellation.Token;

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CancelSourceBackedValueTaskAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Same(signal.LastValueTaskSource!.ExpectedCancellation, exception);
        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(1, signal.LastValueTaskSource.GetResultCount);
        Assert.Equal(new[] { "Target" }, manager.Events.ToArray());
        Assert.Equal(0, manager.CommitCount);
        Assert.Equal(0, service.CommitAfterCount);
        await host.StopAsync();
    }

    /// <summary>
    /// IValueTaskSource 支持的非泛型 ValueTask 成功时只消费一次并按序提交。
    /// </summary>
    [Fact]
    public Task SourceBackedValueTask_WhenCompleted_ShouldConsumeOnceAndCommitInOrder() =>
        AssertSourceBackedValueTaskSuccessAsync(returnsResult: false, completeBeforeInvocation: true);

    /// <summary>
    /// IValueTaskSource 支持的非泛型 ValueTask 信号完成时只消费一次并按序提交。
    /// </summary>
    [Fact]
    public Task SourceBackedValueTask_WhenSignalled_ShouldConsumeOnceAndCommitInOrder() =>
        AssertSourceBackedValueTaskSuccessAsync(returnsResult: false, completeBeforeInvocation: false);

    /// <summary>
    /// IValueTaskSource 支持的泛型 ValueTask 成功时保留精确结果且只消费一次。
    /// </summary>
    [Fact]
    public Task SourceBackedValueTaskOfT_WhenCompleted_ShouldConsumeOnceAndCommitInOrder() =>
        AssertSourceBackedValueTaskSuccessAsync(returnsResult: true, completeBeforeInvocation: true);

    /// <summary>
    /// IValueTaskSource 支持的泛型 ValueTask 信号完成时保留精确结果且只消费一次。
    /// </summary>
    [Fact]
    public Task SourceBackedValueTaskOfT_WhenSignalled_ShouldConsumeOnceAndCommitInOrder() =>
        AssertSourceBackedValueTaskSuccessAsync(returnsResult: true, completeBeforeInvocation: false);

    /// <summary>
    /// 默认嵌套 scope 下，内层 source-backed ValueTask 仍只消费一次并由外层提交。
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task NestedSourceBackedValueTask_ShouldConsumeOnceAndCommitAtOuterBoundary(
        bool returnsResult, bool completeBeforeInvocation)
    {
        using var host = CreateHost();
        await host.StartAsync();
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUnitOfWorkService>();
        var signal = scope.ServiceProvider.GetRequiredService<CancellationSignal>();
        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        signal.PrepareValueTaskSource(completeBeforeInvocation);

        if (returnsResult)
        {
            var operation = service.NestedSourceBackedValueTaskOfTAsync();
            if (!completeBeforeInvocation)
                AssertNestedSourcePendingBeforeSignal(signal, manager, service, operation.IsCompleted);

            Assert.Equal(123, await operation.AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        }
        else
        {
            var operation = service.NestedSourceBackedValueTaskAsync();
            if (!completeBeforeInvocation)
                AssertNestedSourcePendingBeforeSignal(signal, manager, service, operation.IsCompleted);

            await operation.AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(1, signal.LastValueTaskSource!.GetResultCount);
        Assert.Equal(new[] { "NestedOuterTarget", "NestedInnerTarget", "Commit", "CommitAfter" },
            manager.Events.ToArray());
        Assert.Equal(1, manager.CommitCount);
        Assert.Equal(1, service.CommitAfterCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 显式接口 ValueTask 实现沿用接口分派并完成一次工作单元提交。
    /// </summary>
    [Fact]
    public async Task ExplicitInterfaceValueTask_ShouldCommitOnce()
    {
        using var host = CreateHost();
        await host.StartAsync();
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUnitOfWorkService>();
        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();

        Assert.Equal(123, await service.ExplicitInterfaceValueTaskAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal(new[] { "Target", "Commit", "CommitAfter" }, manager.Events.ToArray());
        Assert.Equal(1, manager.CommitCount);
        Assert.Equal(1, service.CommitAfterCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 虚方法类代理在默认或 None scope 下只运行一次拦截器链并提交一次。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VirtualClassProxy_ShouldRunChainOnceAndCommitOnce(bool scopeNone)
    {
        using var host = CreateHost();
        await host.StartAsync();
        var service = host.Services.GetRequiredService<VirtualUnitOfWorkService>();
        var trace = host.Services.GetRequiredService<VirtualClassProxyTrace>();
        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();

        var operation = scopeNone ? service.None() : service.Default();
        Assert.Equal(123, await operation.AsTask().WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal(1, trace.BeforeCount);
        Assert.Equal(1, trace.AfterCount);
        Assert.Equal(1, trace.TargetCount);
        Assert.Equal(1, trace.CommitAfterCount);
        Assert.Equal(new[] { "Target", "Commit", "CommitAfter" }, manager.Events.ToArray());
        Assert.Equal(1, manager.CommitCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 虚方法类代理目标取消时保留原令牌且不提交，两个 scope 均只执行一条拦截链。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VirtualClassProxy_Cancellation_ShouldPreserveToken(bool scopeNone)
    {
        using var host = CreateHost();
        await host.StartAsync();
        var service = host.Services.GetRequiredService<VirtualUnitOfWorkService>();
        var trace = host.Services.GetRequiredService<VirtualClassProxyTrace>();
        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var operation = scopeNone
            ? service.CancelNone(cancellation.Token)
            : service.Cancel(cancellation.Token);
        Assert.Equal(1, trace.BeforeCount);
        Assert.Equal(1, trace.AfterCount);
        Assert.Equal(1, trace.TargetCount);
        Assert.Equal(new[] { "Target" }, manager.Events.ToArray());
        Assert.False(operation.IsCompletedSuccessfully);
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => operation.AsTask().WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(1, trace.BeforeCount);
        Assert.Equal(1, trace.AfterCount);
        Assert.Equal(1, trace.TargetCount);
        Assert.Equal(0, trace.CommitAfterCount);
        Assert.Equal(new[] { "Target" }, manager.Events.ToArray());
        Assert.Equal(0, manager.CommitCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 虚方法类代理目标业务异常保留原异常实例且不提交，两个 scope 均只执行一条拦截链。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VirtualClassProxy_BusinessFailure_ShouldPreserveException(bool scopeNone)
    {
        using var host = CreateHost();
        await host.StartAsync();
        var service = host.Services.GetRequiredService<VirtualUnitOfWorkService>();
        var trace = host.Services.GetRequiredService<VirtualClassProxyTrace>();
        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();

        var operation = scopeNone ? service.FailNone() : service.Fail();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => operation.AsTask().WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Same(trace.BusinessFailure, exception);
        Assert.Equal(1, trace.BeforeCount);
        Assert.Equal(1, trace.AfterCount);
        Assert.Equal(1, trace.TargetCount);
        Assert.Equal(0, trace.CommitAfterCount);
        Assert.Equal(new[] { "Target" }, manager.Events.ToArray());
        Assert.Equal(0, manager.CommitCount);
        await host.StopAsync();
    }

    /// <summary>
    /// IValueTaskSource 支持的泛型 ValueTask 取消时保留原异常及令牌，且不提交。
    /// </summary>
    [Fact]
    public async Task SourceBackedValueTaskOfTCancellation_ShouldConsumeResultOnce()
    {
        using var host = CreateHost();
        await host.StartAsync();
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUnitOfWorkService>();
        var signal = scope.ServiceProvider.GetRequiredService<CancellationSignal>();
        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        signal.NoTokenCancellationToken = cancellation.Token;

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CancelSourceBackedValueTaskOfTAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Same(signal.LastValueTaskSource!.ExpectedCancellation, exception);
        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(1, signal.LastValueTaskSource.GetResultCount);
        Assert.Equal(new[] { "Target" }, manager.Events.ToArray());
        Assert.Equal(0, manager.CommitCount);
        Assert.Equal(0, service.CommitAfterCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 已取消参数被目标忽略时仍执行目标、提交和提交后回调。
    /// </summary>
    [Fact]
    public async Task IgnoreCanceledToken_ShouldCompleteAndCommit()
    {
        using var host = CreateHost();
        await host.StartAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = host.Services.GetRequiredService<IUnitOfWorkService>();

        await service.IgnoreCanceledTokenAsync(cancellation.Token);

        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        Assert.Equal(new[] { "Target", "Commit", "CommitAfter" }, manager.Events.ToArray());
        Assert.Equal(1, manager.CommitCount);
        Assert.Equal(1, service.CommitAfterCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 已取消参数不应阻止目标抛出原有业务异常。
    /// </summary>
    [Fact]
    public async Task PreCanceledToken_TargetFailure_ShouldPreserveBusinessException()
    {
        using var host = CreateHost();
        await host.StartAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUnitOfWorkService>();
        var signal = scope.ServiceProvider.GetRequiredService<CancellationSignal>();
        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        signal.TargetFailure = new InvalidOperationException("target failure with canceled token");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ThrowWithCanceledTokenAsync(cancellation.Token));

        Assert.Same(signal.TargetFailure, exception);
        Assert.Equal(new[] { "Target" }, manager.Events.ToArray());
        Assert.Equal(0, manager.CommitCount);
        Assert.Equal(0, service.CommitAfterCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 已完成的取消提交只尝试一次并向调用者传播取消。
    /// </summary>
    [Fact]
    public Task CommitAlreadyCanceled_ShouldPropagateCancellationOnce() =>
        AssertCommitCancellationAsync(waitForCancellation: false);

    /// <summary>
    /// 信号控制的异步提交取消只尝试一次且不执行提交后回调。
    /// </summary>
    [Fact]
    public Task CommitSignalledCancellation_ShouldPropagateCancellationOnce() =>
        AssertCommitCancellationAsync(waitForCancellation: true);

    /// <summary>
    /// 信号控制的 Task 取消应保留取消令牌，且不提交或执行回调。
    /// </summary>
    [Fact]
    public Task SignalledCancellation_Task_ShouldPreserveTokenAndSkipCommit() =>
        AssertCancellationAsync((service, token) => service.CancelTaskAsync(token), cancelBeforeInvocation: false);

    /// <summary>
    /// 信号控制的 Task<TResult> 取消应保留取消令牌，且不提交或执行回调。
    /// </summary>
    [Fact]
    public Task SignalledCancellation_TaskOfT_ShouldPreserveTokenAndSkipCommit() =>
        AssertCancellationAsync((service, token) => service.CancelTaskOfTAsync(token), cancelBeforeInvocation: false);

    /// <summary>
    /// 信号控制的 ValueTask 取消应保留取消令牌，且不提交或执行回调。
    /// </summary>
    [Fact]
    public Task SignalledCancellation_ValueTask_ShouldPreserveTokenAndSkipCommit() =>
        AssertCancellationAsync((service, token) => service.CancelValueTaskAsync(token).AsTask(),
            cancelBeforeInvocation: false);

    /// <summary>
    /// 信号控制的 ValueTask<TResult> 取消应保留取消令牌，且不提交或执行回调。
    /// </summary>
    [Fact]
    public Task SignalledCancellation_ValueTaskOfT_ShouldPreserveTokenAndSkipCommit() =>
        AssertCancellationAsync((service, token) => service.CancelValueTaskOfTAsync(token).AsTask(),
            cancelBeforeInvocation: false);

    /// <summary>
    /// 验证提交失败时不会执行提交后回调，并保留提交异常。
    /// </summary>
    [Fact]
    public async Task CommitFailure_ShouldNotRunCommitAfter()
    {
        using var host = CreateHost();
        await host.StartAsync();
        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        manager.ThrowOnCommit = true;
        var service = host.Services.GetRequiredService<IUnitOfWorkService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync());

        Assert.Equal(1, manager.CommitCount);
        Assert.Equal(0, service.CommitAfterCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 活动业务 root 下将提交后回调延迟到资源提交确认之后。
    /// </summary>
    [Fact]
    public async Task ActiveRoot_ShouldDeferCommitAfterUntilCompleted()
    {
        using var host = CreateHost();
        await host.StartAsync();
        await using var scope = host.Services.CreateAsyncScope();
        await using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        var service = scope.ServiceProvider.GetRequiredService<IUnitOfWorkService>();

        await service.ExecuteAsync();

        Assert.Equal(1, scope.ServiceProvider.GetRequiredService<RecordingUnitOfWorkManager>().CommitCount);
        Assert.Equal(0, service.CommitAfterCount);
        await uow.CompleteAsync();

        Assert.Equal(1, service.CommitAfterCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 回滚活动业务 root 时不执行提交后回调。
    /// </summary>
    [Fact]
    public async Task RolledBackRoot_ShouldSuppressCommitAfter()
    {
        using var host = CreateHost();
        await host.StartAsync();
        await using var scope = host.Services.CreateAsyncScope();
        await using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        var service = scope.ServiceProvider.GetRequiredService<IUnitOfWorkService>();

        await service.ExecuteAsync();
        Assert.Equal(0, service.CommitAfterCount);
        await uow.RollbackAsync();

        Assert.Equal(0, service.CommitAfterCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 验证 None 范围的嵌套调用分别提交。
    /// </summary>
    [Fact]
    public async Task NoneScope_ShouldCommitEachInvocation()
    {
        using var host = CreateHost();
        await host.StartAsync();
        var service = host.Services.GetRequiredService<IUnitOfWorkService>();

        await service.ExecuteNoneNestedAsync();

        Assert.Equal(2, host.Services.GetRequiredService<RecordingUnitOfWorkManager>().CommitCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 验证 Nested 范围会保留嵌套工作单元边界。
    /// </summary>
    [Fact]
    public async Task NestedScope_ShouldKeepSingleCommitBoundary()
    {
        using var host = CreateHost();
        await host.StartAsync();
        var service = host.Services.GetRequiredService<IUnitOfWorkService>();

        await service.ExecuteNestedScopeAsync();

        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        Assert.Equal(1, manager.CommitCount);
        Assert.Equal(2, manager.Events.Count(x => x == "Target-Nested"));
        await host.StopAsync();
    }

    /// <summary>
    /// 验证工作单元适配器重复注册不会追加服务描述。
    /// </summary>
    [Fact]
    public void EnableBingUnitOfWork_WhenCalledRepeatedly_ShouldRegisterOnce()
    {
        var services = new ServiceCollection();

        services.EnableBingUnitOfWork();
        services.EnableBingUnitOfWork();

        Assert.Single(services, x => x.ServiceType.FullName ==
            "AspectCore.DynamicProxy.IAdditionalInterceptorSelector");
        Assert.Single(services, x => x.ServiceType.FullName ==
            "AspectCore.DependencyInjection.IServiceResolveCallback");
    }

    /// <summary>
    /// 验证不同作用域的并发调用不共享工作单元提交状态。
    /// </summary>
    [Fact]
    public async Task ConcurrentScopes_ShouldCommitIndependently()
    {
        using var host = CreateHost();
        await host.StartAsync();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            using var scope = host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IUnitOfWorkService>().ExecuteConcurrentAsync();
        }));

        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        Assert.Equal(8, manager.CommitCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 验证接口类型标记可以选择工作单元拦截器。
    /// </summary>
    [Fact]
    public async Task InterfaceTypeMarker_ShouldCommitOnce()
    {
        using var host = CreateHost();
        var service = host.Services.GetRequiredService<IInterfaceTypeMarkedService>();

        await service.ExecuteAsync();

        Assert.Equal(1, host.Services.GetRequiredService<RecordingUnitOfWorkManager>().CommitCount);
    }

    /// <summary>
    /// 验证实现类型标记可以通过接口代理选择工作单元拦截器。
    /// </summary>
    [Fact]
    public async Task ImplementationTypeMarker_ShouldCommitOnce()
    {
        using var host = CreateHost();
        var service = host.Services.GetRequiredService<IImplementationTypeMarkedService>();

        await service.ExecuteAsync();

        Assert.Equal(1, host.Services.GetRequiredService<RecordingUnitOfWorkManager>().CommitCount);
    }

    /// <summary>
    /// 验证接口和实现方法同时标记时不会重复提交。
    /// </summary>
    [Fact]
    public async Task InterfaceAndImplementationMethodMarkers_ShouldCommitOnce()
    {
        using var host = CreateHost();
        var service = host.Services.GetRequiredService<IDuplicateMarkerService>();

        await service.ExecuteAsync();

        Assert.Equal(1, host.Services.GetRequiredService<RecordingUnitOfWorkManager>().CommitCount);
    }

    /// <summary>
    /// 构建启用 AspectCore 与工作单元适配器的测试主机。
    /// </summary>
    /// <returns>测试主机。</returns>
    private static IHost CreateHost()
    {
        return new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.AddSingleton<RecordingUnitOfWorkManager>();
                services.AddScoped<IUnitOfWorkManager>(provider =>
                    provider.GetRequiredService<RecordingUnitOfWorkManager>());
                services.AddScoped<CancellationSignal>();
                services.AddSingleton<VirtualClassProxyTrace>();
                services.AddUnitOfWorkScopes();
                services.AddScoped<IUnitOfWorkService, UnitOfWorkService>();
                services.AddScoped<VirtualUnitOfWorkService>();
                services.AddScoped<IInterfaceTypeMarkedService, InterfaceTypeMarkedService>();
                services.AddScoped<IImplementationTypeMarkedService, ImplementationTypeMarkedService>();
                services.AddScoped<IDuplicateMarkerService, DuplicateMarkerService>();
                services.EnableAop();
                services.EnableBingUnitOfWork();
            })
            .Build();
    }

    /// <summary>
    /// 通过预取消或启动信号控制取消，并验证拦截器保留取消状态。
    /// </summary>
    /// <param name="invoke">被拦截的异步调用。</param>
    /// <param name="cancelBeforeInvocation">是否在调用前取消令牌。</param>
    private static async Task AssertCancellationAsync(
        Func<IUnitOfWorkService, CancellationToken, Task> invoke, bool cancelBeforeInvocation)
    {
        using var host = CreateHost();
        await host.StartAsync();
        using (var scope = host.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IUnitOfWorkService>();
            var signal = scope.ServiceProvider.GetRequiredService<CancellationSignal>();
            var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
            using var cancellation = new CancellationTokenSource();
            if (cancelBeforeInvocation)
                cancellation.Cancel();

            var operation = invoke(service, cancellation.Token);
            if (!cancelBeforeInvocation)
            {
                await signal.Started.WaitAsync(TimeSpan.FromSeconds(5));
                cancellation.Cancel();
            }

            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await operation.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(cancellation.Token, exception.CancellationToken);
            Assert.Equal(new[] { "Target" }, manager.Events.ToArray());
            Assert.Equal(0, manager.CommitCount);
            Assert.Equal(0, service.CommitAfterCount);
        }

        await host.StopAsync();
    }

    /// <summary>
    /// 验证无 token 的目标返回已取消异步结果时保留其令牌且不提交。
    /// </summary>
    private static async Task AssertNoTokenCancellationAsync(Func<IUnitOfWorkService, Task> invoke)
    {
        using var host = CreateHost();
        await host.StartAsync();
        using (var scope = host.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IUnitOfWorkService>();
            var signal = scope.ServiceProvider.GetRequiredService<CancellationSignal>();
            var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            signal.NoTokenCancellationToken = cancellation.Token;

            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await invoke(service).WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.Equal(cancellation.Token, exception.CancellationToken);
            Assert.Equal(new[] { "Target" }, manager.Events.ToArray());
            Assert.Equal(0, manager.CommitCount);
            Assert.Equal(0, service.CommitAfterCount);
        }

        await host.StopAsync();
    }

    /// <summary>
    /// 验证工作单元提交同步或异步取消时只尝试一次且不执行回调。
    /// </summary>
    private static async Task AssertCommitCancellationAsync(bool waitForCancellation)
    {
        using var host = CreateHost();
        await host.StartAsync();
        using var cancellation = new CancellationTokenSource();
        if (!waitForCancellation)
            cancellation.Cancel();

        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        manager.CancelCommitWith(cancellation.Token, waitForCancellation);
        var service = host.Services.GetRequiredService<IUnitOfWorkService>();
        var operation = service.ExecuteAsync();
        if (waitForCancellation)
        {
            await manager.CommitStarted.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
        }

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await operation.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(new[] { "Target", "Commit" }, manager.Events.ToArray());
        Assert.Equal(1, manager.CommitCount);
        Assert.Equal(0, service.CommitAfterCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 验证 source-backed ValueTask 完成后目标结果只消费一次，且提交与回调顺序正确。
    /// </summary>
    private static async Task AssertSourceBackedValueTaskSuccessAsync(bool returnsResult,
        bool completeBeforeInvocation)
    {
        using var host = CreateHost();
        await host.StartAsync();
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUnitOfWorkService>();
        var signal = scope.ServiceProvider.GetRequiredService<CancellationSignal>();
        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        signal.PrepareValueTaskSource(completeBeforeInvocation);

        if (returnsResult)
        {
            var operation = service.SourceBackedValueTaskOfTAsync();
            if (!completeBeforeInvocation)
                AssertSourcePendingBeforeSignal(signal, manager, service, operation.IsCompleted);

            var result = await operation.AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(123, result);
        }
        else
        {
            var operation = service.SourceBackedValueTaskAsync();
            if (!completeBeforeInvocation)
                AssertSourcePendingBeforeSignal(signal, manager, service, operation.IsCompleted);

            await operation.AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(1, signal.LastValueTaskSource!.GetResultCount);
        Assert.Equal(new[] { "Target", "Commit", "CommitAfter" }, manager.Events.ToArray());
        Assert.Equal(1, manager.CommitCount);
        Assert.Equal(1, service.CommitAfterCount);
        await host.StopAsync();
    }

    private static void AssertSourcePendingBeforeSignal(CancellationSignal signal,
        RecordingUnitOfWorkManager manager, IUnitOfWorkService service, bool operationCompleted)
    {
        Assert.False(operationCompleted);
        Assert.Equal(0, signal.LastValueTaskSource!.GetResultCount);
        Assert.Equal(new[] { "Target" }, manager.Events.ToArray());
        Assert.Equal(0, manager.CommitCount);
        Assert.Equal(0, service.CommitAfterCount);
        signal.CompleteValueTaskSource();
    }

    private static void AssertNestedSourcePendingBeforeSignal(CancellationSignal signal,
        RecordingUnitOfWorkManager manager, IUnitOfWorkService service, bool operationCompleted)
    {
        Assert.False(operationCompleted);
        Assert.Equal(0, signal.LastValueTaskSource!.GetResultCount);
        Assert.Equal(new[] { "NestedOuterTarget", "NestedInnerTarget" }, manager.Events.ToArray());
        Assert.Equal(0, manager.CommitCount);
        Assert.Equal(0, service.CommitAfterCount);
        signal.CompleteValueTaskSource();
    }

    /// <summary>
    /// 为工作单元拦截测试记录目标调用及提交后回调。
    /// </summary>
    public sealed class UnitOfWorkService : IUnitOfWorkService, ICommitAfter
    {
        /// <summary>
        /// 解析测试服务所需依赖的服务提供程序。
        /// </summary>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// 记录工作单元提交行为的管理器。
        /// </summary>
        private readonly RecordingUnitOfWorkManager _manager;

        /// <summary>
        /// 为取消回归测试发出目标已开始执行的信号。
        /// </summary>
        private readonly CancellationSignal _cancellationSignal;

        /// <summary>
        /// 初始化一个 <see cref="UnitOfWorkService" /> 类型的实例。
        /// </summary>
        /// <param name="serviceProvider">测试服务提供程序。</param>
        /// <param name="manager">工作单元事件记录器。</param>
        public UnitOfWorkService(IServiceProvider serviceProvider, RecordingUnitOfWorkManager manager,
            CancellationSignal cancellationSignal)
        {
            _serviceProvider = serviceProvider;
            _manager = manager;
            _cancellationSignal = cancellationSignal;
        }

        /// <summary>
        /// 记录提交后回调的执行次数。
        /// </summary>
        private int _commitAfterCount;

        /// <summary>
        /// 获取提交后回调的执行次数。
        /// </summary>
        public int CommitAfterCount => Volatile.Read(ref _commitAfterCount);

        /// <inheritdoc />
        [UnitOfWork]
        public Task ExecuteAsync()
        {
            _manager.Record("Target");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        [UnitOfWork]
        public void ExecuteSync() => _manager.Record("Target");

        /// <inheritdoc />
        [UnitOfWork]
        public Task ThrowAsync()
        {
            _manager.Record("Target");
            return Task.FromException(new InvalidOperationException("target failure"));
        }

        /// <inheritdoc />
        [UnitOfWork]
        public async Task CancelAsync()
        {
            _manager.Record("Target");
            await Task.Yield();
            throw new OperationCanceledException();
        }

        /// <inheritdoc />
        [UnitOfWork]
        public Task CancelNoTokenTaskAsync()
        {
            _manager.Record("Target");
            return Task.FromCanceled(_cancellationSignal.NoTokenCancellationToken);
        }

        /// <inheritdoc />
        [UnitOfWork]
        public Task<int> CancelNoTokenTaskOfTAsync()
        {
            _manager.Record("Target");
            return Task.FromCanceled<int>(_cancellationSignal.NoTokenCancellationToken);
        }

        /// <inheritdoc />
        [UnitOfWork]
        public ValueTask CancelNoTokenValueTaskAsync()
        {
            _manager.Record("Target");
            return new ValueTask(Task.FromCanceled(_cancellationSignal.NoTokenCancellationToken));
        }

        /// <inheritdoc />
        [UnitOfWork]
        public ValueTask<int> CancelNoTokenValueTaskOfTAsync()
        {
            _manager.Record("Target");
            return new ValueTask<int>(Task.FromCanceled<int>(_cancellationSignal.NoTokenCancellationToken));
        }

        /// <inheritdoc />
        [UnitOfWork]
        public ValueTask CancelSourceBackedValueTaskAsync()
        {
            _manager.Record("Target");
            return _cancellationSignal.CreateCanceledValueTask();
        }

        /// <inheritdoc />
        [UnitOfWork]
        public ValueTask<int> CancelSourceBackedValueTaskOfTAsync()
        {
            _manager.Record("Target");
            return _cancellationSignal.CreateCanceledValueTaskOfT();
        }

        /// <inheritdoc />
        [UnitOfWork]
        public ValueTask SourceBackedValueTaskAsync()
        {
            _manager.Record("Target");
            return _cancellationSignal.CreateValueTaskFromSource();
        }

        /// <inheritdoc />
        [UnitOfWork]
        public ValueTask<int> SourceBackedValueTaskOfTAsync()
        {
            _manager.Record("Target");
            return _cancellationSignal.CreateValueTaskOfTFromSource();
        }

        ValueTask<int> IUnitOfWorkService.ExplicitInterfaceValueTaskAsync()
        {
            _manager.Record("Target");
            return new ValueTask<int>(123);
        }

        /// <inheritdoc />
        [UnitOfWork]
        public async ValueTask NestedSourceBackedValueTaskAsync()
        {
            _manager.Record("NestedOuterTarget");
            await _serviceProvider.GetRequiredService<IUnitOfWorkService>()
                .NestedSourceBackedValueTaskInnerAsync();
        }

        /// <inheritdoc />
        [UnitOfWork]
        public async ValueTask<int> NestedSourceBackedValueTaskOfTAsync()
        {
            _manager.Record("NestedOuterTarget");
            return await _serviceProvider.GetRequiredService<IUnitOfWorkService>()
                .NestedSourceBackedValueTaskOfTInnerAsync();
        }

        /// <inheritdoc />
        [UnitOfWork]
        public ValueTask NestedSourceBackedValueTaskInnerAsync()
        {
            _manager.Record("NestedInnerTarget");
            return _cancellationSignal.CreateValueTaskFromSource();
        }

        /// <inheritdoc />
        [UnitOfWork]
        public ValueTask<int> NestedSourceBackedValueTaskOfTInnerAsync()
        {
            _manager.Record("NestedInnerTarget");
            return _cancellationSignal.CreateValueTaskOfTFromSource();
        }

        /// <inheritdoc />
        [UnitOfWork]
        public Task CancelInsideTargetAsync()
        {
            _manager.Record("Target");
            var cancellation = _cancellationSignal.TargetCancellationSource!;
            cancellation.Cancel();
            return Task.FromCanceled(cancellation.Token);
        }

        /// <inheritdoc />
        [UnitOfWork]
        public Task IgnoreCanceledTokenAsync(CancellationToken cancellationToken)
        {
            _manager.Record("Target");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        [UnitOfWork]
        public Task ThrowWithCanceledTokenAsync(CancellationToken cancellationToken)
        {
            _manager.Record("Target");
            return Task.FromException(_cancellationSignal.TargetFailure!);
        }

        /// <inheritdoc />
        [UnitOfWork]
        public Task CancelTaskAsync(CancellationToken cancellationToken)
        {
            _manager.Record("Target");
            return cancellationToken.IsCancellationRequested
                ? Task.FromCanceled(cancellationToken)
                : _cancellationSignal.WaitUntilCanceledAsync(cancellationToken);
        }

        /// <inheritdoc />
        [UnitOfWork]
        public Task<int> CancelTaskOfTAsync(CancellationToken cancellationToken)
        {
            _manager.Record("Target");
            return cancellationToken.IsCancellationRequested
                ? Task.FromCanceled<int>(cancellationToken)
                : WaitForCancellationAsync<int>(cancellationToken);
        }

        /// <inheritdoc />
        [UnitOfWork]
        public ValueTask CancelValueTaskAsync(CancellationToken cancellationToken)
        {
            _manager.Record("Target");
            var task = cancellationToken.IsCancellationRequested
                ? Task.FromCanceled(cancellationToken)
                : _cancellationSignal.WaitUntilCanceledAsync(cancellationToken);
            return new ValueTask(task);
        }

        /// <inheritdoc />
        [UnitOfWork]
        public ValueTask<int> CancelValueTaskOfTAsync(CancellationToken cancellationToken)
        {
            _manager.Record("Target");
            var task = cancellationToken.IsCancellationRequested
                ? Task.FromCanceled<int>(cancellationToken)
                : WaitForCancellationAsync<int>(cancellationToken);
            return new ValueTask<int>(task);
        }

        /// <summary>
        /// 等待测试取消令牌并保持泛型异步返回类型。
        /// </summary>
        private async Task<TResult> WaitForCancellationAsync<TResult>(CancellationToken cancellationToken)
        {
            await _cancellationSignal.WaitUntilCanceledAsync(cancellationToken);
            return default;
        }

        /// <inheritdoc />
        [UnitOfWork]
        public async Task ExecuteNestedAsync()
        {
            await _serviceProvider.GetRequiredService<IUnitOfWorkService>().ExecuteInnerAsync();
        }

        /// <inheritdoc />
        [UnitOfWork]
        public Task ExecuteInnerAsync()
        {
            _manager.Record("Target");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        [UnitOfWork(Scope = BingUnitOfWorkScope.None)]
        public async Task ExecuteNoneNestedAsync()
        {
            await _serviceProvider.GetRequiredService<IUnitOfWorkService>().ExecuteNoneInnerAsync();
        }

        /// <inheritdoc />
        [UnitOfWork(Scope = BingUnitOfWorkScope.None)]
        public Task ExecuteNoneInnerAsync()
        {
            _manager.Record("Target");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        [UnitOfWork(Scope = BingUnitOfWorkScope.Nested)]
        public async Task ExecuteNestedScopeAsync()
        {
            _manager.Record("Target-Nested");
            await _serviceProvider.GetRequiredService<IUnitOfWorkService>().ExecuteNestedScopeInnerAsync();
        }

        /// <inheritdoc />
        [UnitOfWork(Scope = BingUnitOfWorkScope.Nested)]
        public Task ExecuteNestedScopeInnerAsync()
        {
            _manager.Record("Target-Nested");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        [UnitOfWork]
        public async Task ExecuteConcurrentAsync()
        {
            await Task.Delay(10);
        }

        /// <inheritdoc />
        public void CommitAfter()
        {
            _manager.Record("CommitAfter");
            Interlocked.Increment(ref _commitAfterCount);
        }
    }

    /// <summary>
    /// 定义工作单元拦截测试使用的服务契约。
    /// </summary>
    public interface IUnitOfWorkService
    {
        /// <summary>
        /// 获取提交后回调的执行次数。
        /// </summary>
        int CommitAfterCount { get; }

        /// <summary>
        /// 执行异步工作单元操作。
        /// </summary>
        Task ExecuteAsync();

        /// <summary>
        /// 执行同步工作单元操作。
        /// </summary>
        void ExecuteSync();

        /// <summary>
        /// 执行并抛出异常的操作。
        /// </summary>
        Task ThrowAsync();

        /// <summary>
        /// 等待一个异步切换后抛出无参取消异常。
        /// </summary>
        Task CancelAsync();

        /// <summary>
        /// 返回已取消的无 token Task。
        /// </summary>
        Task CancelNoTokenTaskAsync();

        /// <summary>
        /// 返回已取消的无 token Task<TResult>。
        /// </summary>
        Task<int> CancelNoTokenTaskOfTAsync();

        /// <summary>
        /// 返回已取消的无 token ValueTask。
        /// </summary>
        ValueTask CancelNoTokenValueTaskAsync();

        /// <summary>
        /// 返回已取消的无 token ValueTask<TResult>。
        /// </summary>
        ValueTask<int> CancelNoTokenValueTaskOfTAsync();

        /// <summary>
        /// 返回由 IValueTaskSource 支持的取消结果。
        /// </summary>
        ValueTask CancelSourceBackedValueTaskAsync();

        /// <summary>
        /// 返回由 IValueTaskSource 支持的泛型取消结果。
        /// </summary>
        ValueTask<int> CancelSourceBackedValueTaskOfTAsync();

        /// <summary>
        /// 返回由 IValueTaskSource 支持的成功结果。
        /// </summary>
        ValueTask SourceBackedValueTaskAsync();

        /// <summary>
        /// 返回由 IValueTaskSource 支持的泛型成功结果。
        /// </summary>
        ValueTask<int> SourceBackedValueTaskOfTAsync();

        /// <summary>
        /// 通过显式接口实现返回工作单元结果。
        /// </summary>
        [UnitOfWork]
        ValueTask<int> ExplicitInterfaceValueTaskAsync();

        /// <summary>
        /// 在默认嵌套工作单元中等待内层 source-backed ValueTask。
        /// </summary>
        ValueTask NestedSourceBackedValueTaskAsync();

        /// <summary>
        /// 在默认嵌套工作单元中等待内层 source-backed ValueTask<TResult>。
        /// </summary>
        ValueTask<int> NestedSourceBackedValueTaskOfTAsync();

        /// <summary>
        /// 返回嵌套 source-backed 非泛型值任务源。
        /// </summary>
        ValueTask NestedSourceBackedValueTaskInnerAsync();

        /// <summary>
        /// 返回嵌套 source-backed 泛型值任务源。
        /// </summary>
        ValueTask<int> NestedSourceBackedValueTaskOfTInnerAsync();

        /// <summary>
        /// 在目标内部取消并返回取消任务。
        /// </summary>
        Task CancelInsideTargetAsync();

        /// <summary>
        /// 忽略已取消令牌并正常完成。
        /// </summary>
        Task IgnoreCanceledTokenAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 使用已取消令牌执行并抛出业务异常。
        /// </summary>
        Task ThrowWithCanceledTokenAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 返回可取消的 Task。
        /// </summary>
        Task CancelTaskAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 返回可取消的 Task<TResult>。
        /// </summary>
        Task<int> CancelTaskOfTAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 返回可取消的 ValueTask。
        /// </summary>
        ValueTask CancelValueTaskAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 返回可取消的 ValueTask<TResult>。
        /// </summary>
        ValueTask<int> CancelValueTaskOfTAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 执行嵌套工作单元操作。
        /// </summary>
        Task ExecuteNestedAsync();

        /// <summary>
        /// 执行嵌套工作单元中的内部操作。
        /// </summary>
        Task ExecuteInnerAsync();

        /// <summary>
        /// 执行无工作单元嵌套操作。
        /// </summary>
        Task ExecuteNoneNestedAsync();

        /// <summary>
        /// 执行无工作单元嵌套中的内部操作。
        /// </summary>
        Task ExecuteNoneInnerAsync();

        /// <summary>
        /// 执行嵌套范围工作单元操作。
        /// </summary>
        Task ExecuteNestedScopeAsync();

        /// <summary>
        /// 执行嵌套范围工作单元中的内部操作。
        /// </summary>
        Task ExecuteNestedScopeInnerAsync();

        /// <summary>
        /// 并发执行工作单元操作。
        /// </summary>
        Task ExecuteConcurrentAsync();
    }

    /// <summary>
    /// 通知测试目标已经开始等待取消。
    /// </summary>
    public sealed class CancellationSignal
    {
        private readonly TaskCompletionSource<bool> _started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 配置无 token 目标返回任务使用的取消令牌。
        /// </summary>
        public CancellationToken NoTokenCancellationToken { get; set; }

        /// <summary>
        /// 配置由目标内部取消的令牌源。
        /// </summary>
        public CancellationTokenSource TargetCancellationSource { get; set; } = null!;

        /// <summary>
        /// 获取或设置带已取消令牌的目标业务异常。
        /// </summary>
        public Exception TargetFailure { get; set; } = null!;

        /// <summary>
        /// 获取最近创建的单次消费值任务源。
        /// </summary>
        public SingleConsumptionValueTaskSource LastValueTaskSource { get; private set; } = null!;

        /// <summary>
        /// 获取目标开始等待取消的信号。
        /// </summary>
        public Task Started => _started.Task;

        /// <summary>
        /// 发出开始信号并等待传入令牌取消。
        /// </summary>
        /// <param name="cancellationToken">调用方取消令牌。</param>
        public async Task WaitUntilCanceledAsync(CancellationToken cancellationToken)
        {
            _started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        /// <summary>
        /// 为成功测试创建已完成或等待信号的值任务源。
        /// </summary>
        public void PrepareValueTaskSource(bool completeImmediately)
        {
            LastValueTaskSource = new SingleConsumptionValueTaskSource();
            if (completeImmediately)
                LastValueTaskSource.SetResult(123);
        }

        /// <summary>
        /// 完成当前等待中的值任务源。
        /// </summary>
        public void CompleteValueTaskSource() => LastValueTaskSource.SetResult(123);

        /// <summary>
        /// 创建由当前单次消费源支持的非泛型值任务。
        /// </summary>
        public ValueTask CreateValueTaskFromSource() => LastValueTaskSource.AsValueTask();

        /// <summary>
        /// 创建由当前单次消费源支持的泛型值任务。
        /// </summary>
        public ValueTask<int> CreateValueTaskOfTFromSource() => LastValueTaskSource.AsValueTaskOfT();

        /// <summary>
        /// 创建由真实 IValueTaskSource 支持的已取消 ValueTask。
        /// </summary>
        public ValueTask CreateCanceledValueTask()
        {
            LastValueTaskSource = new SingleConsumptionValueTaskSource();
            LastValueTaskSource.SetCanceled(NoTokenCancellationToken);
            return LastValueTaskSource.AsValueTask();
        }

        /// <summary>
        /// 创建由真实 IValueTaskSource 支持的泛型已取消 ValueTask。
        /// </summary>
        public ValueTask<int> CreateCanceledValueTaskOfT()
        {
            LastValueTaskSource = new SingleConsumptionValueTaskSource();
            LastValueTaskSource.SetCanceled(NoTokenCancellationToken);
            return LastValueTaskSource.AsValueTaskOfT();
        }
    }

    /// <summary>
    /// 记录并限制底层结果只消费一次的值任务源。
    /// </summary>
    public sealed class SingleConsumptionValueTaskSource : IValueTaskSource, IValueTaskSource<int>
    {
        private ManualResetValueTaskSourceCore<int> _core;
        private int _getResultCount;

        /// <summary>
        /// 初始化单次消费值任务源。
        /// </summary>
        public SingleConsumptionValueTaskSource()
        {
            _core.RunContinuationsAsynchronously = true;
        }

        /// <summary>
        /// 获取底层结果读取次数。
        /// </summary>
        public int GetResultCount => Volatile.Read(ref _getResultCount);

        /// <summary>
        /// 获取配置给当前取消源的原始异常。
        /// </summary>
        public OperationCanceledException ExpectedCancellation { get; private set; } = null!;

        /// <summary>
        /// 设置成功结果。
        /// </summary>
        public void SetResult(int value) => _core.SetResult(value);

        /// <summary>
        /// 设置取消结果并保留取消令牌。
        /// </summary>
        public void SetCanceled(CancellationToken cancellationToken)
        {
            ExpectedCancellation = new OperationCanceledException(cancellationToken);
            _core.SetException(ExpectedCancellation);
        }

        /// <summary>
        /// 创建关联到此值任务源的 ValueTask。
        /// </summary>
        public ValueTask AsValueTask() => new(this, _core.Version);

        /// <summary>
        /// 创建关联到此值任务源的泛型 ValueTask。
        /// </summary>
        public ValueTask<int> AsValueTaskOfT() => new(this, _core.Version);

        /// <inheritdoc />
        void IValueTaskSource.GetResult(short token)
        {
            if (Interlocked.Increment(ref _getResultCount) != 1)
                throw new InvalidOperationException("The source-backed ValueTask result was consumed more than once.");

            _ = _core.GetResult(token);
        }

        /// <inheritdoc />
        int IValueTaskSource<int>.GetResult(short token)
        {
            if (Interlocked.Increment(ref _getResultCount) != 1)
                throw new InvalidOperationException("The source-backed ValueTask result was consumed more than once.");

            return _core.GetResult(token);
        }

        /// <inheritdoc />
        public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

        /// <inheritdoc />
        public void OnCompleted(Action<object> continuation, object state, short token,
            ValueTaskSourceOnCompletedFlags flags) => _core.OnCompleted(continuation, state, token, flags);
    }

    /// <summary>
    /// 记录虚方法类代理的拦截链，并阻止错误分派造成无界重入。
    /// </summary>
    public sealed class VirtualClassProxyTrace
    {
        private int _beforeCount;
        private int _afterCount;
        private int _targetCount;
        private int _commitAfterCount;

        public int BeforeCount => Volatile.Read(ref _beforeCount);

        public int AfterCount => Volatile.Read(ref _afterCount);

        public int TargetCount => Volatile.Read(ref _targetCount);

        public int CommitAfterCount => Volatile.Read(ref _commitAfterCount);

        public InvalidOperationException BusinessFailure { get; } =
            new("The virtual target business failure must retain its identity.");

        public int EnterBefore() => Interlocked.Increment(ref _beforeCount);

        public void RecordAfter() => Interlocked.Increment(ref _afterCount);

        public void RecordTarget() => Interlocked.Increment(ref _targetCount);

        public void RecordCommitAfter() => Interlocked.Increment(ref _commitAfterCount);
    }

    /// <summary>
    /// 统计虚方法类代理拦截链进入次数，并在重入时快速终止测试调用。
    /// </summary>
    public sealed class VirtualProxyBeforeAttribute : AbstractInterceptorAttribute
    {
        public VirtualProxyBeforeAttribute() => Order = -10;

        public override async Task Invoke(AspectContext context, AspectDelegate next)
        {
            var trace = context.ServiceProvider.GetRequiredService<VirtualClassProxyTrace>();
            if (trace.EnterBefore() != 1)
                throw new InvalidOperationException("The virtual proxy interceptor chain was re-entered.");

            await next(context).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 记录虚方法类代理的后置拦截执行次数。
    /// </summary>
    public sealed class VirtualProxyAfterAttribute : AbstractInterceptorAttribute
    {
        public VirtualProxyAfterAttribute() => Order = 10;

        public override async Task Invoke(AspectContext context, AspectDelegate next)
        {
            var trace = context.ServiceProvider.GetRequiredService<VirtualClassProxyTrace>();
            try
            {
                await next(context).ConfigureAwait(false);
            }
            finally
            {
                trace.RecordAfter();
            }
        }
    }

    /// <summary>
    /// 通过虚方法类代理覆盖默认和 None 工作单元范围。
    /// </summary>
    public class VirtualUnitOfWorkService : ICommitAfter
    {
        private readonly RecordingUnitOfWorkManager _manager;
        private readonly VirtualClassProxyTrace _trace;

        public VirtualUnitOfWorkService(RecordingUnitOfWorkManager manager, VirtualClassProxyTrace trace)
        {
            _manager = manager;
            _trace = trace;
        }

        [VirtualProxyBefore, UnitOfWork, VirtualProxyAfter]
        public virtual ValueTask<int> Default()
        {
            RecordTarget();
            return new ValueTask<int>(123);
        }

        [VirtualProxyBefore, UnitOfWork(Scope = BingUnitOfWorkScope.None), VirtualProxyAfter]
        public virtual ValueTask<int> None()
        {
            RecordTarget();
            return new ValueTask<int>(123);
        }

        [VirtualProxyBefore, UnitOfWork, VirtualProxyAfter]
        public virtual ValueTask<int> Cancel(CancellationToken cancellationToken)
        {
            RecordTarget();
            return new ValueTask<int>(Task.FromCanceled<int>(cancellationToken));
        }

        [VirtualProxyBefore, UnitOfWork(Scope = BingUnitOfWorkScope.None), VirtualProxyAfter]
        public virtual ValueTask<int> CancelNone(CancellationToken cancellationToken)
        {
            RecordTarget();
            return new ValueTask<int>(Task.FromCanceled<int>(cancellationToken));
        }

        [VirtualProxyBefore, UnitOfWork, VirtualProxyAfter]
        public virtual ValueTask<int> Fail()
        {
            RecordTarget();
            return new ValueTask<int>(Task.FromException<int>(_trace.BusinessFailure));
        }

        [VirtualProxyBefore, UnitOfWork(Scope = BingUnitOfWorkScope.None), VirtualProxyAfter]
        public virtual ValueTask<int> FailNone()
        {
            RecordTarget();
            return new ValueTask<int>(Task.FromException<int>(_trace.BusinessFailure));
        }

        public void CommitAfter()
        {
            _manager.Record("CommitAfter");
            _trace.RecordCommitAfter();
        }

        private void RecordTarget()
        {
            _manager.Record("Target");
            _trace.RecordTarget();
        }
    }

    /// <summary>
    /// 使用接口类型工作单元标记的服务契约。
    /// </summary>
    [UnitOfWork]
    public interface IInterfaceTypeMarkedService
    {
        /// <summary>
        /// 执行一次业务调用。
        /// </summary>
        Task ExecuteAsync();
    }

    /// <summary>
    /// 使用接口类型工作单元标记的服务实现。
    /// </summary>
    public class InterfaceTypeMarkedService : IInterfaceTypeMarkedService
    {
        /// <inheritdoc />
        public virtual Task ExecuteAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// 使用实现类型工作单元标记的服务契约。
    /// </summary>
    public interface IImplementationTypeMarkedService
    {
        /// <summary>
        /// 执行一次业务调用。
        /// </summary>
        Task ExecuteAsync();
    }

    /// <summary>
    /// 使用实现类型工作单元标记的服务实现。
    /// </summary>
    [UnitOfWork]
    public class ImplementationTypeMarkedService : IImplementationTypeMarkedService
    {
        /// <inheritdoc />
        public virtual Task ExecuteAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// 使用接口和实现方法重复工作单元标记的服务契约。
    /// </summary>
    public interface IDuplicateMarkerService
    {
        /// <summary>
        /// 执行一次业务调用。
        /// </summary>
        [UnitOfWork]
        Task ExecuteAsync();
    }

    /// <summary>
    /// 使用接口和实现方法重复工作单元标记的服务实现。
    /// </summary>
    public class DuplicateMarkerService : IDuplicateMarkerService
    {
        /// <inheritdoc />
        [UnitOfWork]
        public virtual Task ExecuteAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// 记录工作单元提交次数和操作顺序。
    /// </summary>
    public sealed class RecordingUnitOfWorkManager : IUnitOfWorkManager
    {
        /// <summary>
        /// 记录工作单元提交次数。
        /// </summary>
        private int _commitCount;

        /// <summary>
        /// 按顺序保存工作单元事件。
        /// </summary>
        private readonly ConcurrentQueue<string> _events = new();

        /// <summary>
        /// 通知取消提交测试已进入提交方法。
        /// </summary>
        private readonly TaskCompletionSource<bool> _commitStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private CancellationToken? _commitCancellationToken;
        private bool _waitForCommitCancellation;

        /// <summary>
        /// 获取工作单元提交次数。
        /// </summary>
        public int CommitCount => Volatile.Read(ref _commitCount);

        /// <summary>
        /// 获取已记录的工作单元事件。
        /// </summary>
        public IReadOnlyCollection<string> Events => _events.ToArray();

        /// <summary>
        /// 获取提交方法开始执行的信号。
        /// </summary>
        public Task CommitStarted => _commitStarted.Task;

        /// <summary>
        /// 获取或设置提交是否抛出异常。
        /// </summary>
        public bool ThrowOnCommit { get; set; }

        /// <summary>
        /// 配置提交立即取消或等待外部信号后取消。
        /// </summary>
        /// <param name="cancellationToken">提交应传播的取消令牌。</param>
        /// <param name="waitForCancellation">是否等待令牌在提交期间取消。</param>
        public void CancelCommitWith(CancellationToken cancellationToken, bool waitForCancellation)
        {
            _commitCancellationToken = cancellationToken;
            _waitForCommitCancellation = waitForCancellation;
        }

        /// <inheritdoc />
        public void Commit() => Interlocked.Increment(ref _commitCount);

        /// <inheritdoc />
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Record("Commit");
            Interlocked.Increment(ref _commitCount);
            if (ThrowOnCommit)
                throw new InvalidOperationException("commit failure");
            if (_commitCancellationToken is { } commitCancellationToken)
            {
                _commitStarted.TrySetResult(true);
                return _waitForCommitCancellation
                    ? Task.Delay(Timeout.InfiniteTimeSpan, commitCancellationToken)
                    : Task.FromCanceled(commitCancellationToken);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 记录一条工作单元事件。
        /// </summary>
        /// <param name="value">事件名称。</param>
        public void Record(string value) => _events.Enqueue(value);

        /// <inheritdoc />
        public void Register(IUnitOfWork unitOfWork)
        {
        }

        /// <inheritdoc />
        public IReadOnlyCollection<IUnitOfWork> GetUnitOfWorks() => Array.Empty<IUnitOfWork>();
    }
}
