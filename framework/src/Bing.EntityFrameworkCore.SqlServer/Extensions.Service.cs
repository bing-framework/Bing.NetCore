﻿using System.Data.Common;
using Bing.Data;
using Bing.Datas.EntityFramework.Core;
using Bing.Uow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Datas.EntityFramework.SqlServer;

/// <summary>
/// 数据服务 扩展
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// 注册SqlServer工作单元
    /// </summary>
    /// <typeparam name="TService">工作单元接口类型</typeparam>
    /// <typeparam name="TImplementation">工作单元实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connection">连接字符串</param>
    /// <param name="dataConfigAction">数据配置操作</param>
    /// <param name="setupAction">工作单元配置操作</param>
    /// <param name="sqlServerSetupAction">SqlServer配置操作</param>
    public static IServiceCollection AddSqlServerUnitOfWork<TService, TImplementation>(
        this IServiceCollection services,
        DbConnection connection,
        Action<DataConfig> dataConfigAction = null,
        Action<DbContextOptionsBuilder> setupAction = null,
        Action<SqlServerDbContextOptionsBuilder> sqlServerSetupAction = null)
        where TService : class, IUnitOfWork
        where TImplementation : UnitOfWorkBase, TService
    {
        return AddSqlServerUnitOfWork<TService, TImplementation>(services, null, connection, dataConfigAction, setupAction, sqlServerSetupAction);
    }

    /// <summary>
    /// 注册SqlServer工作单元
    /// </summary>
    /// <typeparam name="TService">工作单元接口类型</typeparam>
    /// <typeparam name="TImplementation">工作单元实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connection">连接字符串</param>
    /// <param name="dataConfigAction">数据配置操作</param>
    /// <param name="setupAction">工作单元配置操作</param>
    /// <param name="sqlServerSetupAction">SqlServer配置操作</param>
    public static IServiceCollection AddSqlServerUnitOfWork<TService, TImplementation>(
        this IServiceCollection services,
        string connection,
        Action<DataConfig> dataConfigAction = null,
        Action<DbContextOptionsBuilder> setupAction = null,
        Action<SqlServerDbContextOptionsBuilder> sqlServerSetupAction = null)
        where TService : class, IUnitOfWork
        where TImplementation : UnitOfWorkBase, TService
    {
        return AddSqlServerUnitOfWork<TService, TImplementation>(services, connection, null, dataConfigAction, setupAction, sqlServerSetupAction);
    }

    /// <summary>
    /// 注册SqlServer工作单元
    /// </summary>
    /// <typeparam name="TService">工作单元接口类型</typeparam>
    /// <typeparam name="TImplementation">工作单元实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="connection">数据库连接</param>
    /// <param name="dataConfigSetupAction">数据配置操作</param>
    /// <param name="setupAction">工作单元配置操作</param>
    /// <param name="sqlServerSetupAction">SqlServer配置操作</param>
    private static IServiceCollection AddSqlServerUnitOfWork<TService, TImplementation>(IServiceCollection services,
        string connectionString,
        DbConnection connection,
        Action<DataConfig> dataConfigSetupAction,
        Action<DbContextOptionsBuilder> setupAction,
        Action<SqlServerDbContextOptionsBuilder> sqlServerSetupAction)
        where TService : class, IUnitOfWork
        where TImplementation : UnitOfWorkBase, TService
    {
        services.AddDbContext<TService, TImplementation>(options =>
        {
            setupAction?.Invoke(options);
            if (string.IsNullOrWhiteSpace(connectionString) == false)
            {
                options.UseSqlServer(connectionString, sqlServerSetupAction);
                return;
            }

            if (connection != null)
                options.UseSqlServer(connection, sqlServerSetupAction);
        });

        var dataConfig = new DataConfig();
        if (dataConfigSetupAction != null)
        {
            services.Configure(dataConfigSetupAction);
            dataConfigSetupAction.Invoke(dataConfig);
        }

        services.TryAddScoped<TService>(t => t.GetService<TImplementation>());
        services.TryAddScoped<IUnitOfWork>(t => t.GetService<TImplementation>());
        return services;
    }
}