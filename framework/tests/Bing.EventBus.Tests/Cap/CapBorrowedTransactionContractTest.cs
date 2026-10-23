using System.Data;
using System.Data.Common;
using System.Reflection;
using Bing.Events.Cap;
using Bing.Uow;
using DotNetCore.CAP;
using DotNetCore.CAP.Internal;
using DotNetCore.CAP.Messages;
using DotNetCore.CAP.Persistence;
using DotNetCore.CAP.Transport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.EventBus.Tests.Cap;

public sealed class CapBorrowedTransactionContractTest
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BorrowedBuffer_ShouldLeavePhysicalOwnershipToRootAndFlushOnlyAfterCommit(bool rollback)
    {
        var services = new ServiceCollection();
        services.AddUnitOfWorkScopes();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        await using var root = manager.Begin();
        var physical = new CountingDbTransaction();
        var owner = root.GetOrAddResource("orders", () => new PhysicalOwnerResource(physical));
        var dispatcher = new FailingDispatcher();
        var borrowed = new BorrowedCapTransaction(physical, dispatcher);
        var medium = new MediumMessage
        {
            Origin = new Message(new Dictionary<string, string>(), "borrowed-contract")
        };
        typeof(CapTransactionBase).GetMethod("AddToSent", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(borrowed, new object[] { medium });
        root.OnResourceCommitted("orders", borrowed.FlushNativeAsync);

        Assert.Throws<InvalidOperationException>(() => borrowed.Commit());
        await Assert.ThrowsAsync<InvalidOperationException>(() => borrowed.CommitAsync());
        Assert.Throws<InvalidOperationException>(() => borrowed.Rollback());
        await Assert.ThrowsAsync<InvalidOperationException>(() => borrowed.RollbackAsync());
        borrowed.Dispose();
        Assert.Equal(0, physical.DisposeCount);

        if (rollback)
        {
            await root.RollbackAsync();
            Assert.Equal(0, dispatcher.EnqueueCount);
            Assert.Equal(0, owner.CommitCount);
            Assert.Equal(1, owner.RollbackCount);
            Assert.Equal(0, physical.PhysicalCommits);
            Assert.Equal(1, physical.PhysicalRollbacks);
        }
        else
        {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => root.CompleteAsync());
            Assert.Equal("dispatch failed", failure.Message);
            Assert.True(root.IsCompleted);
            await root.CompleteAsync();
            Assert.Equal(1, dispatcher.EnqueueCount);
            Assert.Equal(1, owner.CommitCount);
            Assert.Equal(0, owner.RollbackCount);
            Assert.Equal(1, physical.PhysicalCommits);
            Assert.Equal(0, physical.PhysicalRollbacks);
        }

        await root.DisposeAsync();
        Assert.Equal(1, physical.DisposeCount);
    }

    private sealed class PhysicalOwnerResource : IUnitOfWorkResource
    {
        private readonly DbTransaction _transaction;

        public PhysicalOwnerResource(DbTransaction transaction) => _transaction = transaction;
        public int CommitCount { get; private set; }
        public int RollbackCount { get; private set; }

        public void Commit()
        {
            CommitCount++;
            _transaction.Commit();
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CommitCount++;
            return _transaction.CommitAsync(cancellationToken);
        }

        public void Rollback()
        {
            RollbackCount++;
            _transaction.Rollback();
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCount++;
            return _transaction.RollbackAsync(cancellationToken);
        }

        public void Dispose() => _transaction.Dispose();
        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }

    private sealed class CountingDbTransaction : DbTransaction
    {
        public int PhysicalCommits { get; private set; }
        public int PhysicalRollbacks { get; private set; }
        public int DisposeCount { get; private set; }
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => null!;

        public override void Commit() => PhysicalCommits++;
        public override void Rollback() => PhysicalRollbacks++;
        public override Task CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commit();
            return Task.CompletedTask;
        }

        public override Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Rollback();
            return Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeCount++;
            base.Dispose(disposing);
        }
    }

    private sealed class FailingDispatcher : IDispatcher
    {
        public int EnqueueCount { get; private set; }

        public ValueTask EnqueueToPublish(MediumMessage message)
        {
            EnqueueCount++;
            return new ValueTask(Task.FromException(new InvalidOperationException("dispatch failed")));
        }

        public ValueTask EnqueueToExecute(MediumMessage message, ConsumerExecutorDescriptor descriptor) =>
            ValueTask.CompletedTask;

        public ValueTask EnqueueToScheduler(MediumMessage message, DateTime executeAt, object payload) =>
            ValueTask.CompletedTask;

        public Task Start(CancellationToken stoppingToken) => Task.CompletedTask;
        public void Dispose() { }
    }
}
