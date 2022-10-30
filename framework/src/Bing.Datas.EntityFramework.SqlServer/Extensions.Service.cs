using System;
using Bing.Data;
using Bing.Datas.EntityFramework.Core;
using Bing.Datas.EntityFramework.Extensions;
using Bing.Uow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Datas.EntityFramework.SqlServer;

/// <summary>
/// 数据服务 扩展
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// 注册SqlServer工作单元服务
    /// </summary>
    /// <typeparam name="TService">工作单元接口类型</typeparam>
    /// <typeparam name="TImplementation">工作单元实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connection">连接字符串你</param>
    /// <param name="level">日志级别</param>
    public static IServiceCollection AddSqlServerUnitOfWork<TService, TImplementation>(
        this IServiceCollection services, string connection, DataLogLevel level = DataLogLevel.Sql)
        where TService : class, IUnitOfWork
        where TImplementation : UnitOfWorkBase, TService
    {
        return services.AddUnitOfWork<TService, TImplementation>(builder => { builder.UseSqlServer(connection); },
            config => config.LogLevel = level);
    }

    /// <summary>
    /// 注册SqlServer工作单元服务
    /// </summary>
    /// <typeparam name="TService">工作单元接口类型</typeparam>
    /// <typeparam name="TImplementation">工作单元实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connection">连接字符串你</param>
    /// <param name="dataConfigAction">数据配置操作</param>
    public static IServiceCollection AddSqlServerUnitOfWork<TService, TImplementation>(
        this IServiceCollection services, string connection, Action<DataConfig> dataConfigAction)
        where TService : class, IUnitOfWork
        where TImplementation : UnitOfWorkBase, TService
    {
        return services.AddUnitOfWork<TService, TImplementation>(builder => { builder.UseSqlServer(connection); },
            dataConfigAction);
    }

    /// <summary>
    /// 注册SqlServer工作单元服务
    /// </summary>
    /// <typeparam name="TService">工作单元接口类型</typeparam>
    /// <typeparam name="TImplementation">工作单元实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connection">连接字符串你</param>
    /// <param name="configuration">配置</param>
    public static IServiceCollection AddSqlServerUnitOfWork<TService, TImplementation>(
        this IServiceCollection services, string connection, IConfiguration configuration)
        where TService : class, IUnitOfWork
        where TImplementation : UnitOfWorkBase, TService
    {
        return services.AddUnitOfWork<TService, TImplementation>(builder => { builder.UseSqlServer(connection); }, null,
            configuration);
    }
}