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
        Assert.True(MySqlSqlProvider.Instance.Capabilities.SupportsMultipleResultSets);
    }

    /// <summary>
    /// 测试目的：注册默认 MySQL 查询对象时应固定使用 MySQL 数据库类型。
    /// </summary>
    [Fact]
    public void AddMySqlQuery_WhenRegistered_ShouldConfigureMySqlDatabaseType()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMySqlQuery("Server=mysql;Database=app;");
        using var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<SqlOptions<MySqlQuery>>();

        // Assert
        Assert.Equal(DatabaseType.MySql, options.DatabaseType);
    }

    /// <summary>
    /// 测试目的：注册默认 MySQL 执行器时应固定使用 MySQL 数据库类型。
    /// </summary>
    [Fact]
    public void AddMySqlExecutor_WhenRegistered_ShouldConfigureMySqlDatabaseType()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMySqlExecutor("Server=mysql;Database=app;");
        using var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<SqlOptions<MySqlExecutor>>();

        // Assert
        Assert.Equal(DatabaseType.MySql, options.DatabaseType);
    }

    /// <summary>
    /// 测试目的：注册默认 MySQL 多结果集执行器时应固定使用 MySQL 数据库类型。
    /// </summary>
    [Fact]
    public void AddMySqlMultipleQueryExecutor_WhenRegistered_ShouldConfigureMySqlDatabaseType()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMySqlMultipleQueryExecutor("Server=mysql;Database=app;");
        using var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<SqlOptions<MySqlMultipleQueryExecutor>>();

        // Assert
        Assert.Equal(DatabaseType.MySql, options.DatabaseType);
    }
}