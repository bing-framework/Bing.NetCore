using System.Data;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// SQLite Provider 服务注册单元测试。
/// </summary>
public class SqliteProviderRegistrationTest
{
    /// <summary>
    /// 测试目的：注册 SQLite Provider 后，查询和执行器选项应使用 SQLite 数据库类型。
    /// </summary>
    [Fact]
    public void AddSqliteProvider_WhenRegistered_ShouldConfigureSqliteOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqliteProvider();
        using var provider = services.BuildServiceProvider();

        // Act
        var queryOptions = provider.GetRequiredService<SqlOptions<SqliteSqlQuery>>();
        var executorOptions = provider.GetRequiredService<SqlOptions<SqliteSqlExecutor>>();

        // Assert
        Assert.Equal(DatabaseType.Sqlite, queryOptions.DatabaseType);
        Assert.Equal(DatabaseType.Sqlite, executorOptions.DatabaseType);
    }

    /// <summary>
    /// 测试目的：仅注册 SQLite Provider 时应自动补齐核心服务，具名数据源可创建 SQLite 查询、执行器、方言和连接工厂实例。
    /// </summary>
    [Fact]
    public void Factories_WhenSqliteDataSourceConfigured_ShouldResolveSqliteServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();

        // Act
        using var query = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        using var executor = provider.GetRequiredService<ISqlExecutorFactory>().Create("sqlite");
        using var connection = provider.GetRequiredService<ISqlDbConnectionFactoryResolver>()
            .Create(SqliteSqlProvider.Instance.Key, "Data Source=:memory:");

        // Assert
        Assert.IsType<SqliteSqlQuery>(query);
        Assert.IsType<SqliteSqlExecutor>(executor);
        Assert.IsType<SqliteDialect>(provider.GetRequiredService<ISqlProviderResolver>()
            .Resolve(SqliteSqlProvider.Instance.Key).Dialect);
        Assert.IsType<SqliteConnection>(connection);
        Assert.Equal("Data Source=:memory:", connection.ConnectionString);
    }

    /// <summary>
    /// 测试目的：注册 SQLite Provider 后应解析对应的类型转换器。
    /// </summary>
    [Fact]
    public void TypeConverterResolver_WhenSqliteProviderRegistered_ShouldResolveSqliteConverter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddSqliteProvider();
        using var provider = services.BuildServiceProvider();

        // Act
        var converter = provider.GetRequiredService<ITypeConverterResolver>().Resolve(DatabaseType.Sqlite);

        // Assert
        Assert.IsType<SqliteTypeConverter>(converter);
    }

    /// <summary>
    /// 测试目的：SQLite Profile 必须如实声明不支持存储过程和输出参数。
    /// </summary>
    [Fact]
    public void SqliteProviderProfile_ShouldNotSupportProceduresOrOutputParameters()
    {
        // Arrange
        var profile = SqliteSqlProvider.Instance.Profile;

        // Assert
        Assert.False(profile.Procedure.SupportsStoredProcedures);
        Assert.False(profile.Procedure.SupportsOutputParameters);
    }

    /// <summary>
    /// 测试目的：SQLite 存储过程入口必须在打开连接前拒绝，避免将不支持的命令交给底层驱动。
    /// </summary>
    [Fact]
    public void ExecuteProcedure_WhenSqliteProviderConfigured_ShouldRejectBeforeOpeningConnection()
    {
        // Arrange
        var connection = new SqliteConnection("Data Source=:memory:");
        var services = new ServiceCollection();
        services.AddSqliteProvider();
        using var provider = services.BuildServiceProvider();
        var options = new SqlOptions<SqliteSqlExecutor> { DatabaseType = DatabaseType.Sqlite };
        options.Connection(connection);
        using var executor = new SqliteSqlExecutor(provider, options);

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => executor.ExecuteProcedure("unsupported_procedure"));

        // Assert
        Assert.Equal("Provider bing.sqlite 不支持存储过程命令。", exception.Message);
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    /// <summary>
    /// 测试目的：SQLite 参数定制器应识别带长度声明的 Provider 类型名称。
    /// </summary>
    [Fact]
    public void SqliteDbParameterCustomizer_WhenProviderTypeContainsLength_ShouldConfigureSqliteType()
    {
        // Arrange
        var customizer = new SqliteDbParameterCustomizer();
        var parameter = new SqliteParameter();
        var sqlParameter = new SqlParam("name", "bing") { ProviderTypeName = "text(128)" };

        // Act
        customizer.Configure(parameter, sqlParameter);

        // Assert
        Assert.Equal(SqliteType.Text, parameter.SqliteType);
        Assert.True(customizer.CanHandle(DatabaseType.Sqlite));
        Assert.False(customizer.CanHandle(DatabaseType.SqlServer));
    }

    /// <summary>
    /// 测试目的：未知 Provider 类型名称不应覆盖 SQLite 参数当前类型。
    /// </summary>
    [Fact]
    public void SqliteDbParameterCustomizer_WhenProviderTypeUnknown_ShouldKeepExistingSqliteType()
    {
        // Arrange
        var customizer = new SqliteDbParameterCustomizer();
        var parameter = new SqliteParameter { SqliteType = SqliteType.Integer };
        var sqlParameter = new SqlParam("id", 1) { ProviderTypeName = "unknown_type" };

        // Act
        customizer.Configure(parameter, sqlParameter);

        // Assert
        Assert.Equal(SqliteType.Integer, parameter.SqliteType);
    }
}