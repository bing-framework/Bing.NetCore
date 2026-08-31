namespace Bing.Auditing;

/// <summary>
/// 表示当前操作关联的审计日志作用域。
/// </summary>
public interface IAuditLogScope
{
    /// <summary>
    /// 获取当前作用域聚合的审计日志信息。
    /// </summary>
    AuditLogInfo Log { get; }
}
