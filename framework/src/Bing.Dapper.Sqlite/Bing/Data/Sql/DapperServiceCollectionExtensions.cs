using Bing.Dapper;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Dapper.Sqlite;

/// <summary>
/// Sqlite 服务集合扩展
/// </summary>
public static class SqliteServiceCollectionExtensions
{
    /// <summary>
    /// 注册 SQLite Provider 能力，不配置默认数据源。
    /// 多 Provider 容器应通过具名数据源完成运行时路由。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddSqliteProvider(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.AddSqlCore();
        services.AddSqlBuilderProvider(SqliteSqlProvider.Instance, CreateBuilder);
        var queryOptions = new SqlOptions<SqliteSqlQuery> { DatabaseType = DatabaseType.Sqlite };
        var executorOptions = new SqlOptions<SqliteSqlExecutor> { DatabaseType = DatabaseType.Sqlite };
        var multipleQueryOptions = new SqlOptions<SqliteSqlMultipleQueryExecutor> { DatabaseType = DatabaseType.Sqlite };
        queryOptions.RegisterStringTypeHandler();
        executorOptions.RegisterStringTypeHandler();
        multipleQueryOptions.RegisterStringTypeHandler();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, SqliteDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(SqliteSqlProvider.Instance.Key, CreateConnection);
        services.AddDatabaseTypeConverter<SqliteTypeConverter>(DatabaseType.Sqlite);
        services.AddSqlProviderRuntime(new SqlProviderRuntime(SqliteSqlProvider.Instance.Key,
            typeof(SqliteSqlQuery), typeof(SqliteSqlExecutor), typeof(SqliteSqlMultipleQueryExecutor)));
        services.TryAddSingleton(queryOptions);
        services.TryAddSingleton(executorOptions);
        services.TryAddSingleton(multipleQueryOptions);
        return services;
    }

    /// <summary>
    /// 根据数据源连接字符串创建 SQLite 独立连接。
    /// </summary>
    /// <param name="connectionString">SQLite 数据源连接字符串。</param>
    /// <returns>尚未打开的 SQLite 数据库连接。</returns>
    private static SqliteConnection CreateConnection(string connectionString) => new(connectionString);

    /// <summary>
    /// 使用查询级共享服务创建 SQLite SQL Builder。
    /// </summary>
    /// <param name="services">当前查询的共享服务。</param>
    /// <returns>绑定该共享服务的 SQLite SQL Builder。</returns>
    private static ISqlBuilder CreateBuilder(SqlBuilderServices services) => new SqliteBuilder(services);
}
