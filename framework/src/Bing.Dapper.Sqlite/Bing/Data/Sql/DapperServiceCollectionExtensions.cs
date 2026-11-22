using Bing.Dapper;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;
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
    /// </summary>
    /// <remarks>
    /// 多 Provider 容器应通过具名数据源完成运行时路由。
    /// </remarks>
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
        // 静态注册避免 Native AOT 裁剪通过开放泛型注册的构造函数。
        services.TryAddSingleton(new SqliteTypeConverter());
        services.AddSingleton<DatabaseTypeConverterRegistration>(provider => new DatabaseTypeConverterRegistration
        {
            DatabaseType = DatabaseType.Sqlite,
            Converter = provider.GetRequiredService<SqliteTypeConverter>()
        });
        services.AddSqlProviderRuntime(SqlProviderRuntime.CreateWithFactories(SqliteSqlProvider.Instance.Key,
            typeof(SqliteSqlQuery), typeof(SqliteSqlExecutor), typeof(SqliteSqlMultipleQueryExecutor),
            (provider, options) => new SqliteSqlQuery(provider, (SqlOptions<SqliteSqlQuery>)options),
            (provider, options) => new SqliteSqlExecutor(provider, (SqlOptions<SqliteSqlExecutor>)options),
            (provider, options) => new SqliteSqlMultipleQueryExecutor(provider, (SqlOptions<SqliteSqlMultipleQueryExecutor>)options),
            serviceType => serviceType == typeof(ISqlQuery) ? new SqlOptions<SqliteSqlQuery>() :
                serviceType == typeof(ISqlExecutor) ? new SqlOptions<SqliteSqlExecutor>() : new SqlOptions<SqliteSqlMultipleQueryExecutor>(),
            (provider, serviceType) => serviceType == typeof(ISqlQuery) ? provider.GetService<SqlOptions<SqliteSqlQuery>>() :
                serviceType == typeof(ISqlExecutor) ? provider.GetService<SqlOptions<SqliteSqlExecutor>>() : provider.GetService<SqlOptions<SqliteSqlMultipleQueryExecutor>>()));
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
