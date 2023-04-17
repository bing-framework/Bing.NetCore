using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Data.Sql;

/// <summary>
/// Dapper服务集合扩展
/// </summary>
public static class DapperServiceCollectionExtensions
{
    /// <summary>
    /// 注册MySql Sql查询对象
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddMySqlSqlQuery<TInterface, TImplementation>(this IServiceCollection services, Action<SqlOptions> setupAction)
        where TInterface : ISqlQuery
        where TImplementation : MySqlSqlQueryBase, TInterface
    {
        var sqlOptions = new SqlOptions<TImplementation>();
        setupAction?.Invoke(sqlOptions);
        services.TryAddTransient(typeof(TInterface), typeof(TImplementation));
        services.TryAddSingleton(typeof(SqlOptions<TImplementation>), _ => sqlOptions);
        return services;
    }
}
