namespace Bing.Auditing;

/// <summary>
/// 审计日志作用域
/// </summary>
public interface IAuditLogScope
{
    /// <summary>
    /// 审计日志
    /// </summary>
    AuditLogInfo Log { get; }
}
