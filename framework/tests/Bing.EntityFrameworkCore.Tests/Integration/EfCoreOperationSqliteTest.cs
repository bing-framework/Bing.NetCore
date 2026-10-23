using Bing.Uow;
using Bing.Data;
using Bing.EntityFrameworkCore.Tests.Core;
using Bing.Data.Transaction;
using Bing.Data.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Data.Sqlite;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// 使用 SQLite 验证自动 operation 的 EF 保存、回滚和非事务路径。
/// </summary>
public sealed class EfCoreOperationSqliteTest
{
    [Theory]
    [InlineData(StoreCancellationSource.OwnerCaller)]
    [InlineData(StoreCancellationSource.Deadline)]
    [InlineData(StoreCancellationSource.ParticipantCaller)]
    public async Task SynchronousStoreCount_WhenOperationTokenIsCancelled_ShouldRejectBeforeSqliteIo(
        StoreCancellationSource cancellationSource)
    {
        using var counters = new EfSqliteLifecycleCounters();
        using var commandCounter = new SqliteCommandCounter();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true, commandInterceptor: commandCounter);
        IUnitOfWorkExecutionScope owner = null;
        IUnitOfWorkExecutionScope participant = null;
        CancellationToken expectedCancellationToken = default;
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            counters.Track(context);
            var initialCommands = commandCounter.CommandCount;
            var initialCommits = counters.TransactionsCommitted;
            var manager = services.GetRequiredService<IUnitOfWorkOperationManager>();
            using var ownerCaller = new CancellationTokenSource();
            owner = cancellationSource switch
            {
                StoreCancellationSource.OwnerCaller => manager.BeginOperation(cancellationToken: ownerCaller.Token),
                StoreCancellationSource.Deadline => manager.BeginOperation(
                    new UnitOfWorkScopeOptions { Timeout = TimeSpan.FromMilliseconds(1) }),
                _ => manager.BeginOperation()
            };

            if (cancellationSource == StoreCancellationSource.OwnerCaller)
                ownerCaller.Cancel();
            else if (cancellationSource == StoreCancellationSource.Deadline)
                Assert.True(owner.CancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(5)));
            else
            {
                using var participantCaller = new CancellationTokenSource();
                participant = manager.BeginOperation(cancellationToken: participantCaller.Token);
                expectedCancellationToken = participant.CancellationToken;
                participantCaller.Cancel();
            }

            if (cancellationSource != StoreCancellationSource.ParticipantCaller)
                expectedCancellationToken = owner.CancellationToken;

            var store = new EfSqliteOperationStore(context);
            var countError = Record.Exception(() => store.Count());
            var listError = Record.Exception(() => store.FindAll());
            var findError = Record.Exception(() => store.FindById(1));
            var rootRollbackOnly = owner.IsRollbackOnly;
            if (participant != null)
            {
                await participant.DisposeAsync();
                participant = null;
            }
            var completionError = await Record.ExceptionAsync(() => owner.CompleteAsync());
            await owner.DisposeAsync();
            owner = null;

            Assert.Equal(expectedCancellationToken, Assert.IsType<OperationCanceledException>(countError).CancellationToken);
            Assert.Equal(expectedCancellationToken, Assert.IsType<OperationCanceledException>(listError).CancellationToken);
            Assert.Equal(expectedCancellationToken, Assert.IsType<OperationCanceledException>(findError).CancellationToken);
            Assert.True(rootRollbackOnly);
            Assert.IsType<InvalidOperationException>(completionError);
            Assert.Equal(initialCommands, commandCounter.CommandCount);
            Assert.Equal(initialCommits, counters.TransactionsCommitted);
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
        }
        finally
        {
            if (participant != null)
                await participant.DisposeAsync();
            if (owner != null)
                await owner.DisposeAsync();
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task SynchronousStoreReads_WhenOperationIsActiveOrAbsent_ShouldRunNormally()
    {
        using var commandCounter = new SqliteCommandCounter();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            addBingOperations: true, commandInterceptor: commandCounter);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            context.Records.Add(new EfSqliteUowRecord { Id = 1499, Name = "sync-store-control" });
            await context.SaveChangesAsync();

            var store = new EfSqliteOperationStore(context);
            Assert.Equal(1, store.Count());
            Assert.Equal(new[] { "sync-store-control" }, store.FindAll().Select(record => record.Name));
            Assert.Equal("sync-store-control", store.FindById(1499)?.Name);

            var operation = services.GetRequiredService<IUnitOfWorkOperationManager>().BeginOperation();
            Assert.Equal(1, store.Count());
            Assert.Equal(new[] { "sync-store-control" }, store.FindAll().Select(record => record.Name));
            Assert.Equal("sync-store-control", store.FindById(1499)?.Name);
            await operation.CompleteAsync();
            await operation.DisposeAsync();
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RequiresNewSameScope_ExplicitSaveBeforeHandler_ShouldRejectOuterPendingEntity(
        bool asynchronous, bool useCommitAlias)
    {
        using var counters = new EfSqliteLifecycleCounters();
        using var commandCounter = new SqliteCommandCounter();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true, commandInterceptor: commandCounter);
        IUnitOfWorkExecutionScope outer = null;
        IUnitOfWorkScope inner = null;
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            counters.Track(context);
            var initialCommands = commandCounter.CommandCount;
            var initialCommits = counters.TransactionsCommitted;
            var manager = services.GetRequiredService<IUnitOfWorkOperationManager>();
            outer = manager.BeginOperation();
            context.Records.Add(new EfSqliteUowRecord { Id = 1501, Name = "outer-explicit-pending" });
            inner = manager.Begin(requiresNew: true);

            Exception saveError = null;
            try
            {
                if (asynchronous)
                {
                    if (useCommitAlias)
                        await context.CommitAsync();
                    else
                        await context.SaveChangesAsync();
                }
                else if (useCommitAlias)
                    context.Commit();
                else
                    context.SaveChanges();

                await inner.CompleteAsync();
            }
            catch (Exception error)
            {
                saveError = error;
            }

            var innerRollbackOnly = inner.IsRollbackOnly;
            await inner.DisposeAsync();
            inner = null;
            await outer.RollbackAsync();
            await outer.DisposeAsync();
            outer = null;

            Assert.IsType<InvalidOperationException>(saveError);
            Assert.True(innerRollbackOnly);
            Assert.Equal(initialCommands, commandCounter.CommandCount);
            Assert.Equal(initialCommits, counters.TransactionsCommitted);
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
        }
        finally
        {
            if (inner != null)
                await inner.DisposeAsync();
            if (outer != null)
                await outer.DisposeAsync();
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequiresNewSameScope_InnerOnlyContextChanges_ShouldCommitAfterOuterRollback(bool asynchronous)
    {
        using var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true);
        IUnitOfWorkExecutionScope outer = null;
        IUnitOfWorkScope inner = null;
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            counters.Track(context);
            var manager = services.GetRequiredService<IUnitOfWorkOperationManager>();
            outer = manager.BeginOperation();
            inner = manager.Begin(requiresNew: true);
            context.Records.Add(new EfSqliteUowRecord { Id = 1502, Name = "inner-only" });

            if (asynchronous)
                await inner.CompleteAsync();
            else
                inner.Complete();
            await inner.DisposeAsync();
            inner = null;
            await outer.RollbackAsync();
            await outer.DisposeAsync();
            outer = null;

            Assert.Equal(new[] { "inner-only" }, await ReadRecordNamesAsync(setup.ConnectionString));
            Assert.Equal(1, counters.TransactionsCommitted);
        }
        finally
        {
            if (inner != null)
                await inner.DisposeAsync();
            if (outer != null)
                await outer.DisposeAsync();
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task RequiresNewSameScope_QueryFirstOuterClaim_ShouldRejectInnerCommitBeforeSqliteIo()
    {
        using var counters = new EfSqliteLifecycleCounters();
        using var commandCounter = new SqliteCommandCounter();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true, commandInterceptor: commandCounter);
        IUnitOfWorkExecutionScope outer = null;
        IUnitOfWorkScope inner = null;
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            counters.Track(context);
            var manager = services.GetRequiredService<IUnitOfWorkOperationManager>();
            outer = manager.BeginOperation();
            Assert.Equal(1, new EfSqliteOperationStore(context).QueryFirstClaim());
            context.Records.Add(new EfSqliteUowRecord { Id = 1503, Name = "outer-after-query-claim" });
            var commandsAfterOuterClaim = commandCounter.CommandCount;
            var commitsBeforeInner = counters.TransactionsCommitted;
            inner = manager.Begin(requiresNew: true);

            var error = Record.Exception(() => context.Commit());
            var innerRollbackOnly = inner.IsRollbackOnly;
            await inner.DisposeAsync();
            inner = null;
            await outer.RollbackAsync();
            await outer.DisposeAsync();
            outer = null;

            Assert.IsType<InvalidOperationException>(error);
            Assert.True(innerRollbackOnly);
            Assert.Equal(commandsAfterOuterClaim, commandCounter.CommandCount);
            Assert.Equal(commitsBeforeInner, counters.TransactionsCommitted);
            Assert.Empty(await ReadRecordNamesAsync(setup.ConnectionString));
        }
        finally
        {
            if (inner != null)
                await inner.DisposeAsync();
            if (outer != null)
                await outer.DisposeAsync();
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Theory]
    [InlineData(OuterPendingKind.TransactionAction, false, false)]
    [InlineData(OuterPendingKind.TransactionAction, false, true)]
    [InlineData(OuterPendingKind.TransactionAction, true, false)]
    [InlineData(OuterPendingKind.TransactionAction, true, true)]
    [InlineData(OuterPendingKind.DomainEvent, false, false)]
    [InlineData(OuterPendingKind.DomainEvent, false, true)]
    [InlineData(OuterPendingKind.DomainEvent, true, false)]
    [InlineData(OuterPendingKind.DomainEvent, true, true)]
    [InlineData(OuterPendingKind.MappedResourceClaim, false, false)]
    [InlineData(OuterPendingKind.MappedResourceClaim, false, true)]
    [InlineData(OuterPendingKind.MappedResourceClaim, true, false)]
    [InlineData(OuterPendingKind.MappedResourceClaim, true, true)]
    public async Task RequiresNewSameScope_ExplicitSaveBeforeHandler_ShouldRejectOuterAuxiliaryState(
        OuterPendingKind pendingKind, bool asynchronous, bool useCommitAlias)
    {
        using var counters = new EfSqliteLifecycleCounters();
        using var commandCounter = new SqliteCommandCounter();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true, commandInterceptor: commandCounter);
        IUnitOfWorkExecutionScope outer = null;
        IUnitOfWorkScope inner = null;
        var actionCalls = 0;
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            var dispatcher = services.GetRequiredService<EfSqliteRecordingDomainEventDispatcher>();
            if (pendingKind == OuterPendingKind.DomainEvent)
            {
                context.Records.Add(new EfSqliteUowRecord { Id = 1504, Name = "event-owner" });
                await context.SaveChangesAsync();
                var unchangedRecord = await context.Records.SingleAsync(record => record.Id == 1504);
                Assert.Equal(EntityState.Unchanged, context.Entry(unchangedRecord).State);
            }

            var manager = services.GetRequiredService<IUnitOfWorkOperationManager>();
            outer = manager.BeginOperation();
            if (pendingKind == OuterPendingKind.TransactionAction)
            {
                services.GetRequiredService<ITransactionActionManager>()
                    .Register(_ => { actionCalls++; return Task.CompletedTask; });
            }
            else if (pendingKind == OuterPendingKind.DomainEvent)
            {
                context.Records.Local.Single(record => record.Id == 1504)
                    .AddDomainEvent(new EfSqliteTestDomainEvent(1505));
            }
            else
            {
                Assert.Equal(1, new EfSqliteOperationStore(context).QueryFirstClaim());
            }

            var initialCommands = commandCounter.CommandCount;
            var initialCommits = counters.TransactionsCommitted;
            inner = manager.Begin(requiresNew: true);
            Exception saveError = null;
            try
            {
                if (asynchronous)
                {
                    if (useCommitAlias)
                        await context.CommitAsync();
                    else
                        await context.SaveChangesAsync();
                }
                else if (useCommitAlias)
                    context.Commit();
                else
                    context.SaveChanges();
            }
            catch (Exception error)
            {
                saveError = error;
            }

            var innerRollbackOnly = inner.IsRollbackOnly;
            await inner.DisposeAsync();
            inner = null;
            await outer.RollbackAsync();
            await outer.DisposeAsync();
            outer = null;

            Assert.IsType<InvalidOperationException>(saveError);
            Assert.True(innerRollbackOnly);
            Assert.Equal(initialCommands, commandCounter.CommandCount);
            Assert.Equal(initialCommits, counters.TransactionsCommitted);
            Assert.Equal(0, actionCalls);
            Assert.Empty(dispatcher.Sequences);
            Assert.Equal(pendingKind == OuterPendingKind.DomainEvent ? new[] { "event-owner" } : Array.Empty<string>(),
                await ReadRecordNamesAsync(setup.ConnectionString));
        }
        finally
        {
            if (inner != null)
                await inner.DisposeAsync();
            if (outer != null)
                await outer.DisposeAsync();
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task RequiresNewSameScope_WithPendingEntity_ShouldRejectBeforeSqliteIo()
    {
        using var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<EfSqliteMappedUnitOfWork>();
            counters.Track(context);
            var transactionsStarted = counters.TransactionsStarted;
            var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
            var outer = manager.BeginOperation();
            context.Records.Add(new EfSqliteUowRecord { Id = 501, Name = "outer-pending" });
            var inner = manager.Begin(requiresNew: true);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => inner.CompleteAsync());
            Assert.Contains("requiresNew", exception.Message, StringComparison.Ordinal);
            await inner.DisposeAsync();
            Assert.Same(outer, manager.Current);
            Assert.Equal(transactionsStarted, counters.TransactionsStarted);
            await outer.RollbackAsync();
            await outer.DisposeAsync();

            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
            Assert.Equal(0, context.SaveAfterCount);
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task RequiresNewSameScope_WithPendingTransactionAction_ShouldRejectBeforeActionOrIo()
    {
        using var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<EfSqliteMappedUnitOfWork>();
            counters.Track(context);
            var actions = scope.ServiceProvider.GetRequiredService<ITransactionActionManager>();
            var actionCalls = 0;
            actions.Register(_ => { actionCalls++; return Task.CompletedTask; });
            var transactionsStarted = counters.TransactionsStarted;
            var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
            var outer = manager.BeginOperation();
            var inner = manager.Begin(requiresNew: true);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => inner.CompleteAsync());
            Assert.Contains("事务动作", exception.Message, StringComparison.Ordinal);
            await inner.DisposeAsync();
            await outer.RollbackAsync();
            await outer.DisposeAsync();

            Assert.Equal(0, actionCalls);
            Assert.Equal(transactionsStarted, counters.TransactionsStarted);
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task RequiresNewSameScope_WithPendingDomainEvent_ShouldRejectBeforeDispatchOrIo()
    {
        using var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            var dispatcher = services.GetRequiredService<EfSqliteRecordingDomainEventDispatcher>();
            counters.Track(context);
            var record = new EfSqliteUowRecord { Id = 502, Name = "event-only" };
            context.Records.Attach(record);
            record.AddDomainEvent(new EfSqliteTestDomainEvent(502));
            var transactionsStarted = counters.TransactionsStarted;
            var manager = services.GetRequiredService<IUnitOfWorkOperationManager>();
            var outer = manager.BeginOperation();
            var inner = manager.Begin(requiresNew: true);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => inner.CompleteAsync());
            Assert.Contains("领域事件", exception.Message, StringComparison.Ordinal);
            await inner.DisposeAsync();
            await outer.RollbackAsync();
            await outer.DisposeAsync();

            Assert.Empty(dispatcher.Sequences);
            Assert.Equal(transactionsStarted, counters.TransactionsStarted);
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task RequiresNewSameScope_WithContextClaimedByOuter_ShouldRejectBeforeInnerSave()
    {
        using var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<EfSqliteMappedUnitOfWork>();
            counters.Track(context);
            var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
            var outer = manager.BeginOperation();
            context.Records.Add(new EfSqliteUowRecord { Id = 503, Name = "outer-claimed" });
            await context.SaveChangesAsync();
            var transactionsStarted = counters.TransactionsStarted;
            var savesAfterFlush = context.SaveAfterCount;
            var inner = manager.Begin(requiresNew: true);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => inner.CompleteAsync());
            Assert.Contains("claim", exception.Message, StringComparison.OrdinalIgnoreCase);
            await inner.DisposeAsync();
            await outer.RollbackAsync();
            await outer.DisposeAsync();

            Assert.Equal(transactionsStarted, counters.TransactionsStarted);
            Assert.Equal(savesAfterFlush, context.SaveAfterCount);
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Theory]
    [InlineData(false, NestedMiddleAction.Complete)]
    [InlineData(true, NestedMiddleAction.Complete)]
    [InlineData(false, NestedMiddleAction.SaveChanges)]
    [InlineData(true, NestedMiddleAction.SaveChanges)]
    [InlineData(false, NestedMiddleAction.Commit)]
    [InlineData(true, NestedMiddleAction.Commit)]
    public async Task RequiresNewNested_WhenDeepestRootExits_ShouldKeepMiddleRootProtected(
        bool asynchronous, NestedMiddleAction action)
    {
        using var counters = new EfSqliteLifecycleCounters();
        using var commandCounter = new SqliteCommandCounter();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true, commandInterceptor: commandCounter);
        IUnitOfWorkExecutionScope outer = null;
        IUnitOfWorkScope middle = null;
        IUnitOfWorkScope deepest = null;
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            counters.Track(context);
            var manager = services.GetRequiredService<IUnitOfWorkOperationManager>();
            outer = manager.BeginOperation();
            context.Records.Add(new EfSqliteUowRecord { Id = 1601, Name = "nested-outer-pending" });
            middle = manager.Begin(requiresNew: true);
            deepest = manager.Begin(requiresNew: true);
            if (asynchronous)
                await deepest.DisposeAsync();
            else
                deepest.Dispose();
            deepest = null;

            var initialCommands = commandCounter.CommandCount;
            var initialCommits = counters.TransactionsCommitted;
            var initialSavesAfter = context.SaveAfterCount;
            Exception failure = null;
            try
            {
                if (action == NestedMiddleAction.SaveChanges)
                {
                    if (asynchronous)
                        await context.SaveChangesAsync();
                    else
                        context.SaveChanges();
                }
                else if (action == NestedMiddleAction.Commit)
                {
                    if (asynchronous)
                        await context.CommitAsync();
                    else
                        context.Commit();
                }

                if (asynchronous)
                    await middle.CompleteAsync();
                else
                    middle.Complete();
            }
            catch (Exception error)
            {
                failure = error;
            }

            var middleRollbackOnly = middle.IsRollbackOnly;
            var middleCompleted = middle.IsCompleted;
            await middle.DisposeAsync();
            middle = null;
            await outer.RollbackAsync();
            await outer.DisposeAsync();
            outer = null;

            Assert.IsType<InvalidOperationException>(failure);
            Assert.True(middleRollbackOnly);
            Assert.False(middleCompleted);
            Assert.Equal(initialCommands, commandCounter.CommandCount);
            Assert.Equal(initialCommits, counters.TransactionsCommitted);
            Assert.Equal(initialSavesAfter, context.SaveAfterCount);
            Assert.Empty(await ReadRecordNamesAsync(setup.ConnectionString));
        }
        finally
        {
            if (deepest != null)
                await deepest.DisposeAsync();
            if (middle != null)
                await middle.DisposeAsync();
            if (outer != null)
                await outer.DisposeAsync();
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Theory]
    [InlineData(NestedOuterPendingKind.Entity, false)]
    [InlineData(NestedOuterPendingKind.Entity, true)]
    [InlineData(NestedOuterPendingKind.TransactionAction, false)]
    [InlineData(NestedOuterPendingKind.TransactionAction, true)]
    [InlineData(NestedOuterPendingKind.DomainEvent, false)]
    [InlineData(NestedOuterPendingKind.DomainEvent, true)]
    [InlineData(NestedOuterPendingKind.MappedResourceClaim, false)]
    [InlineData(NestedOuterPendingKind.MappedResourceClaim, true)]
    public async Task RequiresNewNested_WhenDeepestRootExits_ShouldRejectOuterAuxiliaryState(
        NestedOuterPendingKind pendingKind, bool asynchronous)
    {
        using var counters = new EfSqliteLifecycleCounters();
        using var commandCounter = new SqliteCommandCounter();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true, commandInterceptor: commandCounter);
        IUnitOfWorkExecutionScope outer = null;
        IUnitOfWorkScope middle = null;
        IUnitOfWorkScope deepest = null;
        var actionCalls = 0;
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            var dispatcher = services.GetRequiredService<EfSqliteRecordingDomainEventDispatcher>();
            if (pendingKind == NestedOuterPendingKind.DomainEvent)
            {
                context.Records.Add(new EfSqliteUowRecord { Id = 1602, Name = "nested-event-owner" });
                await context.SaveChangesAsync();
                var unchangedRecord = await context.Records.SingleAsync(record => record.Id == 1602);
                Assert.Equal(EntityState.Unchanged, context.Entry(unchangedRecord).State);
            }

            var manager = services.GetRequiredService<IUnitOfWorkOperationManager>();
            outer = manager.BeginOperation();
            if (pendingKind == NestedOuterPendingKind.Entity)
                context.Records.Add(new EfSqliteUowRecord { Id = 1603, Name = "nested-outer-entity" });
            else if (pendingKind == NestedOuterPendingKind.TransactionAction)
            {
                services.GetRequiredService<ITransactionActionManager>()
                    .Register(_ => { actionCalls++; return Task.CompletedTask; });
            }
            else if (pendingKind == NestedOuterPendingKind.DomainEvent)
            {
                context.Records.Local.Single(record => record.Id == 1602)
                    .AddDomainEvent(new EfSqliteTestDomainEvent(1604));
            }
            else
            {
                Assert.Equal(1, new EfSqliteOperationStore(context).QueryFirstClaim());
            }

            middle = manager.Begin(requiresNew: true);
            deepest = manager.Begin(requiresNew: true);
            if (asynchronous)
                await deepest.DisposeAsync();
            else
                deepest.Dispose();
            deepest = null;

            var initialCommands = commandCounter.CommandCount;
            var initialCommits = counters.TransactionsCommitted;
            Exception failure = null;
            try
            {
                if (asynchronous)
                    await middle.CompleteAsync();
                else
                    middle.Complete();
            }
            catch (Exception error)
            {
                failure = error;
            }

            var middleRollbackOnly = middle.IsRollbackOnly;
            var middleCompleted = middle.IsCompleted;
            await middle.DisposeAsync();
            middle = null;
            await outer.RollbackAsync();
            await outer.DisposeAsync();
            outer = null;

            Assert.IsType<InvalidOperationException>(failure);
            Assert.True(middleRollbackOnly);
            Assert.False(middleCompleted);
            Assert.Equal(initialCommands, commandCounter.CommandCount);
            Assert.Equal(initialCommits, counters.TransactionsCommitted);
            Assert.Equal(0, actionCalls);
            Assert.Empty(dispatcher.Sequences);
            Assert.Equal(pendingKind == NestedOuterPendingKind.DomainEvent
                    ? new[] { "nested-event-owner" }
                    : Array.Empty<string>(),
                await ReadRecordNamesAsync(setup.ConnectionString));
        }
        finally
        {
            if (deepest != null)
                await deepest.DisposeAsync();
            if (middle != null)
                await middle.DisposeAsync();
            if (outer != null)
                await outer.DisposeAsync();
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task RequiresNewNested_InnerOnlyMiddleRoot_ShouldCommitAfterDeepestExitAndOuterRollback()
    {
        using var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true);
        IUnitOfWorkExecutionScope outer = null;
        IUnitOfWorkScope middle = null;
        IUnitOfWorkScope deepest = null;
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            counters.Track(context);
            var manager = services.GetRequiredService<IUnitOfWorkOperationManager>();
            outer = manager.BeginOperation();
            middle = manager.Begin(requiresNew: true);
            context.Records.Add(new EfSqliteUowRecord { Id = 1605, Name = "nested-inner-only" });
            deepest = manager.Begin(requiresNew: true);
            await deepest.DisposeAsync();
            deepest = null;

            await middle.CompleteAsync();
            await middle.DisposeAsync();
            middle = null;
            await outer.RollbackAsync();
            await outer.DisposeAsync();
            outer = null;

            Assert.Equal(new[] { "nested-inner-only" }, await ReadRecordNamesAsync(setup.ConnectionString));
            Assert.Equal(1, counters.TransactionsCommitted);
        }
        finally
        {
            if (deepest != null)
                await deepest.DisposeAsync();
            if (middle != null)
                await middle.DisposeAsync();
            if (outer != null)
                await outer.DisposeAsync();
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task RequiresNewNested_WhenBeginObserverThrows_ShouldKeepCurrentRootProtection()
    {
        using var counters = new EfSqliteLifecycleCounters();
        using var commandCounter = new SqliteCommandCounter();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true, commandInterceptor: commandCounter,
            addDetectChangesThrowingContext: true);
        IUnitOfWorkExecutionScope outer = null;
        IUnitOfWorkScope middle = null;
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteDetectChangesThrowingUnitOfWork>();
            counters.Track(context);
            var manager = services.GetRequiredService<IUnitOfWorkOperationManager>();
            outer = manager.BeginOperation();
            var pending = new EfSqliteDetectChangesThrowingRecord { Id = 1606, Name = "observer-failure-owner" };
            context.Records.Add(pending);
            middle = manager.Begin(requiresNew: true);

            var thrownOnDetect = new EfSqliteDetectChangesThrowingRecord
            {
                Id = 1607,
                Name = "observer-failure-detect"
            };
            context.Records.Attach(thrownOnDetect);
            var initialCommands = commandCounter.CommandCount;
            var initialCommits = counters.TransactionsCommitted;
            thrownOnDetect.ThrowOnNameRead = true;
            var beginError = Record.Exception(() => manager.Begin(requiresNew: true));
            Assert.IsType<InvalidOperationException>(beginError);
            Assert.Same(middle, manager.Current);

            thrownOnDetect.ThrowOnNameRead = false;
            var saveError = Record.Exception(() => context.SaveChanges());
            var middleRollbackOnly = middle.IsRollbackOnly;
            await middle.DisposeAsync();
            middle = null;
            await outer.RollbackAsync();
            await outer.DisposeAsync();
            outer = null;

            Assert.IsType<InvalidOperationException>(saveError);
            Assert.Contains("requiresNew", saveError.Message, StringComparison.Ordinal);
            Assert.True(middleRollbackOnly);
            Assert.Equal(initialCommands, commandCounter.CommandCount);
            Assert.Equal(initialCommits, counters.TransactionsCommitted);
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
        }
        finally
        {
            if (middle != null)
                await middle.DisposeAsync();
            if (outer != null)
                await outer.DisposeAsync();
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MappedOperationSave_ShouldNeverEnterLegacyTransactionCommit(bool asynchronous)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), "bing-ef-uow", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath));
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var provider = EfCoreSqliteUnitOfWorkTestSupport.CreateCapServices(
            connectionString, addBingOperations: true);
        try
        {
            await using (var initializeScope = provider.CreateAsyncScope())
                await initializeScope.ServiceProvider.GetRequiredService<EfSqliteCapMappedUnitOfWork>()
                    .Database.EnsureCreatedAsync();
            await using var scope = provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteCapMappedUnitOfWork>();
            var operation = services.GetRequiredService<IUnitOfWorkOperationManager>().BeginOperation();
            context.Records.Add(new EfSqliteUowRecord { Id = 508, Name = "operation-handler-not-legacy" });

            if (asynchronous)
                await operation.CompleteAsync();
            else
                operation.Complete();
            await operation.DisposeAsync();

            Assert.Equal(0, context.TransactionCommitCallCount);
            Assert.Null(context.Database.CurrentTransaction);
            Assert.Equal(1, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(connectionString));
            context.ChangeTracker.Clear();
            Assert.Equal("operation-handler-not-legacy",
                await context.Records.Where(record => record.Id == 508).Select(record => record.Name).SingleAsync());
        }
        finally
        {
            await provider.DisposeAsync();
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task UnmappedDirtyContext_ShouldRejectBeforeOtherResourceCommit()
    {
        using var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true, addUnmappedEfContext: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var mappedContext = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            var unmappedContext = services.GetRequiredService<EfSqliteUnmappedUnitOfWork>();
            counters.Track(mappedContext);
            var transactionsStarted = counters.TransactionsStarted;
            var transactionsCommitted = counters.TransactionsCommitted;
            var transactionsRolledBack = counters.TransactionsRolledBack;
            var operation = services.GetRequiredService<IUnitOfWorkOperationManager>().BeginOperation();

            await mappedContext.SaveChangesAsync();
            Assert.Equal(1, counters.TransactionsStarted - transactionsStarted);
            unmappedContext.Records.Add(new EfSqliteUowRecord { Id = 509, Name = "unmapped-pending" });

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => operation.CompleteAsync());
            Assert.Contains("没有 SQL 数据源映射", exception.Message, StringComparison.Ordinal);
            await operation.DisposeAsync();

            Assert.Equal(0, counters.TransactionsCommitted - transactionsCommitted);
            Assert.Equal(1, counters.TransactionsRolledBack - transactionsRolledBack);
            Assert.Equal(System.Data.ConnectionState.Closed, mappedContext.Database.GetDbConnection().State);
            Assert.True(unmappedContext.ChangeTracker.HasChanges());
            Assert.Equal(0, mappedContext.SaveAfterCount);
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitLegacyCommitOnUnmappedContext_InActiveOperation_ShouldRejectBeforeSql(
        bool asynchronous)
    {
        using var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(mapEfCore: false,
            lifecycleCounters: counters, addBingOperations: true, addUnmappedEfContext: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteUnmappedUnitOfWork>();
            counters.Track(context);
            var opened = counters.Opened;
            var transactionsStarted = counters.TransactionsStarted;
            var manager = services.GetRequiredService<IUnitOfWorkOperationManager>();
            var operation = manager.BeginOperation();
            context.Records.Add(new EfSqliteUowRecord { Id = 510, Name = "unmapped-explicit-commit" });

            InvalidOperationException exception;
            if (asynchronous)
                exception = await Assert.ThrowsAsync<InvalidOperationException>(() => context.CommitAsync());
            else
                exception = Assert.Throws<InvalidOperationException>(() => context.Commit());

            Assert.Contains("没有 SQL 数据源映射", exception.Message, StringComparison.Ordinal);
            Assert.True(operation.IsRollbackOnly);
            Assert.Equal(opened, counters.Opened);
            Assert.Equal(transactionsStarted, counters.TransactionsStarted);
            Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
            await operation.DisposeAsync();
            Assert.Null(manager.Current);
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task ExecutorNewScope_ShouldCommitItsContextWhenOuterRootRollsBack()
    {
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(addBingOperations: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var callerScope = setup.Provider.CreateAsyncScope();
            var callerServices = callerScope.ServiceProvider;
            var outerContext = callerServices.GetRequiredService<EfSqliteMappedUnitOfWork>();
            var outer = callerServices.GetRequiredService<IUnitOfWorkOperationManager>().BeginOperation();
            var executor = setup.Provider.GetRequiredService<IBingOperationExecutor>();
            EfSqliteMappedUnitOfWork childContext = null;

            await executor.ExecuteAsync<EfSqliteMappedUnitOfWork>((context, _) =>
            {
                childContext = context;
                context.Records.Add(new EfSqliteUowRecord { Id = 504, Name = "executor-child" });
                return Task.CompletedTask;
            });

            Assert.NotNull(childContext);
            Assert.NotSame(outerContext, childContext);
            await outer.RollbackAsync();
            await outer.DisposeAsync();
            Assert.Equal(1, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task StoreRead_WhenParticipantCancelsAfterReaderOpened_ShouldCancelAndDisposeReader()
    {
        using var counters = new EfSqliteLifecycleCounters();
        var interceptor = new BlockingReaderInterceptor();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true, commandInterceptor: interceptor);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<EfSqliteMappedUnitOfWork>();
            var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
            var root = manager.BeginOperation();
            using var cancellation = new CancellationTokenSource();
            var participant = manager.BeginOperation(cancellationToken: cancellation.Token);
            var store = new EfSqliteOperationStore(context);
            var read = store.FindAllAsync(cancellationToken: CancellationToken.None);
            var reader = await interceptor.ReaderOpened.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(interceptor.CommandToken.CanBeCanceled);
            cancellation.Cancel();
            Assert.True(interceptor.CommandToken.IsCancellationRequested);
            interceptor.ReleaseReader();

            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);

            Assert.True(exception.CancellationToken.IsCancellationRequested);
            Assert.True(reader.IsClosed);
            Assert.True(root.IsRollbackOnly);
            Assert.Same(root, manager.Current);
            await participant.DisposeAsync();
            await root.DisposeAsync();
        }
        finally
        {
            interceptor.ReleaseReader();
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task SaveChangesAsync_WithCancelledOperationAndDefaultArgument_ShouldRejectBeforeSqliteIo()
    {
        using var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            using var cancellation = new CancellationTokenSource();
            var context = scope.ServiceProvider.GetRequiredService<EfSqliteMappedUnitOfWork>();
            var transactionsStarted = counters.TransactionsStarted;
            var operation = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>()
                .BeginOperation(cancellationToken: cancellation.Token);
            context.Records.Add(new EfSqliteUowRecord { Name = "cancelled-operation-explicit-none" });
            cancellation.Cancel();

            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => context.SaveChangesAsync(CancellationToken.None));

            Assert.Equal(cancellation.Token, exception.CancellationToken);
            Assert.True(operation.IsRollbackOnly);
            Assert.Equal(transactionsStarted, counters.TransactionsStarted);
            await operation.DisposeAsync();
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task Complete_ShouldSaveTrackedOnlyContextAndCommitMappedResource()
    {
        using var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<EfSqliteMappedUnitOfWork>();
            counters.Track(context);
            var transactionsStarted = counters.TransactionsStarted;
            var transactionsCommitted = counters.TransactionsCommitted;
            var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
            var operation = manager.BeginOperation();
            context.Records.Add(new EfSqliteUowRecord { Name = "operation-tracked-only" });

            await operation.CompleteAsync();
            await operation.DisposeAsync();

            Assert.Equal(1, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
            Assert.Equal(1, context.SaveAfterCount);
            Assert.Equal(1, counters.TransactionsStarted - transactionsStarted);
            Assert.Equal(1, counters.TransactionsCommitted - transactionsCommitted);
            Assert.Null(manager.Current);
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task OperationComplete_AfterManualFlushWithNoPendingChanges_ShouldNotRegisterAnotherSaveNotification()
    {
        using var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<EfSqliteMappedUnitOfWork>();
            var dispatcher = scope.ServiceProvider.GetRequiredService<EfSqliteRecordingDomainEventDispatcher>();
            counters.Track(context);
            var transactionsStarted = counters.TransactionsStarted;
            var transactionsCommitted = counters.TransactionsCommitted;
            var operation = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>()
                .BeginOperation();
            context.Records.Add(new EfSqliteUowRecord { Id = 505, Name = "manual-flush-no-pending" });

            Assert.Equal(1, await context.SaveChangesAsync());

            Assert.False(context.ChangeTracker.HasChanges());
            Assert.Equal(0, context.SaveAfterCount);
            Assert.Empty(dispatcher.Sequences);
            await operation.CompleteAsync();
            await operation.DisposeAsync();

            Assert.Equal(1, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
            Assert.Equal(1, context.SaveAfterCount);
            Assert.Empty(dispatcher.Sequences);
            Assert.Equal(1, counters.TransactionsStarted - transactionsStarted);
            Assert.Equal(1, counters.TransactionsCommitted - transactionsCommitted);
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task OperationComplete_AfterManualFlushWithNewChanges_ShouldSaveThemBeforeCommit()
    {
        using var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<EfSqliteMappedUnitOfWork>();
            counters.Track(context);
            var operation = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>()
                .BeginOperation();
            var record = new EfSqliteUowRecord { Id = 506, Name = "before-final-save" };
            context.Records.Add(record);

            Assert.Equal(1, await context.SaveChangesAsync());
            Assert.Equal(0, context.SaveAfterCount);
            record.Name = "after-final-save";

            await operation.CompleteAsync();
            await operation.DisposeAsync();

            Assert.Equal(1, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
            Assert.Equal("after-final-save", await ReadRecordNameAsync(setup.ConnectionString, record.Id));
            Assert.Equal(2, context.SaveAfterCount);
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task OperationDomainEvent_MultipleManualFlushes_ShouldDispatchOnceAndClearAfterCommit()
    {
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(addBingOperations: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            var dispatcher = services.GetRequiredService<EfSqliteRecordingDomainEventDispatcher>();
            var record = new EfSqliteUowRecord { Id = 507, Name = "operation-domain-event" };
            record.AddDomainEvent(new EfSqliteTestDomainEvent(507));
            context.Records.Add(record);
            var operation = services.GetRequiredService<IUnitOfWorkOperationManager>().BeginOperation();

            await context.SaveChangesAsync();
            Assert.Equal(new[] { 507 }, dispatcher.Sequences);
            Assert.Single(record.GetDomainEvents());
            Assert.Equal(0, context.SaveAfterCount);

            await context.SaveChangesAsync();
            Assert.Equal(new[] { 507 }, dispatcher.Sequences);
            Assert.Single(record.GetDomainEvents());
            Assert.Equal(0, context.SaveAfterCount);

            await operation.CompleteAsync();
            await operation.DisposeAsync();

            Assert.Equal(new[] { 507 }, dispatcher.Sequences);
            Assert.Empty(record.GetDomainEvents());
            Assert.Equal(2, context.SaveAfterCount);
            Assert.Equal(1, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Operation_EfAndDapperShouldShareOneMappedSqliteResourceInEitherAccessOrder(bool efFirst)
    {
        using var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            counters.Track(context);
            var manager = services.GetRequiredService<IUnitOfWorkOperationManager>();
            var root = manager.BeginOperation();
            using var executor = services.GetRequiredService<ISqlExecutorFactory>().Create("orders");
            var transactionsStarted = counters.TransactionsStarted;
            var transactionsCommitted = counters.TransactionsCommitted;

            if (efFirst)
            {
                context.Records.Add(new EfSqliteUowRecord { Id = 601, Name = "operation-ef-first" });
                Assert.Equal(1, await context.SaveChangesAsync());
            }

            await executor.ExecuteSqlAsync(
                "INSERT INTO ef_uow_records (Id, Name) VALUES (@id, @name)",
                new { id = 602, name = "operation-dapper" });

            if (!efFirst)
                context.Records.Add(new EfSqliteUowRecord { Id = 603, Name = "operation-ef-auto-save" });

            var resource = services.GetRequiredService<ISqlUnitOfWorkResourceAccessor>().GetResource(root,
                new DatabaseContext { DbKey = "orders" }, requireComparableIdentity: true);
            await resource.EnsureReadyAsync();
            Assert.Same(context.Database.GetDbConnection(), resource.Connection);
            Assert.Same(context.Database.CurrentTransaction!.GetDbTransaction(), resource.Transaction);
            var resolvedAgain = services.GetRequiredService<ISqlUnitOfWorkResourceAccessor>().GetResource(root,
                new DatabaseContext { DbKey = "orders" }, requireComparableIdentity: true);
            Assert.Same(resource, resolvedAgain);

            await root.CompleteAsync();
            await root.DisposeAsync();

            Assert.Equal(1, counters.TransactionsStarted - transactionsStarted);
            Assert.Equal(1, counters.TransactionsCommitted - transactionsCommitted);
            Assert.Equal(2, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
            Assert.Null(context.Database.CurrentTransaction);
            Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
            Assert.Null(manager.Current);
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task OperationSave_ShouldRejectContextResolvedDuringSaveBeforeCommit()
    {
        var secondPath = Path.Combine(Path.GetTempPath(), "bing-ef-uow", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(secondPath)!);
        var secondConnectionString = $"Data Source={secondPath};Pooling=False";
        using var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(lifecycleCounters: counters,
            addGoodDataSource: true, goodConnectionString: secondConnectionString,
            addSecondMappedEfContext: true, addBingOperations: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            counters.Track(context);
            EfSqliteSecondMappedUnitOfWork secondContext = null;
            context.BeforeSaveChanges = _ =>
            {
                secondContext = services.GetRequiredService<EfSqliteSecondMappedUnitOfWork>();
                secondContext.Records.Add(new EfSqliteUowRecord { Id = 604, Name = "late-context-pending" });
            };
            var manager = services.GetRequiredService<IUnitOfWorkOperationManager>();
            var operation = manager.BeginOperation();
            context.Records.Add(new EfSqliteUowRecord { Id = 605, Name = "before-late-context" });
            var transactionsCommitted = counters.TransactionsCommitted;

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => operation.CompleteAsync());

            Assert.Contains("最终保存快照", exception.Message, StringComparison.Ordinal);
            Assert.NotNull(secondContext);
            Assert.True(operation.IsRollbackOnly);
            Assert.Equal(transactionsCommitted, counters.TransactionsCommitted);
            await operation.DisposeAsync();
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountOtherRowsAsync(secondConnectionString));
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
            if (File.Exists(secondPath))
                File.Delete(secondPath);
        }
    }

    [Fact]
    public async Task OperationSave_ShouldRejectRedirtiedProcessedContextBeforeCommit()
    {
        var secondPath = Path.Combine(Path.GetTempPath(), "bing-ef-uow", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(secondPath)!);
        var secondConnectionString = $"Data Source={secondPath};Pooling=False";
        using var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(lifecycleCounters: counters,
            addGoodDataSource: true, goodConnectionString: secondConnectionString,
            addSecondMappedEfContext: true, addBingOperations: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            var secondContext = services.GetRequiredService<EfSqliteSecondMappedUnitOfWork>();
            counters.Track(context);
            var firstRecord = new EfSqliteUowRecord { Id = 606, Name = "first-context" };
            context.Records.Add(firstRecord);
            secondContext.Records.Add(new EfSqliteUowRecord { Id = 607, Name = "second-context" });
            secondContext.BeforeSaveChanges = _ => firstRecord.Name = "redirtied-after-first-save";
            var manager = services.GetRequiredService<IUnitOfWorkOperationManager>();
            var operation = manager.BeginOperation();
            var transactionsCommitted = counters.TransactionsCommitted;

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => operation.CompleteAsync());

            Assert.Contains("最终保存快照", exception.Message, StringComparison.Ordinal);
            Assert.True(operation.IsRollbackOnly);
            Assert.Equal(transactionsCommitted, counters.TransactionsCommitted);
            await operation.DisposeAsync();
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountOtherRowsAsync(secondConnectionString));
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
            if (File.Exists(secondPath))
                File.Delete(secondPath);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonTransactionalOperation_ShouldRejectTransactionWorkBeforeSql(bool domainEvent)
    {
        using var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            var dispatcher = services.GetRequiredService<EfSqliteRecordingDomainEventDispatcher>();
            counters.Track(context);
            var opened = counters.Opened;
            var transactionsStarted = counters.TransactionsStarted;
            var operation = services.GetRequiredService<IUnitOfWorkOperationManager>()
                .BeginOperation(new UnitOfWorkScopeOptions { IsTransactional = false });
            var record = new EfSqliteUowRecord { Id = domainEvent ? 608 : 609, Name = "non-transaction-work" };
            context.Records.Add(record);
            var actionCalls = 0;
            if (domainEvent)
            {
                record.AddDomainEvent(new EfSqliteTestDomainEvent(record.Id));
            }
            else
            {
                services.GetRequiredService<ITransactionActionManager>()
                    .Register(_ => { actionCalls++; return Task.CompletedTask; });
            }

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => operation.CompleteAsync());

            Assert.Contains("非事务", exception.Message, StringComparison.Ordinal);
            Assert.True(operation.IsRollbackOnly);
            Assert.Equal(0, actionCalls);
            Assert.Empty(dispatcher.Sequences);
            Assert.Equal(opened, counters.Opened);
            Assert.Equal(transactionsStarted, counters.TransactionsStarted);
            await operation.DisposeAsync();
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task NonTransactionalOperation_WhenAfterSaveNotificationFails_ShouldRetainPersistedSqliteRow()
    {
        using var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<EfSqliteMappedUnitOfWork>();
            counters.Track(context);
            context.ThrowOnSaveChangesAfter = true;
            var transactionsStarted = counters.TransactionsStarted;
            var operation = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>()
                .BeginOperation(new UnitOfWorkScopeOptions { IsTransactional = false });
            context.Records.Add(new EfSqliteUowRecord { Id = 610, Name = "non-transaction-persisted" });

            var exception = await Assert.ThrowsAsync<PostCommitNotificationException>(() => operation.CompleteAsync());

            Assert.True(operation.IsRollbackOnly);
            Assert.Equal("non-transaction-persisted", await ReadRecordNameAsync(setup.ConnectionString, 610));
            Assert.Equal(1, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
            Assert.Equal(1, context.SaveAfterCount);
            Assert.Equal(transactionsStarted, counters.TransactionsStarted);
            await operation.DisposeAsync();
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task OperationSave_WhenSqliteCommandFails_ShouldRollbackAndPreserveResourceCleanup()
    {
        using var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using (var connection = new SqliteConnection(setup.ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "DROP TABLE ef_uow_records; CREATE TABLE ef_uow_records (Id INTEGER NOT NULL PRIMARY KEY);";
                await command.ExecuteNonQueryAsync();
            }

            await using var scope = setup.Provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<EfSqliteMappedUnitOfWork>();
            counters.Track(context);
            var transactionsStarted = counters.TransactionsStarted;
            var transactionsRolledBack = counters.TransactionsRolledBack;
            var transactionsCommitted = counters.TransactionsCommitted;
            var operation = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>().BeginOperation();
            context.Records.Add(new EfSqliteUowRecord { Id = 611, Name = "sqlite-command-failure" });

            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => operation.CompleteAsync());

            Assert.Contains("Name", exception.ToString(), StringComparison.Ordinal);
            Assert.True(operation.IsRollbackOnly);
            Assert.Equal(transactionsStarted + 1, counters.TransactionsStarted);
            Assert.Equal(transactionsRolledBack + 1, counters.TransactionsRolledBack);
            Assert.Equal(transactionsCommitted, counters.TransactionsCommitted);
            Assert.Equal(0, context.SaveAfterCount);
            await operation.DisposeAsync();
            Assert.Equal(1, counters.TransactionsDisposed);
            Assert.Null(context.Database.CurrentTransaction);
            Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);

            await using var verification = new SqliteConnection(setup.ConnectionString);
            await verification.OpenAsync();
            await using var query = verification.CreateCommand();
            query.CommandText = "SELECT COUNT(*) FROM ef_uow_records";
            Assert.Equal(0, Convert.ToInt32(await query.ExecuteScalarAsync()));
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task OperationSecondResourceCommitFailure_ShouldKeepCommittedSqliteRowsAndOnlyNotifyCommittedResource()
    {
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(addBingOperations: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var operation = services.GetRequiredService<IUnitOfWorkOperationManager>().BeginOperation();
            using var executor = services.GetRequiredService<ISqlExecutorFactory>().Create("orders");
            await executor.ExecuteSqlAsync(
                "INSERT INTO ef_uow_records (Id, Name) VALUES (@id, @name)",
                new { id = 612, name = "first-resource-committed" });
            var sqliteResource = services.GetRequiredService<ISqlUnitOfWorkResourceAccessor>().GetResource(operation,
                new DatabaseContext { DbKey = "orders" }, requireComparableIdentity: true);
            var failure = new InvalidOperationException("second operation resource commit failed");
            var failingResource = operation.GetOrAddResource("later-resource",
                () => new FailingCommitResource(failure));
            var sqliteNotifications = 0;
            var laterNotifications = 0;
            operation.OnResourceCommitted(sqliteResource.ResourceKey, () =>
            {
                sqliteNotifications++;
                return Task.CompletedTask;
            });
            operation.OnResourceCommitted("later-resource", () =>
            {
                laterNotifications++;
                return Task.CompletedTask;
            });

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => operation.CompleteAsync());

            Assert.Same(failure, exception);
            Assert.Equal(1, sqliteNotifications);
            Assert.Equal(0, laterNotifications);
            Assert.Equal(1, failingResource.CommitCount);
            Assert.Equal(1, failingResource.RollbackCount);
            await operation.DisposeAsync();
            Assert.Equal(1, failingResource.DisposeCount);
            Assert.Equal(1, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
            Assert.Equal("first-resource-committed", await ReadRecordNameAsync(setup.ConnectionString, 612));
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task SaveChangesFalse_ShouldRejectAutomaticCommitAndRollbackSqliteTransaction()
    {
        using var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<EfSqliteMappedUnitOfWork>();
            counters.Track(context);
            var transactionsStarted = counters.TransactionsStarted;
            var transactionsCommitted = counters.TransactionsCommitted;
            var transactionsRolledBack = counters.TransactionsRolledBack;
            var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
            var operation = manager.BeginOperation();
            context.Records.Add(new EfSqliteUowRecord { Name = "unaccepted-flush" });

            await context.SaveChangesAsync(acceptAllChangesOnSuccess: false);
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => operation.CompleteAsync());
            Assert.Contains("AcceptAllChanges", exception.Message, StringComparison.Ordinal);
            await operation.DisposeAsync();

            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
            Assert.Equal(0, context.SaveAfterCount);
            Assert.Equal(1, counters.TransactionsStarted - transactionsStarted);
            Assert.Equal(0, counters.TransactionsCommitted - transactionsCommitted);
            Assert.Equal(1, counters.TransactionsRolledBack - transactionsRolledBack);
            Assert.Null(manager.Current);
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task NonTransactionalOperation_ShouldSaveWithoutStartingTransaction()
    {
        using var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            lifecycleCounters: counters, addBingOperations: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var scope = setup.Provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<EfSqliteMappedUnitOfWork>();
            counters.Track(context);
            var transactionsStarted = counters.TransactionsStarted;
            var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
            var operation = manager.BeginOperation(new UnitOfWorkScopeOptions { IsTransactional = false });
            context.Records.Add(new EfSqliteUowRecord { Name = "non-transaction-operation" });

            await operation.CompleteAsync();
            await operation.DisposeAsync();

            Assert.Equal(1, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
            Assert.Equal(1, context.SaveAfterCount);
            Assert.Equal(0, counters.TransactionsStarted - transactionsStarted);
            Assert.Null(manager.Current);
        }
        finally
        {
            await setup.Provider.DisposeAsync();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    private sealed class EfSqliteOperationStore : StoreBase<EfSqliteUowRecord, int>
    {
        public EfSqliteOperationStore(EfSqliteMappedUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public int QueryFirstClaim() => Sql.Sql("SELECT 1").Scalar<int>();
    }

    public enum StoreCancellationSource
    {
        OwnerCaller,
        Deadline,
        ParticipantCaller
    }

    public enum OuterPendingKind
    {
        TransactionAction,
        DomainEvent,
        MappedResourceClaim
    }

    public enum NestedMiddleAction
    {
        Complete,
        SaveChanges,
        Commit
    }

    public enum NestedOuterPendingKind
    {
        Entity,
        TransactionAction,
        DomainEvent,
        MappedResourceClaim
    }

    private sealed class SqliteCommandCounter : DbCommandInterceptor, IDisposable
    {
        private int _commandCount;

        public int CommandCount => Volatile.Read(ref _commandCount);

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref _commandCount);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _commandCount);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<object> ScalarExecuting(DbCommand command,
            CommandEventData eventData, InterceptionResult<object> result)
        {
            Interlocked.Increment(ref _commandCount);
            return result;
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _commandCount);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<int> NonQueryExecuting(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result)
        {
            Interlocked.Increment(ref _commandCount);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _commandCount);
            return ValueTask.FromResult(result);
        }

        public void Dispose()
        {
        }
    }

    private sealed class BlockingReaderInterceptor : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<DbDataReader> ReaderOpened { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken CommandToken { get; private set; }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
            CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            CommandToken = cancellationToken;
            ReaderOpened.TrySetResult(result);
            await _release.Task.ConfigureAwait(false);
            return result;
        }

        public void ReleaseReader() => _release.TrySetResult();
    }

    private static async Task<string> ReadRecordNameAsync(string connectionString, int id)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM ef_uow_records WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        return Convert.ToString(await command.ExecuteScalarAsync());
    }

    private static async Task<string[]> ReadRecordNamesAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM ef_uow_records ORDER BY Id";
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            names.Add(reader.GetString(0));
        return names.ToArray();
    }

    private sealed class FailingCommitResource : IUnitOfWorkResource
    {
        private readonly Exception _failure;

        public FailingCommitResource(Exception failure) => _failure = failure;

        public int CommitCount { get; private set; }
        public int RollbackCount { get; private set; }
        public int DisposeCount { get; private set; }

        public void Commit()
        {
            CommitCount++;
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(_failure).Throw();
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commit();
            return Task.CompletedTask;
        }

        public void Rollback() => RollbackCount++;

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Rollback();
            return Task.CompletedTask;
        }

        public void Dispose() => DisposeCount++;
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
