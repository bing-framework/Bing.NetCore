using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Threading;
using Bing.Data.Events.Ordering;
using Bing.Data.Transaction;
using Bing.Events.Ordering;
using DotNetCore.CAP;
using DotNetCore.CAP.Messages;
using DotNetCore.CAP.Persistence;
using DotNetCore.CAP.Transport;
using Moq;
using Xunit;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 验证有序事务适配器的原生 CAP 缓冲与资源所有权边界。
/// </summary>
/// <remarks>数据库事务与队列入口使用职责替身，缓冲由真实 CapTransactionBase 管理；这些用例不是数据库或消息代理集成验收。</remarks>
public sealed class CapOrderedEventTransactionFactoryTest
{
    /// <summary>
    /// 验证真正异步开启后由调用方同步激活同一物理事务。
    /// </summary>
    [Fact]
    public async Task BeginAsync_WhenInnerCompletesAsynchronously_ShouldActivateInCallerContext()
    {
        var context = new TestContext();
        var release = NewGate();
        context.Begin = async token =>
        {
            await release.Task.WaitAsync(token);
            return context.Owner;
        };
        using var cancellation = new CancellationTokenSource();
        var pending = context.Factory.BeginAsync(cancellation.Token);
        IOrderedEventTransactionState scope = null;
        try
        {
            Assert.False(pending.IsCompleted);
            Assert.Null(context.Ambient.Value);
            release.TrySetResult(true);
            scope = Assert.IsAssignableFrom<IOrderedEventTransactionState>(await pending);
            Assert.Null(context.Ambient.Value);
            scope.ActivateAmbient();
            Assert.Same(context.Owner.Physical, scope.Transaction);
            Assert.Same(scope.Transaction, context.Ambient.Value.DbTransaction);
            Assert.False(context.Ambient.Value.AutoCommit);
            Assert.Equal(cancellation.Token, context.BeginToken);
            Assert.Equal(0, context.Owner.Activations);
        }
        finally
        {
            release.TrySetResult(true);
            scope ??= (IOrderedEventTransactionState)await pending;
            await scope.DisposeAsync();
        }
        Assert.Null(context.Ambient.Value);
        Assert.Equal(1, context.Owner.Rollbacks);
        Assert.Equal(1, context.Owner.Disposals);
        Assert.Equal(1, context.Owner.Physical.Disposals);
    }

    /// <summary>
    /// 验证提交只在物理提交成功后发送真实 CAP 队列，且不重复发送。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommitAsync_WhenNativeBufferContainsMessage_ShouldFlushOnceAfterPhysicalCommit(bool delayed)
    {
        var context = new TestContext();
        var scope = (IOrderedEventTransactionState)await context.Factory.BeginAsync();
        scope.ActivateAmbient();
        var cap = context.Ambient.Value;
        var message = Buffer(cap, "committed", delayed);
        context.OnDispatch = () => Assert.Equal(1, context.Owner.Physical.Commits);
        try
        {
            Assert.Empty(context.Published);
            Assert.Empty(context.Scheduled);
            await cap.CommitAsync();
            Assert.Equal(TransactionOutcome.Committed, scope.Outcome);
            Assert.Same(message, Assert.Single(delayed ? context.Scheduled : context.Published));
            Assert.Empty(delayed ? context.Published : context.Scheduled);
            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.CommitAsync());
            await scope.RollbackAsync();
            Assert.Equal(1, context.Owner.Commits);
            Assert.Equal(0, context.Owner.Rollbacks);
        }
        finally
        {
            await scope.DisposeAsync();
        }
        Assert.Null(cap.DbTransaction);
        Assert.Null(context.Ambient.Value);
        Assert.Equal(1, context.Owner.Physical.Disposals);
        Assert.Equal(1, context.Published.Count + context.Scheduled.Count);
    }

    /// <summary>
    /// 验证物理事务提交后不能再次激活 CAP ambient。
    /// </summary>
    [Fact]
    public async Task ActivateAmbient_WhenCommitCompleted_ShouldRejectReactivation()
    {
        var context = new TestContext();
        var scope = (IOrderedEventTransactionState)await context.Factory.BeginAsync();
        scope.ActivateAmbient();

        try
        {
            await scope.CommitAsync();
            Assert.Equal(TransactionOutcome.Committed, scope.Outcome);
            Assert.Throws<InvalidOperationException>(scope.ActivateAmbient);
        }
        finally
        {
            await scope.DisposeAsync();
        }
    }

    /// <summary>
    /// 验证 CAP 包装的同步回滚和释放不会发送缓冲或释放借用的物理事务。
    /// </summary>
    [Fact]
    public async Task Rollback_WhenCapWrapperIsDisposed_ShouldKeepPhysicalOwnershipAndDiscardBuffer()
    {
        var context = new TestContext();
        var scope = (IOrderedEventTransactionState)await context.Factory.BeginAsync();
        scope.ActivateAmbient();
        var cap = context.Ambient.Value;
        Buffer(cap, "rolled-back");
        try
        {
            cap.Rollback();
            cap.Dispose();
            Assert.Null(cap.DbTransaction);
            Assert.Equal(0, context.Owner.Physical.Disposals);
            await scope.RollbackAsync();
            Assert.Equal(1, context.Owner.Rollbacks);
            Assert.Equal(TransactionOutcome.RolledBack, scope.Outcome);
            Assert.Empty(context.Published);
        }
        finally
        {
            await scope.DisposeAsync();
        }
        Assert.Equal(1, context.Owner.Disposals);
        Assert.Equal(1, context.Owner.Physical.Disposals);
        Assert.Empty(context.Published);
    }

    /// <summary>
    /// 验证异步重置按底层与宿主顺序执行并只发送新缓冲。
    /// </summary>
    [Fact]
    public async Task ResetBusinessStateAsync_WhenHostCompletesAsynchronously_ShouldReplaceAndDiscardOldBuffer()
    {
        var order = new List<string>();
        var release = NewGate();
        var context = new TestContext(async token =>
        {
            order.Add("host-start");
            await release.Task.WaitAsync(token);
            order.Add("host-end");
        });
        context.Owner.Reset = token =>
        {
            order.Add("inner");
            return Task.CompletedTask;
        };
        var scope = (IOrderedEventTransactionState)await context.Factory.BeginAsync();
        scope.ActivateAmbient();
        var old = context.Ambient.Value;
        Buffer(old, "discarded");
        using var cancellation = new CancellationTokenSource();
        var pending = scope.ResetBusinessStateAsync(cancellation.Token);
        try
        {
            Assert.False(pending.IsCompleted);
            Assert.Same(old, context.Ambient.Value);
            Assert.Same(scope.Transaction, old.DbTransaction);
            Assert.Equal(new[] { "inner", "host-start" }, order);
            release.TrySetResult(true);
            await pending;
            Assert.Same(old, context.Ambient.Value);
            Assert.Null(old.DbTransaction);
            scope.ActivateAmbient();
            Assert.NotSame(old, context.Ambient.Value);
            Assert.Same(scope.Transaction, context.Ambient.Value.DbTransaction);
            Assert.Equal(cancellation.Token, context.Owner.ResetToken);
            var message = Buffer(context.Ambient.Value, "fresh");
            await scope.CommitAsync();
            Assert.Same(message, Assert.Single(context.Published));
            Assert.Equal(new[] { "inner", "host-start", "host-end" }, order);
            Assert.Equal(0, context.Owner.Rollbacks);
            Assert.Equal(0, context.Owner.Physical.Disposals);
        }
        finally
        {
            release.TrySetResult(true);
            await pending;
            await scope.DisposeAsync();
        }
        Assert.Equal(1, context.Owner.Disposals);
        Assert.Equal("fresh", Assert.Single(context.Published).DbId);
    }

    /// <summary>
    /// 验证重置失败保留原异常和原缓冲，完整回滚时不发送业务消息。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResetBusinessStateAsync_WhenResetFails_ShouldKeepOriginalErrorAndRollbackWithoutFlush(bool innerFails)
    {
        var original = new ApplicationException("reset failed");
        var hostCalls = 0;
        var context = new TestContext(_ =>
        {
            hostCalls++;
            return Task.FromException(original);
        });
        if (innerFails)
            context.Owner.Reset = _ => Task.FromException(original);
        var scope = (IOrderedEventTransactionState)await context.Factory.BeginAsync();
        scope.ActivateAmbient();
        var old = context.Ambient.Value;
        Buffer(old, "failed-business");
        try
        {
            Assert.Same(original, await Assert.ThrowsAsync<ApplicationException>(() => scope.ResetBusinessStateAsync()));
            Assert.Equal(innerFails ? 0 : 1, hostCalls);
            Assert.Same(old, context.Ambient.Value);
            Assert.Same(scope.Transaction, old.DbTransaction);
            await scope.RollbackAsync();
        }
        finally
        {
            await scope.DisposeAsync();
        }
        Assert.Empty(context.Published);
        Assert.Equal(1, context.Owner.Rollbacks);
        Assert.Equal(1, context.Owner.Disposals);
    }

    /// <summary>
    /// 验证已有外部 ambient 在开启前被拒绝且不调用底层工厂。
    /// </summary>
    [Fact]
    public async Task BeginAsync_WhenForeignAmbientExists_ShouldRejectWithoutBeginningInnerTransaction()
    {
        var context = new TestContext();
        var foreign = Mock.Of<ICapTransaction>();
        context.Ambient.Value = foreign;
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Factory.BeginAsync());
        Assert.Same(foreign, context.Ambient.Value);
        Assert.Equal(0, context.Begins);
        Assert.Equal(0, context.Owner.Disposals);
    }

    /// <summary>
    /// 验证激活与清理拒绝覆盖后来安装的外部 ambient，但仍清理自身资源。
    /// </summary>
    [Fact]
    public async Task ActivateAmbient_WhenForeignAmbientReplacesOwnedTransaction_ShouldPreserveForeignAndCleanOwned()
    {
        var context = new TestContext();
        var scope = (IOrderedEventTransactionState)await context.Factory.BeginAsync();
        scope.ActivateAmbient();
        var owned = context.Ambient.Value;
        var foreign = Mock.Of<ICapTransaction>();
        context.Ambient.Value = foreign;
        Assert.Throws<InvalidOperationException>(scope.ActivateAmbient);
        Assert.Same(foreign, context.Ambient.Value);
        var cleanup = scope.DisposeAsync().AsTask();
        Assert.Same(foreign, context.Ambient.Value);
        await Assert.ThrowsAsync<InvalidOperationException>(() => cleanup);
        Assert.Null(owned.DbTransaction);
        Assert.Equal(1, context.Owner.Rollbacks);
        Assert.Equal(1, context.Owner.Disposals);
        Assert.Equal(1, context.Owner.Physical.Disposals);
    }

    /// <summary>
    /// 验证物理提交后的原生同步发送错误保持已提交结果与原异常。
    /// </summary>
    [Fact]
    public async Task CommitAsync_WhenNativeFlushThrowsSynchronously_ShouldKeepCommittedOutcome()
    {
        var context = new TestContext();
        var original = new ApplicationException("queue entry failed");
        context.OnDispatch = () => throw original;
        var scope = (IOrderedEventTransactionState)await context.Factory.BeginAsync();
        scope.ActivateAmbient();
        Buffer(context.Ambient.Value, "durable-outbox");
        try
        {
            Assert.Same(original, await Assert.ThrowsAsync<ApplicationException>(() => scope.CommitAsync()));
            Assert.Equal(TransactionOutcome.Committed, scope.Outcome);
            Assert.Equal(TransactionOutcome.Committed, original.GetTransactionOutcome());
            await scope.RollbackAsync();
        }
        finally
        {
            await scope.DisposeAsync();
        }
        Assert.Equal(1, context.Owner.Commits);
        Assert.Equal(0, context.Owner.Rollbacks);
        Assert.Equal(1, context.Owner.Physical.Disposals);
    }

    /// <summary>
    /// 验证物理提交后的异步入队未完成时提交任务不会提前完成。
    /// </summary>
    [Fact]
    public async Task CommitAsync_WhenNativeDispatchIsIncomplete_ShouldWaitForDispatchCompletion()
    {
        var context = new TestContext();
        var release = NewGate();
        context.DispatchTask = release.Task;
        context.DispatchStarted = NewGate();
        var scope = (IOrderedEventTransactionState)await context.Factory.BeginAsync();
        scope.ActivateAmbient();
        Buffer(context.Ambient.Value, "pending-dispatch");

        try
        {
            var commit = context.Ambient.Value.CommitAsync();
            await context.DispatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(commit.IsCompleted);
            Assert.Equal(TransactionOutcome.Committed, scope.Outcome);

            release.TrySetResult(true);
            await commit;
            Assert.Equal(TransactionOutcome.Committed, scope.Outcome);
            Assert.Single(context.Published);
        }
        finally
        {
            release.TrySetResult(true);
            await scope.DisposeAsync();
        }
    }

    /// <summary>
    /// 验证异步入队失败保留已提交结果和原始异常。
    /// </summary>
    [Fact]
    public async Task CommitAsync_WhenNativeDispatchFailsAsynchronously_ShouldKeepCommittedOutcome()
    {
        var context = new TestContext();
        var original = new ApplicationException("async queue entry failed");
        context.DispatchTask = Task.FromException(original);
        var scope = (IOrderedEventTransactionState)await context.Factory.BeginAsync();
        scope.ActivateAmbient();
        Buffer(context.Ambient.Value, "failed-dispatch");

        try
        {
            var exception = await Assert.ThrowsAsync<ApplicationException>(() => scope.CommitAsync());
            Assert.Same(original, exception);
            Assert.Equal(TransactionOutcome.Committed, scope.Outcome);
            Assert.Equal(TransactionOutcome.Committed, original.GetTransactionOutcome());
            Assert.Single(context.Published);
        }
        finally
        {
            await scope.DisposeAsync();
        }

        Assert.Equal(1, context.Owner.Commits);
        Assert.Equal(0, context.Owner.Rollbacks);
        Assert.Equal(1, context.Owner.Physical.Disposals);
    }

    /// <summary>
    /// 验证多个异步入队失败时保留全部原始异常。
    /// </summary>
    [Fact]
    public async Task CommitAsync_WhenMultipleNativeDispatchesFail_ShouldAggregateAllErrors()
    {
        var context = new TestContext();
        var first = new ApplicationException("first async queue entry failed");
        var second = new InvalidOperationException("second async queue entry failed");
        context.DispatchTasks.Enqueue(Task.FromException(first));
        context.DispatchTasks.Enqueue(Task.FromException(second));
        var scope = (IOrderedEventTransactionState)await context.Factory.BeginAsync();
        scope.ActivateAmbient();
        Buffer(context.Ambient.Value, "first-failed-dispatch");
        Buffer(context.Ambient.Value, "second-failed-dispatch");

        try
        {
            var exception = await Assert.ThrowsAsync<AggregateException>(() => scope.CommitAsync());
            Assert.Contains(first, exception.InnerExceptions);
            Assert.Contains(second, exception.InnerExceptions);
            Assert.Equal(TransactionOutcome.Committed, scope.Outcome);
            Assert.Equal(TransactionOutcome.Committed, exception.GetTransactionOutcome());
            Assert.Equal(2, context.Published.Count);
        }
        finally
        {
            await scope.DisposeAsync();
        }

        Assert.Equal(1, context.Owner.Commits);
        Assert.Equal(0, context.Owner.Rollbacks);
        Assert.Equal(1, context.Owner.Physical.Disposals);
    }

    /// <summary>
    /// 验证底层提交错误保留其确认结果，且失败提交不发送缓冲。
    /// </summary>
    [Theory]
    [InlineData(TransactionOutcome.Unknown)]
    [InlineData(TransactionOutcome.Committed)]
    [InlineData(TransactionOutcome.RolledBack)]
    public async Task CommitAsync_WhenInnerThrows_ShouldPreserveInnerOutcomeWithoutFlushing(TransactionOutcome outcome)
    {
        var context = new TestContext();
        var original = new ApplicationException("physical commit failed");
        context.Owner.Commit = _ =>
        {
            context.Owner.Outcome = outcome;
            return Task.FromException(original);
        };
        var scope = (IOrderedEventTransactionState)await context.Factory.BeginAsync();
        scope.ActivateAmbient();
        Buffer(context.Ambient.Value, "not-flushed");
        try
        {
            Assert.Same(original, await Assert.ThrowsAsync<ApplicationException>(() => scope.CommitAsync()));
            Assert.Equal(outcome, scope.Outcome);
            Assert.Equal(outcome, original.GetTransactionOutcome());
        }
        finally
        {
            await scope.DisposeAsync();
        }
        Assert.Empty(context.Published);
        Assert.Equal(outcome == TransactionOutcome.Unknown ? 1 : 0, context.Owner.Rollbacks);
        Assert.Equal(1, context.Owner.Disposals);
    }

    /// <summary>
    /// 验证尚未完成的底层开启和未释放的作用域均阻止并发开启。
    /// </summary>
    [Fact]
    public async Task BeginAsync_WhenAnotherScopeIsOpeningOrActive_ShouldRejectAndAllowReuseAfterDispose()
    {
        var context = new TestContext();
        var release = NewGate();
        context.Begin = async _ =>
        {
            await release.Task;
            return context.Owner;
        };
        var pending = context.Factory.BeginAsync();
        IOrderedEventTransaction scope = null;
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.Factory.BeginAsync());
            Assert.Equal(1, context.Begins);
            release.TrySetResult(true);
            scope = await pending;
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.Factory.BeginAsync());
            Assert.Equal(1, context.Begins);
        }
        finally
        {
            release.TrySetResult(true);
            scope ??= await pending;
            await scope.DisposeAsync();
        }
        var previous = context.Owner;
        context.Owner = new TransactionOwner();
        await using var next = await context.Factory.BeginAsync();
        Assert.NotSame(previous.Physical, next.Transaction);
        Assert.Equal(2, context.Begins);
    }

    /// <summary>
    /// 验证并发及重复释放等待同一任务，且独立回滚令牌与底层释放只使用一次。
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenInnerCleanupIsPending_ShouldWaitSameTaskAndCleanOnlyOnce()
    {
        var context = new TestContext();
        var release = NewGate();
        context.Owner.Dispose = () => release.Task;
        var scope = (IOrderedEventTransactionState)await context.Factory.BeginAsync();
        scope.ActivateAmbient();
        var cap = context.Ambient.Value;
        Buffer(cap, "dispose-without-commit");
        var first = scope.DisposeAsync().AsTask();
        var second = scope.DisposeAsync().AsTask();
        try
        {
            Assert.Same(first, second);
            Assert.False(first.IsCompleted);
            Assert.Null(context.Ambient.Value);
            Assert.Null(cap.DbTransaction);
            Assert.Equal(CancellationToken.None, context.Owner.RollbackToken);
            Assert.Equal(1, context.Owner.Rollbacks);
            Assert.Equal(1, context.Owner.Disposals);
            Assert.Equal(0, context.Owner.Physical.Disposals);
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.Factory.BeginAsync());
            Assert.Throws<ObjectDisposedException>(scope.ActivateAmbient);
        }
        finally
        {
            release.TrySetResult(true);
            await Task.WhenAll(first, second);
        }
        Assert.Same(first, scope.DisposeAsync().AsTask());
        Assert.Equal(1, context.Owner.Physical.Disposals);
        Assert.Empty(context.Published);
    }

    /// <summary>
    /// 验证另一执行上下文释放后仍能同步解绑调用方的旧自有 ambient。
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenExecutedInAnotherContext_ShouldDetachOwnedAmbientOnRepeatedCall()
    {
        var context = new TestContext();
        var scope = (IOrderedEventTransactionState)await context.Factory.BeginAsync();
        scope.ActivateAmbient();
        var cap = context.Ambient.Value;
        await Task.Run(async () =>
        {
            context.Ambient.Value = null;
            await scope.DisposeAsync();
        });
        Assert.Same(cap, context.Ambient.Value);
        Assert.Null(cap.DbTransaction);
        await scope.DisposeAsync();
        Assert.Null(context.Ambient.Value);
        Assert.Equal(1, context.Owner.Disposals);
    }

    /// <summary>
    /// 验证回滚与释放都失败时保留两个原始错误，且不重试任何清理步骤。
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenRollbackAndDisposeFail_ShouldAggregateOriginalErrorsOnce()
    {
        var context = new TestContext();
        var rollback = new ApplicationException("rollback failed");
        var disposal = new InvalidOperationException("dispose failed");
        context.Owner.Rollback = _ => Task.FromException(rollback);
        context.Owner.Dispose = () => Task.FromException(disposal);
        var scope = (IOrderedEventTransactionState)await context.Factory.BeginAsync();
        scope.ActivateAmbient();
        var pending = scope.DisposeAsync().AsTask();
        var failure = await Assert.ThrowsAsync<AggregateException>(() => pending);
        Assert.Collection(failure.InnerExceptions, error => Assert.Same(rollback, error), error => Assert.Same(disposal, error));
        Assert.Same(pending, scope.DisposeAsync().AsTask());
        Assert.Null(context.Ambient.Value);
        Assert.Equal(1, context.Owner.Rollbacks);
        Assert.Equal(1, context.Owner.Disposals);
        Assert.Equal(1, context.Owner.Physical.Disposals);
    }

    /// <summary>
    /// 验证异步底层开启失败不留下 ambient，工厂随后仍可使用。
    /// </summary>
    [Fact]
    public async Task BeginAsync_WhenInnerFailsAsynchronously_ShouldKeepOriginalErrorWithoutAmbient()
    {
        var context = new TestContext();
        var original = new ApplicationException("begin failed");
        context.Begin = async _ =>
        {
            await Task.Yield();
            throw original;
        };
        Assert.Same(original, await Assert.ThrowsAsync<ApplicationException>(() => context.Factory.BeginAsync()));
        Assert.Null(context.Ambient.Value);
        Assert.Equal(0, context.Owner.Rollbacks);
        Assert.Equal(0, context.Owner.Disposals);
        context.Begin = _ => Task.FromResult<IOrderedEventTransaction>(context.Owner);
        await using var scope = await context.Factory.BeginAsync();
        Assert.Same(context.Owner.Physical, scope.Transaction);
    }

    /// <summary>
    /// 验证已获取底层资源后初始化失败保留原错及回滚、释放错误。
    /// </summary>
    [Fact]
    public async Task BeginAsync_WhenScopeInitializationAndCleanupFail_ShouldKeepOriginalAndCleanupErrors()
    {
        var context = new TestContext();
        var original = new ApplicationException("transaction access failed");
        var rollback = new InvalidOperationException("rollback failed");
        var disposal = new ApplicationException("dispose failed");
        context.Owner.TransactionError = original;
        context.Owner.Rollback = _ => Task.FromException(rollback);
        context.Owner.Dispose = () => Task.FromException(disposal);
        Assert.Same(original, await Assert.ThrowsAsync<ApplicationException>(() => context.Factory.BeginAsync()));
        var cleanup = Assert.IsType<AggregateException>(original.Data["Bing.TransactionCleanupException"]);
        Assert.Collection(cleanup.InnerExceptions, error => Assert.Same(rollback, error), error => Assert.Same(disposal, error));
        Assert.Null(context.Ambient.Value);
        Assert.Equal(CancellationToken.None, context.Owner.RollbackToken);
        Assert.Equal(1, context.Owner.Rollbacks);
        Assert.Equal(1, context.Owner.Disposals);
        Assert.Equal(1, context.Owner.Physical.Disposals);
        context.Owner = new TransactionOwner();
        await using var recovered = await context.Factory.BeginAsync();
        Assert.Same(context.Owner.Physical, recovered.Transaction);
    }

    /// <summary>
    /// 通过真实 CAP 受保护入口写入消息，不复制其缓冲算法。
    /// </summary>
    /// <param name="transaction">由生产工厂创建的原生 CAP 包装。</param>
    /// <param name="id">本次缓冲消息标识。</param>
    /// <param name="delayed">是否验证原生延迟队列分支。</param>
    /// <returns>放入原生缓冲的同一消息实例。</returns>
    private static MediumMessage Buffer(ICapTransaction transaction, string id, bool delayed = false)
    {
        var headers = new Dictionary<string, string>();
        if (delayed)
        {
            headers["cap-delaytime"] = "1";
            headers["cap-senttime"] = "2026-10-05T00:00:00";
        }
        var message = new MediumMessage { DbId = id, Origin = new Message(headers, id) };
        var method = typeof(CapTransactionBase).GetMethod("AddToSent", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(transaction, new object[] { message });
        return message;
    }

    /// <summary>
    /// 创建保证异步延续执行的测试门闩。
    /// </summary>
    /// <returns>供当前用例独立控制的完成源。</returns>
    private static TaskCompletionSource<bool> NewGate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 为每个用例提供独立 ambient、物理资源与队列观测。
    /// </summary>
    private sealed class TestContext
    {
        /// <summary>
        /// 获取当前用例的 CAP ambient。
        /// </summary>
        internal AsyncLocal<ICapTransaction> Ambient { get; } = new();

        /// <summary>
        /// 获取或设置底层资源所有者。
        /// </summary>
        internal TransactionOwner Owner { get; set; } = new();

        /// <summary>
        /// 获取或设置底层开启动作。
        /// </summary>
        internal Func<CancellationToken, Task<IOrderedEventTransaction>> Begin { get; set; }

        /// <summary>
        /// 获取实际底层开启次数。
        /// </summary>
        internal int Begins { get; private set; }

        /// <summary>
        /// 获取底层开启接收的令牌。
        /// </summary>
        internal CancellationToken BeginToken { get; private set; }

        /// <summary>
        /// 获取原生缓冲发送到普通队列的消息。
        /// </summary>
        internal List<MediumMessage> Published { get; } = new();

        /// <summary>
        /// 获取原生缓冲发送到延迟队列的消息。
        /// </summary>
        internal List<MediumMessage> Scheduled { get; } = new();

        /// <summary>
        /// 获取或设置同步队列入口观测或错误注入。
        /// </summary>
        internal Action OnDispatch { get; set; }

        /// <summary>
        /// 获取或设置异步入队完成任务。
        /// </summary>
        internal Task DispatchTask { get; set; }

        /// <summary>
        /// 按消息顺序注入异步入队任务。
        /// </summary>
        internal Queue<Task> DispatchTasks { get; } = new();

        /// <summary>
        /// 获取当前异步入队开始信号。
        /// </summary>
        internal TaskCompletionSource<bool> DispatchStarted { get; set; }

        /// <summary>
        /// 获取当前用例的生产事务工厂。
        /// </summary>
        internal CapOrderedEventTransactionFactory Factory { get; }

        /// <summary>
        /// 初始化一个 <see cref="TestContext" /> 类型的实例。
        /// </summary>
        /// <param name="hostReset">可选的宿主重置动作。</param>
        internal TestContext(Func<CancellationToken, Task> hostReset = null)
        {
            Begin = _ => Task.FromResult<IOrderedEventTransaction>(Owner);
            var inner = new Mock<IOrderedEventTransactionFactory>();
            inner.Setup(value => value.BeginAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken token) =>
            {
                Begins++;
                BeginToken = token;
                return Begin(token);
            });
            var publisher = new Mock<ICapPublisher>();
            publisher.SetupGet(value => value.Transaction).Returns(Ambient);
            var dispatcher = new Mock<IDispatcher>();
            dispatcher.Setup(value => value.EnqueueToPublish(It.IsAny<MediumMessage>()))
                .Callback<MediumMessage>(message =>
                {
                    OnDispatch?.Invoke();
                    DispatchStarted?.TrySetResult(true);
                    Published.Add(message);
                })
                .Returns((MediumMessage _) =>
                {
                    var task = DispatchTasks.Count != 0 ? DispatchTasks.Dequeue() : DispatchTask;
                    return task == null ? ValueTask.CompletedTask : new ValueTask(task);
                });
            dispatcher.Setup(value => value.EnqueueToScheduler(It.IsAny<MediumMessage>(), It.IsAny<DateTime>(), It.IsAny<object>()))
                .Callback<MediumMessage, DateTime, object>((message, _, _) =>
                {
                    OnDispatch?.Invoke();
                    DispatchStarted?.TrySetResult(true);
                    Scheduled.Add(message);
                }).Returns((MediumMessage _, DateTime _, object _) =>
                {
                    var task = DispatchTasks.Count != 0 ? DispatchTasks.Dequeue() : DispatchTask;
                    return task == null ? ValueTask.CompletedTask : new ValueTask(task);
                });
            Factory = new CapOrderedEventTransactionFactory(inner.Object, publisher.Object, dispatcher.Object, hostReset);
        }
    }

    /// <summary>
    /// 作为物理事务唯一所有者记录操作、结果和清理次数。
    /// </summary>
    private sealed class TransactionOwner : IOrderedEventTransactionState
    {
        /// <summary>
        /// 获取独立的物理事务职责替身。
        /// </summary>
        internal PhysicalTransaction Physical { get; } = new();

        /// <summary>
        /// 获取或设置事务属性读取失败。
        /// </summary>
        internal Exception TransactionError { get; set; }

        /// <summary>
        /// 获取或设置提交前执行的动作。
        /// </summary>
        internal Func<CancellationToken, Task> Commit { get; set; } = _ => Task.CompletedTask;

        /// <summary>
        /// 获取或设置回滚前执行的动作。
        /// </summary>
        internal Func<CancellationToken, Task> Rollback { get; set; } = _ => Task.CompletedTask;

        /// <summary>
        /// 获取或设置底层状态重置动作。
        /// </summary>
        internal Func<CancellationToken, Task> Reset { get; set; } = _ => Task.CompletedTask;

        /// <summary>
        /// 获取或设置资源释放前执行的动作。
        /// </summary>
        internal Func<Task> Dispose { get; set; } = () => Task.CompletedTask;

        /// <summary>
        /// 获取底层提交调用次数。
        /// </summary>
        internal int Commits { get; private set; }

        /// <summary>
        /// 获取底层回滚调用次数。
        /// </summary>
        internal int Rollbacks { get; private set; }

        /// <summary>
        /// 获取底层释放调用次数。
        /// </summary>
        internal int Disposals { get; private set; }

        /// <summary>
        /// 获取底层 ambient 激活次数。
        /// </summary>
        internal int Activations { get; private set; }

        /// <summary>
        /// 获取回滚接收的取消令牌。
        /// </summary>
        internal CancellationToken RollbackToken { get; private set; }

        /// <summary>
        /// 获取重置接收的取消令牌。
        /// </summary>
        internal CancellationToken ResetToken { get; private set; }

        /// <inheritdoc />
        public DbTransaction Transaction => TransactionError == null ? Physical : throw TransactionError;

        /// <inheritdoc />
        public TransactionOutcome Outcome { get; set; }

        /// <inheritdoc />
        public void ActivateAmbient() => Activations++;

        /// <inheritdoc />
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Commits++;
            await Commit(cancellationToken);
            Physical.Commit();
            Outcome = TransactionOutcome.Committed;
        }

        /// <inheritdoc />
        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Rollbacks++;
            RollbackToken = cancellationToken;
            await Rollback(cancellationToken);
            Physical.Rollback();
            Outcome = TransactionOutcome.RolledBack;
        }

        /// <inheritdoc />
        public Task ResetBusinessStateAsync(CancellationToken cancellationToken = default)
        {
            ResetToken = cancellationToken;
            return Reset(cancellationToken);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            Disposals++;
            try
            {
                await Dispose();
            }
            finally
            {
                Physical.Dispose();
            }
        }
    }

    /// <summary>
    /// 记录 DbTransaction 物理入口调用，不创建连接或执行 SQL。
    /// </summary>
    private sealed class PhysicalTransaction : DbTransaction
    {
        /// <summary>
        /// 获取物理提交次数。
        /// </summary>
        internal int Commits { get; private set; }

        /// <summary>
        /// 获取物理回滚次数。
        /// </summary>
        internal int Rollbacks { get; private set; }

        /// <summary>
        /// 获取物理资源释放次数。
        /// </summary>
        internal int Disposals { get; private set; }

        /// <inheritdoc />
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        /// <inheritdoc />
        protected override DbConnection DbConnection => null;

        /// <inheritdoc />
        public override void Commit() => Commits++;

        /// <inheritdoc />
        public override void Rollback() => Rollbacks++;

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Disposals++;
            base.Dispose(disposing);
        }
    }
}
