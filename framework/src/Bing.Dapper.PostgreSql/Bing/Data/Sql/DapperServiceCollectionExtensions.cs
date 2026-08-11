using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Dapper;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Dapper.PostgreSql;

/// <summary>
/// PostgreSql 服务集合扩展
/// </summary>
public static class PostgreSqlServiceCollectionExtensions
{
    /// <summary>
    /// 注册 PostgreSQL Provider 能力，不配置默认数据源。
    /// 多 Provider 容器应通过具名数据源完成运行时路由。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddPostgreSqlProvider(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.AddSqlCore();
        services.AddSqlBuilderProvider(PostgreSqlSqlProvider.Instance, CreateBuilder);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlBatchUpdateRenderer, PostgreSqlBatchUpdateRenderer>());
        var queryOptions = new SqlOptions<PostgreSqlQuery> { DatabaseType = DatabaseType.PgSql };
        var executorOptions = new SqlOptions<PostgreSqlExecutor> { DatabaseType = DatabaseType.PgSql };
        var multipleQueryOptions = new SqlOptions<PostgreSqlMultipleQueryExecutor> { DatabaseType = DatabaseType.PgSql };
        queryOptions.RegisterStringTypeHandler();
        executorOptions.RegisterStringTypeHandler();
        multipleQueryOptions.RegisterStringTypeHandler();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, PostgreSqlDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(PostgreSqlSqlProvider.Instance.Key, CreateConnection);
        services.AddDatabaseTypeConverter<PostgreSqlTypeConverter>(DatabaseType.PgSql);
        services.AddSqlProviderRuntime(typeof(ISqlQuery), typeof(PostgreSqlQuery), PostgreSqlSqlProvider.Instance.Key);
        services.AddSqlProviderRuntime(typeof(ISqlExecutor), typeof(PostgreSqlExecutor), PostgreSqlSqlProvider.Instance.Key);
        services.AddSqlProviderRuntime(typeof(ISqlMultipleQueryExecutor), typeof(PostgreSqlMultipleQueryExecutor),
            PostgreSqlSqlProvider.Instance.Key);
        services.TryAddTransient<ISqlQuery, PostgreSqlQuery>();
        services.TryAddTransient<ISqlExecutor, PostgreSqlExecutor>();
        services.TryAddTransient<ISqlMultipleQueryExecutor, PostgreSqlMultipleQueryExecutor>();
        services.TryAddSingleton(queryOptions);
        services.TryAddSingleton(executorOptions);
        services.TryAddSingleton(multipleQueryOptions);
        return services;
    }

    /// <summary>
    /// 根据数据源连接字符串创建 PostgreSQL 独立连接。
    /// </summary>
    /// <param name="connectionString">PostgreSQL 数据源连接字符串。</param>
    /// <returns>尚未打开的 PostgreSQL 数据库连接。</returns>
    private static NpgsqlConnection CreateConnection(string connectionString) => new(connectionString);

    /// <summary>
    /// 使用查询级共享服务创建 PostgreSQL SQL Builder。
    /// </summary>
    /// <param name="services">当前查询的共享服务。</param>
    /// <returns>绑定该共享服务的 PostgreSQL SQL Builder。</returns>
    private static ISqlBuilder CreateBuilder(SqlBuilderServices services) => new PostgreSqlBuilder(services);
}
