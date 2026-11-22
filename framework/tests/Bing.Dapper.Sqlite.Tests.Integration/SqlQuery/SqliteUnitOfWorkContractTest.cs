using System.Collections;
using System.Data;
using Bing.Dapper.MySql;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Multiple;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Mutations;
using Bing.Dapper.Tests.Infrastructure;
using Bing.Helpers;
using Bing.Uow;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Data.Common;
using System.ComponentModel.DataAnnotations;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// 通过两个真实 SQLite 文件验证 Query、Executor 与业务工作单元的连接资源边界。
/// </summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteUnitOfWorkContractTest : IAsyncLifetime
{
    /// <summary>
    /// 保存 SQLite 工作单元契约测试数据库夹具。
    /// </summary>
    private readonly SqliteIntegrationDatabaseFixture _fixture;
    /// <summary>
    /// 保存 SQLite 工作单元契约测试使用的根服务提供程序。
    /// </summary>
    private readonly ServiceProvider _rootProvider;

    /// <summary>
    /// 初始化一个 <see cref="SqliteUnitOfWorkContractTest" /> 类型的实例。
    /// </summary>
    /// <param name="fixture">为集成测试提供数据库及相关资源的测试夹具。</param>
    public SqliteUnitOfWorkContractTest(SqliteIntegrationDatabaseFixture fixture)
    {
        _fixture = fixture;
        _rootProvider = CreateProvider(fixture.FirstConnectionString, fixture.SecondConnectionString);
    }

    /// <summary>
    /// 验证同一数据源的独立 Query 和 Executor 共用事务，回滚结果可由新连接观察。
    /// </summary>
    [Fact]
    public async Task IndependentQueriesAndExecutors_ShouldShareOneTransactionAndRollback()
    {
        await using var serviceScope = _rootProvider.CreateAsyncScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        using var root = manager.Begin();
        using var executor = serviceScope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create("first");
        using var query = serviceScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create("first");

        executor.ExecuteSql("Insert Into samples(Name) Values (@name)", new { name = "uow-sync" });
        var count = query.Sql("Select Count(*) From samples").Scalar<int>();
        Assert.Equal(1, count);

        root.Rollback();
        Assert.Equal(0, await _fixture.CountAsync("first"));
    }

    /// <summary>
    /// 验证两个来源分别建立资源，整体提交后两个真实文件均包含写入。
    /// </summary>
    [Fact]
    public async Task MultipleSources_ShouldCommitEachRealDatabase()
    {
        await using var serviceScope = _rootProvider.CreateAsyncScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        await using var root = manager.Begin();
        using var first = serviceScope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create("first");
        using var second = serviceScope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create("second");

        await first.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)", new { name = "first-commit" });
        await second.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)", new { name = "second-commit" });
        await root.CompleteAsync();

        Assert.Equal(new[] { "first-commit" }, await _fixture.ReadNamesAsync("first"));
        Assert.Equal(new[] { "second-commit" }, await _fixture.ReadNamesAsync("second"));
    }

    /// <summary>
    /// 验证多数据源 root 回滚会回滚每个尚未提交的 SQLite 事务。
    /// </summary>
    [Fact]
    public async Task MultipleSources_ShouldRollbackEachRealDatabase()
    {
        await using var serviceScope = _rootProvider.CreateAsyncScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        await using var root = manager.Begin();
        using var first = serviceScope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create("first");
        using var second = serviceScope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create("second");

        await first.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)", new { name = "first-rollback" });
        await second.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)", new { name = "second-rollback" });
        await root.RollbackAsync();

        Assert.Empty(await _fixture.ReadNamesAsync("first"));
        Assert.Empty(await _fixture.ReadNamesAsync("second"));
    }

    /// <summary>
    /// 消息先于 Dapper Query 解析数据源时，两者仍登记并使用同一个物理事务资源。
    /// </summary>
    [Fact]
    public async Task ResourceAccessorFirst_ShouldShareDapperQueryResource()
    {
        var connectionFactory = new CountingSqliteConnectionFactoryResolver();
        await using var provider = CreateProvider(_fixture.FirstConnectionString, _fixture.SecondConnectionString,
            connectionFactory);
        await using var serviceScope = provider.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        await using var root = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        var accessor = services.GetRequiredService<ISqlUnitOfWorkResourceAccessor>();
        var resource = accessor.GetResource(root, new DatabaseContext { DbKey = "first" },
            requireComparableIdentity: true);
        await resource.EnsureReadyAsync();
        var name = $"message-first-{Guid.NewGuid():N}";
        using var executor = services.GetRequiredService<ISqlExecutorFactory>().Create("first");

        await executor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)", new { name });

        Assert.Equal(1, connectionFactory.CreatedCount);
        Assert.DoesNotContain(name, await _fixture.ReadNamesAsync("first"));
        await root.CompleteAsync();
        Assert.Contains(name, await _fixture.ReadNamesAsync("first"));
    }

    /// <summary>
    /// Dapper 先登记数据源资源后，消息 accessor 取得相同连接和事务资源。
    /// </summary>
    [Fact]
    public async Task DapperQueryFirst_ShouldShareResourceWithAccessor()
    {
        var connectionFactory = new CountingSqliteConnectionFactoryResolver();
        await using var provider = CreateProvider(_fixture.FirstConnectionString, _fixture.SecondConnectionString,
            connectionFactory);
        await using var serviceScope = provider.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        await using var root = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        var name = $"query-first-{Guid.NewGuid():N}";
        using var executor = services.GetRequiredService<ISqlExecutorFactory>().Create("first");

        await executor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)", new { name });
        var resource = services.GetRequiredService<ISqlUnitOfWorkResourceAccessor>().GetResource(root,
            new DatabaseContext { DbKey = "first" }, requireComparableIdentity: true);

        Assert.Equal(1, connectionFactory.CreatedCount);
        Assert.NotNull(resource.Transaction);
        Assert.DoesNotContain(name, await _fixture.ReadNamesAsync("first"));
        await root.CompleteAsync();
        Assert.Contains(name, await _fixture.ReadNamesAsync("first"));
    }

    /// <summary>
    /// 默认参与式批量写入与同源 Query 使用同一个 root 事务，提交者仅为 root。
    /// </summary>
    /// <param name="operation">用于选择被测操作的名称。</param>
    /// <param name="asynchronous">是否使用异步执行路径。</param>
    /// <param name="useTransaction">是否在本地事务内执行。</param>
    /// <param name="complete">是否完成根工作单元；否则回滚。</param>
    [Theory]
    [InlineData("insert", false, true, false)]
    [InlineData("insert", false, true, true)]
    [InlineData("insert", false, false, false)]
    [InlineData("insert", false, false, true)]
    [InlineData("insert", true, true, false)]
    [InlineData("insert", true, true, true)]
    [InlineData("insert", true, false, false)]
    [InlineData("insert", true, false, true)]
    [InlineData("update", false, true, false)]
    [InlineData("update", false, true, true)]
    [InlineData("update", false, false, false)]
    [InlineData("update", false, false, true)]
    [InlineData("update", true, true, false)]
    [InlineData("update", true, true, true)]
    [InlineData("update", true, false, false)]
    [InlineData("update", true, false, true)]
    [InlineData("delete", false, true, false)]
    [InlineData("delete", false, true, true)]
    [InlineData("delete", false, false, false)]
    [InlineData("delete", false, false, true)]
    [InlineData("delete", true, true, false)]
    [InlineData("delete", true, true, true)]
    [InlineData("delete", true, false, false)]
    [InlineData("delete", true, false, true)]
    public async Task MutationBatch_ShouldUseAmbientRootTransaction(string operation, bool asynchronous,
        bool useTransaction, bool complete)
    {
        if (operation != "insert")
            await _fixture.InsertSampleAsync("before", null, null, "first");
        var targetId = operation == "insert" ? 0 : await GetSampleIdAsync("before");

        var originalNames = await _fixture.ReadNamesAsync("first");
        var resolver = new MutationBatchConnectionFactoryResolver();
        await using var provider = CreateProvider(_fixture.FirstConnectionString, _fixture.SecondConnectionString,
            resolver);
        await using var serviceScope = provider.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        using var executor = services.GetRequiredService<ISqlExecutorFactory>().Create("first");
        using var query = services.GetRequiredService<ISqlQueryFactory>().Create("first");
        await using var root = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();

        var insert = new[] { new SqliteBatchMutationSample { Id = 501, Name = "batch-insert", Amount = 5 } };
        var update = new[] { new SqliteBatchMutationSample { Id = targetId, Name = "batch-updated", Amount = 7 } };
        var delete = new[] { new SqliteBatchMutationSample { Id = targetId, Name = "before" } };
        var insertOptions = new SqlBatchInsertOptions { UseTransaction = useTransaction };
        var updateOptions = new SqlBatchUpdateOptions { UseTransaction = useTransaction };
        var deleteOptions = new SqlBatchDeleteOptions { UseTransaction = useTransaction };

        var affected = operation switch
        {
            "insert" when asynchronous => await executor.InsertBatchAsync(insert, insertOptions),
            "insert" => executor.InsertBatch(insert, insertOptions),
            "update" when asynchronous => await executor.UpdateBatchAsync(update, updateOptions),
            "update" => executor.UpdateBatch(update, updateOptions),
            "delete" when asynchronous => await executor.DeleteBatchAsync(delete, deleteOptions),
            "delete" => executor.DeleteBatch(delete, deleteOptions),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };

        Assert.Equal(1, affected);
        var expectedInsideRoot = operation switch
        {
            "insert" => new[] { "batch-insert" },
            "update" => new[] { "batch-updated" },
            "delete" => Array.Empty<string>(),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };
        var insideRoot = asynchronous
            ? await query.Sql("Select Name From samples Order By Id").ToListAsync<string>()
            : query.Sql("Select Name From samples Order By Id").ToList<string>();
        Assert.Equal(expectedInsideRoot, insideRoot);
        Assert.Equal(originalNames, await _fixture.ReadNamesAsync("first"));
        Assert.Equal(1, resolver.CreatedCount);
        Assert.Equal(1, resolver.TransactionsBegun);
        Assert.Equal(0, resolver.TransactionsCommitted);

        if (complete)
        {
            if (asynchronous)
                await root.CompleteAsync();
            else
                root.Complete();
        }
        else
        {
            if (asynchronous)
                await root.RollbackAsync();
            else
                root.Rollback();
        }

        Assert.Equal(complete ? 1 : 0, resolver.TransactionsCommitted);
        Assert.Equal(complete ? 0 : 1, resolver.TransactionsRolledBack);
        Assert.Equal(complete ? expectedInsideRoot : originalNames, await _fixture.ReadNamesAsync("first"));
    }

    /// <summary>
    /// batch 中途 SQL 命令失败后，即使调用方捕获异常也不能提交已执行的前序命令。
    /// </summary>
    /// <param name="asynchronous">是否使用异步执行路径。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MutationBatchCommandFailure_ShouldMarkRootRollbackOnly(bool asynchronous)
    {
        var resolver = new MutationBatchConnectionFactoryResolver();
        await using var provider = CreateProvider(_fixture.FirstConnectionString, _fixture.SecondConnectionString,
            resolver);
        await using var serviceScope = provider.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        await using var root = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        using var executor = services.GetRequiredService<ISqlExecutorFactory>().Create("first");
        if (asynchronous)
            await executor.ExecuteSqlAsync("Insert Into samples(Name) Values ('before-batch-error')");
        else
            executor.ExecuteSql("Insert Into samples(Name) Values ('before-batch-error')");

        var duplicateEntities = new[]
        {
            new SqliteBatchMutationSample { Id = 601, Name = "first-window" },
            new SqliteBatchMutationSample { Id = 601, Name = "duplicate-window" }
        };
        var options = new SqlBatchInsertOptions
        {
            Strategy = SqlBatchInsertStrategy.PerEntity,
            BatchSize = 1,
            UseTransaction = true
        };

        if (asynchronous)
            await Assert.ThrowsAnyAsync<Exception>(() => executor.InsertBatchAsync(duplicateEntities, options));
        else
            Assert.ThrowsAny<Exception>(() => executor.InsertBatch(duplicateEntities, options));

        Assert.True(root.IsRollbackOnly);
        Assert.Equal(0, resolver.TransactionsCommitted);
        if (asynchronous)
            await Assert.ThrowsAsync<InvalidOperationException>(() => root.CompleteAsync());
        else
            Assert.Throws<InvalidOperationException>(() => root.Complete());
        Assert.Empty(await _fixture.ReadNamesAsync("first"));
    }

    /// <summary>
    /// 批次第二个实体触发取消时，首批写入仍属于 root 并随 rollback-only 回滚。
    /// </summary>
    [Fact]
    public async Task MutationBatchCancellationAfterFirstCommand_ShouldMarkRootRollbackOnly()
    {
        var resolver = new MutationBatchConnectionFactoryResolver();
        await using var provider = CreateProvider(_fixture.FirstConnectionString, _fixture.SecondConnectionString,
            resolver);
        await using var serviceScope = provider.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        await using var root = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        using var executor = services.GetRequiredService<ISqlExecutorFactory>().Create("first");
        using var cancellation = new CancellationTokenSource();
        var entities = CancelAfterFirstBatch(cancellation,
            new SqliteBatchMutationSample { Id = 701, Name = "cancel-first" },
            new SqliteBatchMutationSample { Id = 702, Name = "cancel-second" });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.InsertBatchAsync(entities,
            new SqlBatchInsertOptions { Strategy = SqlBatchInsertStrategy.PerEntity, BatchSize = 1 },
            cancellationToken: cancellation.Token));

        Assert.True(root.IsRollbackOnly);
        Assert.Equal(0, resolver.TransactionsCommitted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => root.CompleteAsync());
        Assert.Empty(await _fixture.ReadNamesAsync("first"));
    }

    /// <summary>
    /// 验证批量变更预先取消后标记仅回滚且不打开额外连接。
    /// </summary>
    [Fact]
    public async Task MutationBatchPreCancellation_ShouldMarkRootRollbackOnlyWithoutOpeningAnotherConnection()
    {
        var resolver = new MutationBatchConnectionFactoryResolver();
        await using var provider = CreateProvider(_fixture.FirstConnectionString, _fixture.SecondConnectionString,
            resolver);
        await using var serviceScope = provider.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        await using var root = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        using var executor = services.GetRequiredService<ISqlExecutorFactory>().Create("first");
        await executor.ExecuteSqlAsync("Insert Into samples(Name) Values ('before-batch-cancellation')");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.InsertBatchAsync(new[]
        {
            new SqliteBatchMutationSample { Id = 703, Name = "cancel-before-first-command" }
        }, new SqlBatchInsertOptions(), cancellationToken: cancellation.Token));

        Assert.True(root.IsRollbackOnly);
        Assert.Equal(1, resolver.CreatedCount);
        Assert.Equal(0, resolver.TransactionsCommitted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => root.CompleteAsync());
        Assert.Empty(await _fixture.ReadNamesAsync("first"));
    }

    /// <summary>
    /// 并发令牌造成的受影响行数验证失败会 poison root 并回滚此前批量命令。
    /// </summary>
    [Fact]
    public async Task MutationBatchAffectedRowsFailure_ShouldMarkRootRollbackOnly()
    {
        await _fixture.InsertSampleAsync("concurrency-seed", null, "stored-version", "first");
        var targetId = await GetSampleIdAsync("concurrency-seed");
        var resolver = new MutationBatchConnectionFactoryResolver();
        await using var provider = CreateProvider(_fixture.FirstConnectionString, _fixture.SecondConnectionString,
            resolver);
        await using var serviceScope = provider.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        await using var root = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        using var executor = services.GetRequiredService<ISqlExecutorFactory>().Create("first");
        executor.ExecuteSql("Insert Into samples(Name) Values ('before-row-count-failure')");
        var entity = new SqliteBatchConcurrencySample { Id = targetId, Name = "updated", SecretText = "client-version" };

        Assert.Throws<Bing.Exceptions.ConcurrencyException>(() => executor.UpdateBatch(new[] { entity },
            new SqlBatchUpdateOptions
            {
                Strategy = SqlBatchUpdateStrategy.PerEntity,
                BatchSize = 1,
                UpdateOptions = new SqlUpdateOptions<SqliteBatchConcurrencySample>()
                    .Original(item => item.SecretText, "missing-version")
            }));

        Assert.True(root.IsRollbackOnly);
        Assert.Equal(0, resolver.TransactionsCommitted);
        Assert.Throws<InvalidOperationException>(() => root.Complete());
        Assert.Equal(new[] { "concurrency-seed" }, await _fixture.ReadNamesAsync("first"));
    }

    /// <summary>
    /// 没有业务 root 时，默认批次继续自行提交。
    /// </summary>
    [Fact]
    public async Task NoUnitOfWorkBatch_ShouldKeepLegacyTransactionCommit()
    {
        await using var serviceScope = _rootProvider.CreateAsyncScope();
        using var executor = serviceScope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create("first");

        Assert.Equal(1, executor.InsertBatch(new[]
        {
            new SqliteBatchMutationSample { Id = 801, Name = "legacy-batch" }
        }));

        Assert.Equal(new[] { "legacy-batch" }, await _fixture.ReadNamesAsync("first"));
    }

    /// <summary>
    /// 显式 Owned executor 保持独立批量事务，不被业务 root 接管。
    /// </summary>
    [Fact]
    public async Task ExplicitOwnedBatch_ShouldRemainIndependentFromAmbientRoot()
    {
        await using var serviceScope = _rootProvider.CreateAsyncScope();
        await using var root = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        using var executor = serviceScope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create("first");
        using var ownedConnection = new SqliteConnection(_fixture.FirstConnectionString);
        SqlExecutionRuntimeBinding.BindOwnedConnection(executor, ownedConnection, SqlConnectionSource.DataSource);

        Assert.Equal(1, executor.InsertBatch(new[]
        {
            new SqliteBatchMutationSample { Id = 802, Name = "explicit-batch" }
        }));

        await root.RollbackAsync();
        Assert.Equal(new[] { "explicit-batch" }, await _fixture.ReadNamesAsync("first"));
    }

    /// <summary>
    /// 验证不同 SQL Provider 在受控连接适配下分别管理资源，并随同一个业务 root 完成。
    /// </summary>
    /// <remarks>两个执行连接都是真实 SQLite 文件；MySQL Provider 只验证路由与资源隔离，不代表 MySQL 服务端集成测试。</remarks>
    /// <param name="commit">是否提交事务；否则回滚。</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MixedProviders_ShouldManageIndependentResources(bool commit)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.AddSqlDataSource("sqlite", DatabaseType.Sqlite, _fixture.FirstConnectionString);
        services.AddSqlDataSource("mysql", DatabaseType.MySql, "Server=controlled;Database=controlled;");
        services.AddSqliteProvider();
        services.AddMySqlProvider();
        services.AddUnitOfWorkScopes();
        var resolver = new ControlledProviderConnectionResolver(
            _fixture.FirstConnectionString, _fixture.SecondConnectionString);
        services.Replace(ServiceDescriptor.Singleton<ISqlDbConnectionFactoryResolver>(resolver));
        services.Replace(ServiceDescriptor.Singleton<ISqlDatabaseIdentityResolver>(
            new ControlledProviderDatabaseIdentityResolver()));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var serviceScope = provider.CreateAsyncScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        await using var root = manager.Begin();
        using var sqliteExecutor = serviceScope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create("sqlite");
        using var sqliteQuery = serviceScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        using var mysqlExecutor = serviceScope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create("mysql");
        using var mysqlQuery = serviceScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create("mysql");

        await sqliteExecutor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)", new { name = "sqlite-provider" });
        mysqlExecutor.ExecuteSql("Insert Into samples(Name) Values (@name)", new { name = "mysql-provider" });

        Assert.Equal(1, await sqliteQuery.Sql("Select Count(*) From samples").ScalarAsync<int>());
        Assert.Equal(1, mysqlQuery.Sql("Select Count(*) From samples").Scalar<int>());
        Assert.Equal(1, resolver.GetCreatedCount("bing.sqlite"));
        Assert.Equal(1, resolver.GetCreatedCount("bing.mysql"));

        if (commit)
            root.Complete();
        else
            root.Rollback();

        Assert.Equal(commit ? new[] { "sqlite-provider" } : Array.Empty<string>(),
            await _fixture.ReadNamesAsync("first"));
        Assert.Equal(commit ? new[] { "mysql-provider" } : Array.Empty<string>(),
            await _fixture.ReadNamesAsync("second"));
    }

    /// <summary>
    /// 验证 Begin 前创建但首次在 root 内执行的 Query 加入当前业务事务。
    /// </summary>
    [Fact]
    public async Task PrecreatedQuery_ShouldJoinOnItsFirstExecution()
    {
        await using var serviceScope = _rootProvider.CreateAsyncScope();
        var queryFactory = serviceScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        using var query = queryFactory.Create("first");
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        await using var root = manager.Begin();
        using var executor = serviceScope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create("first");

        await executor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)", new { name = "precreated" });
        Assert.Equal(1, await query.Sql("Select Count(*) From samples").ScalarAsync<int>());
        await root.RollbackAsync();

        Assert.Equal(0, await _fixture.CountAsync("first"));
    }

    /// <summary>
    /// 验证已在 Uow 外首次执行的默认 Query 不会迁移到之后的事务 root。
    /// </summary>
    /// <param name="asynchronous">是否使用异步执行路径。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviouslyStandaloneQuery_ShouldRejectJoiningLaterRoot(bool asynchronous)
    {
        await using var serviceScope = _rootProvider.CreateAsyncScope();
        var queryFactory = serviceScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        using var query = queryFactory.Create("first");
        Assert.Equal(0, query.Sql("Select Count(*) From samples").Scalar<int>());

        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        await using var root = manager.Begin();
        using var executor = serviceScope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create("first");
        await executor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)", new { name = "uncommitted" });

        InvalidOperationException exception;
        if (asynchronous)
            exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                query.Sql("Select Count(*) From samples").ScalarAsync<int>());
        else
            exception = Assert.Throws<InvalidOperationException>(() =>
                query.Sql("Select Count(*) From samples").Scalar<int>());
        Assert.Contains("不能迁移", exception.Message);
        await root.RollbackAsync();
        Assert.Equal(0, await _fixture.CountAsync("first"));
    }

    /// <summary>
    /// 验证已在 Uow 外执行的默认 Executor 不能在后续 root 中继续自动提交。
    /// </summary>
    /// <param name="asynchronous">是否使用异步执行路径。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviouslyStandaloneExecutor_ShouldRejectJoiningLaterRoot(bool asynchronous)
    {
        await using var serviceScope = _rootProvider.CreateAsyncScope();
        using var executor = serviceScope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create("first");
        await executor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)", new { name = "standalone" });

        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        await using var root = manager.Begin();
        InvalidOperationException exception;
        if (asynchronous)
            exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                executor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)",
                    new { name = "must-not-escape" }));
        else
            exception = Assert.Throws<InvalidOperationException>(() =>
                executor.ExecuteSql("Insert Into samples(Name) Values (@name)",
                    new { name = "must-not-escape" }));

        Assert.Contains("不能迁移", exception.Message);
        await root.RollbackAsync();
        Assert.Equal(new[] { "standalone" }, await _fixture.ReadNamesAsync("first"));
    }

    /// <summary>
    /// 验证未知物理身份仍可执行，并由 root 管理各 Query 独立资源的回滚。
    /// </summary>
    [Fact]
    public async Task OpaqueResolverQueries_ShouldUseSeparateRootResourcesAndBothRollback()
    {
        var resolver = new OpaqueRotatingSqliteConnectionFactoryResolver(
            _fixture.FirstConnectionString, _fixture.SecondConnectionString);
        await using var provider = CreateProvider(_fixture.FirstConnectionString, _fixture.SecondConnectionString,
            resolver, new OpaqueSqliteDatabaseIdentityResolver());
        await using var serviceScope = provider.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        await using var root = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        using var first = services.GetRequiredService<ISqlExecutorFactory>().Create("first");
        using var second = services.GetRequiredService<ISqlExecutorFactory>().Create("first");

        await first.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)", new { name = "opaque-first" });
        await second.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)", new { name = "opaque-second" });

        Assert.Equal(2, resolver.CreatedCount);
        await root.RollbackAsync();
        await root.DisposeAsync();

        Assert.Empty(await _fixture.ReadNamesAsync("first"));
        Assert.Empty(await _fixture.ReadNamesAsync("second"));
        Assert.All(resolver.Connections, connection => Assert.Equal(System.Data.ConnectionState.Closed, connection.State));
    }

    /// <summary>
    /// 验证 unknown identity 不放宽真正外部连接的物理身份校验。
    /// </summary>
    [Fact]
    public async Task OpaqueExternalConnection_ShouldStillFailClosed()
    {
        await using var provider = CreateProvider(_fixture.FirstConnectionString, _fixture.SecondConnectionString,
            identityResolver: new OpaqueSqliteDatabaseIdentityResolver());
        await using var serviceScope = provider.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        using var query = services.GetRequiredService<ISqlQueryFactory>().Create("first");
        using var connection = new SqliteConnection(_fixture.FirstConnectionString);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SqlQueryRuntimeBinding.BindExternalConnection(query, connection, SqlConnectionSource.External));

        Assert.Contains("外部连接数据库身份与 Query 上下文不一致", exception.Message);
    }

    /// <summary>
    /// 验证各真实异步执行入口在预取消时毒化已有 root，且不创建额外连接。
    /// </summary>
    /// <param name="operation">用于选择被测操作的名称。</param>
    [Theory]
    [InlineData("scalar")]
    [InlineData("list")]
    [InlineData("executor")]
    [InlineData("paging")]
    [InlineData("stream")]
    [InlineData("multiple")]
    public async Task PreCancelledAsyncExecution_ShouldMarkRootRollbackOnlyWithoutCreatingConnection(string operation)
    {
        var resolver = new CountingSqliteConnectionFactoryResolver();
        await using var provider = CreateProvider(_fixture.FirstConnectionString, _fixture.SecondConnectionString,
            resolver);
        await using var serviceScope = provider.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        await using var root = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        using var query = services.GetRequiredService<ISqlQueryFactory>().Create("first");
        using var executor = services.GetRequiredService<ISqlExecutorFactory>().Create("first");
        using var multiple = services.GetRequiredService<ISqlMultipleQueryExecutorFactory>().Create("first");
        await executor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)",
            new { name = "before-cancel" });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var token = cancellation.Token;

        async Task ExecuteAsync()
        {
            switch (operation)
            {
                case "scalar":
                    await query.Sql("Select 1").ScalarAsync<int>(cancellationToken: token);
                    break;
                case "list":
                    await query.Query().Select("1").ToListAsync<int>(token);
                    break;
                case "executor":
                    await executor.ExecuteSqlAsync("Select 1", cancellationToken: token);
                    break;
                case "paging":
                    await query.Query().Select("1").ToPageAsync<int>(new Pager(1, 10), cancellationToken: token);
                    break;
                case "stream":
                    await using (var stream = query.Sql("Select 1").AsAsyncEnumerable<int>()
                                     .GetAsyncEnumerator(token))
                        await stream.MoveNextAsync();
                    break;
                case "multiple":
                    var command = multiple.CreateBatch().Append("Select 1").Build();
                    using (await multiple.ExecuteAsync(command, cancellationToken: token))
                    {
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
            }
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(ExecuteAsync);
        Assert.True(root.IsRollbackOnly);
        Assert.Equal(1, resolver.CreatedCount);
        await Assert.ThrowsAsync<InvalidOperationException>(() => root.CompleteAsync());
        await root.RollbackAsync();
        Assert.Empty(await _fixture.ReadNamesAsync("first"));
    }

    /// <summary>
    /// 验证 root deadline 在真实 SQLite reader 执行期间仍能通过 linked token 取消，并在流退出时释放连接。
    /// </summary>
    [Fact]
    public async Task AsyncStream_WhenRootDeadlineExpires_ShouldCancelReaderAndReleaseConnection()
    {
        var resolver = new MutationBatchConnectionFactoryResolver();
        await using var provider = CreateProvider(_fixture.FirstConnectionString, _fixture.SecondConnectionString,
            resolver, addBingOperations: true);
        await using var serviceScope = provider.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        using var query = services.GetRequiredService<ISqlQueryFactory>().Create("first");
        using var explicitCancellation = new CancellationTokenSource();
        await using var root = services.GetRequiredService<IUnitOfWorkOperationManager>().BeginOperation(
            new UnitOfWorkScopeOptions { Timeout = TimeSpan.FromSeconds(2) });
        await using var reader = query.Sql("Select 1 As Value Union All Select 2 Union All Select 3")
            .AsAsyncEnumerable<long>(cancellationToken: explicitCancellation.Token)
            .GetAsyncEnumerator();

        Assert.True(await reader.MoveNextAsync());
        var connection = Assert.IsType<MutationBatchConnection>(resolver.LastConnection);
        var providerToken = connection.LastExecuteReaderCancellationToken;
        Assert.True(providerToken.CanBeCanceled);
        Assert.NotEqual(root.CancellationToken, providerToken);
        Assert.NotEqual(explicitCancellation.Token, providerToken);

        await Task.Delay(TimeSpan.FromMilliseconds(2300));

        Assert.True(root.CancellationToken.IsCancellationRequested);
        Assert.True(providerToken.IsCancellationRequested);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.MoveNextAsync().AsTask());
        Assert.True(root.IsRollbackOnly);

        await reader.DisposeAsync();
        await root.RollbackAsync();
        await root.DisposeAsync();

        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
        Assert.Empty(await _fixture.ReadNamesAsync("first"));
    }

    /// <summary>
    /// 验证参与者取消读取时取消异步流并释放读取器。
    /// </summary>
    [Fact]
    public async Task AsyncStream_WhenParticipantCancelsDuringReaderRead_ShouldCancelAndReleaseReader()
    {
        var resolver = new MutationBatchConnectionFactoryResolver(blockReaderRead: true);
        await using var provider = CreateProvider(_fixture.FirstConnectionString, _fixture.SecondConnectionString,
            resolver, addBingOperations: true);
        await using var serviceScope = provider.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        var manager = services.GetRequiredService<IUnitOfWorkOperationManager>();
        var root = manager.BeginOperation(new UnitOfWorkScopeOptions { Timeout = TimeSpan.FromSeconds(30) });
        using var cancellation = new CancellationTokenSource();
        var participant = manager.BeginOperation(cancellationToken: cancellation.Token);
        var query = services.GetRequiredService<ISqlQueryFactory>().Create("first");
        var reader = query.Sql("Select 1 As Value Union All Select 2")
            .AsAsyncEnumerable<long>(cancellationToken: CancellationToken.None)
            .GetAsyncEnumerator();

        var firstRead = reader.MoveNextAsync().AsTask();
        MutationBatchConnection connection = null;
        var readerDisposed = false;
        var participantDisposed = false;
        var rootDisposed = false;
        var queryDisposed = false;
        try
        {
            connection = await resolver.ReaderReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var providerToken = connection.LastReaderReadCancellationToken;
            Assert.True(providerToken.CanBeCanceled);
            Assert.NotEqual(root.CancellationToken, providerToken);
            Assert.NotEqual(cancellation.Token, providerToken);

            cancellation.Cancel();
            Assert.True(providerToken.IsCancellationRequested);
            resolver.ReleaseReaderRead();
            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstRead);

            Assert.True(exception.CancellationToken.IsCancellationRequested);
            await reader.DisposeAsync();
            readerDisposed = true;
            await participant.DisposeAsync();
            participantDisposed = true;
            Assert.True(root.IsRollbackOnly);
            Assert.Equal(0, resolver.TransactionsCommitted);
            await root.DisposeAsync();
            rootDisposed = true;
            query.Dispose();
            queryDisposed = true;

            Assert.Equal(1, resolver.TransactionsRolledBack);
            Assert.Equal(1, connection.ReaderDisposeCount);
            Assert.Equal(1, connection.DisposeCount);
            Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
        }
        finally
        {
            resolver.ReleaseReaderRead();
            if (firstRead.IsCompleted == false)
            {
                try
                {
                    await firstRead;
                }
                catch (OperationCanceledException)
                {
                }
            }
            if (readerDisposed == false)
                await reader.DisposeAsync();
            if (participantDisposed == false)
                await participant.DisposeAsync();
            if (rootDisposed == false)
                await root.DisposeAsync();
            if (queryDisposed == false)
                query.Dispose();
            if (connection?.State != System.Data.ConnectionState.Closed)
                connection?.Close();
        }
    }

    /// <summary>
    /// 验证无效多结果命令即使令牌已取消也不标记根操作仅回滚。
    /// </summary>
    [Fact]
    public async Task InvalidMultipleCommand_ShouldNotMarkRootRollbackOnlyEvenWhenTokenIsCancelled()
    {
        var resolver = new CountingSqliteConnectionFactoryResolver();
        await using var provider = CreateProvider(_fixture.FirstConnectionString, _fixture.SecondConnectionString,
            resolver);
        await using var serviceScope = provider.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        await using var root = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        using var multiple = services.GetRequiredService<ISqlMultipleQueryExecutorFactory>().Create("first");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<ArgumentNullException>(() => multiple.ExecuteAsync(null,
            cancellationToken: cancellation.Token));

        Assert.False(root.IsRollbackOnly);
        Assert.Equal(0, resolver.CreatedCount);
        await root.CompleteAsync();
    }

    /// <summary>
    /// 验证同一 Uow 下的同步和异步分页都读取到自身事务中的写入。
    /// </summary>
    /// <param name="asynchronous">是否使用异步执行路径。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PagedQueries_ShouldShareResourceForSyncAndAsyncPlans(bool asynchronous)
    {
        await using var serviceScope = _rootProvider.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        await using var root = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        using var executor = services.GetRequiredService<ISqlExecutorFactory>().Create("first");
        using var query = services.GetRequiredService<ISqlQueryFactory>().Create("first");
        await executor.ExecuteSqlAsync("Insert Into samples(Name) Values ('page-a'), ('page-b')");
        var description = query.Query().Select("Name").From("samples").OrderBy("Id");
        var pager = new Pager(1, 1) { Order = "Id" };

        var page = asynchronous
            ? await description.ToPageAsync<string>(pager)
            : description.ToPage<string>(pager);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { "page-a" }, page.Data);
        await root.RollbackAsync();
        Assert.Empty(await _fixture.ReadNamesAsync("first"));
    }

    /// <summary>
    /// 验证 requiresNew 拒绝外层 Query，退出内层后外层资源仍可继续使用。
    /// </summary>
    [Fact]
    public async Task RequiresNew_ShouldRejectOuterQueryAndRestoreOuterRoot()
    {
        await using var serviceScope = _rootProvider.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        var manager = services.GetRequiredService<IUnitOfWorkScopeManager>();
        using var query = services.GetRequiredService<ISqlQueryFactory>().Create("first");
        await using var outer = manager.Begin();

        Assert.Equal(0, query.Sql("Select Count(*) From samples").Scalar<int>());
        await using (var inner = manager.Begin(
                         new UnitOfWorkScopeOptions { IsTransactional = false }, requiresNew: true))
        {
            Assert.Throws<InvalidOperationException>(() => query.Sql("Select Count(*) From samples").Scalar<int>());
            using var innerQuery = services.GetRequiredService<ISqlQueryFactory>().Create("first");
            Assert.Equal(0, await innerQuery.Sql("Select Count(*) From samples").ScalarAsync<int>());
            await inner.CompleteAsync();
        }

        Assert.Equal(0, query.Sql("Select Count(*) From samples").Scalar<int>());
        await outer.RollbackAsync();
    }

    /// <summary>
    /// 验证活动同步 reader 持有同一 root 资源租约直到 reader 释放。
    /// </summary>
    [Fact]
    public async Task SynchronousReader_ShouldHoldResourceLeaseUntilDisposed()
    {
        await _fixture.InsertSampleAsync("reader", null, null, "first");
        await using var serviceScope = _rootProvider.CreateAsyncScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        await using var root = manager.Begin();
        using var first = serviceScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create("first");
        using var second = serviceScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create("first");
        using var enumerator = first.Sql("Select Name From samples").AsEnumerable<string>().GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => second.Sql("Select Count(*) From samples").Scalar<int>());

        enumerator.Dispose();
        Assert.Equal(1, second.Sql("Select Count(*) From samples").Scalar<int>());
        await root.RollbackAsync();
    }

    /// <summary>
    /// 验证活动异步 reader 持有同一 root 资源租约直到 reader 释放。
    /// </summary>
    [Fact]
    public async Task AsynchronousReader_ShouldHoldResourceLeaseUntilDisposed()
    {
        await _fixture.InsertSampleAsync("reader", null, null, "first");
        await using var serviceScope = _rootProvider.CreateAsyncScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        await using var root = manager.Begin();
        using var first = serviceScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create("first");
        using var second = serviceScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create("first");
        await using var enumerator = first.Sql("Select Name From samples").AsAsyncEnumerable<string>()
            .GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await second.Sql("Select Count(*) From samples").ScalarAsync<int>());

        await enumerator.DisposeAsync();
        Assert.Equal(1, await second.Sql("Select Count(*) From samples").ScalarAsync<int>());
        await root.RollbackAsync();
    }

    /// <summary>
    /// 验证 Multiple 结果对象存活期间会持有同一数据源资源租约。
    /// </summary>
    [Fact]
    public async Task MultipleResult_ShouldHoldResourceLeaseUntilDisposed()
    {
        await _fixture.InsertSampleAsync("multiple", null, null, "first");
        await using var serviceScope = _rootProvider.CreateAsyncScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        await using var root = manager.Begin();
        using var multiple = serviceScope.ServiceProvider.GetRequiredService<ISqlMultipleQueryExecutorFactory>()
            .Create("first");
        using var query = serviceScope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create("first");
        var command = multiple.CreateBatch().Append("Select Name From samples").Append("Select Count(*) From samples")
            .Build();

        using (var result = multiple.Execute(command))
        {
            Assert.Throws<InvalidOperationException>(() => query.Sql("Select Count(*) From samples").Scalar<int>());
            Assert.Single(result.Read<string>());
            Assert.Single(result.Read<int>());
        }

        Assert.Equal(1, query.Sql("Select Count(*) From samples").Scalar<int>());
        await root.RollbackAsync();
    }

    /// <summary>
    /// 未读取完 Multiple 结果时释放读取器会将共享 root 标记为仅回滚。
    /// </summary>
    [Fact]
    public async Task IncompleteMultipleResult_ShouldMarkRootRollbackOnly()
    {
        await using var serviceScope = _rootProvider.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        await using var root = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        using var multiple = services.GetRequiredService<ISqlMultipleQueryExecutorFactory>().Create("first");
        var command = multiple.CreateBatch().Append("Select 1").Append("Select 2").Build();

        using (var result = multiple.Execute(command))
            Assert.Equal(new[] { 1 }, result.Read<int>());

        Assert.True(root.IsRollbackOnly);
        await Assert.ThrowsAsync<InvalidOperationException>(() => root.CompleteAsync());
    }

    /// <summary>
    /// 活动流式 reader 阻止 root 完成；提前释放后 root 进入仅回滚状态。
    /// </summary>
    [Fact]
    public async Task ActiveStream_ShouldBlockRootCompletionAndMarkEarlyDisposeRollbackOnly()
    {
        await using var serviceScope = _rootProvider.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        await using var root = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        using var query = services.GetRequiredService<ISqlQueryFactory>().Create("first");
        using var enumerator = query.Sql("Select 1 Union All Select 2").AsEnumerable<int>().GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => root.Complete());
        Assert.True(root.IsActive);
        enumerator.Dispose();

        Assert.True(root.IsRollbackOnly);
        root.Rollback();
    }

    /// <summary>
    /// 验证显式 SQL 单库事务在业务 root 内仍独立提交，不参与 root 回滚。
    /// </summary>
    [Fact]
    public async Task ExplicitSqlTransactionInsideAmbient_ShouldRemainIndependent()
    {
        await using var serviceScope = _rootProvider.CreateAsyncScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        await using var root = manager.Begin();
        var transactionFactory = serviceScope.ServiceProvider.GetRequiredService<ISqlTransactionScopeFactory>();
        await using (var transaction = await transactionFactory.BeginAsync("first"))
        {
            using var executor = transaction.CreateExecutor();
            await executor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)",
                new { name = "explicit-transaction" });
            await transaction.CommitAsync();
        }

        using (var executor = serviceScope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create("first"))
            await executor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)",
                new { name = "ambient-transaction" });
        await root.RollbackAsync();

        Assert.Equal(new[] { "explicit-transaction" }, await _fixture.ReadNamesAsync("first"));
    }

    /// <summary>
    /// 验证没有业务 Uow 时写入仍保持原有自动提交行为。
    /// </summary>
    [Fact]
    public async Task NoUnitOfWork_ShouldKeepLegacyAutoCommitBehavior()
    {
        await using var serviceScope = _rootProvider.CreateAsyncScope();
        using var executor = serviceScope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create("first");

        await executor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)", new { name = "legacy" });

        Assert.Equal(new[] { "legacy" }, await _fixture.ReadNamesAsync("first"));
    }

    /// <summary>
    /// 验证共享事务中的同步或异步 SQL 命令失败会阻止 root 提交。
    /// </summary>
    /// <param name="asynchronous">是否使用异步执行路径。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommandFailure_ShouldMarkSharedRootRollbackOnly(bool asynchronous)
    {
        await using var serviceScope = _rootProvider.CreateAsyncScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        await using var root = manager.Begin();
        using var executor = serviceScope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create("first");
        if (asynchronous)
            await executor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)", new { name = "before-failure" });
        else
            executor.ExecuteSql("Insert Into samples(Name) Values (@name)", new { name = "before-failure" });

        if (asynchronous)
            await Assert.ThrowsAnyAsync<Exception>(() => executor.ExecuteSqlAsync(
                "Insert Into missing_uow_table(Name) Values ('failure')"));
        else
            Assert.ThrowsAny<Exception>(() => executor.ExecuteSql(
                "Insert Into missing_uow_table(Name) Values ('failure')"));

        Assert.True(root.IsRollbackOnly);
        await Assert.ThrowsAsync<InvalidOperationException>(() => root.CompleteAsync());
        Assert.Empty(await _fixture.ReadNamesAsync("first"));
    }

    /// <inheritdoc />
    public async Task InitializeAsync() => await _fixture.ResetAsync();

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        _rootProvider.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 在枚举首批数据后取消后续批次。
    /// </summary>
    /// <param name="cancellation">用于触发测试取消的令牌源。</param>
    /// <param name="first">第一个映射或批次来源对象。</param>
    /// <param name="second">第二个映射或批次来源对象。</param>
    /// <returns>返回首个样本后触发取消的批次序列。</returns>
    private static IEnumerable<SqliteBatchMutationSample> CancelAfterFirstBatch(
        CancellationTokenSource cancellation, SqliteBatchMutationSample first,
        SqliteBatchMutationSample second)
    {
        yield return first;
        cancellation.Cancel();
        yield return second;
    }

    /// <summary>
    /// 按名称读取测试记录标识。
    /// </summary>
    /// <param name="name">测试对象的名称属性值。</param>
    /// <returns>指定名称对应的测试记录标识。</returns>
    private async Task<int> GetSampleIdAsync(string name)
    {
        await using var connection = new SqliteConnection(_fixture.FirstConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "Select Id From samples Where Name = @name";
        command.Parameters.AddWithValue("@name", name);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// 创建多数据源工作单元测试容器。
    /// </summary>
    /// <param name="firstConnectionString">第一个测试数据库的连接字符串。</param>
    /// <param name="secondConnectionString">第二个测试数据库的连接字符串。</param>
    /// <param name="connectionFactoryResolver">数据库连接工厂解析器。</param>
    /// <param name="identityResolver">物理数据库标识解析器。</param>
    /// <param name="addBingOperations">是否注册操作执行服务。</param>
    /// <returns>注册了当前测试依赖的服务容器。</returns>
    private static ServiceProvider CreateProvider(string firstConnectionString, string secondConnectionString,
        ISqlDbConnectionFactoryResolver connectionFactoryResolver = null,
        ISqlDatabaseIdentityResolver identityResolver = null, bool addBingOperations = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureSqlMetadata(options => options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(SqliteBatchMutationSample),
            TableName = "samples"
        }));
        services.ConfigureSqlMetadata(options => options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(SqliteBatchConcurrencySample),
            TableName = "samples"
        }));
        services.AddSqlCore();
        services.AddSqlDataSource("first", DatabaseType.Sqlite, firstConnectionString);
        services.AddSqlDataSource("second", DatabaseType.Sqlite, secondConnectionString);
        services.AddSqliteProvider();
        if (addBingOperations)
            services.AddBingOperations();
        else
            services.AddUnitOfWorkScopes();
        if (connectionFactoryResolver != null)
            services.Replace(ServiceDescriptor.Singleton(connectionFactoryResolver));
        if (identityResolver != null)
            services.Replace(ServiceDescriptor.Singleton(identityResolver));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    /// <summary>
    /// 提供 SQLite 批量并发测试使用的实体数据。
    /// </summary>
    private sealed class SqliteBatchConcurrencySample
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置实体或结果名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 获取或设置测试使用的数值。
        /// </summary>
        public decimal? Amount { get; set; }

        /// <summary>
        /// 获取或设置批量并发测试记录中的文本内容。
        /// </summary>
        [ConcurrencyCheck]
        public string SecretText { get; set; }
    }

    /// <summary>
    /// 提供 SQLite 批量变更测试使用的实体数据。
    /// </summary>
    private sealed class SqliteBatchMutationSample
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置实体或结果名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 获取或设置测试使用的数值。
        /// </summary>
        public decimal? Amount { get; set; }
    }

    /// <summary>
    /// 记录 SQLite 批量执行并控制读取阻塞的连接工厂解析器。
    /// </summary>
    private sealed class MutationBatchConnectionFactoryResolver : ISqlDbConnectionFactoryResolver
    {
        /// <summary>
        /// 保存变更批次连接工厂创建的连接记录。
        /// </summary>
        private readonly List<MutationBatchConnection> _connections = new();

        /// <summary>
        /// 初始化一个 <see cref="MutationBatchConnectionFactoryResolver" /> 类型的实例。
        /// </summary>
        /// <param name="blockReaderRead">指示 SQLite 测试读取器是否阻塞读取操作。</param>
        public MutationBatchConnectionFactoryResolver(bool blockReaderRead = false) =>
            BlockReaderRead = blockReaderRead;

        /// <summary>
        /// 获取是否阻塞结果读取以测试取消行为。
        /// </summary>
        public bool BlockReaderRead { get; }

        /// <summary>
        /// 获取读取结果行开始时使用的通知源。
        /// </summary>
        public TaskCompletionSource<MutationBatchConnection> ReaderReadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 控制读取器读取在测试释放前保持阻塞。
        /// </summary>
        private readonly TaskCompletionSource _releaseReaderRead =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取创建实例的累计次数。
        /// </summary>
        public int CreatedCount => _connections.Count;
        /// <summary>
        /// 获取最近创建的数据库连接。
        /// </summary>
        public MutationBatchConnection LastConnection => _connections.LastOrDefault();
        /// <summary>
        /// 获取事务开启的累计次数。
        /// </summary>
        public int TransactionsBegun => _connections.Sum(connection => connection.TransactionsBegun);
        /// <summary>
        /// 获取事务提交的累计次数。
        /// </summary>
        public int TransactionsCommitted => _connections.Sum(connection => connection.TransactionsCommitted);
        /// <summary>
        /// 获取事务回滚的累计次数。
        /// </summary>
        public int TransactionsRolledBack => _connections.Sum(connection => connection.TransactionsRolledBack);

        /// <summary>
        /// 释放读取器读取操作的等待门。
        /// </summary>
        public void ReleaseReaderRead() => _releaseReaderRead.TrySetResult();

        /// <inheritdoc />
        public System.Data.IDbConnection Create(string providerKey, string connectionString)
        {
            var connection = new MutationBatchConnection(connectionString, this);
            _connections.Add(connection);
            return connection;
        }

        /// <summary>
        /// 发出读取器开始读取的连接信号。
        /// </summary>
        /// <param name="connection">执行测试命令的数据库连接。</param>
        public void SignalReaderReadStarted(MutationBatchConnection connection) => ReaderReadStarted.TrySetResult(connection);

        /// <summary>
        /// 等待读取器读取操作被释放。
        /// </summary>
        public Task WaitForReaderReadReleaseAsync() => _releaseReaderRead.Task;
    }

    /// <summary>
    /// 包装 SQLite 连接并记录批量执行的数据库连接替身。
    /// </summary>
    private sealed class MutationBatchConnection : DbConnection
    {
        /// <summary>
        /// 保存被包装的 SQLite 连接。
        /// </summary>
        private readonly SqliteConnection _inner;
        /// <summary>
        /// 保存创建该连接的解析器。
        /// </summary>
        private readonly MutationBatchConnectionFactoryResolver _resolver;
        /// <summary>
        /// 记录连接的释放状态。
        /// </summary>
        private int _disposed;

        /// <summary>
        /// 初始化一个 <see cref="MutationBatchConnection" /> 类型的实例。
        /// </summary>
        /// <param name="connectionString">用于建立数据库连接的连接字符串。</param>
        /// <param name="resolver">用于解析对应实例或配置的解析器。</param>
        public MutationBatchConnection(string connectionString, MutationBatchConnectionFactoryResolver resolver)
        {
            _inner = new SqliteConnection(connectionString);
            _resolver = resolver;
        }

        /// <summary>
        /// 获取事务开启的累计次数。
        /// </summary>
        public int TransactionsBegun { get; private set; }
        /// <summary>
        /// 获取事务提交的累计次数。
        /// </summary>
        public int TransactionsCommitted { get; private set; }
        /// <summary>
        /// 获取事务回滚的累计次数。
        /// </summary>
        public int TransactionsRolledBack { get; private set; }
        /// <summary>
        /// 获取释放资源的累计次数。
        /// </summary>
        public int DisposeCount { get; private set; }
        /// <summary>
        /// 获取最近执行读取命令时接收的取消令牌。
        /// </summary>
        public CancellationToken LastExecuteReaderCancellationToken { get; private set; }
        /// <summary>
        /// 获取或设置最近读取结果行时接收的取消令牌。
        /// </summary>
        public CancellationToken LastReaderReadCancellationToken { get; set; }
        /// <summary>
        /// 获取或设置释放读取器的累计次数。
        /// </summary>
        public int ReaderDisposeCount { get; set; }
        /// <summary>
        /// 获取当前连接关联的测试连接工厂解析器。
        /// </summary>
        public MutationBatchConnectionFactoryResolver Resolver => _resolver;
        /// <inheritdoc />
        public override string ConnectionString { get => _inner.ConnectionString; set => _inner.ConnectionString = value; }
        /// <inheritdoc />
        public override string Database => _inner.Database;
        /// <inheritdoc />
        public override string DataSource => _inner.DataSource;
        /// <inheritdoc />
        public override string ServerVersion => _inner.ServerVersion;
        /// <inheritdoc />
        public override System.Data.ConnectionState State => _inner.State;
        /// <inheritdoc />
        public override int ConnectionTimeout => _inner.ConnectionTimeout;

        /// <inheritdoc />
        public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);
        /// <inheritdoc />
        public override void Close() => _inner.Close();
        /// <inheritdoc />
        public override void Open() => _inner.Open();

        /// <inheritdoc />
        protected override DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel)
        {
            TransactionsBegun++;
            return new MutationBatchTransaction(_inner.BeginTransaction(isolationLevel), this);
        }

        /// <inheritdoc />
        protected override DbCommand CreateDbCommand() => new MutationBatchCommand(_inner.CreateCommand(), this);

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                DisposeCount++;
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <inheritdoc />
        public override async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                DisposeCount++;
                await _inner.DisposeAsync().ConfigureAwait(false);
            }
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 包装 SQLite 事务并转发事务操作的测试替身。
        /// </summary>
        private sealed class MutationBatchTransaction : DbTransaction
        {
            /// <summary>
            /// 保存变更批次事务所属的连接。
            /// </summary>
            private readonly MutationBatchConnection _owner;
            /// <summary>
            /// 保存被包装的 SQLite 事务。
            /// </summary>
            private readonly SqliteTransaction _inner;
            /// <summary>
            /// 指示变更批次事务是否已释放。
            /// </summary>
            private bool _disposed;

            /// <summary>
            /// 初始化一个 <see cref="MutationBatchTransaction" /> 类型的实例。
            /// </summary>
            /// <param name="inner">由当前包装器保留或转发的内部对象或异常。</param>
            /// <param name="owner">拥有当前实例或资源的对象。</param>
            public MutationBatchTransaction(SqliteTransaction inner, MutationBatchConnection owner)
            {
                _inner = inner;
                _owner = owner;
            }

            /// <inheritdoc />
            public override System.Data.IsolationLevel IsolationLevel => _inner.IsolationLevel;
            /// <inheritdoc />
            protected override DbConnection DbConnection => _owner;
            /// <summary>
            /// 获取当前包装的内部实例。
            /// </summary>
            public SqliteTransaction Inner => _inner;

            /// <inheritdoc />
            public override void Commit()
            {
                _inner.Commit();
                _owner.TransactionsCommitted++;
            }

            /// <inheritdoc />
            public override void Rollback()
            {
                _inner.Rollback();
                _owner.TransactionsRolledBack++;
            }

            /// <inheritdoc />
            public override Task CommitAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Commit();
                return Task.CompletedTask;
            }

            /// <inheritdoc />
            public override Task RollbackAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Rollback();
                return Task.CompletedTask;
            }

            /// <inheritdoc />
            protected override void Dispose(bool disposing)
            {
                if (disposing && !_disposed)
                    _inner.Dispose();
                _disposed = true;
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// 记录 SQLite 批量语句执行的数据库命令包装器。
        /// </summary>
        private sealed class MutationBatchCommand : DbCommand
        {
            /// <summary>
            /// 保存被包装的 SQLite 命令。
            /// </summary>
            private readonly SqliteCommand _inner;
            /// <summary>
            /// 保存命令所属的变更批次连接。
            /// </summary>
            private readonly MutationBatchConnection _connection;
            /// <summary>
            /// 保存命令所属的变更批次事务。
            /// </summary>
            private MutationBatchTransaction _transaction;

            /// <summary>
            /// 初始化一个 <see cref="MutationBatchCommand" /> 类型的实例。
            /// </summary>
            /// <param name="inner">由当前包装器保留或转发的内部对象或异常。</param>
            /// <param name="connection">当前实例使用的连接对象或连接配置。</param>
            public MutationBatchCommand(SqliteCommand inner, MutationBatchConnection connection)
            {
                _inner = inner;
                _connection = connection;
            }

            /// <inheritdoc />
            public override string CommandText { get => _inner.CommandText; set => _inner.CommandText = value; }
            /// <inheritdoc />
            public override int CommandTimeout { get => _inner.CommandTimeout; set => _inner.CommandTimeout = value; }
            /// <inheritdoc />
            public override System.Data.CommandType CommandType { get => _inner.CommandType; set => _inner.CommandType = value; }
            /// <inheritdoc />
            protected override DbConnection DbConnection
            {
                get => _connection;
                set => _inner.Connection = value is MutationBatchConnection connection
                    ? connection._inner
                    : (SqliteConnection)value;
            }
            /// <inheritdoc />
            protected override DbParameterCollection DbParameterCollection => _inner.Parameters;
            /// <inheritdoc />
            protected override DbTransaction DbTransaction
            {
                get => _transaction;
                set
                {
                    _transaction = value as MutationBatchTransaction;
                    _inner.Transaction = _transaction == null ? (SqliteTransaction)value : _transaction.Inner;
                }
            }
            /// <inheritdoc />
            public override bool DesignTimeVisible { get => _inner.DesignTimeVisible; set => _inner.DesignTimeVisible = value; }
            /// <inheritdoc />
            public override System.Data.UpdateRowSource UpdatedRowSource { get => _inner.UpdatedRowSource; set => _inner.UpdatedRowSource = value; }

            /// <inheritdoc />
            public override void Cancel() => _inner.Cancel();
            /// <inheritdoc />
            public override int ExecuteNonQuery() => _inner.ExecuteNonQuery();
            /// <inheritdoc />
            public override object ExecuteScalar() => _inner.ExecuteScalar();
            /// <inheritdoc />
            public override void Prepare() => _inner.Prepare();
            /// <inheritdoc />
            protected override DbParameter CreateDbParameter() => _inner.CreateParameter();
            /// <inheritdoc />
            protected override DbDataReader ExecuteDbDataReader(System.Data.CommandBehavior behavior) =>
                new MutationBatchDataReader(_inner.ExecuteReader(behavior), _connection);
            /// <inheritdoc />
            protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(System.Data.CommandBehavior behavior,
                CancellationToken cancellationToken)
            {
                _connection.LastExecuteReaderCancellationToken = cancellationToken;
                var reader = await _inner.ExecuteReaderAsync(behavior, cancellationToken).ConfigureAwait(false);
                return new MutationBatchDataReader(reader, _connection);
            }
            /// <inheritdoc />
            public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) =>
                _inner.ExecuteNonQueryAsync(cancellationToken);
            /// <inheritdoc />
            public override Task<object> ExecuteScalarAsync(CancellationToken cancellationToken) =>
                _inner.ExecuteScalarAsync(cancellationToken);

            /// <inheritdoc />
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    _inner.Dispose();
                base.Dispose(disposing);
            }
        }
    }

    /// <summary>
    /// 包装数据库读取器并跟踪关联连接状态的测试替身。
    /// </summary>
    private sealed class MutationBatchDataReader : DbDataReader
    {
        /// <summary>
        /// 保存被包装的数据读取器。
        /// </summary>
        private readonly DbDataReader _inner;
        /// <summary>
        /// 保存读取器所属的变更批次连接。
        /// </summary>
        private readonly MutationBatchConnection _connection;
        /// <summary>
        /// 记录数据读取器的释放状态。
        /// </summary>
        private int _disposed;

        /// <summary>
        /// 初始化一个 <see cref="MutationBatchDataReader" /> 类型的实例。
        /// </summary>
        /// <param name="inner">由当前包装器保留或转发的内部对象或异常。</param>
        /// <param name="connection">当前实例使用的连接对象或连接配置。</param>
        public MutationBatchDataReader(DbDataReader inner, MutationBatchConnection connection)
        {
            _inner = inner;
            _connection = connection;
        }

        /// <inheritdoc />
        public override int Depth => _inner.Depth;
        /// <inheritdoc />
        public override int FieldCount => _inner.FieldCount;
        /// <inheritdoc />
        public override bool HasRows => _inner.HasRows;
        /// <inheritdoc />
        public override bool IsClosed => _inner.IsClosed;
        /// <inheritdoc />
        public override int RecordsAffected => _inner.RecordsAffected;
        /// <inheritdoc />
        public override object this[int ordinal] => _inner[ordinal];
        /// <inheritdoc />
        public override object this[string name] => _inner[name];
        /// <inheritdoc />
        public override bool GetBoolean(int ordinal) => _inner.GetBoolean(ordinal);
        /// <inheritdoc />
        public override byte GetByte(int ordinal) => _inner.GetByte(ordinal);
        /// <inheritdoc />
        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length) =>
            _inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);
        /// <inheritdoc />
        public override char GetChar(int ordinal) => _inner.GetChar(ordinal);
        /// <inheritdoc />
        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length) =>
            _inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);
        /// <inheritdoc />
        public override string GetDataTypeName(int ordinal) => _inner.GetDataTypeName(ordinal);
        /// <inheritdoc />
        public override DateTime GetDateTime(int ordinal) => _inner.GetDateTime(ordinal);
        /// <inheritdoc />
        public override decimal GetDecimal(int ordinal) => _inner.GetDecimal(ordinal);
        /// <inheritdoc />
        public override double GetDouble(int ordinal) => _inner.GetDouble(ordinal);
        /// <inheritdoc />
        public override IEnumerator GetEnumerator() => _inner.GetEnumerator();
        /// <inheritdoc />
        public override Type GetFieldType(int ordinal) => _inner.GetFieldType(ordinal);
        /// <inheritdoc />
        public override float GetFloat(int ordinal) => _inner.GetFloat(ordinal);
        /// <inheritdoc />
        public override Guid GetGuid(int ordinal) => _inner.GetGuid(ordinal);
        /// <inheritdoc />
        public override short GetInt16(int ordinal) => _inner.GetInt16(ordinal);
        /// <inheritdoc />
        public override int GetInt32(int ordinal) => _inner.GetInt32(ordinal);
        /// <inheritdoc />
        public override long GetInt64(int ordinal) => _inner.GetInt64(ordinal);
        /// <inheritdoc />
        public override string GetName(int ordinal) => _inner.GetName(ordinal);
        /// <inheritdoc />
        public override int GetOrdinal(string name) => _inner.GetOrdinal(name);
        /// <inheritdoc />
        public override DataTable GetSchemaTable() => _inner.GetSchemaTable();
        /// <inheritdoc />
        public override string GetString(int ordinal) => _inner.GetString(ordinal);
        /// <inheritdoc />
        public override object GetValue(int ordinal) => _inner.GetValue(ordinal);
        /// <inheritdoc />
        public override int GetValues(object[] values) => _inner.GetValues(values);
        /// <inheritdoc />
        public override bool IsDBNull(int ordinal) => _inner.IsDBNull(ordinal);
        /// <inheritdoc />
        public override bool NextResult() => _inner.NextResult();
        /// <inheritdoc />
        public override Task<bool> NextResultAsync(CancellationToken cancellationToken) =>
            _inner.NextResultAsync(cancellationToken);
        /// <inheritdoc />
        public override bool Read() => _inner.Read();

        /// <inheritdoc />
        public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
        {
            _connection.LastReaderReadCancellationToken = cancellationToken;
            if (_connection.Resolver.BlockReaderRead)
            {
                _connection.Resolver.SignalReaderReadStarted(_connection);
                await _connection.Resolver.WaitForReaderReadReleaseAsync().ConfigureAwait(false);
            }

            return await _inner.ReadAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override T GetFieldValue<T>(int ordinal) => _inner.GetFieldValue<T>(ordinal);
        /// <inheritdoc />
        public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken) =>
            _inner.GetFieldValueAsync<T>(ordinal, cancellationToken);

        /// <inheritdoc />
        public override async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                try
                {
                    await _inner.DisposeAsync().ConfigureAwait(false);
                }
                finally
                {
                    _connection.ReaderDisposeCount++;
                }
            }

            await base.DisposeAsync().ConfigureAwait(false);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                try
                {
                    _inner.Dispose();
                }
                finally
                {
                    _connection.ReaderDisposeCount++;
                }
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// 记录 SQLite 连接工厂创建次数的解析器。
    /// </summary>
    private sealed class CountingSqliteConnectionFactoryResolver : ISqlDbConnectionFactoryResolver
    {
        /// <summary>
        /// 获取创建实例的累计次数。
        /// </summary>
        public int CreatedCount { get; private set; }

        /// <inheritdoc />
        public System.Data.IDbConnection Create(string providerKey, string connectionString)
        {
            CreatedCount++;
            return new SqliteConnection(connectionString);
        }
    }

    /// <summary>
    /// 按 Provider 返回受控数据库连接的解析器。
    /// </summary>
    private sealed class ControlledProviderConnectionResolver : ISqlDbConnectionFactoryResolver
    {
        /// <summary>
        /// 保存受控提供程序解析器使用的 SQLite 连接字符串。
        /// </summary>
        private readonly string _sqliteConnectionString;
        /// <summary>
        /// 保存受控提供程序解析器使用的 MySQL 连接字符串。
        /// </summary>
        private readonly string _mysqlConnectionString;
        /// <summary>
        /// 按数据源键记录已创建的连接数量。
        /// </summary>
        private readonly Dictionary<string, int> _createdCounts = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 初始化一个 <see cref="ControlledProviderConnectionResolver" /> 类型的实例。
        /// </summary>
        /// <param name="sqliteConnectionString">用于建立 SQLite 连接的连接字符串。</param>
        /// <param name="mysqlConnectionString">用于 MySQL 测试数据库的连接字符串。</param>
        public ControlledProviderConnectionResolver(string sqliteConnectionString, string mysqlConnectionString)
        {
            _sqliteConnectionString = sqliteConnectionString;
            _mysqlConnectionString = mysqlConnectionString;
        }

        /// <summary>
        /// 读取指定提供程序已创建连接数。
        /// </summary>
        /// <param name="providerKey">SQL 提供程序注册键。</param>
        /// <returns>符合查询条件的记录数。</returns>
        public int GetCreatedCount(string providerKey) => _createdCounts.TryGetValue(providerKey, out var count) ? count : 0;

        /// <inheritdoc />
        public System.Data.IDbConnection Create(string providerKey, string connectionString)
        {
            _createdCounts[providerKey] = GetCreatedCount(providerKey) + 1;
            return providerKey switch
            {
                "bing.sqlite" => new SqliteConnection(_sqliteConnectionString),
                "bing.mysql" => new SqliteConnection(_mysqlConnectionString),
                _ => throw new InvalidOperationException($"Unexpected Provider Key '{providerKey}'.")
            };
        }
    }

    /// <summary>
    /// 为 MySQL 测试返回可控数据库身份的解析器。
    /// </summary>
    private sealed class ControlledProviderDatabaseIdentityResolver : ISqlDatabaseIdentityResolver
    {
        /// <summary>
        /// 保存回退使用的默认数据库标识解析器。
        /// </summary>
        private readonly ISqlDatabaseIdentityResolver _defaultResolver = new DefaultSqlDatabaseIdentityResolver();

        /// <inheritdoc />
        public SqlDatabaseIdentity Resolve(DatabaseType databaseType, string connectionString)
        {
            if (databaseType == DatabaseType.MySql)
                return new SqlDatabaseIdentity
                {
                    DatabaseType = DatabaseType.MySql,
                    Server = "controlled-test-endpoint",
                    Database = "controlled-test-database"
                };
            return _defaultResolver.Resolve(databaseType, connectionString);
        }
    }

    /// <summary>
    /// 为 SQLite 返回不可比较数据库身份的解析器。
    /// </summary>
    private sealed class OpaqueSqliteDatabaseIdentityResolver : ISqlDatabaseIdentityResolver
    {
        /// <inheritdoc />
        public SqlDatabaseIdentity Resolve(DatabaseType databaseType, string connectionString) =>
            new() { DatabaseType = databaseType, IsComparable = false };
    }

    /// <summary>
    /// 轮换 SQLite 连接串并返回不可比较身份的连接解析器。
    /// </summary>
    private sealed class OpaqueRotatingSqliteConnectionFactoryResolver : ISqlDbConnectionFactoryResolver
    {
        /// <summary>
        /// 保存轮换连接工厂使用的连接字符串。
        /// </summary>
        private readonly string[] _connectionStrings;

        /// <summary>
        /// 初始化一个 <see cref="OpaqueRotatingSqliteConnectionFactoryResolver" /> 类型的实例。
        /// </summary>
        /// <param name="connectionStrings">SQLite 测试使用的数据库连接字符串集合。</param>
        public OpaqueRotatingSqliteConnectionFactoryResolver(params string[] connectionStrings) =>
            _connectionStrings = connectionStrings;

        /// <summary>
        /// 获取创建实例的累计次数。
        /// </summary>
        public int CreatedCount { get; private set; }
        /// <summary>
        /// 获取已经创建的数据库连接集合。
        /// </summary>
        public List<SqliteConnection> Connections { get; } = new();

        /// <inheritdoc />
        public System.Data.IDbConnection Create(string providerKey, string connectionString)
        {
            var selected = _connectionStrings[Math.Min(CreatedCount, _connectionStrings.Length - 1)];
            CreatedCount++;
            var connection = new SqliteConnection(selected);
            Connections.Add(connection);
            return connection;
        }
    }
}
