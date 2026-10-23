using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Filters;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// PostgreSQL Provider 服务注册测试。
/// </summary>
public class PostgreSqlProviderRegistrationTest
{
    /// <summary>
    /// 测试目的：PostgreSQL Query 使用请求作用域的过滤器状态，并在切换后生成完整 SQL。
    /// </summary>
    [Fact]
    public void Query_WhenScopedFilterChanges_ShouldRenderCompletePostgreSqlSql()
    {
        var services = new ServiceCollection();
        services.AddPostgreSqlProvider();
        services.AddSqlDataSource("default", DatabaseType.PgSql, "Host=localhost;Database=scope_test;");
        using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = root.CreateScope();
        var filter = scope.ServiceProvider.GetRequiredService<IDataFilter>();
        using var query = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create();
        var source = query.From<PostgreSqlScopeProbeEntity>();

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

        Assert.Equal("Select \"scope_probe\".\"Id\",\"scope_probe\".\"IsDeleted\" \r\nFrom \"scope_probe\" \r\nWhere \"scope_probe\".\"IsDeleted\"=@_p_0", enabled);
        Assert.Equal("Select \"scope_probe\".\"Id\",\"scope_probe\".\"IsDeleted\" \r\nFrom \"scope_probe\"", disabled);
        Assert.Equal(enabled, enabledSnapshot.Sql);
        Assert.Equal("@_p_0", Assert.Single(enabledSnapshot.Parameters).Name);
        Assert.Equal(false, Assert.Single(enabledSnapshot.Parameters).Value);
        Assert.Equal(disabled, disabledSnapshot.Sql);
        Assert.Empty(disabledSnapshot.Parameters);
        Assert.Equal(enabled, source.ToSql());
    }

    /// <summary>
    /// 测试目的：PostgreSQL 工厂收到未配置的具名数据源时应明确失败，不能进入 SQL 执行路径。
    /// </summary>
    [Fact]
    public void QueryFactory_WhenNamedDataSourceIsMissing_ShouldThrowBeforeExecution()
    {
        var services = new ServiceCollection();
        services.AddPostgreSqlProvider();
        services.AddSqlDataSource("default", DatabaseType.PgSql, "Host=localhost;Database=scope_test;");
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
    /// 测试目的：PostgreSQL dollar-quoted 文本中的参数样式不得触发插值参数改名， 避免文本内容与实际绑定参数发生错误冲突。
    /// </summary>
    [Fact]
    public void SqlInterpolated_WhenTokenAppearsOnlyInDollarQuotedText_ShouldKeepDefaultParameterName()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddPostgreSqlProvider();
        services.AddSqlDataSource("pgsql", DatabaseType.PgSql, "Host=localhost;Database=test;");
        using var provider = services.BuildServiceProvider();
        using var query = provider.GetRequiredService<ISqlQueryFactory>().Create("pgsql");

        // Act
        var description = query.SqlInterpolated(
            $"Select $tag$@p0$tag$ Where Name = {"Bing"}");
        var parameters = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(description.Parameters);

        // Assert
        Assert.Equal("Select $tag$@p0$tag$ Where Name = @p0", description.CommandText);
        Assert.Equal("Bing", Assert.Single(parameters).Value);
        Assert.True(parameters.ContainsKey("p0"));
    }

    /// <summary>
    /// 测试目的：具名 PostgreSQL 数据源应创建 PostgreSQL 查询、执行器、多结果集执行器、方言和 Npgsql 连接。
    /// </summary>
    [Fact]
    public void Factories_WhenPgSqlDataSourceIsConfigured_ShouldResolvePostgreSqlServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddPostgreSqlProvider();
        services.AddSqlDataSource("pgsql", DatabaseType.PgSql, "Host=localhost;Database=test;");
        using var provider = services.BuildServiceProvider();

        // Act
        using var query = provider.GetRequiredService<ISqlQueryFactory>().Create("pgsql");
        using var executor = provider.GetRequiredService<ISqlExecutorFactory>().Create("pgsql");
        using var multipleQueryExecutor = provider.GetRequiredService<ISqlMultipleQueryExecutorFactory>()
            .Create("pgsql");
        using var connection = provider.GetRequiredService<ISqlDbConnectionFactoryResolver>()
            .Create(PostgreSqlSqlProvider.Instance.Key, "Host=localhost;Database=test;");

        // Assert
        Assert.IsType<PostgreSqlQuery>(query);
        Assert.IsType<PostgreSqlExecutor>(executor);
        Assert.IsType<PostgreSqlMultipleQueryExecutor>(multipleQueryExecutor);
        Assert.IsType<PostgreSqlDialect>(provider.GetRequiredService<ISqlProviderResolver>()
            .Resolve(PostgreSqlSqlProvider.Instance.Key).Dialect);
        Assert.IsType<NpgsqlConnection>(connection);
        Assert.IsType<PostgreSqlBatchUpdateRenderer>(provider.GetRequiredService<ISqlBatchUpdateRenderer>());
        Assert.Equal("Host=localhost;Database=test;", connection.ConnectionString);
        Assert.True(PostgreSqlSqlProvider.Instance.Profile.Execution.SupportsMultipleResultSets);
        Assert.True(PostgreSqlSqlProvider.Instance.Profile.Mutation.SupportsUpdateFrom);
        Assert.Equal(SqlQueryCapabilityState.Supported, PostgreSqlSqlProvider.Instance.Profile.Query.Cte);
    }

    /// <summary>
    /// 测试目的：PostgreSQL 原生 Function/Procedure 结果集不应被声明为统一 OUT 参数能力。
    /// </summary>
    [Fact]
    public void Profile_WhenOutputParametersAreRequested_ShouldDeclareDatabaseUnsupported()
    {
        var procedure = PostgreSqlSqlProvider.Instance.Profile.Procedure;

        Assert.False(procedure.SupportsStoredProcedures);
        Assert.Equal(SqlCapabilityFailureReason.DatabaseUnsupported,
            procedure.StoredProceduresFailureReason);
        Assert.False(procedure.SupportsOutputParameters);
        Assert.Equal(SqlCapabilityFailureReason.DatabaseUnsupported,
            procedure.OutputParametersFailureReason);
    }

    [Table("scope_probe")]
    private sealed class PostgreSqlScopeProbeEntity : ISoftDelete
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
