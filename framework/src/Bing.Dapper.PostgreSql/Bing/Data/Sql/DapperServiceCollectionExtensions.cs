﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Data.Sql;

/// <summary>
/// Dapper服务集合扩展
/// </summary>
public static partial class DapperServiceCollectionExtensions
{
    #region AddPostgreSqlQuery(注册PostgreSql Sql查询对象)

    /// <summary>
    /// 注册PostgreSql Sql查询对象
    /// </summary>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddPostgreSqlQuery(this IServiceCollection services)
    {
        services.AddPostgreSqlQuery("");
        return services;
    }

    /// <summary>
    /// 注册PostgreSql Sql查询对象
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddPostgreSqlQuery(this IServiceCollection services, string connection)
    {
        services.AddPostgreSqlQuery<ISqlQuery, PostgreSqlQuery>(connection);
        return services;
    }

    /// <summary>
    /// 注册PostgreSql Sql查询对象
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddPostgreSqlQuery(this IServiceCollection services, Action<SqlOptions> setupAction)
    {
        services.AddPostgreSqlQuery<ISqlQuery, PostgreSqlQuery>(setupAction);
        return services;
    }

    /// <summary>
    /// 注册PostgreSql Sql查询对象
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddPostgreSqlQuery<TInterface, TImplementation>(this IServiceCollection services, string connection)
        where TInterface : ISqlQuery
        where TImplementation : PostgreSqlQueryBase, TInterface
    {
        services.AddPostgreSqlQuery<TInterface, TImplementation>(t => t.ConnectionString(connection));
        return services;
    }

    /// <summary>
    /// 注册PostgreSql Sql查询对象
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddPostgreSqlQuery<TInterface, TImplementation>(this IServiceCollection services, Action<SqlOptions> setupAction)
        where TInterface : ISqlQuery
        where TImplementation : PostgreSqlQueryBase, TInterface
    {
        var sqlOptions = new SqlOptions<TImplementation>();
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        services.TryAddTransient(typeof(TInterface), typeof(TImplementation));
        services.TryAddSingleton(typeof(SqlOptions<TImplementation>), _ => sqlOptions);
        return services;
    }

    #endregion

    #region AddPostgreSqlExecutor(注册PostgreSql Sql执行器)

    /// <summary>
    /// 注册PostgreSql Sql执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddPostgreSqlExecutor(this IServiceCollection services)
    {
        services.AddPostgreSqlExecutor("");
        return services;
    }

    /// <summary>
    /// 注册PostgreSql Sql执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddPostgreSqlExecutor(this IServiceCollection services, string connection)
    {
        services.AddPostgreSqlExecutor<ISqlExecutor, PostgreSqlExecutor>(connection);
        return services;
    }

    /// <summary>
    /// 注册PostgreSql Sql执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddPostgreSqlExecutor(this IServiceCollection services, Action<SqlOptions> setupAction)
    {
        services.AddPostgreSqlExecutor<ISqlExecutor, PostgreSqlExecutor>(setupAction);
        return services;
    }

    /// <summary>
    /// 注册PostgreSql Sql执行器
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddPostgreSqlExecutor<TInterface, TImplementation>(this IServiceCollection services, string connection)
        where TInterface : ISqlExecutor
        where TImplementation : PostgreSqlExecutorBase, TInterface
    {
        services.AddPostgreSqlExecutor<TInterface, TImplementation>(t => t.ConnectionString(connection));
        return services;
    }

    /// <summary>
    /// 注册PostgreSql Sql执行器
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddPostgreSqlExecutor<TInterface, TImplementation>(this IServiceCollection services, Action<SqlOptions> setupAction)
        where TInterface : ISqlExecutor
        where TImplementation : PostgreSqlExecutorBase, TInterface
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
