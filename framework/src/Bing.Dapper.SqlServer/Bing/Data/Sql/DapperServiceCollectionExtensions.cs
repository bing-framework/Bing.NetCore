using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Data.Sql;

/// <summary>
/// Dapper服务集合扩展
/// </summary>
public static partial class DapperServiceCollectionExtensions
{
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
    public static IServiceCollection AddSqlServerSqlQuery<TInterface, TImplementation>(this IServiceCollection services, Action<SqlOptions> setupAction)
        where TInterface : ISqlQuery
        where TImplementation : SqlServerSqlQueryBase, TInterface
    {
        var sqlOptions = new SqlOptions<TImplementation>();
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        services.TryAddTransient(typeof(TInterface), typeof(TImplementation));
        services.TryAddSingleton(typeof(SqlOptions<TImplementation>), _ => sqlOptions);
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
    public static IServiceCollection AddSqlServerSqlExecutor<TInterface, TImplementation>(this IServiceCollection services, Action<SqlOptions> setupAction)
        where TInterface : ISqlExecutor
        where TImplementation : SqlServerSqlExecutorBase, TInterface
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
