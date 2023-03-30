namespace Bing.Auditing;

/// <summary>
/// 审计日志保存处理
/// </summary>
public interface IAuditLogSaveHandle : IDisposable
{
    /// <summary>
    /// 保存
    /// </summary>
    Task SaveAsync();
}
