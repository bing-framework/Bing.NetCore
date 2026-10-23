using Bing.Data.Transaction;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Datas.EntityFramework.Core;
using Bing.EntityFrameworkCore.Tests.Core;
using Bing.Uow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// 验证 EF Shared 仓储与应用层默认 SQL Query 使用同一 SQLite 工作单元资源。
/// </summary>
public sealed class EfCoreSqliteUnitOfWorkContractTest : IAsyncLifetime
{
    private (ServiceProvider Provider, string ConnectionString, string DatabasePath) _setup;

    public async Task InitializeAsync()
    {
        _setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices();
        await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(_setup.Provider);
    }

    public Task DisposeAsync()
    {
        _setup.Provider.Dispose();
        if (File.Exists(_setup.DatabasePath))
            File.Delete(_setup.DatabasePath);
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SharedQueries_ShouldShareEfTransactionInEitherOrder(bool repositoryFirst, bool commit)
    {
        await EfCoreSqliteUnitOfWorkTestSupport.ClearRowsAsync(_setup.ConnectionString);
        await using var diScope = _setup.Provider.CreateAsyncScope();
        var services = diScope.ServiceProvider;
        var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
        var manager = services.GetRequiredService<IUnitOfWorkScopeManager>();
        var query = services.GetRequiredService<ISqlQuery>();
        var store = new EfSqliteMappedOrderStore(context);
        await using var uow = manager.Begin();

        if (repositoryFirst)
        {
            Assert.Equal(2, await store.InsertAsync(2, "repository", CancellationToken.None));
            var transaction = context.Database.CurrentTransaction;
            Assert.NotNull(transaction);
            store.Dispose();
            Assert.Equal(System.Data.ConnectionState.Open, context.Database.GetDbConnection().State);
            Assert.Same(transaction, context.Database.CurrentTransaction);
            Assert.Equal(1, await InsertAsync(query, 1, "application"));
        }
        else
        {
            Assert.Equal(1, await InsertAsync(query, 1, "application"));
            var transaction = context.Database.CurrentTransaction;
            Assert.NotNull(transaction);
            Assert.Equal(2, await store.InsertAsync(2, "repository", CancellationToken.None));
            Assert.Same(transaction, context.Database.CurrentTransaction);
            store.Dispose();
            Assert.Equal(System.Data.ConnectionState.Open, context.Database.GetDbConnection().State);
        }

        Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(_setup.ConnectionString));
        if (commit)
            await uow.CompleteAsync();
        else
            await uow.RollbackAsync();

        Assert.Equal(commit ? 2 : 0,
            await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(_setup.ConnectionString));
        await uow.DisposeAsync();
        Assert.Null(context.Database.CurrentTransaction);
        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    /// <summary>
    /// 消息 accessor 先登记 EF 映射资源时，SaveChanges 仍只写入 root 尚未提交的事务。
    /// </summary>
    [Fact]
    public async Task ResourceAccessorFirst_ShouldUseMappedEfTransaction()
    {
        await EfCoreSqliteUnitOfWorkTestSupport.ClearRowsAsync(_setup.ConnectionString);
        await using var diScope = _setup.Provider.CreateAsyncScope();
        var services = diScope.ServiceProvider;
        var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
        await using var uow = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        var resource = services.GetRequiredService<ISqlUnitOfWorkResourceAccessor>().GetResource(uow,
            new DatabaseContext { DbKey = "orders" }, requireComparableIdentity: true);

        await resource.EnsureReadyAsync();
        Assert.Same(context.Database.CurrentTransaction!.GetDbTransaction(), resource.Transaction);
        context.Records.Add(new EfSqliteUowRecord { Id = 801, Name = "message-first-ef" });
        Assert.Equal(1, await context.SaveChangesAsync());
        Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(_setup.ConnectionString));

        await uow.CompleteAsync();

        Assert.Equal(1, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(_setup.ConnectionString));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task SaveChangesOverloadsAndLegacyCommit_ShouldFlushUntilRootCompletes(int overload)
    {
        await EfCoreSqliteUnitOfWorkTestSupport.ClearRowsAsync(_setup.ConnectionString);
        await using var diScope = _setup.Provider.CreateAsyncScope();
        var services = diScope.ServiceProvider;
        var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
        var query = services.GetRequiredService<ISqlQuery>();
        var actions = services.GetRequiredService<ITransactionActionManager>();
        await using var uow = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        context.Records.Add(new EfSqliteUowRecord { Id = 11 + overload, Name = "tracked" });
        System.Data.IDbTransaction actionTransaction = null;
        actions.Register(transaction =>
        {
            actionTransaction = transaction;
            return Task.CompletedTask;
        });

        var affected = overload switch
        {
            0 => context.SaveChanges(),
            1 => context.SaveChanges(acceptAllChangesOnSuccess: false),
            2 => await context.SaveChangesAsync(CancellationToken.None),
            3 => await context.SaveChangesAsync(acceptAllChangesOnSuccess: true, CancellationToken.None),
            4 => context.Commit(),
            5 => await context.CommitAsync(CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(overload))
        };

        Assert.Equal(1, affected);
        Assert.NotNull(context.Database.CurrentTransaction);
        Assert.Same(context.Database.CurrentTransaction.GetDbTransaction(), actionTransaction);
        Assert.Equal(0, context.SaveAfterCount);
        Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(_setup.ConnectionString));
        Assert.Equal(1, await query.Sql("SELECT COUNT(*) FROM ef_uow_records")
            .ScalarAsync<int>(cancellationToken: CancellationToken.None));
        if (overload == 1)
        {
            Assert.True(context.ChangeTracker.HasChanges());
            context.ChangeTracker.AcceptAllChanges();
        }

        await uow.CompleteAsync();

        Assert.Equal(1, context.SaveAfterCount);
        Assert.Equal(1, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(_setup.ConnectionString));
        Assert.Null(context.Database.CurrentTransaction);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueryFirst_MappedAdmissionFailureAfterGoodWrite_ShouldPoisonRoot(bool asynchronous)
    {
        var badPath = Path.Combine(Path.GetTempPath(), "bing-ef-uow", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(badPath));
        var badConnectionString = $"Data Source={badPath};Pooling=False";
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(
            contextConnectionString: badConnectionString, addGoodDataSource: true);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureRecordsTableAsync(setup.ConnectionString);
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureRecordsTableAsync(badConnectionString);
            await using var diScope = setup.Provider.CreateAsyncScope();
            var services = diScope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            await using var uow = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
            using var goodExecutor = services.GetRequiredService<ISqlExecutorFactory>().Create("good");
            using var mappedQuery = services.GetRequiredService<ISqlQueryFactory>().Create("orders");
            if (asynchronous)
            {
                await goodExecutor.ExecuteSqlAsync(
                    "INSERT INTO ef_uow_records (Id, Name) VALUES (@id, @name)",
                    new { id = 91, name = "good-before-rejected-mapped-source" });
            }
            else
            {
                goodExecutor.ExecuteSql(
                    "INSERT INTO ef_uow_records (Id, Name) VALUES (@id, @name)",
                    new { id = 91, name = "good-before-rejected-mapped-source" });
            }

            if (asynchronous)
            {
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    mappedQuery.Sql("SELECT 1").ScalarAsync<int>());
                Assert.Contains("物理数据库", exception.Message);
            }
            else
            {
                var exception = Assert.Throws<InvalidOperationException>(() =>
                    mappedQuery.Sql("SELECT 1").Scalar<int>());
                Assert.Contains("物理数据库", exception.Message);
            }

            Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
            Assert.True(uow.IsRollbackOnly);
            if (asynchronous)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => uow.CompleteAsync());
                await uow.RollbackAsync();
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => uow.Complete());
                uow.Rollback();
            }
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(badConnectionString));
        }
        finally
        {
            setup.Provider.Dispose();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
            if (File.Exists(badPath))
                File.Delete(badPath);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task MappedEfExecutorBatch_ShouldUseFrozenRootResourceAndCommitOnce(bool complete, bool asynchronous)
    {
        var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(lifecycleCounters: counters);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var diScope = setup.Provider.CreateAsyncScope();
            var services = diScope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            counters.Track(context);
            var query = services.GetRequiredService<ISqlQuery>();
            using var executor = services.GetRequiredService<ISqlExecutorFactory>().Create("orders");
            await using var uow = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();

            var id = (complete, asynchronous) switch
            {
                (true, false) => 92,
                (false, false) => 93,
                (true, true) => 94,
                _ => 95
            };
            var entities = new[] { new EfSqliteUowRecord { Id = id, Name = "mapped-batch" } };
            var options = new SqlBatchInsertOptions { UseTransaction = true };
            var affected = asynchronous
                ? await executor.InsertBatchAsync(entities, options, cancellationToken: CancellationToken.None)
                : executor.InsertBatch(entities, options);
            Assert.Equal(1, affected);
            Assert.NotNull(context.Database.CurrentTransaction);
            Assert.Equal(1, await query.Sql("SELECT COUNT(*) FROM ef_uow_records")
                .ScalarAsync<int>(cancellationToken: CancellationToken.None));
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
            Assert.Equal(1, counters.Opened);
            Assert.Equal(1, counters.TransactionsStarted);
            Assert.Equal(0, counters.TransactionsCommitted);

            if (complete)
                await uow.CompleteAsync();
            else
                await uow.RollbackAsync();
            await uow.DisposeAsync();

            Assert.Equal(complete ? 1 : 0,
                await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
            Assert.Equal(complete ? 1 : 0, counters.TransactionsCommitted);
            Assert.Equal(complete ? 0 : 1, counters.TransactionsRolledBack);
            Assert.Equal(1, counters.TransactionsDisposed);
            Assert.Equal(1, counters.Closed);
            Assert.Null(context.Database.CurrentTransaction);
            Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
        }
        finally
        {
            counters.Dispose();
            setup.Provider.Dispose();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task Rollback_ShouldKeepFlushedRowsInvisibleAndSuppressNotifications()
    {
        await EfCoreSqliteUnitOfWorkTestSupport.ClearRowsAsync(_setup.ConnectionString);
        await using var diScope = _setup.Provider.CreateAsyncScope();
        var services = diScope.ServiceProvider;
        var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
        await using var uow = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        context.Records.Add(new EfSqliteUowRecord { Id = 30, Name = "rollback" });

        Assert.Equal(1, await context.SaveChangesAsync());
        Assert.Equal(0, context.SaveAfterCount);
        Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(_setup.ConnectionString));
        await uow.RollbackAsync();

        Assert.Equal(0, context.SaveAfterCount);
        Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(_setup.ConnectionString));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MappedEfResource_ShouldObserveOnePhysicalTransactionAndConnectionLifecycle(bool commit)
    {
        var counters = new EfSqliteLifecycleCounters();
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(lifecycleCounters: counters);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var diScope = setup.Provider.CreateAsyncScope();
            var services = diScope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            counters.Track(context);
            var uow = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
            context.Records.Add(new EfSqliteUowRecord { Id = commit ? 48 : 49, Name = "physical-lifecycle" });

            Assert.Equal(1, await context.SaveChangesAsync());
            Assert.Equal(1, counters.Opened);
            Assert.Equal(0, counters.Closed);
            Assert.Equal(1, counters.TransactionsStarted);
            Assert.Equal(0, counters.TransactionsCommitted);
            Assert.Equal(0, counters.TransactionsRolledBack);

            if (commit)
                await uow.CompleteAsync();
            else
                await uow.RollbackAsync();
            await uow.DisposeAsync();

            Assert.Equal(1, counters.TransactionsCommitted + counters.TransactionsRolledBack);
            Assert.Equal(commit ? 1 : 0, counters.TransactionsCommitted);
            Assert.Equal(commit ? 0 : 1, counters.TransactionsRolledBack);
            Assert.True(counters.TransactionsDisposed == 1, counters.ObservedDiagnosticEvents);
            Assert.Equal(1, counters.Closed);
            Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
            Assert.Equal(commit ? 1 : 0,
                await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
        }
        finally
        {
            counters.Dispose();
            setup.Provider.Dispose();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task Complete_ShouldNotSaveTrackedOnlyChanges()
    {
        await EfCoreSqliteUnitOfWorkTestSupport.ClearRowsAsync(_setup.ConnectionString);
        await using var diScope = _setup.Provider.CreateAsyncScope();
        var context = diScope.ServiceProvider.GetRequiredService<EfSqliteMappedUnitOfWork>();
        await using var uow = diScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        context.Records.Add(new EfSqliteUowRecord { Id = 31, Name = "not-flushed" });

        await uow.CompleteAsync();

        Assert.True(context.ChangeTracker.HasChanges());
        Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(_setup.ConnectionString));
    }

    [Theory]
    [InlineData("completed", "save")]
    [InlineData("completed", "save-no-accept")]
    [InlineData("completed", "save-async")]
    [InlineData("completed", "save-async-no-accept")]
    [InlineData("completed", "commit")]
    [InlineData("completed", "commit-async")]
    [InlineData("rolled-back", "save")]
    [InlineData("rolled-back", "save-no-accept")]
    [InlineData("rolled-back", "save-async")]
    [InlineData("rolled-back", "save-async-no-accept")]
    [InlineData("rolled-back", "commit")]
    [InlineData("rolled-back", "commit-async")]
    [InlineData("faulted", "save")]
    [InlineData("faulted", "save-no-accept")]
    [InlineData("faulted", "save-async")]
    [InlineData("faulted", "save-async-no-accept")]
    [InlineData("faulted", "commit")]
    [InlineData("faulted", "commit-async")]
    public async Task FirstSaveAfterCurrentRootIsTerminal_ShouldRejectInsteadOfUsingLegacyCommit(
        string terminalState, string operation)
    {
        await EfCoreSqliteUnitOfWorkTestSupport.ClearRowsAsync(_setup.ConnectionString);
        await using var diScope = _setup.Provider.CreateAsyncScope();
        var services = diScope.ServiceProvider;
        var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
        var uow = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        if (terminalState == "completed")
            await uow.CompleteAsync();
        else if (terminalState == "rolled-back")
            await uow.RollbackAsync();
        else
        {
            uow.GetOrAddResource("failing-commit", () => new ThrowingCommitResource());
            await Assert.ThrowsAsync<InvalidOperationException>(() => uow.CompleteAsync());
        }
        context.Records.Add(new EfSqliteUowRecord { Id = 33, Name = "terminal-first-save" });

        InvalidOperationException exception;
        switch (operation)
        {
            case "save": exception = Assert.Throws<InvalidOperationException>(() => context.SaveChanges()); break;
            case "save-no-accept": exception = Assert.Throws<InvalidOperationException>(() => context.SaveChanges(false)); break;
            case "save-async": exception = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync()); break;
            case "save-async-no-accept": exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                context.SaveChangesAsync(false, CancellationToken.None)); break;
            case "commit": exception = Assert.Throws<InvalidOperationException>(() => context.Commit()); break;
            case "commit-async": exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                context.CommitAsync(CancellationToken.None)); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }

        Assert.Contains("终态", exception.Message);
        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
        Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(_setup.ConnectionString));
        await uow.DisposeAsync();
    }

    [Fact]
    public async Task SaveChangesFirst_WithMismatchedPhysicalDatabase_ShouldRejectBeforeOpeningEitherConnection()
    {
        var otherPath = Path.Combine(Path.GetTempPath(), "bing-ef-uow", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(otherPath));
        var otherConnectionString = $"Data Source={otherPath};Pooling=False";
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(contextConnectionString: otherConnectionString);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureRecordsTableAsync(setup.ConnectionString);
            await using var diScope = setup.Provider.CreateAsyncScope();
            var services = diScope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            await using var uow = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
            context.Records.Add(new EfSqliteUowRecord { Id = 39, Name = "wrong-physical-database" });

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());

            Assert.Contains("物理数据库", exception.Message);
            Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
            await uow.RollbackAsync();
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(otherConnectionString));
        }
        finally
        {
            setup.Provider.Dispose();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
            if (File.Exists(otherPath))
                File.Delete(otherPath);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task SaveChangesFirst_WithInvalidFrozenSource_ShouldRejectBeforeOpeningConnection(
        bool isReadOnly, bool supportsTransactions)
    {
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(configureDataSource: dataSource =>
        {
            dataSource.IsReadOnly = isReadOnly;
            dataSource.SupportsTransactions = supportsTransactions;
            dataSource.PrimaryReadStrategy = PrimaryReadStrategy.PrimaryDataSource;
            dataSource.PrimaryDataSourceKey = "primary";
        });
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var diScope = setup.Provider.CreateAsyncScope();
            var services = diScope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            await using var uow = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
            context.Records.Add(new EfSqliteUowRecord { Id = isReadOnly ? 42 : supportsTransactions ? 43 : 44,
                Name = "invalid-frozen-source" });

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());

            Assert.Contains(isReadOnly ? "只读" : supportsTransactions ? "副本" : "不支持本地事务",
                exception.Message);
            Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
            await uow.RollbackAsync();
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
        }
        finally
        {
            setup.Provider.Dispose();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task SaveChangesFirst_NonTransactionalRootWithTransactionReadStrategy_ShouldRejectBeforeOpeningConnection()
    {
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(configureDataSource: dataSource =>
        {
            dataSource.PrimaryReadStrategy = PrimaryReadStrategy.Transaction;
            dataSource.PrimaryDataSourceKey = "orders";
        });
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var diScope = setup.Provider.CreateAsyncScope();
            var services = diScope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            await using var uow = services.GetRequiredService<IUnitOfWorkScopeManager>()
                .Begin(new UnitOfWorkScopeOptions { IsTransactional = false });
            context.Records.Add(new EfSqliteUowRecord { Id = 50, Name = "non-transactional-transaction-read" });

            var exception = Assert.Throws<InvalidOperationException>(() => context.SaveChanges());

            Assert.Contains("PrimaryReadStrategy.Transaction", exception.Message);
            Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
            Assert.True(uow.IsRollbackOnly);
            await uow.RollbackAsync();
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
        }
        finally
        {
            setup.Provider.Dispose();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task QueryFirst_WithMismatchedPhysicalDatabase_ShouldRejectBeforeOpeningContextConnection()
    {
        var otherPath = Path.Combine(Path.GetTempPath(), "bing-ef-uow", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(otherPath));
        var otherConnectionString = $"Data Source={otherPath};Pooling=False";
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(contextConnectionString: otherConnectionString);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureRecordsTableAsync(setup.ConnectionString);
            await using var diScope = setup.Provider.CreateAsyncScope();
            var services = diScope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            var query = services.GetRequiredService<ISqlQuery>();
            await using var uow = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                query.Sql("SELECT 1").ScalarAsync<int>());

            Assert.Contains("物理数据库", exception.Message);
            Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
            await uow.RollbackAsync();
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(otherConnectionString));
        }
        finally
        {
            setup.Provider.Dispose();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
            if (File.Exists(otherPath))
                File.Delete(otherPath);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateCommitAfterMappedRootCompletedWithoutChanges_ShouldReturnZero(bool asynchronous)
    {
        await using var diScope = _setup.Provider.CreateAsyncScope();
        var services = diScope.ServiceProvider;
        var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
        await using var uow = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        context.Records.Add(new EfSqliteUowRecord { Id = 36, Name = "committed-before-late-commit" });
        Assert.Equal(1, await context.SaveChangesAsync());

        await uow.CompleteAsync();

        if (asynchronous)
            Assert.Equal(0, await context.CommitAsync(CancellationToken.None));
        else
            Assert.Equal(0, context.Commit());
        Assert.Equal(1, context.SaveAfterCount);
    }

    [Fact]
    public async Task EfResourcePartialCommit_ShouldRunOnlyCommittedResourceNotification()
    {
        await EfCoreSqliteUnitOfWorkTestSupport.ClearRowsAsync(_setup.ConnectionString);
        await using var diScope = _setup.Provider.CreateAsyncScope();
        var services = diScope.ServiceProvider;
        var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
        var dispatcher = services.GetRequiredService<EfSqliteRecordingDomainEventDispatcher>();
        var record = new EfSqliteUowRecord { Id = 45, Name = "partial-commit" };
        record.AddDomainEvent(new EfSqliteTestDomainEvent(45));
        context.Records.Add(record);
        var uow = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        var globalNotificationCount = 0;
        await context.SaveChangesAsync();
        uow.GetOrAddResource("later-failing-resource", () => new ThrowingCommitResource());
        uow.OnCompleted(() => { globalNotificationCount++; return Task.CompletedTask; });

        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.CompleteAsync());

        Assert.Equal(new[] { 45 }, dispatcher.Sequences);
        Assert.Empty(record.GetDomainEvents());
        Assert.Equal(1, context.SaveAfterCount);
        Assert.Equal(0, globalNotificationCount);
        Assert.Equal(1, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(_setup.ConnectionString));
        await uow.DisposeAsync();
    }

    [Fact]
    public async Task EfPostCommitNotificationFailure_ShouldRemainCommittedAndLateCommitMustNotRetryIt()
    {
        await EfCoreSqliteUnitOfWorkTestSupport.ClearRowsAsync(_setup.ConnectionString);
        await using var diScope = _setup.Provider.CreateAsyncScope();
        var services = diScope.ServiceProvider;
        var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
        var uow = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        context.ThrowOnSaveChangesAfter = true;
        context.Records.Add(new EfSqliteUowRecord { Id = 46, Name = "notification-failure" });
        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<Bing.Data.Transaction.PostCommitNotificationException>(
            () => uow.CompleteAsync());

        Assert.Equal(Bing.Data.Transaction.TransactionOutcome.Committed, exception.GetTransactionOutcome());
        Assert.Equal(1, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(_setup.ConnectionString));
        Assert.Equal(1, context.SaveAfterCount);
        Assert.Equal(0, await context.CommitAsync(CancellationToken.None));
        Assert.Equal(1, context.SaveAfterCount);
        await uow.DisposeAsync();
    }

    [Fact]
    public async Task EfReader_ShouldExcludeSaveChangesAndRootCompletionUntilReaderIsDisposed()
    {
        await using var diScope = _setup.Provider.CreateAsyncScope();
        var services = diScope.ServiceProvider;
        var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
        var query = services.GetRequiredService<ISqlQuery>();
        var uow = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        using var reader = query.Sql("SELECT 1 UNION ALL SELECT 2").AsEnumerable<int>().GetEnumerator();

        Assert.True(reader.MoveNext());
        context.Records.Add(new EfSqliteUowRecord { Id = 47, Name = "while-reader-active" });
        Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.CompleteAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.DisposeAsync().AsTask());
        Assert.True(uow.IsActive);

        reader.Dispose();
        await uow.RollbackAsync();
        await uow.DisposeAsync();
        Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(_setup.ConnectionString));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreCancelledMappedSaveChangesAsync_ShouldMarkRootRollbackOnlyWithoutOpeningConnection(
        bool acceptAllChangesOnSuccess)
    {
        await using var diScope = _setup.Provider.CreateAsyncScope();
        var services = diScope.ServiceProvider;
        var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
        await using var uow = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        context.Records.Add(new EfSqliteUowRecord { Id = acceptAllChangesOnSuccess ? 37 : 38, Name = "before-cancel" });
        Assert.Equal(1, await context.SaveChangesAsync());
        context.Records.Add(new EfSqliteUowRecord { Id = acceptAllChangesOnSuccess ? 34 : 35, Name = "cancelled" });
        var stateBeforeCancellation = context.Database.GetDbConnection().State;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var save = acceptAllChangesOnSuccess
            ? context.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellation.Token)
            : context.SaveChangesAsync(cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => save);

        Assert.True(uow.IsRollbackOnly);
        Assert.Equal(stateBeforeCancellation, context.Database.GetDbConnection().State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.CompleteAsync());
        await uow.RollbackAsync();
        Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(_setup.ConnectionString));
    }

    [Fact]
    public async Task SaveChangesFalse_ShouldRequireAcceptAllChangesAfterCommit()
    {
        await EfCoreSqliteUnitOfWorkTestSupport.ClearRowsAsync(_setup.ConnectionString);
        await using var diScope = _setup.Provider.CreateAsyncScope();
        var services = diScope.ServiceProvider;
        var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
        await using var uow = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        context.Records.Add(new EfSqliteUowRecord { Id = 32, Name = "not-accepted" });

        await context.SaveChangesAsync(acceptAllChangesOnSuccess: false, CancellationToken.None);
        await uow.CompleteAsync();

        var exception = Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
        Assert.Contains("仍有待保存更改", exception.Message);
        Assert.Equal(1, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(_setup.ConnectionString));
    }

    [Fact]
    public async Task RepeatedFlush_ShouldDispatchDomainEventsOnceAndDelayCleanupUntilCommit()
    {
        await EfCoreSqliteUnitOfWorkTestSupport.ClearRowsAsync(_setup.ConnectionString);
        await using var diScope = _setup.Provider.CreateAsyncScope();
        var services = diScope.ServiceProvider;
        var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
        var dispatcher = services.GetRequiredService<EfSqliteRecordingDomainEventDispatcher>();
        var record = new EfSqliteUowRecord { Id = 40, Name = "domain-event" };
        record.AddDomainEvent(new EfSqliteTestDomainEvent(1));
        context.Records.Add(record);
        await using var uow = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();

        await context.SaveChangesAsync();
        record.Name = "domain-event-updated";
        await context.SaveChangesAsync();

        Assert.Equal(new[] { 1 }, dispatcher.Sequences);
        Assert.Single(record.GetDomainEvents());
        Assert.Equal(0, context.SaveAfterCount);
        Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(_setup.ConnectionString));
        await uow.CompleteAsync();

        Assert.Empty(record.GetDomainEvents());
        Assert.Equal(2, context.SaveAfterCount);
        Assert.Equal(1, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(_setup.ConnectionString));
        await uow.DisposeAsync();
    }

    [Fact]
    public async Task Rollback_ShouldRetainDispatchedDomainEvents()
    {
        await EfCoreSqliteUnitOfWorkTestSupport.ClearRowsAsync(_setup.ConnectionString);
        await using var diScope = _setup.Provider.CreateAsyncScope();
        var services = diScope.ServiceProvider;
        var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
        var dispatcher = services.GetRequiredService<EfSqliteRecordingDomainEventDispatcher>();
        var record = new EfSqliteUowRecord { Id = 41, Name = "rollback-event" };
        record.AddDomainEvent(new EfSqliteTestDomainEvent(2));
        context.Records.Add(record);
        await using var uow = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();

        await context.SaveChangesAsync();
        Assert.Equal(new[] { 2 }, dispatcher.Sequences);
        await uow.RollbackAsync();

        Assert.Single(record.GetDomainEvents());
        Assert.Equal(0, context.SaveAfterCount);
        Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(_setup.ConnectionString));
    }

    [Fact]
    public async Task CapOverrideWithTransactionActions_ShouldRejectBeforeDispatch()
    {
        var provider = EfCoreSqliteUnitOfWorkTestSupport.CreateCapServices(_setup.ConnectionString);
        try
        {
            await using (var setupScope = provider.CreateAsyncScope())
                await setupScope.ServiceProvider.GetRequiredService<EfSqliteCapMappedUnitOfWork>()
                    .Database.EnsureCreatedAsync();
            await using var diScope = provider.CreateAsyncScope();
            var services = diScope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteCapMappedUnitOfWork>();
            var actions = services.GetRequiredService<ITransactionActionManager>();
            var actionCalled = false;
            actions.Register(_ =>
            {
                actionCalled = true;
                return Task.CompletedTask;
            });
            context.Records.Add(new EfSqliteUowRecord { Id = 42, Name = "cap" });
            await using var uow = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());

            Assert.Contains("TransactionCommit", exception.Message);
            Assert.False(actionCalled);
            Assert.Equal(0, context.TransactionCommitCallCount);
            Assert.True(uow.IsRollbackOnly);
            await uow.RollbackAsync();
            Assert.Equal(0, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(_setup.ConnectionString));
        }
        finally
        {
            provider.Dispose();
        }
    }

    [Fact]
    public async Task UnmappedSharedQuery_ShouldKeepLegacyAutocommitBehavior()
    {
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices(mapEfCore: false);
        try
        {
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(setup.Provider);
            await using var diScope = setup.Provider.CreateAsyncScope();
            var context = diScope.ServiceProvider.GetRequiredService<EfSqliteMappedUnitOfWork>();
            var store = new EfSqliteMappedOrderStore(context);
            await using var uow = diScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>().Begin();

            await store.InsertAsync(43, "unmapped", CancellationToken.None);
            store.Dispose();

            Assert.Null(context.Database.CurrentTransaction);
            Assert.Equal(1, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
            await uow.RollbackAsync();
            Assert.Equal(1, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(setup.ConnectionString));
        }
        finally
        {
            setup.Provider.Dispose();
            if (File.Exists(setup.DatabasePath))
                File.Delete(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task IndependentQuery_ShouldRemainOutsideMappedEfTransaction()
    {
        await EfCoreSqliteUnitOfWorkTestSupport.ClearRowsAsync(_setup.ConnectionString);
        await using var diScope = _setup.Provider.CreateAsyncScope();
        var services = diScope.ServiceProvider;
        var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
        await using var uow = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        using (var query = services.GetRequiredService<IEfCoreSqlQueryFactory>()
                   .Create(context, EfCoreSqlConnectionMode.Independent, "orders"))
        {
            await query.Sql("INSERT INTO ef_uow_records (Id, Name) VALUES (@id, @name) RETURNING Id",
                    new { id = 44, name = "independent" })
                .ScalarAsync<int>(cancellationToken: CancellationToken.None);
        }

        Assert.Null(context.Database.CurrentTransaction);
        Assert.Equal(1, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(_setup.ConnectionString));
        await uow.RollbackAsync();
        Assert.Equal(1, await EfCoreSqliteUnitOfWorkTestSupport.CountRowsAsync(_setup.ConnectionString));
    }

    [Fact]
    public async Task StoreDisposeWithoutSql_ShouldNotCreateOrOpenAQuery()
    {
        await using var diScope = _setup.Provider.CreateAsyncScope();
        var context = diScope.ServiceProvider.GetRequiredService<EfSqliteMappedUnitOfWork>();
        var store = new EfSqliteMappedOrderStore(context);

        store.Dispose();
        store.Dispose();

        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    private static Task<int> InsertAsync(ISqlQuery query, int id, string name) =>
        query.Sql("INSERT INTO ef_uow_records (Id, Name) VALUES (@id, @name) RETURNING Id", new { id, name })
            .ScalarAsync<int>(cancellationToken: CancellationToken.None);

    private sealed class ThrowingCommitResource : IUnitOfWorkResource
    {
        public void Commit() => throw new InvalidOperationException("Controlled resource commit failure.");

        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            Task.FromException(new InvalidOperationException("Controlled resource commit failure."));

        public void Rollback() { }
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

internal sealed class EfSqliteMappedOrderStore : StoreBase<EfSqliteUowRecord, int>
{
    public EfSqliteMappedOrderStore(EfSqliteMappedUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public Task<int> InsertAsync(int id, string name, CancellationToken cancellationToken) =>
        Sql.Sql("INSERT INTO ef_uow_records (Id, Name) VALUES (@id, @name) RETURNING Id", new { id, name })
            .ScalarAsync<int>(cancellationToken: cancellationToken);
}
