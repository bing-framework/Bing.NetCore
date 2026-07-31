using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Dapper;
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
        services.AddSqlImplementationType<ISqlQuery, SqlServerSqlQuery>(SqlServerSqlProvider.Instance.Key);
        services.AddSqlImplementationType<ISqlExecutor, SqlServerSqlExecutor>(SqlServerSqlProvider.Instance.Key);
        services.AddSqlImplementationType<ISqlMultipleQueryExecutor, SqlServerSqlMultipleQueryExecutor>(SqlServerSqlProvider.Instance.Key);
        services.TryAddTransient<ISqlQuery, SqlServerSqlQuery>();
        services.TryAddTransient<ISqlExecutor, SqlServerSqlExecutor>();
        services.TryAddTransient<ISqlMultipleQueryExecutor, SqlServerSqlMultipleQueryExecutor>();
        services.TryAddSingleton(queryOptions);
        services.TryAddSingleton(executorOptions);
        services.TryAddSingleton(multipleQueryOptions);
        return services;
    }

    #region AddSqlServerSqlQuery(注册SqlServer Sql查询对象)

    /// <summary>
    /// 注册SqlServer Sql查询对象
    /// </summary>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddSqlServerSqlQuery(this IServiceCollection services)
    {
        services.AddSqlServerSqlQuery("");
        return services;
    }

    /// <summary>
    /// 注册SqlServer Sql查询对象
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddSqlServerSqlQuery(this IServiceCollection services, string connection)
    {
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(connection);
        return services;
    }

    /// <summary>
    /// 注册SqlServer Sql查询对象
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddSqlServerSqlQuery(this IServiceCollection services, Action<SqlOptions> setupAction)
    {
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(setupAction);
        return services;
    }

    /// <summary>
    /// 注册SqlServer Sql查询对象
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddSqlServerSqlQuery<TInterface, TImplementation>(this IServiceCollection services, string connection)
        where TInterface : ISqlQuery
        where TImplementation : SqlServerSqlQueryBase, TInterface
    {
        services.AddSqlServerSqlQuery<TInterface, TImplementation>(t => t.ConnectionString(connection));
        return services;
    }

    /// <summary>
    /// 注册SqlServer Sql查询对象
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    /// <returns>当前服务集合，以支持链式注册。</returns>
    public static IServiceCollection AddSqlServerSqlQuery<TInterface, TImplementation>(this IServiceCollection services, Action<SqlOptions> setupAction)
        where TInterface : ISqlQuery
        where TImplementation : SqlServerSqlQueryBase, TInterface
    {
        services.AddSqlBuilderProvider(SqlServerSqlProvider.Instance, CreateBuilder);
        var sqlOptions = new SqlOptions<TImplementation>();
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        services.AddSqlDataSource(null, DatabaseType.SqlServer, sqlOptions.ConnectionString);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, SqlServerDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(SqlServerSqlProvider.Instance.Key, CreateConnection);
        services.AddDatabaseTypeConverter<SqlServerTypeConverter>(DatabaseType.SqlServer);
        services.AddSqlImplementationType<TInterface, TImplementation>(SqlServerSqlProvider.Instance.Key);
        services.TryAddTransient(typeof(TInterface), typeof(TImplementation));
        services.TryAddSingleton(typeof(SqlOptions<TImplementation>), _ => sqlOptions);
        return services;
    }

    #endregion

    #region AddSqlServerSqlMultipleQueryExecutor(注册 SQL Server 多结果集查询执行器)

    /// <summary>
    /// 注册 SQL Server 多结果集查询执行器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddSqlServerSqlMultipleQueryExecutor(this IServiceCollection services)
    {
        return services.AddSqlServerSqlMultipleQueryExecutor("");
    }

    /// <summary>
    /// 注册 SQL Server 多结果集查询执行器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="connection">数据库连接字符串。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddSqlServerSqlMultipleQueryExecutor(this IServiceCollection services,
        string connection)
    {
        return services.AddSqlServerSqlMultipleQueryExecutor(options => options.ConnectionString(connection));
    }

    /// <summary>
    /// 注册 SQL Server 多结果集查询执行器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="setupAction">SQL 配置操作。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddSqlServerSqlMultipleQueryExecutor(this IServiceCollection services,
        Action<SqlOptions> setupAction)
    {
        var sqlOptions = new SqlOptions<SqlServerSqlMultipleQueryExecutor>();
        services.AddSqlBuilderProvider(SqlServerSqlProvider.Instance, CreateBuilder);
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        services.AddSqlDataSource(null, DatabaseType.SqlServer, sqlOptions.ConnectionString);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, SqlServerDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(SqlServerSqlProvider.Instance.Key, CreateConnection);
        services.AddDatabaseTypeConverter<SqlServerTypeConverter>(DatabaseType.SqlServer);
        services.AddSqlImplementationType<ISqlMultipleQueryExecutor, SqlServerSqlMultipleQueryExecutor>(
            SqlServerSqlProvider.Instance.Key);
        services.TryAddTransient<ISqlMultipleQueryExecutor, SqlServerSqlMultipleQueryExecutor>();
        services.TryAddSingleton(sqlOptions);
        return services;
    }

    #endregion

    #region AddSqlServerSqlExecutor(注册SqlServer Sql执行器)

    /// <summary>
    /// 注册SqlServer Sql执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddSqlServerSqlExecutor(this IServiceCollection services)
    {
        services.AddSqlServerSqlExecutor("");
        return services;
    }

    /// <summary>
    /// 注册SqlServer Sql执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddSqlServerSqlExecutor(this IServiceCollection services, string connection)
    {
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(connection);
        return services;
    }

    /// <summary>
    /// 注册SqlServer Sql执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddSqlServerSqlExecutor(this IServiceCollection services, Action<SqlOptions> setupAction)
    {
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(setupAction);
        return services;
    }

    /// <summary>
    /// 注册SqlServer Sql执行器
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddSqlServerSqlExecutor<TInterface, TImplementation>(this IServiceCollection services, string connection)
        where TInterface : ISqlExecutor
        where TImplementation : SqlServerSqlExecutorBase, TInterface
    {
        services.AddSqlServerSqlExecutor<TInterface, TImplementation>(t => t.ConnectionString(connection));
        return services;
    }

    /// <summary>
    /// 注册SqlServer Sql执行器
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    /// <returns>当前服务集合，以支持链式注册。</returns>
    public static IServiceCollection AddSqlServerSqlExecutor<TInterface, TImplementation>(this IServiceCollection services, Action<SqlOptions> setupAction)
        where TInterface : ISqlExecutor
        where TImplementation : SqlServerSqlExecutorBase, TInterface
    {
        services.AddSqlBuilderProvider(SqlServerSqlProvider.Instance, CreateBuilder);
        var sqlOptions = new SqlOptions<TImplementation>();
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        services.AddSqlDataSource(null, DatabaseType.SqlServer, sqlOptions.ConnectionString);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, SqlServerDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(SqlServerSqlProvider.Instance.Key, CreateConnection);
        services.AddDatabaseTypeConverter<SqlServerTypeConverter>(DatabaseType.SqlServer);
        services.AddSqlImplementationType<TInterface, TImplementation>(SqlServerSqlProvider.Instance.Key);
        services.TryAddTransient(typeof(TInterface), typeof(TImplementation));
        services.TryAddSingleton(typeof(SqlOptions<TImplementation>), _ => sqlOptions);
        return services;
    }

    #endregion

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
