using System;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
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
        services.AddSqlBuilderProvider(OracleSqlProvider.Instance, services => new OracleBuilder(services));
        var queryOptions = new SqlOptions<OracleSqlQuery> { DatabaseType = DatabaseType.Oracle };
        var executorOptions = new SqlOptions<OracleSqlExecutor> { DatabaseType = DatabaseType.Oracle };
        queryOptions.RegisterStringTypeHandler();
        queryOptions.RegisterGuidTypeHandler();
        executorOptions.RegisterStringTypeHandler();
        executorOptions.RegisterGuidTypeHandler();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, OracleDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(OracleSqlProvider.Instance.Key, connection => new OracleConnection(connection));
        services.AddDatabaseTypeConverter<OracleTypeConverter>(DatabaseType.Oracle);
        services.AddSqlImplementationType<ISqlQuery, OracleSqlQuery>(DatabaseType.Oracle);
        services.AddSqlImplementationType<ISqlExecutor, OracleSqlExecutor>(DatabaseType.Oracle);
        services.TryAddTransient<ISqlQuery, OracleSqlQuery>();
        services.TryAddTransient<ISqlExecutor, OracleSqlExecutor>();
        services.TryAddSingleton(queryOptions);
        services.TryAddSingleton(executorOptions);
        return services;
    }

    #region AddOracleSqlQuery(注册Oracle Sql查询对象)

    /// <summary>
    /// 注册Oracle Sql查询对象
    /// </summary>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddOracleSqlQuery(this IServiceCollection services)
    {
        services.AddOracleSqlQuery("");
        return services;
    }

    /// <summary>
    /// 注册Oracle Sql查询对象
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddOracleSqlQuery(this IServiceCollection services, string connection)
    {
        services.AddOracleSqlQuery<ISqlQuery, OracleSqlQuery>(connection);
        return services;
    }

    /// <summary>
    /// 注册Oracle Sql查询对象
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddOracleSqlQuery(this IServiceCollection services, Action<SqlOptions> setupAction)
    {
        services.AddOracleSqlQuery<ISqlQuery, OracleSqlQuery>(setupAction);
        return services;
    }

    /// <summary>
    /// 注册Oracle Sql查询对象
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddOracleSqlQuery<TInterface, TImplementation>(this IServiceCollection services, string connection)
        where TInterface : ISqlQuery
        where TImplementation : OracleSqlQueryBase, TInterface
    {
        services.AddOracleSqlQuery<TInterface, TImplementation>(t => t.ConnectionString(connection));
        return services;
    }

    /// <summary>
    /// 注册Oracle Sql查询对象
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddOracleSqlQuery<TInterface, TImplementation>(this IServiceCollection services, Action<SqlOptions> setupAction)
        where TInterface : ISqlQuery
        where TImplementation : OracleSqlQueryBase, TInterface
    {
        var sqlOptions = new SqlOptions<TImplementation> { DatabaseType = DatabaseType.Oracle };
        services.AddSqlBuilderProvider(OracleSqlProvider.Instance, services => new OracleBuilder(services));
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        sqlOptions.RegisterGuidTypeHandler();
        services.AddSqlDataSource(null, DatabaseType.Oracle, sqlOptions.ConnectionString);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, OracleDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(OracleSqlProvider.Instance.Key, connection => new OracleConnection(connection));
        services.AddDatabaseTypeConverter<OracleTypeConverter>(DatabaseType.Oracle);
        services.AddSqlImplementationType<TInterface, TImplementation>(DatabaseType.Oracle);
        services.TryAddTransient(typeof(TInterface), typeof(TImplementation));
        services.TryAddSingleton(typeof(SqlOptions<TImplementation>), _ => sqlOptions);
        return services;
    }

    #endregion

    #region AddOracleSqlExecutor(注册Oracle Sql执行器)

    /// <summary>
    /// 注册Oracle Sql执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddOracleSqlExecutor(this IServiceCollection services)
    {
        services.AddOracleSqlExecutor("");
        return services;
    }

    /// <summary>
    /// 注册Oracle Sql执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddOracleSqlExecutor(this IServiceCollection services, string connection)
    {
        services.AddOracleSqlExecutor<ISqlExecutor, OracleSqlExecutor>(connection);
        return services;
    }

    /// <summary>
    /// 注册Oracle Sql执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddOracleSqlExecutor(this IServiceCollection services, Action<SqlOptions> setupAction)
    {
        services.AddOracleSqlExecutor<ISqlExecutor, OracleSqlExecutor>(setupAction);
        return services;
    }

    /// <summary>
    /// 注册Oracle Sql执行器
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddOracleSqlExecutor<TInterface, TImplementation>(this IServiceCollection services, string connection)
        where TInterface : ISqlExecutor
        where TImplementation : OracleSqlExecutorBase, TInterface
    {
        services.AddOracleSqlExecutor<TInterface, TImplementation>(t => t.ConnectionString(connection));
        return services;
    }

    /// <summary>
    /// 注册Oracle Sql执行器
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddOracleSqlExecutor<TInterface, TImplementation>(this IServiceCollection services, Action<SqlOptions> setupAction)
        where TInterface : ISqlExecutor
        where TImplementation : OracleSqlExecutorBase, TInterface
    {
        var sqlOptions = new SqlOptions<TImplementation> { DatabaseType = DatabaseType.Oracle };
        services.AddSqlBuilderProvider(OracleSqlProvider.Instance, services => new OracleBuilder(services));
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        services.AddSqlDataSource(null, DatabaseType.Oracle, sqlOptions.ConnectionString);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, OracleDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(OracleSqlProvider.Instance.Key, connection => new OracleConnection(connection));
        services.AddDatabaseTypeConverter<OracleTypeConverter>(DatabaseType.Oracle);
        services.AddSqlImplementationType<TInterface, TImplementation>(DatabaseType.Oracle);
        services.TryAddTransient(typeof(TInterface), typeof(TImplementation));
        services.TryAddSingleton(typeof(SqlOptions<TImplementation>), _ => sqlOptions);
        return services;
    }

    #endregion
}
