namespace Bing.Auditing;

/// <summary>
/// 审计日志构造函数
/// </summary>
public abstract class AuditLogContributor
{
    /// <summary>
    /// 在审计日志构造前贡献字段。
    /// </summary>
    /// <param name="context">审计日志构造上下文</param>
    public virtual void PreContribute(AuditLogContributionContext context)
    {
    }

    /// <summary>
    /// 在审计日志构造后贡献字段。
    /// </summary>
    /// <param name="context">审计日志构造上下文</param>
    public virtual void PostContribute(AuditLogContributionContext context)
    {
    }
}
