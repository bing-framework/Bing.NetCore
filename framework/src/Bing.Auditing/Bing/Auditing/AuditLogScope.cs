namespace Bing.Auditing;

/// <summary>
/// 审计日志作用域
/// </summary>
public class AuditLogScope : IAuditLogScope
{
    /// <summary>
    /// 初始化一个<see cref="AuditLogScope"/>类型的实例
    /// </summary>
    /// <param name="log">审计日志</param>
    public AuditLogScope(AuditLogInfo log)
    {
        Log = log;
    }

    /// <summary>
    /// 审计日志
    /// </summary>
    public AuditLogInfo Log { get; }
}
