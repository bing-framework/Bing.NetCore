using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bing.Auditing.Persistence;

/// <summary>
/// 注册数据库审计存储。
/// </summary>
public static class AuditingPersistenceExtensions
{
    /// <summary>
    /// 注册经过授权的审计日志查询服务。
    /// </summary>
    /// <typeparam name="TAuthorizer">查询授权器类型。</typeparam>
    /// <param name="services">服务集合。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>该入口不会替换低层 <see cref="IAuditLogQueryStore"/>，重复注册也不会覆盖调用方已有授权器。</remarks>
    public static IServiceCollection AddBingAuditLogQuery<TAuthorizer>(this IServiceCollection services)
        where TAuthorizer : class, IAuditLogQueryAuthorizer
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.TryAddScoped<IAuditLogQueryAuthorizer, TAuthorizer>();
        services.TryAddScoped<IAuditLogQueryService, AuditLogQueryService>();
        return services;
    }

    /// <summary>
    /// 注册经过授权的审计日志保留清理服务。
    /// </summary>
    /// <typeparam name="TAuthorizer">清理授权器类型。</typeparam>
    /// <param name="services">服务集合。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>该入口不会替换低层 <see cref="IAuditLogRetentionStore"/>，重复注册也不会覆盖调用方已有授权器。</remarks>
    public static IServiceCollection AddBingAuditLogRetention<TAuthorizer>(this IServiceCollection services)
        where TAuthorizer : class, IAuditLogRetentionAuthorizer
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.TryAddScoped<IAuditLogRetentionAuthorizer, TAuthorizer>();
        services.TryAddScoped<IAuditLogRetentionService, AuditLogRetentionService>();
        return services;
    }

    /// <summary>
    /// 注册经过授权的审计日志归档服务。
    /// </summary>
    /// <typeparam name="TAuthorizer">归档授权器类型。</typeparam>
    /// <param name="services">服务集合。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>
    /// 归档使用与保留清理相同的授权器，但不会自动启动调度任务；调用方必须显式调用归档服务。
    /// </remarks>
    public static IServiceCollection AddBingAuditLogArchive<TAuthorizer>(this IServiceCollection services)
        where TAuthorizer : class, IAuditLogRetentionAuthorizer
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.TryAddScoped<IAuditLogRetentionAuthorizer, TAuthorizer>();
        services.TryAddScoped<IAuditLogArchiveService, AuditLogArchiveService>();
        return services;
    }

    /// <summary>
    /// 注册经过授权的审计归档历史清理服务。
    /// </summary>
    /// <typeparam name="TAuthorizer">归档历史清理授权器类型。</typeparam>
    /// <param name="services">服务集合。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>该入口只注册显式调用所需服务，不会创建后台调度任务。</remarks>
    public static IServiceCollection AddBingAuditLogArchiveRetention<TAuthorizer>(
        this IServiceCollection services)
        where TAuthorizer : class, IAuditLogArchiveRetentionAuthorizer
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.TryAddScoped<IAuditLogArchiveRetentionAuthorizer, TAuthorizer>();
        services.TryAddScoped<IAuditLogArchiveRetentionService, AuditLogArchiveRetentionService>();
        return services;
    }

    /// <summary>
    /// 注册经过授权的审计归档历史清理调度。
    /// </summary>
    /// <typeparam name="TAuthorizer">后台归档历史清理授权器类型。</typeparam>
    /// <param name="services">服务集合。</param>
    /// <param name="configure">清理调度配置。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>
    /// 调度默认关闭。启用后仍通过 <see cref="IAuditLogArchiveRetentionService"/> 执行，
    /// 不会绕过归档历史授权器或直接解析低层存储。启用前必须确认外部备份和业务重放窗口已经满足。
    /// </remarks>
    public static IServiceCollection AddBingAuditLogArchiveRetentionScheduler<TAuthorizer>(
        this IServiceCollection services,
        Action<AuditLogArchiveRetentionScheduleOptions> configure = null)
        where TAuthorizer : class, IAuditLogArchiveRetentionAuthorizer
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.AddBingAuditLogArchiveRetention<TAuthorizer>();
        services.AddOptions<AuditLogArchiveRetentionScheduleOptions>();
        if (configure != null)
            services.Configure(configure);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService,
            AuditLogArchiveRetentionHostedService>());
        return services;
    }

    /// <summary>
    /// 注册经过授权的审计日志保留清理调度。
    /// </summary>
    /// <typeparam name="TAuthorizer">后台清理授权器类型。</typeparam>
    /// <param name="services">服务集合。</param>
    /// <param name="configure">清理调度配置。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>
    /// 调度默认关闭。启用后仍通过 <see cref="IAuditLogRetentionService"/> 执行，
    /// 不会绕过授权器或直接解析低层存储。
    /// </remarks>
    public static IServiceCollection AddBingAuditLogRetentionScheduler<TAuthorizer>(
        this IServiceCollection services, Action<AuditLogRetentionScheduleOptions> configure = null)
        where TAuthorizer : class, IAuditLogRetentionAuthorizer
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.AddBingAuditLogRetention<TAuthorizer>();
        services.AddOptions<AuditLogRetentionScheduleOptions>();
        if (configure != null)
            services.Configure(configure);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService,
            AuditLogRetentionHostedService>());
        return services;
    }

    /// <summary>
    /// 注册经过授权的租户范围审计日志保留清理服务。
    /// </summary>
    /// <typeparam name="TAuthorizer">租户清理授权器类型。</typeparam>
    /// <param name="services">服务集合。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>该入口不会替换低层 <see cref="IAuditLogTenantRetentionStore"/>，重复注册也不会覆盖调用方已有授权器。</remarks>
    public static IServiceCollection AddBingAuditLogTenantRetention<TAuthorizer>(this IServiceCollection services)
        where TAuthorizer : class, IAuditLogTenantRetentionAuthorizer
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.TryAddScoped<IAuditLogTenantRetentionAuthorizer, TAuthorizer>();
        services.TryAddScoped<IAuditLogTenantRetentionService, AuditLogTenantRetentionService>();
        return services;
    }

    /// <summary>
    /// 注册数据库审计存储。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="database">数据库语法。</param>
    /// <param name="connectionFactory">每次返回新建未打开连接的工厂。</param>
    /// <param name="configure">安全持久化规则。</param>
    /// <remarks>显式替换日志存储；不自动建表，不创建第二套工作单元，普通审计仍独立提交。</remarks>
    public static IServiceCollection AddBingDatabaseAuditing(this IServiceCollection services,
        AuditLogDatabase database, Func<DbConnection> connectionFactory,
        Action<AuditLogPersistenceOptions> configure = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (connectionFactory == null)
            throw new ArgumentNullException(nameof(connectionFactory));
        var options = new AuditLogPersistenceOptions();
        configure?.Invoke(options);
        var store = new DbAuditingStore(database, connectionFactory, options);
        services.AddBingAuditing();
        services.RemoveAll<DbAuditingStore>();
        services.RemoveAll<IAuditingStore>();
        services.RemoveAll<ITransactionalAuditingStore>();
        services.RemoveAll<IAuditLogRetentionStore>();
        services.RemoveAll<IAuditLogArchiveStore>();
        services.RemoveAll<IAuditLogArchiveRetentionStore>();
        services.RemoveAll<IAuditLogTenantRetentionStore>();
        services.RemoveAll<IAuditLogQueryStore>();
        services.AddSingleton(store);
        services.AddSingleton<IAuditingStore>(provider => provider.GetRequiredService<DbAuditingStore>());
        services.AddSingleton<ITransactionalAuditingStore>(provider => provider.GetRequiredService<DbAuditingStore>());
        services.AddSingleton<IAuditLogRetentionStore>(provider => provider.GetRequiredService<DbAuditingStore>());
        services.AddSingleton<IAuditLogArchiveStore>(provider => provider.GetRequiredService<DbAuditingStore>());
        services.AddSingleton<IAuditLogArchiveRetentionStore>(provider =>
            provider.GetRequiredService<DbAuditingStore>());
        services.AddSingleton<IAuditLogTenantRetentionStore>(provider => provider.GetRequiredService<DbAuditingStore>());
        services.AddSingleton<IAuditLogQueryStore>(provider => provider.GetRequiredService<DbAuditingStore>());
        return services;
    }
}
