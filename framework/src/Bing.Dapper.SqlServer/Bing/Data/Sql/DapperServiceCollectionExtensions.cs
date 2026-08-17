using Bing.Dapper;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Dapper.SqlServer;

/// <summary>
/// SqlServer 服务集合扩展
/// </summary>
public static class SqlServerServiceCollectionExtensions
{
    /// <summary>
    /// 注册 SQL Server Provider 能力，不配置默认数据源。
    /// 多 Provider 容器应通过具名数据源完成运行时路由。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddSqlServerProvider(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.AddSqlCore();
        services.AddSqlBuilderProvider(SqlServerSqlProvider.Instance, CreateBuilder);
        var queryOptions = new SqlOptions<SqlServerSqlQuery>();
        var executorOptions = new SqlOptions<SqlServerSqlExecutor>();
        var multipleQueryOptions = new SqlOptions<SqlServerSqlMultipleQueryExecutor>();
        queryOptions.RegisterStringTypeHandler();
        executorOptions.RegisterStringTypeHandler();
        multipleQueryOptions.RegisterStringTypeHandler();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, SqlServerDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(SqlServerSqlProvider.Instance.Key, CreateConnection);
        services.AddDatabaseTypeConverter<SqlServerTypeConverter>(DatabaseType.SqlServer);
        services.AddSqlProviderRuntime(new SqlProviderRuntime(SqlServerSqlProvider.Instance.Key,
            typeof(SqlServerSqlQuery), typeof(SqlServerSqlExecutor), typeof(SqlServerSqlMultipleQueryExecutor)));
        services.TryAddSingleton(queryOptions);
        services.TryAddSingleton(executorOptions);
        services.TryAddSingleton(multipleQueryOptions);
        return services;
    }

    /// <summary>
    /// 根据数据源连接字符串创建 SQL Server 独立连接。
    /// </summary>
    /// <param name="connectionString">SQL Server 数据源连接字符串。</param>
    /// <returns>尚未打开的 SQL Server 数据库连接。</returns>
    private static SqlConnection CreateConnection(string connectionString) => new(connectionString);

    /// <summary>
    /// 使用查询级共享服务创建 SQL Server SQL Builder。
    /// </summary>
    /// <param name="services">当前查询的共享服务。</param>
    /// <returns>绑定该共享服务的 SQL Server SQL Builder。</returns>
    private static ISqlBuilder CreateBuilder(SqlBuilderServices services) => new SqlServerBuilder(services);
}
