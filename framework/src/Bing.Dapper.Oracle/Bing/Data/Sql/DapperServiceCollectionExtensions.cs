﻿using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Data.Sql;

/// <summary>
/// Dapper服务集合扩展
/// </summary>
public static partial class DapperServiceCollectionExtensions
{
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
        var sqlOptions = new SqlOptions<TImplementation>();
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        sqlOptions.RegisterGuidTypeHandler();
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
        var sqlOptions = new SqlOptions<TImplementation>();
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        services.TryAddTransient(typeof(TInterface), typeof(TImplementation));
        services.TryAddSingleton(typeof(SqlOptions<TImplementation>), _ => sqlOptions);
        return services;
    }

    #endregion
}
