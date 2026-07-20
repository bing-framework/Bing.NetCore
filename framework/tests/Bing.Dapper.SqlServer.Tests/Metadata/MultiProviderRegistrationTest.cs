using System.Data;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
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
    /// 测试 - 同一容器应根据具名数据源创建对应 Provider 的查询、方言和参数规则。
    /// </summary>
    [Fact]
    public void Create_WhenMultipleProvidersRegistered_ShouldResolveQueryAndDialectByDbKey()
    {
        // Arrange
        using var provider = CreateProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();

        // Act
        using var mysql = factory.Create<ISqlQuery>("mysql");
        using var postgreSql = factory.Create<ISqlQuery>("pgsql");
        using var sqlServer = factory.Create<ISqlQuery>("sqlserver");
        using var sqlite = factory.Create<ISqlQuery>("sqlite");
        using var oracle = factory.Create<ISqlQuery>("oracle");

        // Assert
        Assert.IsType<MySqlQuery>(mysql);
        Assert.IsType<MySqlDialect>(((ISqlPartAccessor)mysql).Dialect);
        Assert.Equal('`', ((ISqlPartAccessor)mysql).Dialect.OpeningIdentifier);
        Assert.IsType<PostgreSqlQuery>(postgreSql);
        Assert.IsType<PostgreSqlDialect>(((ISqlPartAccessor)postgreSql).Dialect);
        Assert.Equal('"', ((ISqlPartAccessor)postgreSql).Dialect.OpeningIdentifier);
        Assert.IsType<SqlServerSqlQuery>(sqlServer);
        Assert.IsType<SqlServerDialect>(((ISqlPartAccessor)sqlServer).Dialect);
        Assert.Equal('[', ((ISqlPartAccessor)sqlServer).Dialect.OpeningIdentifier);
        Assert.IsType<SqliteSqlQuery>(sqlite);
        Assert.IsType<SqliteDialect>(((ISqlPartAccessor)sqlite).Dialect);
        Assert.Equal('`', ((ISqlPartAccessor)sqlite).Dialect.OpeningIdentifier);
        Assert.IsType<OracleSqlQuery>(oracle);
        Assert.IsType<OracleDialect>(((ISqlPartAccessor)oracle).Dialect);
        Assert.Equal('"', ((ISqlPartAccessor)oracle).Dialect.OpeningIdentifier);
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
        var transactionScopeFactory = provider.GetRequiredService<ISqlTransactionScopeFactory>();

        // Act
        using var query = factory.Create<ISqlQuery>("doris");
        var exception = Assert.Throws<NotSupportedException>(() => transactionScopeFactory.Begin("doris"));

        // Assert
        Assert.IsType<MySqlQuery>(query);
        Assert.IsType<MySqlDialect>(((ISqlPartAccessor)query).Dialect);
        Assert.Contains("doris", exception.Message);
        Assert.DoesNotContain("Password", exception.Message, StringComparison.OrdinalIgnoreCase);
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
            (DatabaseType.MySql, "Server=mysql;Database=test;", "MySqlConnection"),
            (DatabaseType.PgSql, "Host=pgsql;Database=test;", "NpgsqlConnection"),
            (DatabaseType.SqlServer, "Server=sqlserver;Database=test;", "SqlConnection"),
            (DatabaseType.Sqlite, "Data Source=executor-only.db", "SqliteConnection"),
            (DatabaseType.Oracle, "User Id=bing;Password=secret;Data Source=oracle-test", "OracleConnection")
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
        services.AddDatabase<TestDatabase>();
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
        services.AddSqlDataSource("doris", DatabaseType.MySql, "Server=doris;Database=analytics;",
            setupAction: source =>
            {
                source.MappingProfile = "doris";
                source.SupportsTransactions = false;
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
        services.AddDatabase<TestDatabase>();
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