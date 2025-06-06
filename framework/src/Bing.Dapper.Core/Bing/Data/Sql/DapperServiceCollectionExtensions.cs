﻿using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Data.Sql;

/// <summary>
/// Dapper服务集合扩展
/// </summary>
public static partial class DapperServiceCollectionExtensions
{
    /// <summary>
    /// 注册数据库信息
    /// </summary>
    /// <typeparam name="TDatabase">数据库信息类型</typeparam>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddDatabase<TDatabase>(this IServiceCollection services)
        where TDatabase : class, IDatabase
    {
        return services.AddDatabase<IDatabase, TDatabase>();
    }

    /// <summary>
    /// 注册数据库信息
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddDatabase<TInterface, TImplementation>(this IServiceCollection services)
        where TInterface : IDatabase
        where TImplementation : class, TInterface
    {
        services.TryAddScoped(typeof(TInterface), typeof(TImplementation));
        services.TryAddScoped<ITableDatabase, DefaultTableDatabase>();
        return services;
    }

    /// <summary>
    /// 注册实体元数据
    /// </summary>
    /// <typeparam name="TEntityMetadata">实体元数据类型</typeparam>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddEntityMetadata<TEntityMetadata>(this IServiceCollection services)
        where TEntityMetadata : class, IEntityMetadata
    {
        return services.AddEntityMetadata<IEntityMetadata, TEntityMetadata>();
    }

    /// <summary>
    /// 注册实体元数据
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddEntityMetadata<TInterface, TImplementation>(this IServiceCollection services)
        where TInterface : IEntityMetadata
        where TImplementation : class, TInterface
    {
        services.TryAddScoped(typeof(TInterface), typeof(TImplementation));
        return services;
    }

}
