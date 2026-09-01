using Bing.DependencyInjection;

namespace Bing.Auditing;

/// <summary>
/// 提供审计日志贡献者所需的构造上下文。
/// </summary>
public class AuditLogContributionContext : IServiceProviderAccessor
{
    /// <summary>
    /// 初始化一个 <see cref="AuditLogContributionContext"/> 类型的实例。
    /// </summary>
    /// <param name="serviceProvider">用于解析贡献者依赖的服务提供程序。</param>
    /// <param name="auditInfo">要补充的审计日志信息。</param>
    public AuditLogContributionContext(IServiceProvider serviceProvider, AuditLogInfo auditInfo)
    {
        ServiceProvider = serviceProvider;
        AuditInfo = auditInfo;
    }

    /// <summary>
    /// 获取服务提供程序。
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 获取审计日志信息。
    /// </summary>
    public AuditLogInfo AuditInfo { get; }
}
