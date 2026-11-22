using Bing.Dapper;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MySqlConnector;

namespace Bing.Dapper.MySql;

/// <summary>
/// MySql 服务集合扩展
/// </summary>
public static class MySqlServiceCollectionExtensions
{
    /// <summary>
    /// 注册 MySQL Provider 能力，不配置默认数据源。
    /// </summary>
    /// <remarks>
    /// 多 Provider 容器应先调用此方法，再通过具名 <c>AddSqlDataSource</c> 配置实际数据源。
    /// </remarks>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddMySqlProvider(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.AddSqlCore();
        services.AddSqlBuilderProvider(MySqlSqlProvider.Instance, CreateBuilder);
        var queryOptions = new SqlOptions<MySqlQuery> { DatabaseType = DatabaseType.MySql };
        var executorOptions = new SqlOptions<MySqlExecutor> { DatabaseType = DatabaseType.MySql };
        var multipleQueryOptions = new SqlOptions<MySqlMultipleQueryExecutor> { DatabaseType = DatabaseType.MySql };
        queryOptions.RegisterStringTypeHandler();
        executorOptions.RegisterStringTypeHandler();
        multipleQueryOptions.RegisterStringTypeHandler();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, MySqlDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(MySqlSqlProvider.Instance.Key, CreateConnection);
        // 静态创建保留构造函数，同时保持调用方预注册实例的优先级。
        services.TryAddSingleton(new MySqlTypeConverter());
        services.AddSingleton<DatabaseTypeConverterRegistration>(provider => new DatabaseTypeConverterRegistration
        {
            DatabaseType = DatabaseType.MySql,
            Converter = provider.GetRequiredService<MySqlTypeConverter>()
        });
        services.AddSingleton<DatabaseTypeConverterRegistration>(provider => new DatabaseTypeConverterRegistration
        {
            DatabaseType = DatabaseType.Doris,
            Converter = provider.GetRequiredService<MySqlTypeConverter>()
        });
        services.AddSqlProviderRuntime(SqlProviderRuntime.CreateWithFactories(MySqlSqlProvider.Instance.Key,
            typeof(MySqlQuery), typeof(MySqlExecutor), typeof(MySqlMultipleQueryExecutor),
            (provider, options) => new MySqlQuery(provider, (SqlOptions<MySqlQuery>)options),
            (provider, options) => new MySqlExecutor(provider, (SqlOptions<MySqlExecutor>)options),
            (provider, options) => new MySqlMultipleQueryExecutor(provider, (SqlOptions<MySqlMultipleQueryExecutor>)options),
            serviceType => serviceType == typeof(ISqlQuery) ? new SqlOptions<MySqlQuery>() :
                serviceType == typeof(ISqlExecutor) ? new SqlOptions<MySqlExecutor>() : new SqlOptions<MySqlMultipleQueryExecutor>(),
            (provider, serviceType) => serviceType == typeof(ISqlQuery) ? provider.GetService<SqlOptions<MySqlQuery>>() :
                serviceType == typeof(ISqlExecutor) ? provider.GetService<SqlOptions<MySqlExecutor>>() : provider.GetService<SqlOptions<MySqlMultipleQueryExecutor>>()));
        services.TryAddSingleton(queryOptions);
        services.TryAddSingleton(executorOptions);
        services.TryAddSingleton(multipleQueryOptions);
        return services;
    }

    /// <summary>
    /// 根据数据源连接字符串创建 MySQL 独立连接。
    /// </summary>
    /// <param name="connectionString">MySQL 数据源连接字符串。</param>
    /// <returns>尚未打开的 MySQL 数据库连接。</returns>
    private static MySqlConnection CreateConnection(string connectionString) => new(connectionString);

    /// <summary>
    /// 使用查询级共享服务创建 MySQL SQL Builder。
    /// </summary>
    /// <param name="services">当前查询的共享服务。</param>
    /// <returns>绑定该共享服务的 MySQL SQL Builder。</returns>
    private static ISqlBuilder CreateBuilder(SqlBuilderServices services) => new MySqlBuilder(services);
}
