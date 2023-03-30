namespace Bing.Auditing;

/// <summary>
/// 审计日志管理器
/// </summary>
public interface IAuditingManager
{
    /// <summary>
    /// 当前审计日志作用域
    /// </summary>
    IAuditLogScope Current { get; }

    /// <summary>
    /// 开始作用范围
    /// </summary>
    IAuditLogSaveHandle BeginScope();
}
