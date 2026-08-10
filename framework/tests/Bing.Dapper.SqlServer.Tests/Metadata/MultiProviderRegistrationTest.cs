using System.Data;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;
using Bing.Dapper;
using Bing.Dapper.MySql;
using Bing.Dapper.Oracle;
using Bing.Dapper.PostgreSql;
using Bing.Dapper.Sqlite;
using Bing.Dapper.SqlServer;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// 同容器多 Provider 注册测试。
/// </summary>
public class MultiProviderRegistrationTest
{
    /// <summary>
    /// 测试目的：同容器直接注入根查询时应按当前数据库上下文路由，而非返回第一个 Provider 的实现。
    /// </summary>
    [Fact]
    public void ResolveDirectly_WhenDatabaseScopeChanges_ShouldRouteQueryByCurrentDataSource()
    {
        // Arrange
        using var provider = CreateProvider();
        var databaseScopeManager = provider.GetRequiredService<IDatabaseScopeManager>();

        // Act
        using var sqliteScope = databaseScopeManager.Use("sqlite");
        using var sqliteQuery = provider.GetRequiredService<ISqlQuery>();

        // Assert
        Assert.IsType<SqliteSqlQuery>(sqliteQuery);
    }

    /// <summary>
    /// 测试 - 同一容器应根据具名数据源创建对应 Provider 的查询、方言和参数规则。
    /// </summary>
    [Fact]
    public void Create_WhenMultipleProvidersRegistered_ShouldResolveQueryAndDialectByDbKey()
    {
        // Arrange
        using var provider = CreateProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();
        var providerResolver = provider.GetRequiredService<ISqlProviderResolver>();

        // Act
        using var mysql = factory.Create<ISqlQuery>("mysql");
        using var postgreSql = factory.Create<ISqlQuery>("pgsql");
        using var sqlServer = factory.Create<ISqlQuery>("sqlserver");
        using var sqlite = factory.Create<ISqlQuery>("sqlite");
        using var oracle = factory.Create<ISqlQuery>("oracle");

        // Assert
        Assert.IsType<MySqlQuery>(mysql);
        Assert.IsType<MySqlDialect>(providerResolver.Resolve(MySqlSqlProvider.Instance.Key).Dialect);
        Assert.Equal('`', providerResolver.Resolve(MySqlSqlProvider.Instance.Key).Dialect.OpeningIdentifier);
        Assert.IsType<PostgreSqlQuery>(postgreSql);
        Assert.IsType<PostgreSqlDialect>(providerResolver.Resolve(PostgreSqlSqlProvider.Instance.Key).Dialect);
        Assert.Equal('"', providerResolver.Resolve(PostgreSqlSqlProvider.Instance.Key).Dialect.OpeningIdentifier);
        Assert.IsType<SqlServerSqlQuery>(sqlServer);
        Assert.IsType<SqlServerDialect>(providerResolver.Resolve(SqlServerSqlProvider.Instance.Key).Dialect);
        Assert.Equal('[', providerResolver.Resolve(SqlServerSqlProvider.Instance.Key).Dialect.OpeningIdentifier);
        Assert.IsType<SqliteSqlQuery>(sqlite);
        Assert.IsType<SqliteDialect>(providerResolver.Resolve(SqliteSqlProvider.Instance.Key).Dialect);
        Assert.Equal('`', providerResolver.Resolve(SqliteSqlProvider.Instance.Key).Dialect.OpeningIdentifier);
        Assert.IsType<OracleSqlQuery>(oracle);
        Assert.IsType<OracleDialect>(providerResolver.Resolve(OracleSqlProvider.Instance.Key).Dialect);
        Assert.Equal('"', providerResolver.Resolve(OracleSqlProvider.Instance.Key).Dialect.OpeningIdentifier);
    }

    /// <summary>
    /// 测试目的：多结果集执行器工厂应按数据源类型创建支持批量结果读取的 Provider 实现。
    /// </summary>
    [Fact]
    public void CreateMultipleQueryExecutor_WhenSupportedProvidersRegistered_ShouldResolveByDbKey()
    {
        // Arrange
        using var provider = CreateProvider();
        var factory = provider.GetRequiredService<ISqlMultipleQueryExecutorFactory>();

        // Act
        using var mySql = factory.Create("mysql");
        using var doris = factory.Create("doris");
        using var postgreSql = factory.Create("pgsql");
        using var sqlServer = factory.Create("sqlserver");
        using var sqlite = factory.Create("sqlite");

        // Assert
        Assert.IsType<MySqlMultipleQueryExecutor>(mySql);
        Assert.IsType<MySqlMultipleQueryExecutor>(doris);
        Assert.IsType<PostgreSqlMultipleQueryExecutor>(postgreSql);
        Assert.IsType<SqlServerSqlMultipleQueryExecutor>(sqlServer);
        Assert.IsType<SqliteSqlMultipleQueryExecutor>(sqlite);
    }

    /// <summary>
    /// 测试 - Doris 数据源应复用 MySQL Provider，并拒绝本地事务。
    /// </summary>
    [Fact]
    public void Create_WhenDorisUsesMySqlProtocol_ShouldUseMySqlProviderAndRejectLocalTransaction()
    {
        // Arrange
        using var provider = CreateProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();
        var providerResolver = provider.GetRequiredService<ISqlProviderResolver>();
        var transactionScopeFactory = provider.GetRequiredService<ISqlTransactionScopeFactory>();

        // Act
        using var query = factory.Create<ISqlQuery>("doris");
        var exception = Assert.Throws<NotSupportedException>(() => transactionScopeFactory.Begin("doris"));

        // Assert
        Assert.IsType<MySqlQuery>(query);
        Assert.IsType<MySqlDialect>(providerResolver.Resolve(MySqlSqlProvider.Instance.Key).Dialect);
        Assert.Contains("doris", exception.Message);
        Assert.DoesNotContain("Password", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 测试目的：Doris 只读数据源必须在创建连接前拒绝写入型存储过程。
    /// </summary>
    [Fact]
    public void ExecuteProcedure_WhenDorisDataSourceIsReadOnly_ShouldRejectBeforeConnectionAccess()
    {
        // Arrange
        using var provider = CreateProvider();
        var factory = provider.GetRequiredService<ISqlExecutorFactory>();
        using var executor = factory.Create<ISqlExecutor>("doris");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => executor.ExecuteProcedure("sync_analytics"));

        // Assert
        Assert.Contains("doris", exception.Message);
        Assert.Contains("只读", exception.Message);
    }

    /// <summary>
    /// 测试目的：SQL Server Provider 应将 Database、Schema 和表名逐段格式化。
    /// </summary>
    [Fact]
    public void Formatter_WhenSqlServerReferenceContainsDatabaseAndSchema_ShouldFormatThreeParts()
    {
        // Arrange
        using var provider = CreateProvider();
        var formatter = provider.GetRequiredService<ISqlObjectNameFormatter>();
        var dialect = provider.GetRequiredService<ISqlProviderResolver>()
            .Resolve(SqlServerSqlProvider.Instance.Key).Dialect;
        using var query = provider.GetRequiredService<ISqlQueryFactory>().Create<ISqlQuery>("sqlserver");
        var reference = new SqlTableReference
        {
            Database = "reporting",
            Schema = "dbo",
            TableName = "users"
        };

        // Act
        var result = formatter.Format(reference, dialect, DatabaseType.SqlServer);

        // Assert
        Assert.Equal("[reporting].[dbo].[users]", result);
    }

    /// <summary>
    /// 测试 - 仅注册各 Provider 执行器时，连接工厂解析器仍应创建对应连接。
    /// </summary>
    [Fact]
    public void ConnectionFactoryResolver_WhenOnlyExecutorsRegistered_ShouldCreateProviderConnections()
    {
        // Arrange
        using var provider = CreateExecutorOnlyProvider();
        var resolver = provider.GetRequiredService<ISqlDbConnectionFactoryResolver>();
        var registrations = new[]
        {
            (MySqlSqlProvider.Instance.Key, "Server=mysql;Database=test;", "MySqlConnection"),
            (PostgreSqlSqlProvider.Instance.Key, "Host=pgsql;Database=test;", "NpgsqlConnection"),
            (SqlServerSqlProvider.Instance.Key, "Server=sqlserver;Database=test;", "SqlConnection"),
            (SqliteSqlProvider.Instance.Key, "Data Source=executor-only.db", "SqliteConnection"),
            (OracleSqlProvider.Instance.Key, "User Id=bing;Password=secret;Data Source=oracle-test", "OracleConnection")
        };

        // Act
        var connections = registrations.Select(registration => resolver.Create(registration.Item1, registration.Item2))
            .ToList();

        // Assert
        for (var index = 0; index < registrations.Length; index++)
        {
            using var connection = connections[index];
            Assert.Equal(registrations[index].Item3, connection.GetType().Name);
            Assert.Equal(registrations[index].Item2, connection.ConnectionString);
        }
    }

    /// <summary>
    /// 创建同时注册多个 Provider 的服务提供程序。
    /// </summary>
    /// <returns>服务提供程序。</returns>
    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddMySqlProvider();
        services.AddPostgreSqlProvider();
        services.AddSqlServerProvider();
        services.AddSqliteProvider();
        services.AddOracleProvider();
        services.AddSqlDataSource("mysql", DatabaseType.MySql, "Server=mysql;Database=test;");
        services.AddSqlDataSource("pgsql", DatabaseType.PgSql, "Host=pgsql;Database=test;");
        services.AddSqlDataSource("sqlserver", DatabaseType.SqlServer, "Server=sqlserver;Database=test;");
        services.AddSqlDataSource("sqlite", DatabaseType.Sqlite, "Data Source=:memory:");
        services.AddSqlDataSource("oracle", DatabaseType.Oracle,
            "User Id=bing;Password=secret;Data Source=oracle-test");
        services.AddSqlDataSource("doris", DatabaseType.Doris, "Server=doris;Database=analytics;",
            setupAction: source =>
            {
                source.MappingProfile = "doris";
            });
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建仅注册执行器的服务提供程序。
    /// </summary>
    /// <returns>服务提供程序。</returns>
    private static ServiceProvider CreateExecutorOnlyProvider()
    {
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddMySqlExecutor("Server=mysql;Database=test;");
        services.AddPostgreSqlExecutor("Host=pgsql;Database=test;");
        services.AddSqlServerSqlExecutor("Server=sqlserver;Database=test;");
        services.AddSqliteSqlExecutor("Data Source=executor-only.db");
        services.AddOracleSqlExecutor("User Id=bing;Password=secret;Data Source=oracle-test");
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 工厂创建阶段不使用实际连接的测试数据库。
    /// </summary>
    private sealed class TestDatabase : IDatabase
    {
        /// <inheritdoc />
        public IDbConnection GetConnection() => null;
    }
}