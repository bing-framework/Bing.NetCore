using System.Data;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
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
    /// 测试目的：事务作用域创建的子查询释放后不得经资源绑定器重新绑定并恢复执行能力。
    /// </summary>
    [Fact]
    public void BindTransactionScope_WhenChildQueryDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        var query = new MySqlTestQuery(provider, new SqlOptions<MySqlTestQuery>());
        var connection = new Mock<IDbConnection>().Object;
        var transaction = new Mock<IDbTransaction>();
        transaction.SetupGet(item => item.Connection).Returns(connection);
        var lease = new TestTransactionScopeLease();
        var context = new DatabaseContext
        {
            DbKey = "mysql",
            DataSource = new SqlDataSourceDescriptor { Key = "mysql", DatabaseType = DatabaseType.MySql }
        };
        var binder = (ISqlTransactionScopeResourceBinder)query;
        binder.BindTransactionScope(context, connection, transaction.Object, lease);
        query.Dispose();

        // Act and Assert
        Assert.Throws<ObjectDisposedException>(() =>
            binder.BindTransactionScope(context, connection, transaction.Object, lease));
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
        services.AddSqlDataSource("mysql", DatabaseType.MySql, "Server=mysql;Database=app;");
        services.AddSqlDataSource("sqlite", DatabaseType.Sqlite, "Data Source=app.db");
        services.AddSqlImplementationType<ISqlQuery, MySqlTestQuery>(DatabaseType.MySql);
        services.AddSqlImplementationType<ISqlQuery, SqliteTestQuery>(DatabaseType.Sqlite);
        services.AddSqlImplementationType<ISqlExecutor, MySqlTestExecutor>(DatabaseType.MySql);
        services.AddSqlImplementationType<ISqlExecutor, SqliteTestExecutor>(DatabaseType.Sqlite);
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
    /// 测试目的：未指定数据源 Key 时，工厂应克隆当前上下文而不是重新解析默认数据源。
    /// </summary>
    [Fact]
    public void Create_WhenCurrentContextHasDataSource_ShouldPreserveContextSnapshot()
    {
        // Arrange
        var services = CreateServices();
        services.AddSqlImplementationType<ISqlQuery, MySqlTestQuery>(DatabaseType.MySql);
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
        services.AddSqlDataSource("mysql", DatabaseType.MySql);
        services.AddSqlImplementationType<ISqlQuery, MySqlTestQuery>(DatabaseType.MySql);
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
    /// 用于事务绑定测试的活动租约。
    /// </summary>
    private sealed class TestTransactionScopeLease : ISqlTransactionScopeLease
    {
        /// <inheritdoc />
        public string TransactionId { get; } = "test-transaction";

        /// <inheritdoc />
        public bool IsActive { get; private set; } = true;

        /// <inheritdoc />
        public void EnsureActive()
        {
            if (IsActive == false)
                throw new InvalidOperationException("事务作用域租约已失效。");
        }
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