using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Filters;
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
    /// 测试目的：MySQL Query 使用请求作用域的过滤器状态，并在切换后生成完整 SQL。
    /// </summary>
    [Fact]
    public void Query_WhenScopedFilterChanges_ShouldRenderCompleteMySqlSql()
    {
        var services = new ServiceCollection();
        services.AddMySqlProvider();
        services.AddSqlDataSource("default", DatabaseType.MySql, "Server=localhost;Database=scope_test;");
        using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = root.CreateScope();
        var filter = scope.ServiceProvider.GetRequiredService<IDataFilter>();
        using var query = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create();
        var source = query.From<MySqlScopeProbeEntity>();

        var enabled = source.ToSql();
        var enabledSnapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(
            ((ISqlQueryBuilderAccessor)source).GetSqlBuilder());
        string disabled;
        SqlBuilderExecutionSnapshot disabledSnapshot;
        using (filter.Disable<ISoftDelete>())
        {
            disabled = source.ToSql();
            disabledSnapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(
                ((ISqlQueryBuilderAccessor)source).GetSqlBuilder());
        }

        Assert.Equal("Select `scope_probe`.`Id`,`scope_probe`.`IsDeleted` \r\nFrom `scope_probe` \r\nWhere `scope_probe`.`IsDeleted`=@_p_0", enabled);
        Assert.Equal("Select `scope_probe`.`Id`,`scope_probe`.`IsDeleted` \r\nFrom `scope_probe`", disabled);
        Assert.Equal(enabled, enabledSnapshot.Sql);
        Assert.Equal("@_p_0", Assert.Single(enabledSnapshot.Parameters).Name);
        Assert.Equal(false, Assert.Single(enabledSnapshot.Parameters).Value);
        Assert.Equal(disabled, disabledSnapshot.Sql);
        Assert.Empty(disabledSnapshot.Parameters);
        Assert.Equal(enabled, source.ToSql());
    }

    /// <summary>
    /// 测试目的：MySQL 工厂收到未配置的具名数据源时应明确失败，不能进入 SQL 执行路径。
    /// </summary>
    [Fact]
    public void QueryFactory_WhenNamedDataSourceIsMissing_ShouldThrowBeforeExecution()
    {
        var services = new ServiceCollection();
        services.AddMySqlProvider();
        services.AddSqlDataSource("default", DatabaseType.MySql, "Server=localhost;Database=scope_test;");
        var connectionResolver = new CountingConnectionResolver();
        services.AddSingleton<ISqlDbConnectionFactoryResolver>(connectionResolver);
        using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = root.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();

        var exception = Assert.Throws<InvalidOperationException>(() => factory.Create("missing"));

        Assert.Contains("missing", exception.Message);
        Assert.Equal(0, connectionResolver.CallCount);
    }

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

    [Table("scope_probe")]
    private sealed class MySqlScopeProbeEntity : ISoftDelete
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
    }

    private sealed class CountingConnectionResolver : ISqlDbConnectionFactoryResolver
    {
        public int CallCount { get; private set; }

        public System.Data.IDbConnection Create(string providerKey, string connectionString)
        {
            CallCount++;
            throw new InvalidOperationException("The connection resolver should not be called.");
        }
    }
}
