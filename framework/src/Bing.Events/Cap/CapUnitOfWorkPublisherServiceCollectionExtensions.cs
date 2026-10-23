using Bing.Data.Sql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bing.Events.Cap;

/// <summary>
/// 注册事务型业务工作单元的 CAP 发布服务。
/// </summary>
public static class CapUnitOfWorkPublisherServiceCollectionExtensions
{
    /// <summary>
    /// 为每个已配置 SQL 数据源建立独立 CAP runtime，并注册 Uow Outbox 发布入口。
    /// </summary>
    /// <param name="services">应用服务集合。</param>
    /// <param name="configure">CAP source runtime 配置。</param>
    /// <returns>应用服务集合。</returns>
    public static IServiceCollection AddCapUnitOfWorkMessagePublisher(this IServiceCollection services,
        Action<CapUnitOfWorkBuilder> configure)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        var descriptor = services.LastOrDefault(item =>
            item.ServiceType == typeof(CapUnitOfWorkRegistrationRegistry));
        CapUnitOfWorkRegistrationRegistry registry;
        if (descriptor == null)
        {
            registry = new CapUnitOfWorkRegistrationRegistry();
            services.AddSingleton(registry);
        }
        else if (descriptor.ImplementationInstance is CapUnitOfWorkRegistrationRegistry existing)
        {
            registry = existing;
        }
        else
        {
            throw new InvalidOperationException("CAP Uow runtime registry 已被不同服务描述覆盖。");
        }

        configure(new CapUnitOfWorkBuilder(registry));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDatabaseIdentityContributor,
            DefaultSqlDatabaseIdentityContributor>());
        services.TryAddSingleton<ISqlDatabaseIdentityResolver, DefaultSqlDatabaseIdentityResolver>();
        services.TryAddScoped<IUnitOfWorkMessagePublisher, CapUnitOfWorkMessagePublisher>();
        services.TryAddSingleton<CapUnitOfWorkRuntimeManager>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService,
            CapUnitOfWorkRuntimeHostedService>());
        return services;
    }
}
