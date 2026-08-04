using System.Data;
using System.Reflection;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Dapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Bing.Dapper.Core.Tests.Factories;

/// <summary>
/// <see cref="SqlQueryFactory"/> 和 <see cref="SqlExecutorFactory"/> 单元测试。
/// </summary>
public class SqlFactoryTest
{
    /// <summary>
    /// 测试目的：Root 查询不得继续公开废弃的调试、过程执行和参数清理继承入口。
    /// </summary>
    [Fact]
    public void SqlQueryBase_WhenApiInspected_ShouldNotExposeLegacyExecutionMembers()
    {
        // Arrange
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Assert
        Assert.Null(typeof(SqlQueryBase).GetMethod("GetDebugSql", flags));
        Assert.Null(typeof(SqlQueryBase).GetMethod("InternalProcedureQuery", flags));
        Assert.Null(typeof(SqlQueryBase).GetMethod("InternalProcedureQueryAsync", flags));
        Assert.Null(typeof(SqlQueryBase).GetMethod("ClearParams", flags));
    }

    /// <summary>
    /// 测试目的：查询释放后不得再次创建独立查询描述并恢复执行能力。
    /// </summary>
    [Fact]
    public void Sql_WhenQueryDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        var query = new MySqlTestQuery(provider, new SqlOptions<MySqlTestQuery>());
        query.Dispose();

        // Act and Assert
        Assert.Throws<ObjectDisposedException>(() => query.Sql<int>("Select 1"));
    }

    /// <summary>
    /// 测试目的：同步 Open 回退期间发生取消时，异步开始事务不得继续创建事务。
    /// </summary>
    [Fact]
    public async Task BeginAsync_WhenCancellationOccursDuringSynchronousOpen_ShouldNotBeginTransaction()
    {
        // Arrange
        using var provider = CreateServices().BuildServiceProvider();
        using var cancellationTokenSource = new CancellationTokenSource();
        var connection = new Mock<IDbConnection>();
        var transaction = new Mock<IDbTransaction>();
        transaction.SetupGet(item => item.Connection).Returns(connection.Object);
        connection.SetupGet(item => item.State).Returns(ConnectionState.Closed);
        connection.Setup(item => item.Open()).Callback(cancellationTokenSource.Cancel);
        var options = new SqlOptions<MySqlTestQuery>
        {
            Connection = connection.Object
        };
        options.SetDatabaseContext(new DatabaseContext
        {
            DbKey = "mysql",
            DataSource = new SqlDataSourceDescriptor { Key = "mysql", DatabaseType = DatabaseType.MySql }
        });
        var query = new MySqlTestQuery(provider, options);
        var scopeFactory = new SqlTransactionScopeFactory(new FixedQueryFactory(query), new ThrowingExecutorFactory());

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scopeFactory.BeginAsync(cancellationToken: cancellationTokenSource.Token));
        connection.Verify(item => item.BeginTransaction(It.IsAny<IsolationLevel>()), Times.Never);
        connection.Verify(item => item.Dispose(), Times.Never);
    }

    /// <summary>
    /// 测试目的：显式数据源 Key 应选择对应 Provider 的实现类型与独立配置快照。
    /// </summary>
    [Fact]
    public void Create_WhenDbKeySpecified_ShouldUseMappedImplementationAndDataSourceOptions()
    {
        // Arrange
        var services = CreateServices();
        AddTestProvider(services, "test.mysql", DatabaseType.MySql);
        AddTestProvider(services, "test.sqlite", DatabaseType.Sqlite);
        services.AddSqlDataSource("mysql", DatabaseType.MySql, "Server=mysql;Database=app;", providerKey: "test.mysql");
        services.AddSqlDataSource("sqlite", DatabaseType.Sqlite, "Data Source=app.db", providerKey: "test.sqlite");
        services.AddSqlImplementationType<ISqlQuery, MySqlTestQuery>("test.mysql");
        services.AddSqlImplementationType<ISqlQuery, SqliteTestQuery>("test.sqlite");
        services.AddSqlImplementationType<ISqlExecutor, MySqlTestExecutor>("test.mysql");
        services.AddSqlImplementationType<ISqlExecutor, SqliteTestExecutor>("test.sqlite");
        using var provider = services.BuildServiceProvider();
        var queryFactory = provider.GetRequiredService<ISqlQueryFactory>();
        var executorFactory = provider.GetRequiredService<ISqlExecutorFactory>();

        // Act
        var query = queryFactory.Create<ISqlQuery>("mysql");
        var executor = executorFactory.Create<ISqlExecutor>("sqlite");

        // Assert
        var mysqlQuery = Assert.IsType<MySqlTestQuery>(query);
        Assert.Equal(DatabaseType.MySql, mysqlQuery.CurrentOptions.DatabaseType);
        Assert.Equal("Server=mysql;Database=app;", mysqlQuery.CurrentOptions.ConnectionString);
        Assert.Equal("mysql", mysqlQuery.CurrentOptions.GetDatabaseContext().DbKey);

        var sqliteExecutor = Assert.IsType<SqliteTestExecutor>(executor);
        Assert.Equal(DatabaseType.Sqlite, sqliteExecutor.CurrentOptions.DatabaseType);
        Assert.Equal("Data Source=app.db", sqliteExecutor.CurrentOptions.ConnectionString);
        Assert.Equal("sqlite", sqliteExecutor.CurrentOptions.GetDatabaseContext().DbKey);
    }

    /// <summary>
    /// 测试目的：相同数据库类型的不同 Provider Key 必须解析各自的 Query 实现，不能由注册顺序覆盖。
    /// </summary>
    [Fact]
    public void Create_WhenProviderKeysShareDatabaseType_ShouldUseDistinctImplementations()
    {
        // Arrange
        var services = CreateServices();
        AddTestProvider(services, "custom.sqlite.first", DatabaseType.Sqlite);
        AddTestProvider(services, "custom.sqlite.second", DatabaseType.Sqlite);
        services.AddSqlDataSource("first", DatabaseType.Sqlite, "Data Source=first.db", providerKey: "custom.sqlite.first");
        services.AddSqlDataSource("second", DatabaseType.Sqlite, "Data Source=second.db", providerKey: "custom.sqlite.second");
        services.AddSqlImplementationType<ISqlQuery, MySqlTestQuery>("custom.sqlite.first");
        services.AddSqlImplementationType<ISqlQuery, SqliteTestQuery>("custom.sqlite.second");
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();

        // Act
        var first = factory.Create<ISqlQuery>("first");
        var second = factory.Create<ISqlQuery>("second");

        // Assert
        Assert.IsType<MySqlTestQuery>(first);
        Assert.IsType<SqliteTestQuery>(second);
    }

    /// <summary>
    /// 测试目的：未指定数据源 Key 时，工厂应克隆当前上下文而不是重新解析默认数据源。
    /// </summary>
    [Fact]
    public void Create_WhenCurrentContextHasDataSource_ShouldPreserveContextSnapshot()
    {
        // Arrange
        var services = CreateServices();
        AddTestProvider(services, "test.mysql", DatabaseType.MySql);
        services.AddSqlImplementationType<ISqlQuery, MySqlTestQuery>("test.mysql");
        using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IDatabaseContextAccessor>();
        accessor.Current = new DatabaseContext
        {
            DbKey = "tenant-primary",
            TenantId = "tenant-1",
            MappingProfile = "tenant-profile",
            ReadPreference = SqlReadPreference.Primary,
            DataSource = new SqlDataSourceDescriptor
            {
                Key = "tenant-primary",
                ProviderKey = "test.mysql",
                DatabaseType = DatabaseType.MySql,
                ConnectionString = "Server=tenant;Database=app;"
            }
        };
        var factory = provider.GetRequiredService<ISqlQueryFactory>();

        // Act
        var query = factory.Create<ISqlQuery>();
        var context = Assert.IsType<MySqlTestQuery>(query).CurrentOptions.GetDatabaseContext();

        // Assert
        Assert.Equal("tenant-primary", context.DbKey);
        Assert.Equal("tenant-1", context.TenantId);
        Assert.Equal("tenant-profile", context.MappingProfile);
        Assert.Equal(SqlReadPreference.Primary, context.ReadPreference);
        Assert.Equal("Server=tenant;Database=app;", ((MySqlTestQuery)query).CurrentOptions.ConnectionString);
    }

    /// <summary>
    /// 测试目的：数据源未提供连接字符串时，工厂应保留模板中的外部连接对象。
    /// </summary>
    [Fact]
    public void Create_WhenDataSourceHasNoConnectionString_ShouldKeepTemplateConnection()
    {
        // Arrange
        var connection = new Mock<IDbConnection>().Object;
        var template = new SqlOptions<MySqlTestQuery>
        {
            DatabaseType = DatabaseType.MySql,
            Connection = connection
        };
        var services = CreateServices();
        AddTestProvider(services, "test.mysql", DatabaseType.MySql);
        services.AddSqlDataSource("mysql", DatabaseType.MySql, providerKey: "test.mysql");
        services.AddSqlImplementationType<ISqlQuery, MySqlTestQuery>("test.mysql");
        services.AddSingleton(template);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();

        // Act
        var query = Assert.IsType<MySqlTestQuery>(factory.Create<ISqlQuery>("mysql"));

        // Assert
        Assert.NotSame(template, query.CurrentOptions);
        Assert.Same(connection, query.CurrentOptions.Connection);
        Assert.Null(query.CurrentOptions.ConnectionString);
        Assert.Equal(DatabaseType.MySql, query.CurrentOptions.DatabaseType);
    }

    /// <summary>
    /// 测试目的：数据源指定未注册 Provider Key 时，Factory 必须在实例创建前拒绝，不能仅凭实现映射继续执行。
    /// </summary>
    [Fact]
    public void Create_WhenDataSourceProviderKeyIsNotRegistered_ShouldThrowNotSupportedException()
    {
        // Arrange
        var services = CreateServices();
        services.AddSqlDataSource("missing", DatabaseType.Sqlite, "Data Source=missing.db", providerKey: "custom.missing");
        services.AddSqlImplementationType<ISqlQuery, SqliteTestQuery>("custom.missing");
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();

        // Act and Assert
        var exception = Assert.Throws<NotSupportedException>(() => factory.Create<ISqlQuery>("missing"));
        Assert.Contains("custom.missing", exception.Message);
    }

    /// <summary>
    /// 创建核心服务集合。
    /// </summary>
    /// <returns>已注册 Dapper Core 服务的集合。</returns>
    private static IServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSqlCore();
        return services;
    }

    /// <summary>
    /// 注册仅用于工厂路由测试的 Provider。
    /// </summary>
    /// <param name="services">要注册测试 Provider 的服务集合。</param>
    /// <param name="key">供 Factory 路由解析的 Provider Key。</param>
    /// <param name="databaseType">该测试 Provider 代表的数据库类型。</param>
    private static void AddTestProvider(IServiceCollection services, string key, DatabaseType databaseType) =>
        services.AddSqlBuilderProvider(new TestSqlProvider(key, databaseType), _ => null);

    /// <summary>
    /// 仅用于 Factory Provider 路由测试的 SQL Provider。
    /// </summary>
    private sealed class TestSqlProvider : ISqlProvider
    {
        /// <summary>
        /// 初始化测试 Provider。
        /// </summary>
        /// <param name="key">Provider 路由标识。</param>
        /// <param name="databaseType">Provider 对应的数据库类型。</param>
        public TestSqlProvider(string key, DatabaseType databaseType)
        {
            Key = key;
            DatabaseType = databaseType;
        }

        /// <inheritdoc />
        public string Key { get; }

        /// <inheritdoc />
        public DatabaseType DatabaseType { get; }

        /// <inheritdoc />
        public IDialect Dialect => null;

        /// <inheritdoc />
        public ISqlClauseFactory ClauseFactory => null;

        /// <inheritdoc />
        public ISqlTableReferenceParser TableReferenceParser => null;

        /// <inheritdoc />
        public ISqlPaginationRenderer PaginationRenderer => null;

        /// <inheritdoc />
        public IParameterManagerFactory ParameterManagerFactory => null;

        /// <inheritdoc />
        public IParamLiteralsResolver ParamLiteralsResolver => null;
    }

    /// <summary>
    /// MySQL 查询测试实现。
    /// </summary>
    private sealed class MySqlTestQuery : SqlQueryBase
    {
        /// <summary>
        /// 初始化一个 <see cref="MySqlTestQuery"/> 类型的实例。
        /// </summary>
        /// <param name="serviceProvider">服务提供程序。</param>
        /// <param name="options">SQL 配置。</param>
        public MySqlTestQuery(IServiceProvider serviceProvider, SqlOptions<MySqlTestQuery> options)
            : base(serviceProvider, options)
        {
        }

        /// <summary>
        /// 当前 SQL 配置。
        /// </summary>
        public SqlOptions CurrentOptions => Options;

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }

    /// <summary>
    /// SQLite 查询测试实现。
    /// </summary>
    private sealed class SqliteTestQuery : SqlQueryBase
    {
        /// <summary>
        /// 初始化一个 <see cref="SqliteTestQuery"/> 类型的实例。
        /// </summary>
        /// <param name="serviceProvider">服务提供程序。</param>
        /// <param name="options">SQL 配置。</param>
        public SqliteTestQuery(IServiceProvider serviceProvider, SqlOptions<SqliteTestQuery> options)
            : base(serviceProvider, options)
        {
        }

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }

    /// <summary>
    /// MySQL 执行器测试实现。
    /// </summary>
    private sealed class MySqlTestExecutor : SqlExecutorBase
    {
        /// <summary>
        /// 初始化一个 <see cref="MySqlTestExecutor"/> 类型的实例。
        /// </summary>
        /// <param name="serviceProvider">服务提供程序。</param>
        /// <param name="options">SQL 配置。</param>
        public MySqlTestExecutor(IServiceProvider serviceProvider, SqlOptions<MySqlTestExecutor> options)
            : base(serviceProvider, options)
        {
        }

        /// <summary>
        /// 当前 SQL 配置。
        /// </summary>
        public SqlOptions CurrentOptions => Options;

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }

    /// <summary>
    /// SQLite 执行器测试实现。
    /// </summary>
    private sealed class SqliteTestExecutor : SqlExecutorBase
    {
        /// <summary>
        /// 初始化一个 <see cref="SqliteTestExecutor"/> 类型的实例。
        /// </summary>
        /// <param name="serviceProvider">服务提供程序。</param>
        /// <param name="options">SQL 配置。</param>
        public SqliteTestExecutor(IServiceProvider serviceProvider, SqlOptions<SqliteTestExecutor> options)
            : base(serviceProvider, options)
        {
        }

        /// <summary>
        /// 当前 SQL 配置。
        /// </summary>
        public SqlOptions CurrentOptions => Options;

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }

    /// <summary>
    /// 固定返回测试查询对象的工厂。
    /// </summary>
    private sealed class FixedQueryFactory : ISqlQueryFactory
    {
        private readonly ISqlQuery _query;

        /// <summary>
        /// 初始化一个 <see cref="FixedQueryFactory"/> 类型的实例。
        /// </summary>
        /// <param name="query">测试查询对象。</param>
        public FixedQueryFactory(ISqlQuery query) => _query = query;

        /// <inheritdoc />
        public TQuery Create<TQuery>(string dbKey) where TQuery : class, ISqlQuery => _query as TQuery;

        /// <inheritdoc />
        public TQuery Create<TQuery>() where TQuery : class, ISqlQuery => _query as TQuery;
    }

    /// <summary>
    /// 不应在事务开始阶段调用的执行器工厂。
    /// </summary>
    private sealed class ThrowingExecutorFactory : ISqlExecutorFactory
    {
        /// <inheritdoc />
        public TExecutor Create<TExecutor>(string dbKey) where TExecutor : class, ISqlExecutor =>
            throw new InvalidOperationException("事务开始阶段不应创建执行器。");

        /// <inheritdoc />
        public TExecutor Create<TExecutor>() where TExecutor : class, ISqlExecutor =>
            throw new InvalidOperationException("事务开始阶段不应创建执行器。");
    }
}