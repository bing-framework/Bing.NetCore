using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql;
using Bing.Dapper;
using MySqlConnector;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Dapper.MySql;

/// <summary>
/// MySql 服务集合扩展
/// </summary>
public static class MySqlServiceCollectionExtensions
{
    /// <summary>
    /// 注册 MySQL Provider 能力，不配置默认数据源。
    /// 多 Provider 容器应先调用此方法，再通过具名 <c>AddSqlDataSource</c> 配置实际数据源。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddMySqlProvider(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        var queryOptions = new SqlOptions<MySqlQuery>();
        var executorOptions = new SqlOptions<MySqlExecutor>();
        queryOptions.RegisterStringTypeHandler();
        executorOptions.RegisterStringTypeHandler();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, MySqlDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(DatabaseType.MySql, connection => new MySqlConnection(connection));
        services.AddDatabaseTypeConverter<MySqlTypeConverter>(DatabaseType.MySql);
        services.AddSqlImplementationType<ISqlQuery, MySqlQuery>(DatabaseType.MySql);
        services.AddSqlImplementationType<ISqlExecutor, MySqlExecutor>(DatabaseType.MySql);
        services.TryAddTransient<ISqlQuery, MySqlQuery>();
        services.TryAddTransient<ISqlExecutor, MySqlExecutor>();
        services.TryAddSingleton(queryOptions);
        services.TryAddSingleton(executorOptions);
        return services;
    }

    #region AddMySqlQuery(注册MySql Sql查询对象)

    /// <summary>
    /// 注册MySql Sql查询对象
    /// </summary>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddMySqlQuery(this IServiceCollection services)
    {
        services.AddMySqlQuery("");
        return services;
    }

    /// <summary>
    /// 注册MySql Sql查询对象
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddMySqlQuery(this IServiceCollection services, string connection)
    {
        services.AddMySqlQuery<ISqlQuery, MySqlQuery>(connection);
        return services;
    }

    /// <summary>
    /// 注册MySql Sql查询对象
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddMySqlQuery(this IServiceCollection services, Action<SqlOptions> setupAction)
    {
        services.AddMySqlQuery<ISqlQuery, MySqlQuery>(setupAction);
        return services;
    }

    /// <summary>
    /// 注册MySql Sql查询对象
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddMySqlQuery<TInterface, TImplementation>(this IServiceCollection services, string connection)
        where TInterface : ISqlQuery
        where TImplementation : MySqlQueryBase, TInterface
    {
        services.AddMySqlQuery<TInterface, TImplementation>(t => t.ConnectionString(connection));
        return services;
    }

    /// <summary>
    /// 注册MySql Sql查询对象
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddMySqlQuery<TInterface, TImplementation>(this IServiceCollection services, Action<SqlOptions> setupAction)
        where TInterface : ISqlQuery
        where TImplementation : MySqlQueryBase, TInterface
    {
        var sqlOptions = new SqlOptions<TImplementation>();
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        services.AddSqlDataSource(null, DatabaseType.MySql, sqlOptions.ConnectionString);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, MySqlDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(DatabaseType.MySql, connection => new MySqlConnection(connection));
        services.AddDatabaseTypeConverter<MySqlTypeConverter>(DatabaseType.MySql);
        services.AddSqlImplementationType<TInterface, TImplementation>(DatabaseType.MySql);
        services.TryAddTransient(typeof(TInterface), typeof(TImplementation));
        services.TryAddSingleton(typeof(SqlOptions<TImplementation>), _ => sqlOptions);
        return services;
    }

    #endregion

    #region AddMySqlExecutor(注册MySql Sql执行器)

    /// <summary>
    /// 注册MySql Sql执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddMySqlExecutor(this IServiceCollection services)
    {
        services.AddMySqlExecutor("");
        return services;
    }

    /// <summary>
    /// 注册MySql Sql执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddMySqlExecutor(this IServiceCollection services, string connection)
    {
        services.AddMySqlExecutor<ISqlExecutor, MySqlExecutor>(connection);
        return services;
    }

    /// <summary>
    /// 注册MySql Sql执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddMySqlExecutor(this IServiceCollection services, Action<SqlOptions> setupAction)
    {
        services.AddMySqlExecutor<ISqlExecutor, MySqlExecutor>(setupAction);
        return services;
    }

    /// <summary>
    /// 注册MySql Sql执行器
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddMySqlExecutor<TInterface, TImplementation>(this IServiceCollection services, string connection)
        where TInterface : ISqlExecutor
        where TImplementation : MySqlExecutorBase, TInterface
    {
        services.AddMySqlExecutor<TInterface, TImplementation>(t => t.ConnectionString(connection));
        return services;
    }

    /// <summary>
    /// 注册MySql Sql执行器
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddMySqlExecutor<TInterface, TImplementation>(this IServiceCollection services, Action<SqlOptions> setupAction)
        where TInterface : ISqlExecutor
        where TImplementation : MySqlExecutorBase, TInterface
    {
        var sqlOptions = new SqlOptions<TImplementation>();
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        services.AddSqlDataSource(null, DatabaseType.MySql, sqlOptions.ConnectionString);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, MySqlDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(DatabaseType.MySql, connection => new MySqlConnection(connection));
        services.AddDatabaseTypeConverter<MySqlTypeConverter>(DatabaseType.MySql);
        services.AddSqlImplementationType<TInterface, TImplementation>(DatabaseType.MySql);
        services.TryAddTransient(typeof(TInterface), typeof(TImplementation));
        services.TryAddSingleton(typeof(SqlOptions<TImplementation>), _ => sqlOptions);
        return services;
    }

    #endregion
}
