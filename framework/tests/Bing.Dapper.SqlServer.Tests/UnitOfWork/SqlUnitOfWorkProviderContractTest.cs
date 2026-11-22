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
    /// <summary>
    /// 定义测试的自定义 SQL 提供程序键。
    /// </summary>
    private const string CustomProviderKey = "test.custom-uow";

    /// <summary>
    /// 获取参与测试的全部 Provider 注册键。
    /// </summary>
    public static IEnumerable<object[]> AllProviderKeys => ProviderCases.Select(item => new object[] { item.Key });

    /// <summary>
    /// 获取支持事务测试的 Provider 注册键集合。
    /// </summary>
    public static IEnumerable<object[]> TransactionalProviderKeys => ProviderCases
        .Where(item => item.Key != "doris")
        .Select(item => new object[] { item.Key });

    /// <summary>
    /// 获取不支持原生异步事务 API 的 Provider 集合。
    /// </summary>
    public static IEnumerable<object[]> ProvidersWithoutNativeAsyncTransactions => ProviderCases
        .Where(item => item.Key is "sqlserver" or "oracle" or "sqlite")
        .Select(item => new object[] { item.Key });

    /// <summary>
    /// 保存 SQL 提供程序契约的测试用例矩阵。
    /// </summary>
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
    /// <param name="key">用于关联测试操作的键。</param>
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
    /// <param name="key">用于关联测试操作的键。</param>
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
    /// <param name="key">用于关联测试操作的键。</param>
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
    /// <param name="key">用于关联测试操作的键。</param>
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
    /// <param name="key">用于关联测试操作的键。</param>
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

    /// <summary>
    /// 创建多提供程序工作单元契约测试容器。
    /// </summary>
    /// <returns>注册了当前测试依赖的服务容器。</returns>
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

    /// <summary>
    /// 向作用域注册 Dapper SQL 连接资源。
    /// </summary>
    /// <param name="scope">目标工作单元作用域。</param>
    /// <param name="source">测试数据源描述。</param>
    /// <param name="provider">目标 SQL 提供程序。</param>
    /// <param name="connectionResolver">数据库连接工厂解析器。</param>
    /// <param name="transactionCapabilities">提供程序支持的事务能力。</param>
    /// <returns>注册到目标作用域的 SQL 连接资源。</returns>
    private static DapperSqlUnitOfWorkConnectionResource RegisterResource(IUnitOfWorkScope scope,
        SqlDataSourceDescriptor source, ISqlProvider provider, ISqlDbConnectionFactoryResolver connectionResolver,
        SqlProviderTransactionCapabilities transactionCapabilities)
    {
        var key = new object();
        return scope.GetOrAddResource(key, () => new DapperSqlUnitOfWorkConnectionResource(key, scope, source,
            provider.Key, source.ConnectionString, connectionResolver, transactionCapabilities));
    }

    /// <summary>
    /// 解析指定数据源对应的 SQL 提供程序。
    /// </summary>
    /// <param name="serviceProvider">解析测试服务的容器。</param>
    /// <param name="source">测试数据源描述。</param>
    /// <returns>与目标数据库或数据源匹配的 SQL 提供程序。</returns>
    private static ISqlProvider ResolveProvider(IServiceProvider serviceProvider, SqlDataSourceDescriptor source) =>
        serviceProvider.GetRequiredService<ISqlProviderResolver>().Resolve(new DatabaseContext
        {
            DbKey = source.Key,
            DataSource = source
        }, databaseType: source.DatabaseType);

    /// <summary>
    /// 按键读取已注册的数据源描述。
    /// </summary>
    /// <param name="serviceProvider">解析测试服务的容器。</param>
    /// <param name="key">用于关联测试操作的键。</param>
    /// <returns>测试使用的数据源描述。</returns>
    private static SqlDataSourceDescriptor GetSource(IServiceProvider serviceProvider, string key) =>
        serviceProvider.GetRequiredService<Bing.Data.Sql.Configs.SqlMetadataOptions>()
            .DataSources.DataSources[key];

    /// <summary>
    /// 创建具有指定主库读取策略的数据源描述。
    /// </summary>
    /// <param name="providerCase">数据库提供程序的测试用例。</param>
    /// <param name="key">用于关联测试操作的键。</param>
    /// <param name="primaryReadStrategy">主库读取策略。</param>
    /// <param name="primaryDataSourceKey">数据源关联的主库键。</param>
    /// <returns>测试使用的数据源描述。</returns>
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

    /// <summary>
    /// 按键查找数据库提供程序测试用例。
    /// </summary>
    /// <param name="key">用于关联测试操作的键。</param>
    /// <returns>与给定键匹配的数据库提供程序用例。</returns>
    private static ProviderCase GetCase(string key) =>
        ProviderCases.Single(item => string.Equals(item.Key, key, StringComparison.Ordinal));

    /// <summary>
    /// 保存 Provider 契约矩阵测试使用的数据库与执行器配置。
    /// </summary>
    /// <param name="Key">用于标识当前对象的名称或键。</param>
    /// <param name="DatabaseType">用于初始化当前实例的DatabaseType值。</param>
    /// <param name="Provider">用于初始化当前实例的Provider值。</param>
    /// <param name="ProviderKey">用于初始化当前实例的ProviderKey值。</param>
    /// <param name="QueryType">用于初始化当前实例的QueryType值。</param>
    /// <param name="ExecutorType">用于初始化当前实例的ExecutorType值。</param>
    /// <param name="MultipleQueryExecutorType">用于初始化当前实例的MultipleQueryExecutorType值。</param>
    /// <param name="ConnectionType">用于初始化当前实例的ConnectionType值。</param>
    /// <param name="ConnectionString">用于初始化当前实例的ConnectionString值。</param>
    private sealed record ProviderCase(string Key, DatabaseType DatabaseType, ISqlProvider Provider,
        string ProviderKey, Type QueryType, Type ExecutorType, Type MultipleQueryExecutorType, Type ConnectionType,
        string ConnectionString);

    /// <summary>
    /// 重复提供指定数据库连接并记录创建次数的解析器。
    /// </summary>
    private sealed class ControlledConnectionResolver : ISqlDbConnectionFactoryResolver
    {
        /// <summary>
        /// 保存受控连接解析器返回的连接。
        /// </summary>
        private readonly IDbConnection _connection;

        /// <summary>
        /// 初始化一个 <see cref="ControlledConnectionResolver" /> 类型的实例。
        /// </summary>
        /// <param name="connection">当前实例使用的连接对象或连接配置。</param>
        public ControlledConnectionResolver(IDbConnection connection) => _connection = connection;

        /// <summary>
        /// 获取创建实例的累计次数。
        /// </summary>
        public int CreateCount { get; private set; }

        /// <summary>
        /// 获取最近一次操作使用的 Provider 键。
        /// </summary>
        public string LastProviderKey { get; private set; }

        /// <summary>
        /// 获取最近观察到的数据库连接字符串。
        /// </summary>
        public string LastConnectionString { get; private set; }

        /// <inheritdoc />
        public IDbConnection Create(string providerKey, string connectionString)
        {
            CreateCount++;
            LastProviderKey = providerKey;
            LastConnectionString = connectionString;
            return _connection;
        }
    }

    /// <summary>
    /// 记录连接、事务和资源释放次数的事务观测状态。
    /// </summary>
    private sealed class TransactionObservation
    {
        /// <summary>
        /// 获取或设置打开连接的累计次数。
        /// </summary>
        public int OpenCount { get; set; }
        /// <summary>
        /// 获取或设置关闭连接的累计次数。
        /// </summary>
        public int CloseCount { get; set; }
        /// <summary>
        /// 获取或设置释放连接的累计次数。
        /// </summary>
        public int ConnectionDisposeCount { get; set; }
        /// <summary>
        /// 获取或设置同步事务开启次数。
        /// </summary>
        public int SyncBeginCount { get; set; }
        /// <summary>
        /// 获取或设置原生异步事务开启次数。
        /// </summary>
        public int NativeBeginCount { get; set; }
        /// <summary>
        /// 获取或设置同步事务提交次数。
        /// </summary>
        public int SyncCommitCount { get; set; }
        /// <summary>
        /// 获取或设置原生异步事务提交次数。
        /// </summary>
        public int NativeCommitCount { get; set; }
        /// <summary>
        /// 获取或设置同步事务回滚次数。
        /// </summary>
        public int SyncRollbackCount { get; set; }
        /// <summary>
        /// 获取或设置原生异步事务回滚次数。
        /// </summary>
        public int NativeRollbackCount { get; set; }
        /// <summary>
        /// 获取或设置释放事务的累计次数。
        /// </summary>
        public int TransactionDisposeCount { get; set; }
        /// <summary>
        /// 获取开启事务的累计次数。
        /// </summary>
        public int BeginCount => SyncBeginCount + NativeBeginCount;
        /// <summary>
        /// 获取提交事务的累计次数。
        /// </summary>
        public int CommitCount => SyncCommitCount + NativeCommitCount;
        /// <summary>
        /// 获取回滚事务的累计次数。
        /// </summary>
        public int RollbackCount => SyncRollbackCount + NativeRollbackCount;
    }

    /// <summary>
    /// 记录打开、关闭和释放调用的数据库连接测试替身。
    /// </summary>
    private class ControlledDbConnection : DbConnection
    {
        /// <summary>
        /// 保存受控连接的观测记录。
        /// </summary>
        private readonly TransactionObservation _observation;
        /// <summary>
        /// 保存受控连接的当前状态。
        /// </summary>
        private ConnectionState _state = ConnectionState.Closed;
        /// <summary>
        /// 指示受控连接是否已释放。
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 初始化一个 <see cref="ControlledDbConnection" /> 类型的实例。
        /// </summary>
        /// <param name="observation">本次测试记录的观察结果。</param>
        protected ControlledDbConnection(TransactionObservation observation) => _observation = observation;

        /// <summary>
        /// 按事务能力创建受控数据库连接。
        /// </summary>
        /// <param name="observation">记录连接及事务生命周期的观测器。</param>
        /// <param name="capabilities">提供程序支持的事务能力。</param>
        /// <returns>支持指定原生异步事务能力的受控连接。</returns>
        public static ControlledDbConnection Create(TransactionObservation observation,
            SqlProviderTransactionCapabilities capabilities) => capabilities.SupportsNativeAsyncBegin
            ? new NativeAsyncControlledDbConnection(observation, capabilities)
            : new ControlledDbConnection(observation);

        /// <inheritdoc />
        public override string ConnectionString { get; set; }

        /// <inheritdoc />
        public override string Database => "provider-matrix";

        /// <inheritdoc />
        public override string DataSource => "controlled";

        /// <inheritdoc />
        public override string ServerVersion => "1";

        /// <inheritdoc />
        public override ConnectionState State => _state;

        /// <inheritdoc />
        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Open()
        {
            _observation.OpenCount++;
            _state = ConnectionState.Open;
        }

        /// <inheritdoc />
        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Open();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void Close()
        {
            if (_state == ConnectionState.Open)
                _observation.CloseCount++;
            _state = ConnectionState.Closed;
        }

        /// <inheritdoc />
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            _observation.SyncBeginCount++;
            return CreateTransaction(nativeAsync: false);
        }

        /// <inheritdoc />
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();

        /// <summary>
        /// 创建用于观测事务行为的测试事务。
        /// </summary>
        /// <param name="nativeAsync">是否创建原生异步事务测试替身；true 使用异步替身，false 使用同步替身。</param>
        /// <returns>关联当前连接和观测计数器的测试事务。</returns>
        protected virtual DbTransaction CreateTransaction(bool nativeAsync) => nativeAsync
            ? new NativeAsyncControlledDbTransaction(this, _observation)
            : new ControlledDbTransaction(this, _observation, nativeAsync: false);

        /// <inheritdoc />
        public override ValueTask DisposeAsync()
        {
            DisposeOnce();
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeOnce();
            base.Dispose(disposing);
        }

        /// <summary>
        /// 关闭并释放受控连接且只记录一次。
        /// </summary>
        private void DisposeOnce()
        {
            if (_disposed)
                return;
            _disposed = true;
            Close();
            _observation.ConnectionDisposeCount++;
        }
    }

    /// <summary>
    /// 可控制异步打开与关闭行为的数据库连接测试替身。
    /// </summary>
    private sealed class NativeAsyncControlledDbConnection : ControlledDbConnection
    {
        /// <summary>
        /// 保存支持原生异步的受控连接观测记录。
        /// </summary>
        private readonly TransactionObservation _observation;

        /// <summary>
        /// 初始化一个 <see cref="NativeAsyncControlledDbConnection" /> 类型的实例。
        /// </summary>
        /// <param name="observation">本次测试记录的观察结果。</param>
        /// <param name="capabilities">当前 SQL 提供程序支持的事务能力。</param>
        public NativeAsyncControlledDbConnection(TransactionObservation observation,
            SqlProviderTransactionCapabilities capabilities) : base(observation)
        {
            _observation = observation;
            Capabilities = capabilities;
        }

        /// <summary>
        /// 获取当前实现支持的能力集合。
        /// </summary>
        private SqlProviderTransactionCapabilities Capabilities { get; }

        /// <inheritdoc />
        protected override ValueTask<DbTransaction> BeginDbTransactionAsync(IsolationLevel isolationLevel,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _observation.NativeBeginCount++;
            return ValueTask.FromResult(CreateTransaction(Capabilities.SupportsNativeAsyncCommit ||
                Capabilities.SupportsNativeAsyncRollback));
        }

        /// <inheritdoc />
        protected override DbTransaction CreateTransaction(bool nativeAsync) =>
            nativeAsync ? new NativeAsyncControlledDbTransaction(this, _observation) :
                new ControlledDbTransaction(this, _observation, nativeAsync: false);
    }

    /// <summary>
    /// 记录提交、回滚和释放调用的数据库事务测试替身。
    /// </summary>
    private class ControlledDbTransaction : DbTransaction
    {
        /// <summary>
        /// 保存受控事务所属的连接。
        /// </summary>
        private readonly ControlledDbConnection _connection;
        /// <summary>
        /// 保存受控事务的观测记录。
        /// </summary>
        private readonly TransactionObservation _observation;
        /// <summary>
        /// 指示受控事务是否使用原生异步提交。
        /// </summary>
        private readonly bool _nativeAsync;
        /// <summary>
        /// 指示受控事务是否已释放。
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 初始化一个 <see cref="ControlledDbTransaction" /> 类型的实例。
        /// </summary>
        /// <param name="connection">当前实例使用的连接对象或连接配置。</param>
        /// <param name="observation">本次测试记录的观察结果。</param>
        /// <param name="nativeAsync">指示当前 SQL 提供程序是否支持原生异步操作。</param>
        public ControlledDbTransaction(ControlledDbConnection connection, TransactionObservation observation,
            bool nativeAsync)
        {
            _connection = connection;
            _observation = observation;
            _nativeAsync = nativeAsync;
        }

        /// <inheritdoc />
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        /// <inheritdoc />
        protected override DbConnection DbConnection => _connection;

        /// <inheritdoc />
        public override void Commit()
        {
            _observation.SyncCommitCount++;
        }

        /// <inheritdoc />
        public override void Rollback()
        {
            _observation.SyncRollbackCount++;
        }

        /// <inheritdoc />
        public override ValueTask DisposeAsync()
        {
            DisposeOnce();
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeOnce();
            base.Dispose(disposing);
        }

        /// <summary>
        /// 记录受控事务释放且只执行一次。
        /// </summary>
        protected void DisposeOnce()
        {
            if (_disposed)
                return;
            _disposed = true;
            _observation.TransactionDisposeCount++;
        }

        /// <summary>
        /// 获取当前 Provider 是否使用原生异步事务 API。
        /// </summary>
        protected bool UsesNativeAsync => _nativeAsync;

        /// <summary>
        /// 获取当前事务使用的观测计数对象。
        /// </summary>
        protected TransactionObservation Observation => _observation;
    }

    /// <summary>
    /// 可控制异步提交、回滚和释放行为的数据库事务测试替身。
    /// </summary>
    private sealed class NativeAsyncControlledDbTransaction : ControlledDbTransaction
    {
        /// <summary>
        /// 初始化一个 <see cref="NativeAsyncControlledDbTransaction" /> 类型的实例。
        /// </summary>
        /// <param name="connection">当前实例使用的连接对象或连接配置。</param>
        /// <param name="observation">本次测试记录的观察结果。</param>
        public NativeAsyncControlledDbTransaction(ControlledDbConnection connection,
            TransactionObservation observation) : base(connection, observation, nativeAsync: true)
        {
        }

        /// <inheritdoc />
        public override Task CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Observation.NativeCommitCount++;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Observation.NativeRollbackCount++;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 为 Provider 注册矩阵测试提供自定义 SQL Provider。
    /// </summary>
    private sealed class MatrixCustomProvider : ISqlProvider, ISqlProviderProfileProvider
    {
        /// <summary>
        /// 获取自定义 SQL Provider 的共享实例。
        /// </summary>
        public static MatrixCustomProvider Instance { get; } = new();

        /// <inheritdoc />
        public string Key => CustomProviderKey;
        /// <inheritdoc />
        public DatabaseType DatabaseType => DatabaseType.SqlServer;
        /// <inheritdoc />
        public IDialect Dialect => SqlServerSqlProvider.Instance.Dialect;
        /// <inheritdoc />
        public ISqlClauseFactory ClauseFactory => SqlServerSqlProvider.Instance.ClauseFactory;
        /// <inheritdoc />
        public ISqlTableReferenceParser TableReferenceParser => SqlServerSqlProvider.Instance.TableReferenceParser;
        /// <inheritdoc />
        public ISqlPaginationRenderer PaginationRenderer => SqlServerSqlProvider.Instance.PaginationRenderer;
        /// <inheritdoc />
        public IParameterManagerFactory ParameterManagerFactory => SqlServerSqlProvider.Instance.ParameterManagerFactory;
        /// <inheritdoc />
        public IParamLiteralsResolver ParamLiteralsResolver => SqlServerSqlProvider.Instance.ParamLiteralsResolver;
        /// <inheritdoc />
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
