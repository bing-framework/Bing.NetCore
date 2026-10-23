using Bing.Data.Sql;
using Bing.Datas.EntityFramework.Core;
using Bing.Uow;
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
        services.TryAddScoped<IEfCoreSqlQueryFactory, EfCoreSqlQueryFactory>();
        return services;
    }

    /// <summary>
    /// 注册自动业务操作使用的 EF Core 已解析实例登记与保存处理器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddEfCoreOperationSaving(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        services.TryAddScoped<ResolvedEfCoreUnitOfWorkRegistry>();
        services.TryAddScoped<IUnitOfWorkRootStartingObserver>(provider =>
            provider.GetRequiredService<ResolvedEfCoreUnitOfWorkRegistry>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IUnitOfWorkSaveChangesHandler,
            EfCoreOperationSaveChangesHandler>());
        return services;
    }

    /// <summary>
    /// 将 SQL 数据源显式映射到当前依赖注入范围中的 EF Core 工作单元。
    /// </summary>
    /// <typeparam name="TContext">由依赖注入管理的 EF Core 工作单元类型。</typeparam>
    /// <param name="services">服务集合。</param>
    /// <param name="dataSourceKey">已注册的 SQL 数据源键。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddEfCoreSqlUnitOfWork<TContext>(this IServiceCollection services,
        string dataSourceKey) where TContext : UnitOfWorkBase
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (string.IsNullOrWhiteSpace(dataSourceKey))
            throw new ArgumentException("SQL 数据源键不能为空。", nameof(dataSourceKey));

        var existing = services.Where(descriptor => descriptor.ServiceType == typeof(EfCoreSqlUnitOfWorkMapping))
            .Select(descriptor => descriptor.ImplementationInstance as EfCoreSqlUnitOfWorkMapping)
            .Where(mapping => mapping != null && string.Equals(mapping.DataSourceKey, dataSourceKey,
                StringComparison.Ordinal)).ToArray();
        if (existing.Any(mapping => mapping.ContextType != typeof(TContext)))
            throw new InvalidOperationException($"SQL 数据源 {dataSourceKey} 已映射到其他 EF Core 工作单元类型。");
        if (services.Where(descriptor => descriptor.ServiceType == typeof(EfCoreSqlUnitOfWorkMapping))
            .Select(descriptor => descriptor.ImplementationInstance as EfCoreSqlUnitOfWorkMapping)
            .Any(mapping => mapping != null && mapping.ContextType == typeof(TContext) &&
                            !string.Equals(mapping.DataSourceKey, dataSourceKey, StringComparison.Ordinal)))
            throw new InvalidOperationException($"EF Core 工作单元类型 {typeof(TContext).FullName} 已映射到其他 SQL 数据源。");
        if (existing.Length == 0)
            services.AddSingleton(new EfCoreSqlUnitOfWorkMapping(typeof(TContext), dataSourceKey));

        services.AddEfCoreSqlQueryFactory();
        services.AddEfCoreOperationSaving();
        services.AddUnitOfWorkScopes();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IUnitOfWorkConnectionProvider,
            EfCoreSqlUnitOfWorkConnectionProvider>());
        return services;
    }
}
