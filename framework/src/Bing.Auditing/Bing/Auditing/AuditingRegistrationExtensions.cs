using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Auditing;

/// <summary>
/// 注册审计作用域服务。
/// </summary>
public static class AuditingRegistrationExtensions
{
    /// <summary>
    /// 注册审计管理器及默认日志存储。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddBingAuditing(this IServiceCollection services)
    {
        services.AddLogging();
        services.TryAddScoped<IAuditingManager, AuditingManager>();
        services.TryAddSingleton<IAuditingStore, SimpleLogAuditingStore>();
        return services;
    }
}
