using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Data.Sql;

/// <summary>
/// Dapper服务集合扩展
/// </summary>
public static partial class DapperServiceCollectionExtensions
{
    #region AddSqliteSqlQuery(注册Sqlite Sql查询对象)

    /// <summary>
    /// 注册Sqlite Sql查询对象
    /// </summary>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddSqliteSqlQuery(this IServiceCollection services)
    {
        services.AddSqliteSqlQuery("");
        return services;
    }

    /// <summary>
    /// 注册Sqlite Sql查询对象
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddSqliteSqlQuery(this IServiceCollection services, string connection)
    {
        services.AddSqliteSqlQuery<ISqlQuery, SqliteSqlQuery>(connection);
        return services;
    }

    /// <summary>
    /// 注册Sqlite Sql查询对象
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddSqliteSqlQuery(this IServiceCollection services, Action<SqlOptions> setupAction)
    {
        services.AddSqliteSqlQuery<ISqlQuery, SqliteSqlQuery>(setupAction);
        return services;
    }

    /// <summary>
    /// 注册Sqlite Sql查询对象
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddSqliteSqlQuery<TInterface, TImplementation>(this IServiceCollection services, string connection)
        where TInterface : ISqlQuery
        where TImplementation : SqliteSqlQueryBase, TInterface
    {
        services.AddSqliteSqlQuery<TInterface, TImplementation>(t => t.ConnectionString(connection));
        return services;
    }

    /// <summary>
    /// 注册Sqlite Sql查询对象
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddSqliteSqlQuery<TInterface, TImplementation>(this IServiceCollection services, Action<SqlOptions> setupAction)
        where TInterface : ISqlQuery
        where TImplementation : SqliteSqlQueryBase, TInterface
    {
        var sqlOptions = new SqlOptions<TImplementation>();
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        services.TryAddTransient(typeof(TInterface), typeof(TImplementation));
        services.TryAddSingleton(typeof(SqlOptions<TImplementation>), _ => sqlOptions);
        return services;
    }

    #endregion

    #region AddSqliteSqlExecutor(注册Sqlite Sql执行器)

    /// <summary>
    /// 注册Sqlite Sql执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddSqliteSqlExecutor(this IServiceCollection services)
    {
        services.AddSqliteSqlExecutor("");
        return services;
    }

    /// <summary>
    /// 注册Sqlite Sql执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddSqliteSqlExecutor(this IServiceCollection services, string connection)
    {
        services.AddSqliteSqlExecutor<ISqlExecutor, SqliteSqlExecutor>(connection);
        return services;
    }

    /// <summary>
    /// 注册Sqlite Sql执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddSqliteSqlExecutor(this IServiceCollection services, Action<SqlOptions> setupAction)
    {
        services.AddSqliteSqlExecutor<ISqlExecutor, SqliteSqlExecutor>(setupAction);
        return services;
    }

    /// <summary>
    /// 注册Sqlite Sql执行器
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddSqliteSqlExecutor<TInterface, TImplementation>(this IServiceCollection services, string connection)
        where TInterface : ISqlExecutor
        where TImplementation : SqliteSqlExecutorBase, TInterface
    {
        services.AddSqliteSqlExecutor<TInterface, TImplementation>(t => t.ConnectionString(connection));
        return services;
    }

    /// <summary>
    /// 注册Sqlite Sql执行器
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddSqliteSqlExecutor<TInterface, TImplementation>(this IServiceCollection services, Action<SqlOptions> setupAction)
        where TInterface : ISqlExecutor
        where TImplementation : SqliteSqlExecutorBase, TInterface
    {
        var sqlOptions = new SqlOptions<TImplementation>();
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        services.TryAddTransient(typeof(TInterface), typeof(TImplementation));
        services.TryAddSingleton(typeof(SqlOptions<TImplementation>), _ => sqlOptions);
        return services;
    }

    #endregion
}
