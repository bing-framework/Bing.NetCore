using Bing.Data.Sql;
using Bing.Datas.EntityFramework.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.EntityFrameworkCore;

/// <summary>
/// EF Core SQL 查询服务集合扩展
/// </summary>
public static class EfCoreSqlQueryServiceCollectionExtensions
{
    /// <summary>
    /// 注册 EF Core SQL 查询工厂
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddEfCoreSqlQueryFactory(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDatabaseIdentityContributor, DefaultSqlDatabaseIdentityContributor>());
        services.TryAddSingleton<ISqlDatabaseIdentityResolver, DefaultSqlDatabaseIdentityResolver>();
        services.TryAddSingleton<IEfCoreSqlQueryFactory, EfCoreSqlQueryFactory>();
        return services;
    }
}