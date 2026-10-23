using System.Runtime.ExceptionServices;
using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bing.Uow.Tests;

/// <summary>
/// 验证业务操作 root、参与范围、保存阶段与超时契约。
/// </summary>
public sealed class OperationScopeLifecycleTest
{
    [Fact]
    public async Task ParticipantFailure_WhenCaught_ShouldPreventOwnerCommit()
    {
        var services = new ServiceCollection();
        services.AddBingOperations();
        using var provider = services.BuildServiceProvider();
        using var serviceScope = provider.CreateScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var root = manager.BeginOperation();
        var resource = root.GetOrAddResource("orders", () => new CountingResource());
        var participant = manager.BeginOperation();

        try
        {
            throw new InvalidOperationException("inner failure");
        }
        catch (InvalidOperationException)
        {
            await participant.DisposeAsync();
        }

        root.IsRollbackOnly.ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(() => root.CompleteAsync());
        resource.CommitCount.ShouldBe(0);
        resource.RollbackCount.ShouldBe(1);
        await root.DisposeAsync();
    }

    [Fact]
    public async Task Complete_ShouldSaveBeforeCommittingResources()
    {
        var order = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(order);
        services.AddScoped<IUnitOfWorkSaveChangesHandler>(provider =>
            new RecordingSaveChangesHandler(provider.GetRequiredService<List<string>>()));
        services.AddBingOperations();
        using var provider = services.BuildServiceProvider();
        using var serviceScope = provider.CreateScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var root = manager.BeginOperation();

        await root.CompleteAsync();
        await root.DisposeAsync();

        order.ShouldBe(new[] { "save", "commit" });
    }

    [Fact]
    public async Task CommitFailure_ShouldKeepPartialOutcomeAndNotifyOnlyCommittedResource()
    {
        var services = new ServiceCollection();
        services.AddBingOperations();
        using var provider = services.BuildServiceProvider();
        using var serviceScope = provider.CreateScope();
        var root = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>()
            .BeginOperation();
        var first = root.GetOrAddResource("first", () => new OperationFaultResource());
        var commitFailure = new InvalidOperationException("second resource commit failed");
        var rollbackFailure = new InvalidOperationException("second resource rollback failed");
        var second = root.GetOrAddResource("second", () => new OperationFaultResource
        {
            CommitFailure = commitFailure,
            RollbackFailure = rollbackFailure
        });
        var third = root.GetOrAddResource("third", () => new OperationFaultResource());
        var notifications = new List<string>();
        root.OnResourceCommitted("first", () =>
        {
            notifications.Add("first");
            return Task.CompletedTask;
        });
        root.OnResourceCommitted("second", () =>
        {
            notifications.Add("second");
            return Task.CompletedTask;
        });
        root.OnResourceCommitted("third", () =>
        {
            notifications.Add("third");
            return Task.CompletedTask;
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => root.CompleteAsync());

        Assert.Same(commitFailure, exception);
        Assert.Contains(rollbackFailure,
            Assert.IsType<Exception[]>(exception.Data["Bing.Operation.CleanupExceptions"]));
        Assert.Equal(new[] { "first" }, notifications);
        Assert.Equal(1, first.CommitCount);
        Assert.Equal(0, first.RollbackCount);
        Assert.Equal(1, second.CommitCount);
        Assert.Equal(1, second.RollbackCount);
        Assert.Equal(CancellationToken.None, second.LastRollbackToken);
        Assert.Equal(0, third.CommitCount);
        Assert.Equal(1, third.RollbackCount);
        Assert.Equal(CancellationToken.None, third.LastRollbackToken);

        await root.DisposeAsync();
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.DisposeCount);
        Assert.Equal(1, third.DisposeCount);
        await root.DisposeAsync();
        Assert.Equal(1, second.RollbackCount);
        Assert.Equal(1, second.DisposeCount);
    }

    [Fact]
    public async Task NotificationFailure_ShouldNotRetryAfterOperationResourcesCommitted()
    {
        var services = new ServiceCollection();
        services.AddBingOperations();
        using var provider = services.BuildServiceProvider();
        using var serviceScope = provider.CreateScope();
        var root = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>()
            .BeginOperation();
        var resource = root.GetOrAddResource("resource", () => new OperationFaultResource());
        var notificationFailure = new InvalidOperationException("operation notification failed");
        var notificationCount = 0;
        root.OnResourceCommitted("resource", () =>
        {
            notificationCount++;
            return Task.FromException(notificationFailure);
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => root.CompleteAsync());

        Assert.Same(notificationFailure, exception);
        Assert.True(root.IsCompleted);
        await root.CompleteAsync();
        Assert.Equal(1, resource.CommitCount);
        Assert.Equal(0, resource.RollbackCount);
        Assert.Equal(1, notificationCount);
        await root.DisposeAsync();
        Assert.Equal(1, resource.DisposeCount);
    }

    [Fact]
    public async Task SaveCancellation_ShouldPreservePrimaryAndAttemptNonCancelableCleanupOnce()
    {
        using var cancellation = new CancellationTokenSource();
        var primary = new OperationCanceledException("save canceled", cancellation.Token);
        var rollbackFailure = new InvalidOperationException("rollback cleanup failed");
        var disposeFailure = new InvalidOperationException("dispose cleanup failed");
        var handler = new DelegateSaveChangesHandler(saveAsync: (_, _) =>
        {
            cancellation.Cancel();
            return Task.FromException(primary);
        });
        var services = new ServiceCollection();
        services.AddSingleton<IUnitOfWorkSaveChangesHandler>(handler);
        services.AddBingOperations();
        using var provider = services.BuildServiceProvider();
        using var serviceScope = provider.CreateScope();
        var root = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>()
            .BeginOperation();
        var resource = root.GetOrAddResource("resource", () => new OperationFaultResource
        {
            RollbackFailure = rollbackFailure,
            DisposeFailure = disposeFailure
        });
        var completedCallbacks = 0;
        root.OnCompleted(() =>
        {
            completedCallbacks++;
            return Task.CompletedTask;
        });

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => root.CompleteAsync());

        Assert.Same(primary, exception);
        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Contains(rollbackFailure,
            Assert.IsType<Exception[]>(exception.Data["Bing.Operation.CleanupExceptions"]));
        Assert.True(root.IsRollbackOnly);
        Assert.False(root.IsCompleted);
        Assert.Equal(0, resource.CommitCount);
        Assert.Equal(1, resource.RollbackCount);
        Assert.Equal(CancellationToken.None, resource.LastRollbackToken);
        Assert.Equal(0, completedCallbacks);

        var cleanupException = await Assert.ThrowsAsync<InvalidOperationException>(() => root.DisposeAsync().AsTask());
        Assert.Same(disposeFailure, cleanupException);
        Assert.Equal(1, resource.DisposeCount);
        Assert.Equal(1, resource.RollbackCount);
        await root.DisposeAsync();
        Assert.Equal(1, resource.DisposeCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveHandlerRollbackOnly_ShouldRollbackEveryResourceWithoutCallbacks(bool asynchronous)
    {
        var duringSave = new CountingResource();
        var handler = new DelegateSaveChangesHandler(
            scope =>
            {
                scope.GetOrAddResource("during-save", () => duringSave);
                scope.SetRollbackOnly();
            });
        var services = new ServiceCollection();
        services.AddSingleton<IUnitOfWorkSaveChangesHandler>(handler);
        services.AddBingOperations();
        using var provider = services.BuildServiceProvider();
        using var serviceScope = provider.CreateScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var root = manager.BeginOperation();
        var beforeSave = root.GetOrAddResource("before-save", () => new CountingResource());
        var completedCallbacks = 0;
        root.OnCompleted(() =>
        {
            completedCallbacks++;
            return Task.CompletedTask;
        });

        if (asynchronous)
            await Should.ThrowAsync<InvalidOperationException>(() => root.CompleteAsync());
        else
            Should.Throw<InvalidOperationException>(() => root.Complete());

        root.IsRollbackOnly.ShouldBeTrue();
        beforeSave.CommitCount.ShouldBe(0);
        beforeSave.RollbackCount.ShouldBe(1);
        duringSave.CommitCount.ShouldBe(0);
        duringSave.RollbackCount.ShouldBe(1);
        completedCallbacks.ShouldBe(0);
        await root.DisposeAsync();
        beforeSave.DisposeCount.ShouldBe(1);
        duringSave.DisposeCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CapturedWriteFailureMarkedRollbackOnly_ShouldNotCommit(bool asynchronous)
    {
        var writeFailure = new InvalidOperationException("captured write failure");
        Action<IUnitOfWorkScope> save = scope =>
        {
            try
            {
                throw writeFailure;
            }
            catch (InvalidOperationException)
            {
                scope.SetRollbackOnly();
            }
        };
        var services = new ServiceCollection();
        services.AddSingleton<IUnitOfWorkSaveChangesHandler>(new DelegateSaveChangesHandler(save));
        services.AddBingOperations();
        using var provider = services.BuildServiceProvider();
        using var serviceScope = provider.CreateScope();
        var root = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>().BeginOperation();
        var resource = root.GetOrAddResource("write", () => new CountingResource());

        if (asynchronous)
            await Should.ThrowAsync<InvalidOperationException>(() => root.CompleteAsync());
        else
            Should.Throw<InvalidOperationException>(() => root.Complete());

        resource.CommitCount.ShouldBe(0);
        resource.RollbackCount.ShouldBe(1);
        await root.DisposeAsync();
        resource.DisposeCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("rollback", false)]
    [InlineData("rollback", true)]
    [InlineData("complete", false)]
    [InlineData("complete", true)]
    [InlineData("dispose", false)]
    [InlineData("dispose", true)]
    [InlineData("participant", false)]
    [InlineData("participant", true)]
    public async Task ReentrantRootOperationDuringSave_ShouldBeRejectedAndRollback(string action, bool asynchronous)
    {
        IUnitOfWorkOperationManager manager = null;
        Action<IUnitOfWorkScope> save = scope =>
        {
            switch (action)
            {
                case "rollback":
                    Should.Throw<InvalidOperationException>(() => scope.Rollback());
                    break;
                case "complete":
                    Should.Throw<InvalidOperationException>(() => scope.Complete());
                    break;
                case "dispose":
                    Should.Throw<InvalidOperationException>(() => scope.Dispose());
                    break;
                case "participant":
                    Should.Throw<InvalidOperationException>(() => manager.BeginOperation());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action));
            }
        };
        var services = new ServiceCollection();
        services.AddSingleton<IUnitOfWorkSaveChangesHandler>(new DelegateSaveChangesHandler(save));
        services.AddBingOperations();
        using var provider = services.BuildServiceProvider();
        using var serviceScope = provider.CreateScope();
        manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var root = manager.BeginOperation();
        var resource = root.GetOrAddResource("resource", () => new CountingResource());

        if (asynchronous)
            await Should.ThrowAsync<InvalidOperationException>(() => root.CompleteAsync());
        else
            Should.Throw<InvalidOperationException>(() => root.Complete());

        root.IsRollbackOnly.ShouldBeTrue();
        root.IsCompleted.ShouldBeFalse();
        resource.CommitCount.ShouldBe(0);
        resource.RollbackCount.ShouldBe(1);
        await root.DisposeAsync();
        resource.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task SavingHandlerPending_ShouldRejectOtherExecutionFlowAdmission()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new DelegateSaveChangesHandler(
            saveAsync: async (_, _) =>
            {
                entered.TrySetResult();
                await release.Task.ConfigureAwait(false);
            });
        var services = new ServiceCollection();
        services.AddSingleton<IUnitOfWorkSaveChangesHandler>(handler);
        services.AddBingOperations();
        using var provider = services.BuildServiceProvider();
        using var serviceScope = provider.CreateScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var root = manager.BeginOperation();
        var resource = root.GetOrAddResource("root", () => new CountingResource());
        var lateFactoryCalls = 0;
        var completedCallbacks = 0;
        root.OnCompleted(() =>
        {
            completedCallbacks++;
            return Task.CompletedTask;
        });

        var completion = root.CompleteAsync();
        await entered.Task;
        Should.Throw<InvalidOperationException>(() => root.GetOrAddResource("late", () =>
        {
            lateFactoryCalls++;
            return new CountingResource();
        }));
        Should.Throw<InvalidOperationException>(() => manager.BeginOperation());
        Should.Throw<InvalidOperationException>(() => root.DisposeAsync());
        lateFactoryCalls.ShouldBe(0);
        release.TrySetResult();

        await Should.ThrowAsync<InvalidOperationException>(() => completion);
        root.IsRollbackOnly.ShouldBeTrue();
        resource.CommitCount.ShouldBe(0);
        resource.RollbackCount.ShouldBe(1);
        completedCallbacks.ShouldBe(0);
        await root.DisposeAsync();
        resource.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task ParticipantCompleteAsync_WithNonCancelledArgument_ShouldPreserveParticipantCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        using var argumentCancellation = new CancellationTokenSource();
        var services = new ServiceCollection();
        services.AddBingOperations();
        using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var root = manager.BeginOperation();
        var participant = manager.BeginOperation(cancellationToken: cancellation.Token);
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => participant.CompleteAsync(argumentCancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.True(root.IsRollbackOnly);
        await participant.DisposeAsync();
        await root.DisposeAsync();
    }

    [Fact]
    public async Task ParticipantComplete_WithCancelledParticipant_ShouldRejectSynchronousCompletion()
    {
        using var cancellation = new CancellationTokenSource();
        var services = new ServiceCollection();
        services.AddBingOperations();
        using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var root = manager.BeginOperation();
        var participant = manager.BeginOperation(cancellationToken: cancellation.Token);
        cancellation.Cancel();

        var exception = Assert.ThrowsAny<OperationCanceledException>(() => participant.Complete());

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.True(root.IsRollbackOnly);
        await participant.DisposeAsync();
        await root.DisposeAsync();
    }

    [Fact]
    public async Task NestedParticipant_ShouldInheritParentParticipantCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var services = new ServiceCollection();
        services.AddBingOperations();
        using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var root = manager.BeginOperation();
        var parent = manager.BeginOperation(cancellationToken: cancellation.Token);
        var nested = manager.BeginOperation();

        cancellation.Cancel();

        Assert.True(nested.CancellationToken.IsCancellationRequested);
        Assert.Same(root, manager.Current);
        await nested.RollbackAsync();
        await nested.DisposeAsync();
        await parent.RollbackAsync();
        await parent.DisposeAsync();
        await root.DisposeAsync();
    }

    [Fact]
    public void BeginOperation_ShouldSnapshotOptionsAndComposeParticipantCancellation()
    {
        var services = new ServiceCollection();
        services.AddBingOperations();
        using var provider = services.BuildServiceProvider();
        using var serviceScope = provider.CreateScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        using var rootCaller = new CancellationTokenSource();
        using var participantCaller = new CancellationTokenSource();
        var options = new UnitOfWorkScopeOptions { Timeout = TimeSpan.FromSeconds(10) };
        var root = manager.BeginOperation(options, rootCaller.Token);
        options.Timeout = TimeSpan.FromSeconds(20);
        var participant = manager.BeginOperation(new UnitOfWorkScopeOptions
        {
            Timeout = TimeSpan.FromSeconds(10)
        }, participantCaller.Token);

        root.Options.Timeout.ShouldBe(TimeSpan.FromSeconds(10));
        participant.Options.Timeout.ShouldBe(TimeSpan.FromSeconds(10));
        root.CancellationToken.CanBeCanceled.ShouldBeTrue();
        participant.CancellationToken.CanBeCanceled.ShouldBeTrue();
        participantCaller.Cancel();
        participant.CancellationToken.IsCancellationRequested.ShouldBeTrue();
        root.CancellationToken.IsCancellationRequested.ShouldBeFalse();

        participant.Dispose();
        root.Dispose();
    }

    [Fact]
    public async Task Timeout_ShouldCancelRootWithoutRenewingParticipantDeadline()
    {
        var services = new ServiceCollection();
        services.AddBingOperations();
        using var provider = services.BuildServiceProvider();
        using var serviceScope = provider.CreateScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var root = manager.BeginOperation(new UnitOfWorkScopeOptions
        {
            Timeout = TimeSpan.FromMilliseconds(120)
        });
        await Task.Delay(TimeSpan.FromMilliseconds(80));
        var participant = manager.BeginOperation(new UnitOfWorkScopeOptions
        {
            Timeout = TimeSpan.FromMilliseconds(120)
        });
        await Task.Delay(TimeSpan.FromMilliseconds(80));

        root.CancellationToken.IsCancellationRequested.ShouldBeTrue();
        participant.CancellationToken.IsCancellationRequested.ShouldBeTrue();
        await participant.DisposeAsync();
        await Should.ThrowAsync<InvalidOperationException>(() => root.CompleteAsync());
        await root.DisposeAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BeginOperation_WithNonPositiveTimeout_ShouldReject(long milliseconds)
    {
        var services = new ServiceCollection();
        services.AddBingOperations();
        using var provider = services.BuildServiceProvider();
        using var serviceScope = provider.CreateScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();

        Should.Throw<ArgumentOutOfRangeException>(() => manager.BeginOperation(new UnitOfWorkScopeOptions
        {
            Timeout = TimeSpan.FromMilliseconds(milliseconds)
        }));
    }

    private sealed class RecordingSaveChangesHandler : IUnitOfWorkSaveChangesHandler
    {
        private readonly List<string> _order;

        public RecordingSaveChangesHandler(List<string> order) => _order = order;

        public void SaveChanges(IUnitOfWorkScope scope)
        {
            scope.IsActive.ShouldBeTrue();
            _order.Add("save");
            scope.GetOrAddResource("during-save", () => new OrderedResource(_order));
        }

        public Task SaveChangesAsync(IUnitOfWorkScope scope, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveChanges(scope);
            return Task.CompletedTask;
        }
    }

    private sealed class CountingResource : IUnitOfWorkResource
    {
        public int CommitCount { get; private set; }
        public int RollbackCount { get; private set; }
        public int DisposeCount { get; private set; }
        public void Commit() => CommitCount++;
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CommitCount++;
            return Task.CompletedTask;
        }
        public void Rollback() => RollbackCount++;
        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCount++;
            return Task.CompletedTask;
        }
        public void Dispose() => DisposeCount++;
        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DelegateSaveChangesHandler : IUnitOfWorkSaveChangesHandler
    {
        private readonly Action<IUnitOfWorkScope> _save;
        private readonly Func<IUnitOfWorkScope, CancellationToken, Task> _saveAsync;

        public DelegateSaveChangesHandler(Action<IUnitOfWorkScope> save = null,
            Func<IUnitOfWorkScope, CancellationToken, Task> saveAsync = null)
        {
            _save = save ?? (_ => { });
            _saveAsync = saveAsync ?? ((scope, token) =>
            {
                token.ThrowIfCancellationRequested();
                _save(scope);
                return Task.CompletedTask;
            });
        }

        public void SaveChanges(IUnitOfWorkScope scope) => _save(scope);

        public Task SaveChangesAsync(IUnitOfWorkScope scope, CancellationToken cancellationToken = default) =>
            _saveAsync(scope, cancellationToken);
    }

    private sealed class OperationFaultResource : IUnitOfWorkResource
    {
        public Exception CommitFailure { get; init; }
        public Exception RollbackFailure { get; init; }
        public Exception DisposeFailure { get; init; }
        public int CommitCount { get; private set; }
        public int RollbackCount { get; private set; }
        public int DisposeCount { get; private set; }
        public CancellationToken LastRollbackToken { get; private set; }

        public void Commit()
        {
            CommitCount++;
            if (CommitFailure != null)
                ExceptionDispatchInfo.Capture(CommitFailure).Throw();
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commit();
            return Task.CompletedTask;
        }

        public void Rollback()
        {
            RollbackCount++;
            if (RollbackFailure != null)
                ExceptionDispatchInfo.Capture(RollbackFailure).Throw();
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
            if (DisposeFailure != null)
                ExceptionDispatchInfo.Capture(DisposeFailure).Throw();
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class OrderedResource : IUnitOfWorkResource
    {
        private readonly List<string> _order;
        public OrderedResource(List<string> order) => _order = order;
        public void Commit() => _order.Add("commit");
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commit();
            return Task.CompletedTask;
        }
        public void Rollback() { }
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
