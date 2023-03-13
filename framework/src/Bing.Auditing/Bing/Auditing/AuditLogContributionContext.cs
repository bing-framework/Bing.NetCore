using Bing.DependencyInjection;

namespace Bing.Auditing;

/// <summary>
/// 审计日志贡献上下文
/// </summary>
public class AuditLogContributionContext : IServiceProviderAccessor
{
    /// <summary>
    /// 初始化一个<see cref="AuditLogContributionContext"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="auditInfo">审计日志信息</param>
    public AuditLogContributionContext(IServiceProvider serviceProvider, AuditLogInfo auditInfo)
    {
        ServiceProvider = serviceProvider;
        AuditInfo = auditInfo;
    }

    /// <summary>
    /// 服务提供程序
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 审计日志信息
    /// </summary>
    public AuditLogInfo AuditInfo { get; }
}
