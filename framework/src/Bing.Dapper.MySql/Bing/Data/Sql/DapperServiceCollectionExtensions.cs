using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Data.Sql;

/// <summary>
/// Dapper服务集合扩展
/// </summary>
public static partial class DapperServiceCollectionExtensions
{
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
        services.TryAddTransient(typeof(TInterface), typeof(TImplementation));
        services.TryAddSingleton(typeof(SqlOptions<TImplementation>), _ => sqlOptions);
        return services;
    }

    #endregion
}
