using System.Data;
using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bing.Uow.Tests;

/// <summary>
/// 验证 root 工作单元的资源登记、提交状态和清理契约。
/// </summary>
public class UnitOfWorkScopeTest
{
    /// <summary>
    /// 验证同 root 同键命中，同 root 不同键和不同 root 均隔离。
    /// </summary>
    [Fact]
    public void GetOrAdd_ShouldHitMissAndIsolateRoots()
    {
        var (provider, serviceScope, manager) = CreateManager();
        using (provider)
        using (serviceScope)
        {
            var root = manager.Begin();
            var first = root.GetOrAddResource("orders", () => new TestResource());
            var hit = root.GetOrAddResource("orders", () => new TestResource());
            var secondKey = root.GetOrAddResource("reporting", () => new TestResource());
            var inner = manager.Begin(requiresNew: true);
            var independent = inner.GetOrAddResource("orders", () => new TestResource());

            ReferenceEquals(first, hit).ShouldBeTrue();
            ReferenceEquals(first, secondKey).ShouldBeFalse();
            ReferenceEquals(first, independent).ShouldBeFalse();
            Should.Throw<InvalidOperationException>(() =>
                root.GetOrAddResource("orders", () => new OtherTestResource()));

            inner.Rollback();
            inner.Dispose();
            root.Rollback();
            root.Dispose();
        }
    }

    /// <summary>
    /// 验证资源工厂失败不会留下登记项，之后可再次创建。
    /// </summary>
    [Fact]
    public void GetOrAdd_WhenFactoryThrows_ShouldNotRegister()
    {
        var (provider, serviceScope, manager) = CreateManager();
        using (provider)
        using (serviceScope)
        {
            var root = manager.Begin();
            Should.Throw<InvalidOperationException>(() =>
                root.GetOrAddResource<TestResource>("orders", () => throw new InvalidOperationException("factory")));
            var resource = root.GetOrAddResource("orders", () => new TestResource());
            resource.ShouldNotBeNull();

            root.Rollback();
            root.Dispose();
        }
    }

    /// <summary>
    /// 验证同一资源只允许一个活动执行，完成在活动租约期间 fail fast。
    /// </summary>
    [Fact]
    public void AcquireExecutionLease_ShouldRejectConcurrentUseAndAllowRetryAfterRelease()
    {
        var (provider, serviceScope, manager) = CreateManager();
        using (provider)
        using (serviceScope)
        {
            var root = manager.Begin();
            root.GetOrAddResource("orders", () => new TestResource());
            var lease = root.AcquireExecutionLease("orders");
            Should.Throw<InvalidOperationException>(() => root.AcquireExecutionLease("orders"));
            Should.Throw<InvalidOperationException>(() => root.Complete());
            root.IsCompleted.ShouldBeFalse();

            lease.Dispose();
            lease.Dispose();
            using (root.AcquireExecutionLease("orders")) { }
            root.Complete();
            root.Dispose();
        }
    }

    /// <summary>
    /// 验证未完成的参与式范围会将 root 标记为 rollback-only。
    /// </summary>
    [Fact]
    public void Complete_WhenParticipantIncomplete_ShouldRollback()
    {
        var (provider, serviceScope, manager) = CreateManager();
        using (provider)
        using (serviceScope)
        {
            var root = manager.Begin();
            var resource = root.GetOrAddResource("orders", () => new TestResource());
            var participant = manager.Begin();
            participant.Dispose();

            root.IsRollbackOnly.ShouldBeTrue();
            Should.Throw<InvalidOperationException>(() => root.Complete());
            resource.RollbackCount.ShouldBe(1);
            resource.CommitCount.ShouldBe(0);
            root.IsCompleted.ShouldBeFalse();

            root.Dispose();
            resource.RollbackCount.ShouldBe(1);
        }
    }

    /// <summary>
    /// 验证第二个资源提交失败时保留首库结果并回滚尚未提交资源。
    /// </summary>
    [Fact]
    public async Task Complete_WhenSecondResourceFails_ShouldReportPartialCommit()
    {
        var (provider, serviceScope, manager) = CreateManager();
        using (provider)
        using (serviceScope)
        {
            var root = manager.Begin();
            var first = root.GetOrAddResource("first", () => new TestResource());
            var second = root.GetOrAddResource("second", () => new TestResource
            {
                CommitAction = () => throw new InvalidOperationException("commit-second")
            });
            var third = root.GetOrAddResource("third", () => new TestResource());
            var notifications = new List<string>();
            root.OnResourceCommitted("first", () =>
            {
                notifications.Add("first");
                return Task.FromException(new InvalidOperationException("notification-first"));
            });
            root.OnResourceCommitted("second", () => { notifications.Add("second"); return Task.CompletedTask; });
            root.OnResourceCommitted("third", () => { notifications.Add("third"); return Task.CompletedTask; });
            root.OnCompleted(() => { notifications.Add("all"); return Task.CompletedTask; });

            var exception = await Should.ThrowAsync<AggregateException>(() => root.CompleteAsync());
            exception.Flatten().InnerExceptions.Select(item => item.Message)
                .ShouldContain("commit-second");
            exception.Flatten().InnerExceptions.Select(item => item.Message)
                .ShouldContain("notification-first");
            first.CommitAsyncCount.ShouldBe(1);
            second.CommitAsyncCount.ShouldBe(1);
            third.CommitAsyncCount.ShouldBe(0);
            first.RollbackCount.ShouldBe(0);
            second.RollbackCount.ShouldBe(1);
            third.RollbackCount.ShouldBe(1);
            notifications.ShouldBe(new[] { "first" });
            root.IsCompleted.ShouldBeFalse();

            await root.DisposeAsync();
            first.DisposeAsyncCount.ShouldBe(1);
            second.DisposeAsyncCount.ShouldBe(1);
            third.DisposeAsyncCount.ShouldBe(1);
        }
    }

    /// <summary>
    /// 验证取消的提交会回滚资源并使用不可取消令牌清理。
    /// </summary>
    [Fact]
    public async Task Cancellation_ShouldRollbackRemainingWithNone()
    {
        var (provider, serviceScope, manager) = CreateManager();
        using (provider)
        using (serviceScope)
        {
            var root = manager.Begin();
            var resource = root.GetOrAddResource("orders", () => new TestResource());
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Should.ThrowAsync<OperationCanceledException>(() => root.CompleteAsync(cancellation.Token));
            resource.RollbackCount.ShouldBe(1);
            resource.LastRollbackToken.ShouldBe(CancellationToken.None);
            root.IsCompleted.ShouldBeFalse();

            await root.DisposeAsync();
            resource.RollbackCount.ShouldBe(1);
        }
    }

    /// <summary>
    /// 验证逐资源回调在对应提交后执行，全局回调等待全部提交且通知失败不重提。
    /// </summary>
    [Fact]
    public async Task NotificationFailure_ShouldKeepCompletedAndNotRetry()
    {
        var (provider, serviceScope, manager) = CreateManager();
        using (provider)
        using (serviceScope)
        {
            var root = manager.Begin();
            var resource = root.GetOrAddResource("orders", () => new TestResource());
            var callbackCount = 0;
            root.OnResourceCommitted("orders", () =>
            {
                callbackCount++;
                return Task.FromException(new InvalidOperationException("notification"));
            });

            await Should.ThrowAsync<InvalidOperationException>(() => root.CompleteAsync());
            root.IsCompleted.ShouldBeTrue();
            await root.CompleteAsync();
            callbackCount.ShouldBe(1);
            resource.CommitAsyncCount.ShouldBe(1);
            resource.RollbackCount.ShouldBe(0);

            await root.DisposeAsync();
        }
    }

    /// <summary>
    /// 验证全局完成回调只在所有资源提交后执行一次，回滚和提交失败均不触发。
    /// </summary>
    [Fact]
    public async Task OnCompleted_ShouldRunOnceAfterCommitAndNeverAfterRollbackOrCommitFailure()
    {
        var (provider, serviceScope, manager) = CreateManager();
        using (provider)
        using (serviceScope)
        {
            var committed = manager.Begin();
            var committedResource = committed.GetOrAddResource("committed", () => new TestResource());
            var callbackCalls = 0;
            committed.OnCompleted(() =>
            {
                committedResource.CommitAsyncCount.ShouldBe(1);
                callbackCalls++;
                return Task.CompletedTask;
            });
            callbackCalls.ShouldBe(0);
            await committed.CompleteAsync();
            await committed.CompleteAsync();
            callbackCalls.ShouldBe(1);
            await committed.DisposeAsync();

            var rolledBack = manager.Begin();
            var rollbackCallbackCalls = 0;
            rolledBack.OnCompleted(() =>
            {
                rollbackCallbackCalls++;
                return Task.CompletedTask;
            });
            await rolledBack.RollbackAsync();
            await Should.ThrowAsync<InvalidOperationException>(() => rolledBack.CompleteAsync());
            rollbackCallbackCalls.ShouldBe(0);
            await rolledBack.DisposeAsync();

            var failedCommit = manager.Begin();
            var failedResource = failedCommit.GetOrAddResource("failed", () => new TestResource
            {
                CommitAction = () => throw new InvalidOperationException("commit-failed")
            });
            var failedCallbackCalls = 0;
            failedCommit.OnCompleted(() =>
            {
                failedCallbackCalls++;
                return Task.CompletedTask;
            });
            await Should.ThrowAsync<InvalidOperationException>(() => failedCommit.CompleteAsync());
            await Should.ThrowAsync<InvalidOperationException>(() => failedCommit.CompleteAsync());
            failedCallbackCalls.ShouldBe(0);
            failedResource.CommitAsyncCount.ShouldBe(1);
            await failedCommit.DisposeAsync();
        }
    }

    /// <summary>
    /// 验证 DisposeAsync 在等待资源异步清理前已恢复调用方 Current。
    /// </summary>
    [Fact]
    public async Task DisposeAsync_ShouldRestoreCallerBeforeAsyncCleanup()
    {
        var (provider, serviceScope, manager) = CreateManager();
        using (provider)
        using (serviceScope)
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var root = manager.Begin();
            var resource = root.GetOrAddResource("orders", () => new TestResource
            {
                DisposeAsyncTask = gate.Task
            });

            var dispose = root.DisposeAsync();
            manager.Current.ShouldBeNull();
            resource.DisposeAsyncCount.ShouldBe(1);
            dispose.IsCompleted.ShouldBeFalse();
            gate.SetResult();
            await dispose;
        }
    }

    /// <summary>
    /// 验证 LIFO 错误不改变当前范围，选项快照和资源成功回调顺序稳定。
    /// </summary>
    [Fact]
    public async Task OutOfOrderDispose_ShouldRejectAndOptionsShouldBeCopied()
    {
        var (provider, serviceScope, manager) = CreateManager();
        using (provider)
        using (serviceScope)
        {
            var options = new UnitOfWorkScopeOptions { IsolationLevel = IsolationLevel.Serializable };
            var root = manager.Begin(options);
            options.IsolationLevel = IsolationLevel.ReadCommitted;
            root.Options.IsolationLevel.ShouldBe(IsolationLevel.Serializable);
            var inner = manager.Begin(requiresNew: true);

            Should.Throw<InvalidOperationException>(() => root.Dispose());
            manager.Current.Id.ShouldBe(inner.Id);
            await inner.CompleteAsync();
            await inner.DisposeAsync();
            root.Rollback();
            root.Dispose();
        }
    }

    /// <summary>
    /// 验证父执行上下文不会被子任务弹出参与式范围的操作清除。
    /// </summary>
    [Fact]
    public async Task ChildTaskPop_ShouldNotClearParent()
    {
        var (provider, serviceScope, manager) = CreateManager();
        using (provider)
        using (serviceScope)
        {
            var root = manager.Begin();
            var id = await Task.Run(async () =>
            {
                var participant = manager.Begin();
                await Task.Yield();
                participant.Complete();
                await participant.DisposeAsync();
                return manager.Current.Id;
            });

            id.ShouldBe(root.Id);
            manager.Current.Id.ShouldBe(root.Id);
            root.Rollback();
            root.Dispose();
        }
    }

    /// <summary>
    /// 验证继承终态 frame 的延迟子任务不会回落到独立连接工作模式。
    /// </summary>
    [Fact]
    public async Task InheritedEndedFrame_ShouldRejectStandaloneFallback()
    {
        var (provider, serviceScope, manager) = CreateManager();
        using (provider)
        using (serviceScope)
        {
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var root = manager.Begin();
            var inherited = Task.Run(async () =>
            {
                ready.SetResult();
                await resume.Task.ConfigureAwait(false);
                manager.Current.ShouldNotBeNull();
                Should.Throw<InvalidOperationException>(() => manager.Begin());
            });

            await ready.Task;
            root.Rollback();
            root.Dispose();
            resume.SetResult();
            await inherited;
            manager.Current.ShouldBeNull();
        }
    }

    /// <summary>
    /// 验证无效隔离级别和参与式选项冲突在安装新 frame 前被拒绝。
    /// </summary>
    [Fact]
    public void Begin_ShouldRejectInvalidOrConflictingOptions()
    {
        var (provider, serviceScope, manager) = CreateManager();
        using (provider)
        using (serviceScope)
        {
            Should.Throw<ArgumentOutOfRangeException>(() => manager.Begin(
                new UnitOfWorkScopeOptions { IsolationLevel = IsolationLevel.Unspecified }));
            Should.Throw<ArgumentException>(() => manager.Begin(
                new UnitOfWorkScopeOptions { IsTransactional = false, IsolationLevel = IsolationLevel.ReadCommitted }));
            manager.Current.ShouldBeNull();

            var root = manager.Begin(new UnitOfWorkScopeOptions { IsolationLevel = IsolationLevel.Serializable });
            Should.Throw<InvalidOperationException>(() => manager.Begin(
                new UnitOfWorkScopeOptions { IsolationLevel = IsolationLevel.ReadCommitted }));
            manager.Current.Id.ShouldBe(root.Id);
            root.Rollback();
            root.Dispose();
        }
    }

    /// <summary>
    /// 验证清理失败仍尝试每个资源，且后续 Dispose 不重复回滚或释放。
    /// </summary>
    [Fact]
    public void CleanupFailure_ShouldAttemptEveryResourceOnce()
    {
        var (provider, serviceScope, manager) = CreateManager();
        using (provider)
        using (serviceScope)
        {
            var root = manager.Begin();
            var first = root.GetOrAddResource("first", () => new TestResource
            {
                RollbackAction = () => throw new InvalidOperationException("rollback-first"),
                DisposeAction = () => throw new InvalidOperationException("dispose-first")
            });
            var second = root.GetOrAddResource("second", () => new TestResource
            {
                RollbackAction = () => throw new InvalidOperationException("rollback-second"),
                DisposeAction = () => throw new InvalidOperationException("dispose-second")
            });

            Should.Throw<AggregateException>(() => root.Dispose());
            root.IsDisposed.ShouldBeTrue();
            first.RollbackCount.ShouldBe(1);
            second.RollbackCount.ShouldBe(1);
            first.DisposeCount.ShouldBe(1);
            second.DisposeCount.ShouldBe(1);

            root.Dispose();
            first.RollbackCount.ShouldBe(1);
            first.DisposeCount.ShouldBe(1);
        }
    }

    private static (ServiceProvider Provider, IServiceScope Scope, IUnitOfWorkScopeManager Manager) CreateManager()
    {
        var services = new ServiceCollection();
        services.AddUnitOfWorkScopes();
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        return (provider, scope, scope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>());
    }

    private sealed class TestResource : IUnitOfWorkResource
    {
        public Action CommitAction { get; init; }
        public Action RollbackAction { get; init; }
        public Action DisposeAction { get; init; }
        public Task DisposeAsyncTask { get; init; } = Task.CompletedTask;
        public int CommitCount { get; private set; }
        public int CommitAsyncCount { get; private set; }
        public int RollbackCount { get; private set; }
        public int DisposeCount { get; private set; }
        public int DisposeAsyncCount { get; private set; }
        public CancellationToken LastRollbackToken { get; private set; }

        public void Commit()
        {
            CommitCount++;
            CommitAction?.Invoke();
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitAsyncCount++;
            cancellationToken.ThrowIfCancellationRequested();
            CommitAction?.Invoke();
            return Task.CompletedTask;
        }

        public void Rollback()
        {
            RollbackCount++;
            RollbackAction?.Invoke();
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            LastRollbackToken = cancellationToken;
            Rollback();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            DisposeCount++;
            DisposeAction?.Invoke();
        }

        public async ValueTask DisposeAsync()
        {
            DisposeAsyncCount++;
            await DisposeAsyncTask.ConfigureAwait(false);
        }
    }

    private sealed class OtherTestResource : IUnitOfWorkResource
    {
        public void Commit() { }
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Rollback() { }
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => default;
    }
}
