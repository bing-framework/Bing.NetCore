namespace Bing.Auditing;

/// <summary>
/// 审计日志存储器
/// </summary>
public interface IAuditingStore
{
    /// <summary>
    /// 保存
    /// </summary>
    /// <param name="auditInfo">审计日志信息</param>
    Task SaveAsync(AuditLogInfo auditInfo);
}
