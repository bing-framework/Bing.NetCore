using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// MySQL Provider 服务注册单元测试。
/// </summary>
public class MySqlProviderRegistrationTest
{
    /// <summary>
    /// 测试目的：注册 MySQL Provider 后，查询、执行器和多结果集执行器选项应使用 MySQL 数据库类型。
    /// </summary>
    [Fact]
    public void AddMySqlProvider_WhenRegistered_ShouldConfigureMySqlOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMySqlProvider();
        using var provider = services.BuildServiceProvider();

        // Act
        var queryOptions = provider.GetRequiredService<SqlOptions<MySqlQuery>>();
        var executorOptions = provider.GetRequiredService<SqlOptions<MySqlExecutor>>();
        var multipleQueryOptions = provider.GetRequiredService<SqlOptions<MySqlMultipleQueryExecutor>>();

        // Assert
        Assert.Equal(DatabaseType.MySql, queryOptions.DatabaseType);
        Assert.Equal(DatabaseType.MySql, executorOptions.DatabaseType);
        Assert.Equal(DatabaseType.MySql, multipleQueryOptions.DatabaseType);
        Assert.True(MySqlSqlProvider.Instance.Profile.Execution.SupportsMultipleResultSets);
        Assert.Equal(SqlQueryCapabilityState.Supported, MySqlSqlProvider.Instance.Profile.Query.Pagination);
    }

    /// <summary>
    /// 测试目的：固定 Query Factory 应为 MySQL 数据源创建官方查询实现。
    /// </summary>
    [Fact]
    public void QueryFactory_WhenMySqlDataSourceRegistered_ShouldCreateMySqlQuery()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMySqlProvider();
        services.AddSqlDataSource("default", DatabaseType.MySql, "Server=mysql;Database=app;");
        using var provider = services.BuildServiceProvider();

        // Act
        var query = provider.GetRequiredService<ISqlQueryFactory>().Create();

        // Assert
        Assert.IsType<MySqlQuery>(query);
    }

    /// <summary>
    /// 测试目的：固定 Executor Factory 应为 MySQL 数据源创建官方执行器实现。
    /// </summary>
    [Fact]
    public void ExecutorFactory_WhenMySqlDataSourceRegistered_ShouldCreateMySqlExecutor()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMySqlProvider();
        services.AddSqlDataSource("default", DatabaseType.MySql, "Server=mysql;Database=app;");
        using var provider = services.BuildServiceProvider();

        // Act
        var executor = provider.GetRequiredService<ISqlExecutorFactory>().Create();

        // Assert
        Assert.IsType<MySqlExecutor>(executor);
    }

    /// <summary>
    /// 测试目的：固定 Multiple Factory 应为 MySQL 数据源创建官方多结果集执行器实现。
    /// </summary>
    [Fact]
    public void MultipleQueryExecutorFactory_WhenMySqlDataSourceRegistered_ShouldCreateMySqlExecutor()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMySqlProvider();
        services.AddSqlDataSource("default", DatabaseType.MySql, "Server=mysql;Database=app;");
        using var provider = services.BuildServiceProvider();

        // Act
        var executor = provider.GetRequiredService<ISqlMultipleQueryExecutorFactory>().Create();

        // Assert
        Assert.IsType<MySqlMultipleQueryExecutor>(executor);
    }
}