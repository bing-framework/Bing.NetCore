using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
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
        services.AddDatabaseTypeConverter<MySqlTypeConverter>(DatabaseType.MySql);
        services.AddDatabaseTypeConverter<MySqlTypeConverter>(DatabaseType.Doris);
        services.AddSqlImplementationType<ISqlQuery, MySqlQuery>(MySqlSqlProvider.Instance.Key);
        services.AddSqlImplementationType<ISqlExecutor, MySqlExecutor>(MySqlSqlProvider.Instance.Key);
        services.AddSqlImplementationType<ISqlMultipleQueryExecutor, MySqlMultipleQueryExecutor>(MySqlSqlProvider.Instance.Key);
        services.TryAddTransient<ISqlQuery, MySqlQuery>();
        services.TryAddTransient<ISqlExecutor, MySqlExecutor>();
        services.TryAddTransient<ISqlMultipleQueryExecutor, MySqlMultipleQueryExecutor>();
        services.TryAddSingleton(queryOptions);
        services.TryAddSingleton(executorOptions);
        services.TryAddSingleton(multipleQueryOptions);
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
    /// <returns>当前服务集合，以支持链式注册。</returns>
    public static IServiceCollection AddMySqlQuery<TInterface, TImplementation>(this IServiceCollection services, Action<SqlOptions> setupAction)
        where TInterface : ISqlQuery
        where TImplementation : MySqlQueryBase, TInterface
    {
        var sqlOptions = new SqlOptions<TImplementation> { DatabaseType = DatabaseType.MySql };
        services.AddSqlBuilderProvider(MySqlSqlProvider.Instance, CreateBuilder);
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        services.AddSqlDataSource(null, DatabaseType.MySql, sqlOptions.ConnectionString);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, MySqlDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(MySqlSqlProvider.Instance.Key, CreateConnection);
        services.AddDatabaseTypeConverter<MySqlTypeConverter>(DatabaseType.MySql);
        services.AddSqlImplementationType<TInterface, TImplementation>(MySqlSqlProvider.Instance.Key);
        services.TryAddTransient(typeof(TInterface), typeof(TImplementation));
        services.TryAddSingleton(typeof(SqlOptions<TImplementation>), _ => sqlOptions);
        return services;
    }

    #endregion

    #region AddMySqlMultipleQueryExecutor(注册 MySQL 多结果集查询执行器)

    /// <summary>
    /// 注册 MySQL 多结果集查询执行器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddMySqlMultipleQueryExecutor(this IServiceCollection services)
    {
        return services.AddMySqlMultipleQueryExecutor("");
    }

    /// <summary>
    /// 注册 MySQL 多结果集查询执行器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="connection">数据库连接字符串。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddMySqlMultipleQueryExecutor(this IServiceCollection services, string connection)
    {
        return services.AddMySqlMultipleQueryExecutor(options => options.ConnectionString(connection));
    }

    /// <summary>
    /// 注册 MySQL 多结果集查询执行器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="setupAction">SQL 配置操作。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddMySqlMultipleQueryExecutor(this IServiceCollection services,
        Action<SqlOptions> setupAction)
    {
        var sqlOptions = new SqlOptions<MySqlMultipleQueryExecutor> { DatabaseType = DatabaseType.MySql };
        services.AddSqlBuilderProvider(MySqlSqlProvider.Instance, CreateBuilder);
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        services.AddSqlDataSource(null, DatabaseType.MySql, sqlOptions.ConnectionString);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, MySqlDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(MySqlSqlProvider.Instance.Key, CreateConnection);
        services.AddDatabaseTypeConverter<MySqlTypeConverter>(DatabaseType.MySql);
        services.AddSqlImplementationType<ISqlMultipleQueryExecutor, MySqlMultipleQueryExecutor>(MySqlSqlProvider.Instance.Key);
        services.TryAddTransient<ISqlMultipleQueryExecutor, MySqlMultipleQueryExecutor>();
        services.TryAddSingleton(sqlOptions);
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
    /// <returns>当前服务集合，以支持链式注册。</returns>
    public static IServiceCollection AddMySqlExecutor<TInterface, TImplementation>(this IServiceCollection services, Action<SqlOptions> setupAction)
        where TInterface : ISqlExecutor
        where TImplementation : MySqlExecutorBase, TInterface
    {
        var sqlOptions = new SqlOptions<TImplementation> { DatabaseType = DatabaseType.MySql };
        services.AddSqlBuilderProvider(MySqlSqlProvider.Instance, CreateBuilder);
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        services.AddSqlDataSource(null, DatabaseType.MySql, sqlOptions.ConnectionString);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, MySqlDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(MySqlSqlProvider.Instance.Key, CreateConnection);
        services.AddDatabaseTypeConverter<MySqlTypeConverter>(DatabaseType.MySql);
        services.AddSqlImplementationType<TInterface, TImplementation>(MySqlSqlProvider.Instance.Key);
        services.TryAddTransient(typeof(TInterface), typeof(TImplementation));
        services.TryAddSingleton(typeof(SqlOptions<TImplementation>), _ => sqlOptions);
        return services;
    }

    #endregion
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
