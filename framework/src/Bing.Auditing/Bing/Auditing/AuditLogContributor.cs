namespace Bing.Auditing;

/// <summary>
/// 审计日志构造函数
/// </summary>
public abstract class AuditLogContributor
{
    /// <summary>
    /// 构造前
    /// </summary>
    /// <param name="context">审计日志构造上下文</param>
    public virtual void PreContribute(AuditLogContributionContext context)
    {
    }

    /// <summary>
    /// 构造后
    /// </summary>
    /// <param name="context">审计日志构造上下文</param>
    public virtual void PostContribute(AuditLogContributionContext context)
    {
    }
}
