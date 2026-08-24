using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// Oracle Provider 服务注册单元测试。
/// </summary>
public class OracleProviderRegistrationTest
{
    /// <summary>
    /// 测试目的：注册 Oracle Provider 后，查询和执行器选项应使用 Oracle 数据库类型。
    /// </summary>
    [Fact]
    public void AddOracleProvider_WhenRegistered_ShouldConfigureOracleOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOracleProvider();
        using var provider = services.BuildServiceProvider();

        // Act
        var queryOptions = provider.GetRequiredService<SqlOptions<OracleSqlQuery>>();
        var executorOptions = provider.GetRequiredService<SqlOptions<OracleSqlExecutor>>();

        // Assert
        Assert.Equal(DatabaseType.Oracle, queryOptions.DatabaseType);
        Assert.Equal(DatabaseType.Oracle, executorOptions.DatabaseType);
    }

    /// <summary>
    /// 测试目的：固定 Query Factory 应为 Oracle 数据源创建官方查询实现。
    /// </summary>
    [Fact]
    public void QueryFactory_WhenOracleDataSourceRegistered_ShouldCreateOracleQuery()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOracleProvider();
        services.AddSqlDataSource("default", DatabaseType.Oracle, "Data Source=oracle;User Id=bing;Password=secret;");
        using var provider = services.BuildServiceProvider();

        // Act
        var query = provider.GetRequiredService<ISqlQueryFactory>().Create();

        // Assert
        Assert.IsType<OracleSqlQuery>(query);
    }

    /// <summary>
    /// 测试目的：固定 Executor Factory 应为 Oracle 数据源创建官方执行器实现。
    /// </summary>
    [Fact]
    public void ExecutorFactory_WhenOracleDataSourceRegistered_ShouldCreateOracleExecutor()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOracleProvider();
        services.AddSqlDataSource("default", DatabaseType.Oracle, "Data Source=oracle;User Id=bing;Password=secret;");
        using var provider = services.BuildServiceProvider();

        // Act
        var executor = provider.GetRequiredService<ISqlExecutorFactory>().Create();

        // Assert
        Assert.IsType<OracleSqlExecutor>(executor);
    }

    /// <summary>
    /// 测试目的：Oracle 插值 SQL 应使用冒号参数 Token，并在遇到已有同名 Token 时生成与 Dapper 绑定键一致的独立名称。
    /// </summary>
    [Fact]
    public void SqlInterpolated_WhenOracleTokenConflicts_ShouldUseColonPrefixAndUnprefixedBindingKey()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOracleProvider();
        services.AddSqlDataSource("default", DatabaseType.Oracle, "Data Source=oracle;User Id=bing;Password=secret;");
        using var provider = services.BuildServiceProvider();
        var query = provider.GetRequiredService<ISqlQueryFactory>().Create();

        // Act
        var description = query.SqlInterpolated(
            $"Select :p0 As ExistingValue, {"value"} As InterpolatedValue From dual");
        var parameters = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(description.Parameters);

        // Assert
        Assert.Equal("Select :p0 As ExistingValue, :p0_1 As InterpolatedValue From dual", description.CommandText);
        Assert.Equal("value", Assert.Single(parameters).Value);
        Assert.True(parameters.ContainsKey("p0_1"));
        Assert.DoesNotContain(parameters.Keys, key => key.StartsWith(":"));
    }
}