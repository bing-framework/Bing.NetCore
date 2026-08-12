using System;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Dapper;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Dapper.Oracle;

/// <summary>
/// Oracle 服务集合扩展
/// </summary>
public static class OracleServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Oracle Provider 能力，不配置默认数据源。
    /// 多 Provider 容器应先调用此方法，再通过具名 <c>AddSqlDataSource</c> 配置实际数据源。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddOracleProvider(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.AddSqlCore();
        services.AddSqlBuilderProvider(OracleSqlProvider.Instance, CreateBuilder);
        var queryOptions = new SqlOptions<OracleSqlQuery> { DatabaseType = DatabaseType.Oracle };
        var executorOptions = new SqlOptions<OracleSqlExecutor> { DatabaseType = DatabaseType.Oracle };
        queryOptions.RegisterStringTypeHandler();
        queryOptions.RegisterGuidTypeHandler();
        executorOptions.RegisterStringTypeHandler();
        executorOptions.RegisterGuidTypeHandler();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, OracleDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(OracleSqlProvider.Instance.Key, CreateConnection);
        services.AddDatabaseTypeConverter<OracleTypeConverter>(DatabaseType.Oracle);
        services.AddSqlProviderRuntime(new SqlProviderRuntime(OracleSqlProvider.Instance.Key, typeof(OracleSqlQuery),
            typeof(OracleSqlExecutor)));
        services.TryAddTransient<ISqlQuery, OracleSqlQuery>();
        services.TryAddTransient<ISqlExecutor, OracleSqlExecutor>();
        services.TryAddSingleton(queryOptions);
        services.TryAddSingleton(executorOptions);
        return services;
    }

    /// <summary>
    /// 根据数据源连接字符串创建 Oracle 独立连接。
    /// </summary>
    /// <param name="connectionString">Oracle 数据源连接字符串。</param>
    /// <returns>尚未打开的 Oracle 数据库连接。</returns>
    private static OracleConnection CreateConnection(string connectionString) => new(connectionString);

    /// <summary>
    /// 使用查询级共享服务创建 Oracle SQL Builder。
    /// </summary>
    /// <param name="services">当前查询的共享服务。</param>
    /// <returns>绑定该共享服务的 Oracle SQL Builder。</returns>
    private static ISqlBuilder CreateBuilder(SqlBuilderServices services) => new OracleBuilder(services);
}
