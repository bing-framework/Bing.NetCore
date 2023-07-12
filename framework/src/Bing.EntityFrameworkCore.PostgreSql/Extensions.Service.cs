using System.Data.Common;
using Bing.Data;
using Bing.Datas.EntityFramework.Core;
using Bing.Uow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Bing.Datas.EntityFramework.PgSql;

/// <summary>
/// 数据服务 扩展
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// 注册PgSql工作单元
    /// </summary>
    /// <typeparam name="TService">工作单元接口类型</typeparam>
    /// <typeparam name="TImplementation">工作单元实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connection">连接字符串</param>
    /// <param name="dataConfigAction">数据配置操作</param>
    /// <param name="setupAction">工作单元配置操作</param>
    /// <param name="pgSqlSetupAction">PgSql配置操作</param>
    public static IServiceCollection AddPgSqlUnitOfWork<TService, TImplementation>(
        this IServiceCollection services,
        DbConnection connection,
        Action<DataConfig> dataConfigAction = null,
        Action<DbContextOptionsBuilder> setupAction = null,
        Action<NpgsqlDbContextOptionsBuilder> pgSqlSetupAction = null)
        where TService : class, IUnitOfWork
        where TImplementation : UnitOfWorkBase, TService
    {
        return AddPgSqlUnitOfWork<TService, TImplementation>(services, null, connection, dataConfigAction, setupAction, pgSqlSetupAction);
    }

    /// <summary>
    /// 注册PgSql工作单元
    /// </summary>
    /// <typeparam name="TService">工作单元接口类型</typeparam>
    /// <typeparam name="TImplementation">工作单元实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connection">连接字符串</param>
    /// <param name="dataConfigAction">数据配置操作</param>
    /// <param name="setupAction">工作单元配置操作</param>
    /// <param name="pgSqlSetupAction">PgSql配置操作</param>
    public static IServiceCollection AddPgSqlUnitOfWork<TService, TImplementation>(
        this IServiceCollection services,
        string connection,
        Action<DataConfig> dataConfigAction = null,
        Action<DbContextOptionsBuilder> setupAction = null,
        Action<NpgsqlDbContextOptionsBuilder> pgSqlSetupAction = null)
        where TService : class, IUnitOfWork
        where TImplementation : UnitOfWorkBase, TService
    {
        return AddPgSqlUnitOfWork<TService, TImplementation>(services, connection, null, dataConfigAction, setupAction, pgSqlSetupAction);
    }

    /// <summary>
    /// 注册PgSql工作单元
    /// </summary>
    /// <typeparam name="TService">工作单元接口类型</typeparam>
    /// <typeparam name="TImplementation">工作单元实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="connection">数据库连接</param>
    /// <param name="dataConfigSetupAction">数据配置操作</param>
    /// <param name="setupAction">工作单元配置操作</param>
    /// <param name="pgSqlSetupAction">PgSql配置操作</param>
    private static IServiceCollection AddPgSqlUnitOfWork<TService, TImplementation>(IServiceCollection services,
        string connectionString,
        DbConnection connection,
        Action<DataConfig> dataConfigSetupAction,
        Action<DbContextOptionsBuilder> setupAction,
        Action<NpgsqlDbContextOptionsBuilder> pgSqlSetupAction)
        where TService : class, IUnitOfWork
        where TImplementation : UnitOfWorkBase, TService
    {
        services.AddDbContext<TService, TImplementation>(options =>
        {
            setupAction?.Invoke(options);
            if (string.IsNullOrWhiteSpace(connectionString) == false)
            {
                options.UseNpgsql(connectionString, pgSqlSetupAction);
                return;
            }

            if (connection != null)
                options.UseNpgsql(connection, pgSqlSetupAction);
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
