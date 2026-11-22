using System.Reflection;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Configs;
using Bing.Dapper.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Dapper.Tests;

/// <summary>
/// SQLite 静态查询工厂测试。
/// </summary>
public class SqliteStaticQueryFactoryTest
{
    /// <summary>
    /// 测试目的：SQLite Provider 注册静态工厂后，应创建正确的查询对象并绑定具名数据源上下文。
    /// </summary>
    [Fact]
    public void Create_WhenSqliteStaticFactoryRegistered_ShouldCreateSqliteQuery()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqliteProvider();
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();

        // Act
        using var query = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var options = GetOptions(query);

        // Assert
        Assert.IsType<SqliteSqlQuery>(query);
        Assert.Equal(DatabaseType.Sqlite, options.DatabaseType);
        Assert.Equal("Data Source=:memory:", options.ConnectionString);
        Assert.Equal("sqlite", options.GetDatabaseContext().DbKey);
        Assert.True(options.AutoGenerateTableAlias);
    }

    /// <summary>
    /// 测试目的：不同数据源和映射配置应生成各自 SQL，并从各自 SQLite 文件读取结果。
    /// </summary>
    [Fact]
    public void Create_WhenDbKeyAndMappingProfileDiffer_ShouldIsolateSqlAndConnections()
    {
        // Arrange
        var firstPath = CreateDatabasePath();
        var secondPath = CreateDatabasePath();
        try
        {
            CreateRows(firstPath, "first_table", "first-db:first-table");
            CreateRows(firstPath, "second_table", "first-db:second-table");
            CreateRows(secondPath, "first_table", "second-db:first-table");
            CreateRows(secondPath, "second_table", "second-db:second-table");

            var services = new ServiceCollection();
            services.ConfigureSqlMetadata(options =>
            {
                options.EntityMappings.Add(new EntityMappingOptions
                {
                    EntityType = typeof(StaticFactoryRow),
                    DbKey = "first",
                    MappingProfile = "first-profile",
                    TableName = "first_table"
                });
                options.EntityMappings.Add(new EntityMappingOptions
                {
                    EntityType = typeof(StaticFactoryRow),
                    DbKey = "second",
                    MappingProfile = "second-profile",
                    TableName = "second_table"
                });
            });
            services.AddSqliteProvider();
            services.AddSqlDataSource("first", DatabaseType.Sqlite, CreateConnectionString(firstPath),
                setupAction: source => source.MappingProfile = "first-profile");
            services.AddSqlDataSource("second", DatabaseType.Sqlite, CreateConnectionString(secondPath),
                setupAction: source => source.MappingProfile = "second-profile");
            using var provider = services.BuildServiceProvider();
            var queryFactory = provider.GetRequiredService<ISqlQueryFactory>();

            // Act
            using var firstQuery = queryFactory.Create("first");
            using var secondQuery = queryFactory.Create("second");
            var first = firstQuery.From<StaticFactoryRow>()
                .Select<StaticFactoryRow>(row => new object[] { row.Id, row.Value });
            var second = secondQuery.From<StaticFactoryRow>()
                .Select<StaticFactoryRow>(row => new object[] { row.Id, row.Value });
            var firstSql = first.ToSql();
            var secondSql = second.ToSql();
            var firstRows = first.ToList<StaticFactoryRow>();
            var secondRows = second.ToList<StaticFactoryRow>();

            // Assert
            Assert.Equal("Select `row`.`Id`,`row`.`Value` \r\nFrom `first_table` As `row`", firstSql);
            Assert.Equal("Select `row`.`Id`,`row`.`Value` \r\nFrom `second_table` As `row`", secondSql);
            Assert.Equal("first-db:first-table", Assert.Single(firstRows).Value);
            Assert.Equal("second-db:second-table", Assert.Single(secondRows).Value);
        }
        finally
        {
            DeleteDatabase(firstPath);
            DeleteDatabase(secondPath);
        }
    }

    /// <summary>
    /// 测试目的：静态工厂应复制模板选项，并为每个查询深克隆查询能力配置。
    /// </summary>
    [Fact]
    public void Create_WhenTemplateOptionsConfigured_ShouldCopyValuesAndCloneCapabilities()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        var capabilities = new SqlQueryCapabilities
        {
            Cte = SqlQueryCapabilityState.Supported,
            Union = SqlQueryCapabilityState.Unsupported,
            UnionAll = SqlQueryCapabilityState.Supported,
            Intersect = SqlQueryCapabilityState.Unsupported,
            Except = SqlQueryCapabilityState.Supported,
            RightJoin = SqlQueryCapabilityState.Unsupported,
            FullJoin = SqlQueryCapabilityState.Supported,
            Pagination = SqlQueryCapabilityState.Unsupported
        };
        var template = new SqlOptions<SqliteSqlQuery>
        {
            DatabaseType = DatabaseType.Sqlite,
            IncludeTenantIdInDiagnostics = true,
            AutoGenerateTableAlias = false,
            ConnectionString = "template-connection-string",
            Connection = connection,
            QueryCapabilities = capabilities
        };
        var services = new ServiceCollection();
        services.AddSingleton(template);
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", DatabaseType.Sqlite);
        using var provider = services.BuildServiceProvider();
        var queryFactory = provider.GetRequiredService<ISqlQueryFactory>();

        // Act
        using var query = queryFactory.Create("sqlite");
        var queryOptions = GetOptions(query);
        var queryBuilder = query.From<StaticFactoryRow>()
            .Select<StaticFactoryRow>(row => new object[] { row.Id, row.Value });
        const string disabledAliasSql = "Select `StaticFactoryRow`.`Id`,`StaticFactoryRow`.`Value` \r\nFrom `StaticFactoryRow`";
        Assert.Equal(disabledAliasSql, queryBuilder.ToSql());

        template.AutoGenerateTableAlias = true;
        capabilities.Cte = SqlQueryCapabilityState.Unsupported;
        using var nextQuery = queryFactory.Create("sqlite");
        var nextOptions = GetOptions(nextQuery);
        var nextQueryBuilder = nextQuery.From<StaticFactoryRow>()
            .Select<StaticFactoryRow>(row => new object[] { row.Id, row.Value });
        const string enabledAliasSql = "Select `row`.`Id`,`row`.`Value` \r\nFrom `StaticFactoryRow` As `row`";

        template.AutoGenerateTableAlias = false;

        // Assert
        Assert.NotSame(template, queryOptions);
        Assert.Equal(DatabaseType.Sqlite, queryOptions.DatabaseType);
        Assert.True(queryOptions.IncludeTenantIdInDiagnostics);
        Assert.False(queryOptions.AutoGenerateTableAlias);
        Assert.Equal("template-connection-string", queryOptions.ConnectionString);
        Assert.Same(connection, queryOptions.Connection);
        Assert.NotSame(capabilities, queryOptions.QueryCapabilities);
        Assert.Equal(SqlQueryCapabilityState.Supported, queryOptions.QueryCapabilities.Cte);
        Assert.Equal(SqlQueryCapabilityState.Unsupported, queryOptions.QueryCapabilities.Union);
        Assert.Equal(SqlQueryCapabilityState.Supported, queryOptions.QueryCapabilities.UnionAll);
        Assert.Equal(SqlQueryCapabilityState.Unsupported, queryOptions.QueryCapabilities.Intersect);
        Assert.Equal(SqlQueryCapabilityState.Supported, queryOptions.QueryCapabilities.Except);
        Assert.Equal(SqlQueryCapabilityState.Unsupported, queryOptions.QueryCapabilities.RightJoin);
        Assert.Equal(SqlQueryCapabilityState.Supported, queryOptions.QueryCapabilities.FullJoin);
        Assert.Equal(SqlQueryCapabilityState.Unsupported, queryOptions.QueryCapabilities.Pagination);
        Assert.Equal(SqlQueryCapabilityState.Unsupported, nextOptions.QueryCapabilities.Cte);
        Assert.NotSame(queryOptions, nextOptions);
        Assert.NotSame(queryOptions.QueryCapabilities, nextOptions.QueryCapabilities);
        Assert.True(nextOptions.AutoGenerateTableAlias);
        Assert.Equal(enabledAliasSql, nextQueryBuilder.ToSql());
        Assert.Equal(disabledAliasSql, queryBuilder.ToSql());

        using var finalQuery = queryFactory.Create("sqlite");
        var finalOptions = GetOptions(finalQuery);
        var finalSql = finalQuery.From<StaticFactoryRow>()
            .Select<StaticFactoryRow>(row => new object[] { row.Id, row.Value })
            .ToSql();
        Assert.False(finalOptions.AutoGenerateTableAlias);
        Assert.Equal(disabledAliasSql, finalSql);
        Assert.Equal(enabledAliasSql, nextQueryBuilder.ToSql());
    }

    /// <summary>
    /// 测试目的：没有静态委托的 Provider 运行时仍应使用原有实例化路径。
    /// </summary>
    [Fact]
    public void Create_WhenRuntimeHasNoStaticFactory_ShouldUseActivatorUtilitiesPath()
    {
        // Arrange
        var services = new ServiceCollection();
        var template = new SqlOptions<SqliteSqlQuery> { AutoGenerateTableAlias = false };
        services.AddSingleton(template);
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", DatabaseType.Sqlite, "Data Source=:memory:");
        services.RemoveAll<SqlProviderRuntime>();
        services.AddSqlProviderRuntime(new SqlProviderRuntime(SqliteSqlProvider.Instance.Key,
            typeof(SqliteSqlQuery), typeof(SqliteSqlExecutor), typeof(SqliteSqlMultipleQueryExecutor)));
        using var provider = services.BuildServiceProvider();

        // Act
        using var query = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var options = GetOptions(query);
        var queryBuilder = query.From<StaticFactoryRow>()
            .Select<StaticFactoryRow>(row => new object[] { row.Id, row.Value });
        const string disabledAliasSql = "Select `StaticFactoryRow`.`Id`,`StaticFactoryRow`.`Value` \r\nFrom `StaticFactoryRow`";
        Assert.Equal(disabledAliasSql, queryBuilder.ToSql());

        template.AutoGenerateTableAlias = true;
        using var nextQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var nextOptions = GetOptions(nextQuery);
        var nextQueryBuilder = nextQuery.From<StaticFactoryRow>()
            .Select<StaticFactoryRow>(row => new object[] { row.Id, row.Value });
        const string enabledAliasSql = "Select `row`.`Id`,`row`.`Value` \r\nFrom `StaticFactoryRow` As `row`";
        template.AutoGenerateTableAlias = false;

        // Assert
        Assert.IsType<SqliteSqlQuery>(query);
        Assert.Equal(DatabaseType.Sqlite, options.DatabaseType);
        Assert.Equal("Data Source=:memory:", options.ConnectionString);
        Assert.Equal("sqlite", options.GetDatabaseContext().DbKey);
        Assert.NotSame(template, options);
        Assert.False(options.AutoGenerateTableAlias);
        Assert.NotSame(options, nextOptions);
        Assert.True(nextOptions.AutoGenerateTableAlias);
        Assert.Equal(disabledAliasSql, queryBuilder.ToSql());
        Assert.Equal(enabledAliasSql, nextQueryBuilder.ToSql());

        using var finalQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var finalOptions = GetOptions(finalQuery);
        var finalSql = finalQuery.From<StaticFactoryRow>()
            .Select<StaticFactoryRow>(row => new object[] { row.Id, row.Value })
            .ToSql();
        Assert.False(finalOptions.AutoGenerateTableAlias);
        Assert.Equal(disabledAliasSql, finalSql);
        Assert.Equal(enabledAliasSql, nextQueryBuilder.ToSql());
    }

    /// <summary>
    /// 测试目的：相同静态工厂应允许重复注册，不一致的工厂模式或委托应被拒绝。
    /// </summary>
    [Fact]
    public void AddSqlProviderRuntime_WhenStaticFactoryIsRepeatedOrConflicting_ShouldValidateConfiguration()
    {
        // Arrange
        const string providerKey = "test.sqlite.static";
        var services = new ServiceCollection();
        services.AddSqlProviderRuntime(CreateStaticRuntime(providerKey));

        // Act
        services.AddSqlProviderRuntime(CreateStaticRuntime(providerKey));

        // Assert
        Assert.Throws<InvalidOperationException>(() => services.AddSqlProviderRuntime(
            new SqlProviderRuntime(providerKey, typeof(SqliteSqlQuery), typeof(SqliteSqlExecutor),
                typeof(SqliteSqlMultipleQueryExecutor))));
        Assert.Throws<InvalidOperationException>(() => services.AddSqlProviderRuntime(
            SqlProviderRuntime.CreateWithQueryFactory(providerKey, typeof(SqliteSqlQuery),
                typeof(SqliteSqlExecutor), typeof(SqliteSqlMultipleQueryExecutor), CreateAlternateRuntimeQuery,
                CreateRuntimeOptions, GetRuntimeTemplate)));
    }

    /// <summary>
    /// 测试目的：静态查询工厂创建入口应拒绝缺失的委托参数。
    /// </summary>
    [Fact]
    public void SqlProviderRuntime_WhenStaticFactoryArgumentIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        Func<SqlOptions> optionsFactory = () => new SqlOptions<SqliteSqlQuery>();
        Func<IServiceProvider, SqlOptions> templateFactory = _ => null;
        Func<IServiceProvider, SqlOptions, ISqlQuery> queryFactory = (_, _) => null;

        // Act and Assert
        var queryFactoryException = Assert.Throws<ArgumentNullException>(() => SqlProviderRuntime.CreateWithQueryFactory(
            SqliteSqlProvider.Instance.Key, typeof(SqliteSqlQuery), typeof(SqliteSqlExecutor),
            typeof(SqliteSqlMultipleQueryExecutor), null, optionsFactory, templateFactory));
        var optionsFactoryException = Assert.Throws<ArgumentNullException>(() => SqlProviderRuntime.CreateWithQueryFactory(
            SqliteSqlProvider.Instance.Key, typeof(SqliteSqlQuery), typeof(SqliteSqlExecutor),
            typeof(SqliteSqlMultipleQueryExecutor), queryFactory, null, templateFactory));
        var templateFactoryException = Assert.Throws<ArgumentNullException>(() => SqlProviderRuntime.CreateWithQueryFactory(
            SqliteSqlProvider.Instance.Key, typeof(SqliteSqlQuery), typeof(SqliteSqlExecutor),
            typeof(SqliteSqlMultipleQueryExecutor), queryFactory, optionsFactory, null));

        Assert.Equal("queryFactory", queryFactoryException.ParamName);
        Assert.Equal("optionsFactory", optionsFactoryException.ParamName);
        Assert.Equal("templateFactory", templateFactoryException.ParamName);
    }

    /// <summary>
    /// 创建使用静态查询工厂的 SQLite 提供程序运行时。
    /// </summary>
    /// <param name="providerKey">SQL 提供程序注册键。</param>
    /// <returns>使用静态查询工厂的 SQLite 运行时描述。</returns>
    private static SqlProviderRuntime CreateStaticRuntime(string providerKey) =>
        SqlProviderRuntime.CreateWithQueryFactory(providerKey, typeof(SqliteSqlQuery), typeof(SqliteSqlExecutor),
            typeof(SqliteSqlMultipleQueryExecutor), CreateRuntimeQuery, CreateRuntimeOptions, GetRuntimeTemplate);

    /// <summary>
    /// 创建 SQLite 运行时查询实例。
    /// </summary>
    /// <param name="provider">解析测试服务的容器。</param>
    /// <param name="options">需要配置或使用的测试选项。</param>
    /// <returns>使用指定容器及选项创建的 SQLite 查询。</returns>
    private static ISqlQuery CreateRuntimeQuery(IServiceProvider provider, SqlOptions options) =>
        new SqliteSqlQuery(provider, (SqlOptions<SqliteSqlQuery>)options);

    /// <summary>
    /// 创建 SQLite 运行时查询实例。
    /// </summary>
    /// <param name="provider">解析测试服务的容器。</param>
    /// <param name="options">需要配置或使用的测试选项。</param>
    /// <returns>使用指定容器及选项创建的 SQLite 查询。</returns>
    private static ISqlQuery CreateAlternateRuntimeQuery(IServiceProvider provider, SqlOptions options) =>
        new SqliteSqlQuery(provider, (SqlOptions<SqliteSqlQuery>)options);

    /// <summary>
    /// 创建 SQLite 查询选项模板。
    /// </summary>
    /// <returns>测试查询所使用的 SQL 选项。</returns>
    private static SqlOptions CreateRuntimeOptions() => new SqlOptions<SqliteSqlQuery>();

    /// <summary>
    /// 解析 SQLite 查询选项模板。
    /// </summary>
    /// <param name="provider">解析测试服务的容器。</param>
    /// <returns>测试查询所使用的 SQL 选项。</returns>
    private static SqlOptions GetRuntimeTemplate(IServiceProvider provider) =>
        provider.GetService<SqlOptions<SqliteSqlQuery>>();

    /// <summary>
    /// 读取查询实例持有的 SQL 选项。
    /// </summary>
    /// <param name="query">待执行或构造的测试查询。</param>
    /// <returns>测试查询所使用的 SQL 选项。</returns>
    private static SqlOptions GetOptions(ISqlQuery query)
    {
        var optionsProperty = typeof(SqlQueryBase).GetProperty("Options", BindingFlags.Instance | BindingFlags.NonPublic);
        return Assert.IsAssignableFrom<SqlOptions>(optionsProperty?.GetValue(query));
    }

    /// <summary>
    /// 创建唯一的 SQLite 测试数据库路径。
    /// </summary>
    /// <returns>唯一的 SQLite 临时数据库文件路径。</returns>
    private static string CreateDatabasePath() => Path.Combine(Path.GetTempPath(), $"bing-sqlite-static-{Guid.NewGuid():N}.db");

    /// <summary>
    /// 创建禁用连接池的 SQLite 连接字符串。
    /// </summary>
    /// <param name="path">测试数据库文件路径。</param>
    /// <returns>禁用连接池的 SQLite 连接字符串。</returns>
    private static string CreateConnectionString(string path) => $"Data Source={path};Pooling=False";

    /// <summary>
    /// 创建 SQLite 测试表并写入测试值。
    /// </summary>
    /// <param name="path">测试数据库文件路径。</param>
    /// <param name="tableName">目标物理表名。</param>
    /// <param name="value">测试对象、记录或输入文本的值。</param>
    private static void CreateRows(string path, string tableName, string value)
    {
        using var connection = new SqliteConnection(CreateConnectionString(path));
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"Create Table {tableName}(Id Integer Primary Key, Value Text); Insert Into {tableName}(Value) Values (@value);";
        command.Parameters.AddWithValue("@value", value);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 清理连接池并删除 SQLite 测试数据库。
    /// </summary>
    /// <param name="path">测试数据库文件路径。</param>
    private static void DeleteDatabase(string path)
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>
    /// 表示静态查询工厂返回的测试行。
    /// </summary>
    private sealed class StaticFactoryRow
    {
        /// <summary>
        /// 获取或设置静态查询返回行的标识。
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 获取或设置静态查询返回的测试值。
        /// </summary>
        public string Value { get; set; }
    }
}
