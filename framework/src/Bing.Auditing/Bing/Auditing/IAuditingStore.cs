namespace Bing.Auditing;

/// <summary>
/// 定义审计日志信息的保存目标。
/// </summary>
public interface IAuditingStore
{
    /// <summary>
    /// 异步保存审计日志信息。
    /// </summary>
    /// <param name="auditInfo">要保存的审计日志信息。</param>
    Task SaveAsync(AuditLogInfo auditInfo);
}
