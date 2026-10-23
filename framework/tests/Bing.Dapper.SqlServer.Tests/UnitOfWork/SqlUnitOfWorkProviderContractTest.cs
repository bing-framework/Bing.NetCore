using System.Data;
using System.Data.Common;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Dapper;
using Bing.Dapper.MySql;
using Bing.Dapper.Oracle;
using Bing.Dapper.PostgreSql;
using Bing.Dapper.Sqlite;
using Bing.Dapper.SqlServer;
using Bing.Uow;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Npgsql;
using Oracle.ManagedDataAccess.Client;

namespace Bing.Dapper.Tests.UnitOfWork;

/// <summary>
/// 直接验证 SQL Provider 在业务 Uow 资源中的路由、事务能力和所有权边界。
/// </summary>
public sealed class SqlUnitOfWorkProviderContractTest
{
    private const string CustomProviderKey = "test.custom-uow";

    public static IEnumerable<object[]> AllProviderKeys => ProviderCases.Select(item => new object[] { item.Key });

    public static IEnumerable<object[]> TransactionalProviderKeys => ProviderCases
        .Where(item => item.Key != "doris")
        .Select(item => new object[] { item.Key });

    public static IEnumerable<object[]> ProvidersWithoutNativeAsyncTransactions => ProviderCases
        .Where(item => item.Key is "sqlserver" or "oracle" or "sqlite")
        .Select(item => new object[] { item.Key });

    private static readonly ProviderCase[] ProviderCases =
    {
        new("mysql", DatabaseType.MySql, MySqlSqlProvider.Instance, "bing.mysql",
            typeof(MySqlQuery), typeof(MySqlExecutor), typeof(MySqlMultipleQueryExecutor), typeof(MySqlConnection),
            "Server=matrix-mysql;Database=probe;"),
        new("postgresql", DatabaseType.PgSql, PostgreSqlSqlProvider.Instance, "bing.postgresql",
            typeof(PostgreSqlQuery), typeof(PostgreSqlExecutor), typeof(PostgreSqlMultipleQueryExecutor),
            typeof(NpgsqlConnection), "Host=matrix-postgresql;Database=probe;"),
        new("sqlserver", DatabaseType.SqlServer, SqlServerSqlProvider.Instance, "bing.sqlserver",
            typeof(SqlServerSqlQuery), typeof(SqlServerSqlExecutor), typeof(SqlServerSqlMultipleQueryExecutor),
            typeof(SqlConnection), "Server=matrix-sqlserver;Database=probe;"),
        new("oracle", DatabaseType.Oracle, OracleSqlProvider.Instance, "bing.oracle",
            typeof(OracleSqlQuery), typeof(OracleSqlExecutor), null, typeof(OracleConnection),
            "User Id=matrix;Password=matrix;Data Source=localhost:1521/probe"),
        new("sqlite", DatabaseType.Sqlite, SqliteSqlProvider.Instance, "bing.sqlite",
            typeof(SqliteSqlQuery), typeof(SqliteSqlExecutor), typeof(SqliteSqlMultipleQueryExecutor),
            typeof(SqliteConnection), "Data Source=provider-matrix.db"),
        new("doris", DatabaseType.Doris, MySqlSqlProvider.Instance, "bing.mysql",
            typeof(MySqlQuery), typeof(MySqlExecutor), typeof(MySqlMultipleQueryExecutor), typeof(MySqlConnection),
            "Server=matrix-doris;Database=analytics;"),
        new("custom", DatabaseType.SqlServer, MatrixCustomProvider.Instance, CustomProviderKey,
            typeof(SqlServerSqlQuery), typeof(SqlServerSqlExecutor), typeof(SqlServerSqlMultipleQueryExecutor),
            typeof(SqlConnection), "Server=matrix-custom;Database=probe;")
    };

    /// <summary>
    /// 所有具名源必须通过实际 Provider resolver、运行时实现和连接工厂选择对应分支。
    /// </summary>
    [Theory]
    [MemberData(nameof(AllProviderKeys))]
    public void ProviderAndDataSourceKey_ShouldSelectExpectedRuntimeAndConnectionFactory(string key)
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var expected = GetCase(key);
        var source = GetSource(scope.ServiceProvider, key);
        var resolved = scope.ServiceProvider.GetRequiredService<ISqlProviderResolver>().Resolve(
            new DatabaseContext { DbKey = key, DataSource = source }, databaseType: source.DatabaseType);
        var runtime = Assert.Single(scope.ServiceProvider.GetServices<SqlProviderRuntime>(),
            item => string.Equals(item.ProviderKey, expected.ProviderKey, StringComparison.OrdinalIgnoreCase));

        Assert.Same(expected.Provider, resolved);
        Assert.Equal(expected.QueryType, runtime.QueryType);
        Assert.Equal(expected.ExecutorType, runtime.ExecutorType);
        Assert.Equal(expected.MultipleQueryExecutorType, runtime.MultipleQueryExecutorType);

        using var query = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create(key);
        Assert.IsType(expected.QueryType, query);

        using var connection = scope.ServiceProvider.GetRequiredService<ISqlDbConnectionFactoryResolver>()
            .Create(expected.ProviderKey, source.ConnectionString);
        Assert.IsType(expected.ConnectionType, connection);
    }

    /// <summary>
    /// 受支持的 Provider 必须由 root 资源完成一次真实事务调用，并遵循各自的原生异步能力声明。
    /// </summary>
    [Theory]
    [MemberData(nameof(TransactionalProviderKeys))]
    public async Task ProviderUnitOfWorkResource_ShouldCommitAndDisposeOnceUsingDeclaredAsyncMode(string key)
    {
        using var provider = CreateProvider();
        using var serviceScope = provider.CreateScope();
        var source = GetSource(serviceScope.ServiceProvider, key);
        var sqlProvider = ResolveProvider(serviceScope.ServiceProvider, source);
        var transactionCapabilities = Assert.IsAssignableFrom<ISqlProviderProfileProvider>(sqlProvider)
            .Profile.Transaction;
        Assert.True(transactionCapabilities.SupportsTransactions);

        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        var root = manager.Begin();
        var observation = new TransactionObservation();
        var connection = ControlledDbConnection.Create(observation, transactionCapabilities);
        var resolver = new ControlledConnectionResolver(connection);
        var registration = RegisterResource(root, source, sqlProvider, resolver, transactionCapabilities);

        await registration.EnsureReadyAsync();

        Assert.Equal(sqlProvider.Key, resolver.LastProviderKey);
        Assert.Equal(source.ConnectionString, resolver.LastConnectionString);
        Assert.Equal(1, resolver.CreateCount);
        Assert.Equal(1, observation.OpenCount);
        Assert.Equal(transactionCapabilities.SupportsNativeAsyncBegin ? 1 : 0, observation.NativeBeginCount);
        Assert.Equal(transactionCapabilities.SupportsNativeAsyncBegin ? 0 : 1, observation.SyncBeginCount);
        Assert.Equal(0, observation.CommitCount);

        await root.CompleteAsync();
        Assert.Equal(1, observation.CommitCount);
        Assert.Equal(transactionCapabilities.SupportsNativeAsyncCommit ? 1 : 0, observation.NativeCommitCount);
        Assert.Equal(transactionCapabilities.SupportsNativeAsyncCommit ? 0 : 1, observation.SyncCommitCount);

        await root.DisposeAsync();
        Assert.Equal(1, observation.TransactionDisposeCount);
        Assert.Equal(1, observation.ConnectionDisposeCount);
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    /// <summary>
    /// Uow 回滚同样必须调用 Provider 实际声明的异步事务路径并且只释放一次。
    /// </summary>
    [Theory]
    [MemberData(nameof(TransactionalProviderKeys))]
    public async Task ProviderUnitOfWorkResource_ShouldRollbackAndDisposeOnceUsingDeclaredAsyncMode(string key)
    {
        using var provider = CreateProvider();
        using var serviceScope = provider.CreateScope();
        var source = GetSource(serviceScope.ServiceProvider, key);
        var sqlProvider = ResolveProvider(serviceScope.ServiceProvider, source);
        var transactionCapabilities = Assert.IsAssignableFrom<ISqlProviderProfileProvider>(sqlProvider)
            .Profile.Transaction;
        var root = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        var observation = new TransactionObservation();
        var connection = ControlledDbConnection.Create(observation, transactionCapabilities);
        var resolver = new ControlledConnectionResolver(connection);
        var registration = RegisterResource(root, source, sqlProvider, resolver, transactionCapabilities);

        await registration.EnsureReadyAsync();
        await root.RollbackAsync();
        await root.DisposeAsync();

        Assert.Equal(1, resolver.CreateCount);
        Assert.Equal(sqlProvider.Key, resolver.LastProviderKey);
        Assert.Equal(1, observation.OpenCount);
        Assert.Equal(1, observation.RollbackCount);
        Assert.Equal(transactionCapabilities.SupportsNativeAsyncRollback ? 1 : 0, observation.NativeRollbackCount);
        Assert.Equal(transactionCapabilities.SupportsNativeAsyncRollback ? 0 : 1, observation.SyncRollbackCount);
        Assert.Equal(0, observation.CommitCount);
        Assert.Equal(1, observation.TransactionDisposeCount);
        Assert.Equal(1, observation.ConnectionDisposeCount);
    }

    /// <summary>
    /// 事务 Uow 不能在冻结的 replica 上执行；冻结到 primary 的正确上下文仍可准备事务。
    /// </summary>
    [Theory]
    [MemberData(nameof(TransactionalProviderKeys))]
    public async Task FrozenReplica_ShouldRejectBeforeOpenAndPrimarySnapshotShouldRecover(string key)
    {
        using var provider = CreateProvider();
        using var serviceScope = provider.CreateScope();
        var providerResolver = serviceScope.ServiceProvider.GetRequiredService<ISqlProviderResolver>();
        var caseInfo = GetCase(key);
        var replica = CreateSource(caseInfo, "replica", PrimaryReadStrategy.PrimaryDataSource, "primary");
        var primary = CreateSource(caseInfo, "primary", PrimaryReadStrategy.PrimaryDataSource, "primary");
        var replicaProvider = providerResolver.Resolve(new DatabaseContext { DbKey = replica.Key, DataSource = replica },
            databaseType: replica.DatabaseType);
        var primaryProvider = providerResolver.Resolve(new DatabaseContext { DbKey = primary.Key, DataSource = primary },
            databaseType: primary.DatabaseType);
        var capabilities = Assert.IsAssignableFrom<ISqlProviderProfileProvider>(replicaProvider).Profile.Transaction;
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();

        var rejectedRoot = manager.Begin();
        var rejectedObservation = new TransactionObservation();
        var rejectedConnection = ControlledDbConnection.Create(rejectedObservation, capabilities);
        var rejectedResolver = new ControlledConnectionResolver(rejectedConnection);
        var rejected = RegisterResource(rejectedRoot, replica, replicaProvider, rejectedResolver, capabilities);
        var rejection = await Assert.ThrowsAsync<NotSupportedException>(() => rejected.EnsureReadyAsync());
        Assert.Contains("只读副本", rejection.Message);
        Assert.True(rejectedRoot.IsRollbackOnly);
        Assert.Equal(0, rejectedObservation.OpenCount);
        Assert.Equal(0, rejectedObservation.BeginCount);
        await rejectedRoot.RollbackAsync();
        await rejectedRoot.DisposeAsync();
        Assert.Equal(1, rejectedObservation.ConnectionDisposeCount);

        var acceptedRoot = manager.Begin();
        var acceptedObservation = new TransactionObservation();
        var acceptedConnection = ControlledDbConnection.Create(acceptedObservation, capabilities);
        var acceptedResolver = new ControlledConnectionResolver(acceptedConnection);
        var accepted = RegisterResource(acceptedRoot, primary, primaryProvider, acceptedResolver, capabilities);
        await accepted.EnsureReadyAsync();
        Assert.Equal(1, acceptedObservation.OpenCount);
        Assert.Equal(1, acceptedObservation.BeginCount);
        await acceptedRoot.RollbackAsync();
        await acceptedRoot.DisposeAsync();
        Assert.Equal(1, acceptedObservation.RollbackCount);
        Assert.Equal(1, acceptedObservation.ConnectionDisposeCount);
    }

    /// <summary>
    /// Doris 仍走 MySQL 方言和连接工厂，但事务 Uow 在打开连接前拒绝，非事务 Uow 只管理连接生命周期。
    /// </summary>
    [Fact]
    public async Task Doris_ShouldUseMySqlProviderAndKeepItsNonTransactionalBoundary()
    {
        using var provider = CreateProvider();
        using var serviceScope = provider.CreateScope();
        var metadata = serviceScope.ServiceProvider.GetRequiredService<Bing.Data.Sql.Configs.SqlMetadataOptions>();
        var source = metadata.DataSources.DataSources["doris"];
        Assert.True(source.IsReadOnly);
        Assert.False(source.SupportsTransactions);
        var sqlProvider = ResolveProvider(serviceScope.ServiceProvider, source);
        Assert.Same(MySqlSqlProvider.Instance, sqlProvider);

        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        var transactionRoot = manager.Begin();
        var rejectedObservation = new TransactionObservation();
        var rejectedResolver = new ControlledConnectionResolver(
            ControlledDbConnection.Create(rejectedObservation, MySqlSqlProvider.Instance.Profile.Transaction));
        var rejected = RegisterResource(transactionRoot, source, sqlProvider, rejectedResolver,
            MySqlSqlProvider.Instance.Profile.Transaction);
        await Assert.ThrowsAsync<NotSupportedException>(() => rejected.EnsureReadyAsync());
        Assert.True(transactionRoot.IsRollbackOnly);
        Assert.Equal(0, rejectedObservation.OpenCount);
        await transactionRoot.RollbackAsync();
        await transactionRoot.DisposeAsync();
        Assert.Equal(1, rejectedObservation.ConnectionDisposeCount);

        var nonTransactionalRoot = manager.Begin(new UnitOfWorkScopeOptions { IsTransactional = false });
        var allowedObservation = new TransactionObservation();
        var allowedResolver = new ControlledConnectionResolver(
            ControlledDbConnection.Create(allowedObservation, MySqlSqlProvider.Instance.Profile.Transaction));
        var allowed = RegisterResource(nonTransactionalRoot, source, sqlProvider, allowedResolver,
            MySqlSqlProvider.Instance.Profile.Transaction);
        await allowed.EnsureReadyAsync();
        await nonTransactionalRoot.CompleteAsync();
        await nonTransactionalRoot.DisposeAsync();

        Assert.Equal(1, allowedObservation.OpenCount);
        Assert.Equal(0, allowedObservation.BeginCount);
        Assert.Equal(0, allowedObservation.CommitCount);
        Assert.Equal(1, allowedObservation.ConnectionDisposeCount);
    }

    /// <summary>
    /// 一个 root 可同时登记不同 Provider 的连接；两笔事务独立提交并各自只清理一次。
    /// </summary>
    [Fact]
    public async Task RootWithTwoProviderResources_ShouldCommitAndDisposeEachIndependently()
    {
        using var provider = CreateProvider();
        using var serviceScope = provider.CreateScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        var root = manager.Begin();
        var mysql = GetCase("mysql");
        var postgresql = GetCase("postgresql");
        var mysqlSource = GetSource(serviceScope.ServiceProvider, mysql.Key);
        var postgresSource = GetSource(serviceScope.ServiceProvider, postgresql.Key);
        var mysqlProvider = ResolveProvider(serviceScope.ServiceProvider, mysqlSource);
        var postgresProvider = ResolveProvider(serviceScope.ServiceProvider, postgresSource);
        var mysqlObservation = new TransactionObservation();
        var postgresObservation = new TransactionObservation();
        var mysqlResolver = new ControlledConnectionResolver(ControlledDbConnection.Create(mysqlObservation,
            MySqlSqlProvider.Instance.Profile.Transaction));
        var postgresResolver = new ControlledConnectionResolver(ControlledDbConnection.Create(postgresObservation,
            PostgreSqlSqlProvider.Instance.Profile.Transaction));

        var mysqlResource = RegisterResource(root, mysqlSource, mysqlProvider, mysqlResolver,
            MySqlSqlProvider.Instance.Profile.Transaction);
        var postgresResource = RegisterResource(root, postgresSource, postgresProvider, postgresResolver,
            PostgreSqlSqlProvider.Instance.Profile.Transaction);
        await mysqlResource.EnsureReadyAsync();
        await postgresResource.EnsureReadyAsync();
        await root.CompleteAsync();
        await root.DisposeAsync();

        Assert.Equal(MySqlSqlProvider.Instance.Key, mysqlResolver.LastProviderKey);
        Assert.Equal(PostgreSqlSqlProvider.Instance.Key, postgresResolver.LastProviderKey);
        Assert.Equal(1, mysqlObservation.BeginCount);
        Assert.Equal(1, postgresObservation.BeginCount);
        Assert.Equal(1, mysqlObservation.CommitCount);
        Assert.Equal(1, postgresObservation.CommitCount);
        Assert.Equal(1, mysqlObservation.TransactionDisposeCount);
        Assert.Equal(1, postgresObservation.TransactionDisposeCount);
        Assert.Equal(1, mysqlObservation.ConnectionDisposeCount);
        Assert.Equal(1, postgresObservation.ConnectionDisposeCount);
    }

    /// <summary>
    /// 无原生异步事务声明的 Provider 必须使用同步事务成员完成异步 Uow。
    /// </summary>
    [Theory]
    [MemberData(nameof(ProvidersWithoutNativeAsyncTransactions))]
    public async Task ProviderWithoutNativeAsyncTransactions_ShouldUseSynchronousFallback(string key)
    {
        using var provider = CreateProvider();
        using var serviceScope = provider.CreateScope();
        var source = GetSource(serviceScope.ServiceProvider, key);
        var sqlProvider = ResolveProvider(serviceScope.ServiceProvider, source);
        var capabilities = Assert.IsAssignableFrom<ISqlProviderProfileProvider>(sqlProvider).Profile.Transaction;
        Assert.False(capabilities.SupportsNativeAsyncBegin);
        Assert.False(capabilities.SupportsNativeAsyncCommit);
        Assert.False(capabilities.SupportsNativeAsyncRollback);

        var root = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        var observation = new TransactionObservation();
        var registration = RegisterResource(root, source, sqlProvider,
            new ControlledConnectionResolver(ControlledDbConnection.Create(observation, capabilities)), capabilities);
        await registration.EnsureReadyAsync();
        await root.RollbackAsync();
        await root.DisposeAsync();

        Assert.Equal(1, observation.SyncBeginCount);
        Assert.Equal(0, observation.NativeBeginCount);
        Assert.Equal(1, observation.SyncRollbackCount);
        Assert.Equal(0, observation.NativeRollbackCount);
        Assert.Equal(1, observation.TransactionDisposeCount);
        Assert.Equal(1, observation.ConnectionDisposeCount);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddUnitOfWorkScopes();
        services.AddMySqlProvider();
        services.AddPostgreSqlProvider();
        services.AddSqlServerProvider();
        services.AddOracleProvider();
        services.AddSqliteProvider();
        services.AddSqlBuilderProvider(MatrixCustomProvider.Instance,
            builderServices => new SqlServerBuilder(builderServices));
        services.AddSqlProviderRuntime(new SqlProviderRuntime(CustomProviderKey, typeof(SqlServerSqlQuery),
            typeof(SqlServerSqlExecutor), typeof(SqlServerSqlMultipleQueryExecutor)));
        services.AddSqlDbConnectionFactory(CustomProviderKey, connectionString => new SqlConnection(connectionString));

        foreach (var item in ProviderCases.Where(item => item.Key != "doris"))
            services.AddSqlDataSource(item.Key, item.DatabaseType, item.ConnectionString, providerKey: item.ProviderKey);
        services.AddSqlDataSource("doris", DatabaseType.Doris, GetCase("doris").ConnectionString,
            providerKey: MySqlSqlProvider.Instance.Key);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    private static DapperSqlUnitOfWorkConnectionResource RegisterResource(IUnitOfWorkScope scope,
        SqlDataSourceDescriptor source, ISqlProvider provider, ISqlDbConnectionFactoryResolver connectionResolver,
        SqlProviderTransactionCapabilities transactionCapabilities)
    {
        var key = new object();
        return scope.GetOrAddResource(key, () => new DapperSqlUnitOfWorkConnectionResource(key, scope, source,
            provider.Key, source.ConnectionString, connectionResolver, transactionCapabilities));
    }

    private static ISqlProvider ResolveProvider(IServiceProvider serviceProvider, SqlDataSourceDescriptor source) =>
        serviceProvider.GetRequiredService<ISqlProviderResolver>().Resolve(new DatabaseContext
        {
            DbKey = source.Key,
            DataSource = source
        }, databaseType: source.DatabaseType);

    private static SqlDataSourceDescriptor GetSource(IServiceProvider serviceProvider, string key) =>
        serviceProvider.GetRequiredService<Bing.Data.Sql.Configs.SqlMetadataOptions>()
            .DataSources.DataSources[key];

    private static SqlDataSourceDescriptor CreateSource(ProviderCase providerCase, string key,
        PrimaryReadStrategy primaryReadStrategy, string primaryDataSourceKey) => new()
    {
        Key = key,
        DatabaseType = providerCase.DatabaseType,
        ProviderKey = providerCase.ProviderKey,
        ConnectionString = providerCase.ConnectionString,
        PrimaryReadStrategy = primaryReadStrategy,
        PrimaryDataSourceKey = primaryDataSourceKey,
        SupportsTransactions = true
    };

    private static ProviderCase GetCase(string key) =>
        ProviderCases.Single(item => string.Equals(item.Key, key, StringComparison.Ordinal));

    private sealed record ProviderCase(string Key, DatabaseType DatabaseType, ISqlProvider Provider,
        string ProviderKey, Type QueryType, Type ExecutorType, Type MultipleQueryExecutorType, Type ConnectionType,
        string ConnectionString);

    private sealed class ControlledConnectionResolver : ISqlDbConnectionFactoryResolver
    {
        private readonly IDbConnection _connection;

        public ControlledConnectionResolver(IDbConnection connection) => _connection = connection;

        public int CreateCount { get; private set; }

        public string LastProviderKey { get; private set; }

        public string LastConnectionString { get; private set; }

        public IDbConnection Create(string providerKey, string connectionString)
        {
            CreateCount++;
            LastProviderKey = providerKey;
            LastConnectionString = connectionString;
            return _connection;
        }
    }

    private sealed class TransactionObservation
    {
        public int OpenCount { get; set; }
        public int CloseCount { get; set; }
        public int ConnectionDisposeCount { get; set; }
        public int SyncBeginCount { get; set; }
        public int NativeBeginCount { get; set; }
        public int SyncCommitCount { get; set; }
        public int NativeCommitCount { get; set; }
        public int SyncRollbackCount { get; set; }
        public int NativeRollbackCount { get; set; }
        public int TransactionDisposeCount { get; set; }
        public int BeginCount => SyncBeginCount + NativeBeginCount;
        public int CommitCount => SyncCommitCount + NativeCommitCount;
        public int RollbackCount => SyncRollbackCount + NativeRollbackCount;
    }

    private class ControlledDbConnection : DbConnection
    {
        private readonly TransactionObservation _observation;
        private ConnectionState _state = ConnectionState.Closed;
        private bool _disposed;

        protected ControlledDbConnection(TransactionObservation observation) => _observation = observation;

        public static ControlledDbConnection Create(TransactionObservation observation,
            SqlProviderTransactionCapabilities capabilities) => capabilities.SupportsNativeAsyncBegin
            ? new NativeAsyncControlledDbConnection(observation, capabilities)
            : new ControlledDbConnection(observation);

        public override string ConnectionString { get; set; }

        public override string Database => "provider-matrix";

        public override string DataSource => "controlled";

        public override string ServerVersion => "1";

        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public override void Open()
        {
            _observation.OpenCount++;
            _state = ConnectionState.Open;
        }

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Open();
            return Task.CompletedTask;
        }

        public override void Close()
        {
            if (_state == ConnectionState.Open)
                _observation.CloseCount++;
            _state = ConnectionState.Closed;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            _observation.SyncBeginCount++;
            return CreateTransaction(nativeAsync: false);
        }

        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();

        protected virtual DbTransaction CreateTransaction(bool nativeAsync) => nativeAsync
            ? new NativeAsyncControlledDbTransaction(this, _observation)
            : new ControlledDbTransaction(this, _observation, nativeAsync: false);

        public override ValueTask DisposeAsync()
        {
            DisposeOnce();
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeOnce();
            base.Dispose(disposing);
        }

        private void DisposeOnce()
        {
            if (_disposed)
                return;
            _disposed = true;
            Close();
            _observation.ConnectionDisposeCount++;
        }
    }

    private sealed class NativeAsyncControlledDbConnection : ControlledDbConnection
    {
        private readonly TransactionObservation _observation;

        public NativeAsyncControlledDbConnection(TransactionObservation observation,
            SqlProviderTransactionCapabilities capabilities) : base(observation)
        {
            _observation = observation;
            Capabilities = capabilities;
        }

        private SqlProviderTransactionCapabilities Capabilities { get; }

        protected override ValueTask<DbTransaction> BeginDbTransactionAsync(IsolationLevel isolationLevel,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _observation.NativeBeginCount++;
            return ValueTask.FromResult(CreateTransaction(Capabilities.SupportsNativeAsyncCommit ||
                Capabilities.SupportsNativeAsyncRollback));
        }

        protected override DbTransaction CreateTransaction(bool nativeAsync) =>
            nativeAsync ? new NativeAsyncControlledDbTransaction(this, _observation) :
                new ControlledDbTransaction(this, _observation, nativeAsync: false);
    }

    private class ControlledDbTransaction : DbTransaction
    {
        private readonly ControlledDbConnection _connection;
        private readonly TransactionObservation _observation;
        private readonly bool _nativeAsync;
        private bool _disposed;

        public ControlledDbTransaction(ControlledDbConnection connection, TransactionObservation observation,
            bool nativeAsync)
        {
            _connection = connection;
            _observation = observation;
            _nativeAsync = nativeAsync;
        }

        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        protected override DbConnection DbConnection => _connection;

        public override void Commit()
        {
            _observation.SyncCommitCount++;
        }

        public override void Rollback()
        {
            _observation.SyncRollbackCount++;
        }

        public override ValueTask DisposeAsync()
        {
            DisposeOnce();
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeOnce();
            base.Dispose(disposing);
        }

        protected void DisposeOnce()
        {
            if (_disposed)
                return;
            _disposed = true;
            _observation.TransactionDisposeCount++;
        }

        protected bool UsesNativeAsync => _nativeAsync;

        protected TransactionObservation Observation => _observation;
    }

    private sealed class NativeAsyncControlledDbTransaction : ControlledDbTransaction
    {
        public NativeAsyncControlledDbTransaction(ControlledDbConnection connection,
            TransactionObservation observation) : base(connection, observation, nativeAsync: true)
        {
        }

        public override Task CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Observation.NativeCommitCount++;
            return Task.CompletedTask;
        }

        public override Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Observation.NativeRollbackCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class MatrixCustomProvider : ISqlProvider, ISqlProviderProfileProvider
    {
        public static MatrixCustomProvider Instance { get; } = new();

        public string Key => CustomProviderKey;
        public DatabaseType DatabaseType => DatabaseType.SqlServer;
        public IDialect Dialect => SqlServerSqlProvider.Instance.Dialect;
        public ISqlClauseFactory ClauseFactory => SqlServerSqlProvider.Instance.ClauseFactory;
        public ISqlTableReferenceParser TableReferenceParser => SqlServerSqlProvider.Instance.TableReferenceParser;
        public ISqlPaginationRenderer PaginationRenderer => SqlServerSqlProvider.Instance.PaginationRenderer;
        public IParameterManagerFactory ParameterManagerFactory => SqlServerSqlProvider.Instance.ParameterManagerFactory;
        public IParamLiteralsResolver ParamLiteralsResolver => SqlServerSqlProvider.Instance.ParamLiteralsResolver;
        public SqlProviderProfile Profile { get; } = new()
        {
            Transaction = new SqlProviderTransactionCapabilities
            {
                SupportsTransactions = true,
                SupportsNativeAsyncBegin = true,
                SupportsNativeAsyncCommit = true,
                SupportsNativeAsyncRollback = true
            }
        };
    }
}
