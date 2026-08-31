namespace Bing.Auditing;

/// <summary>
/// 保存当前操作审计日志的默认作用域实现。
/// </summary>
public class AuditLogScope : IAuditLogScope
{
    /// <summary>
    /// 使用指定审计日志初始化 <see cref="AuditLogScope"/> 的实例。
    /// </summary>
    /// <param name="log">要暴露给当前作用域的审计日志。</param>
    public AuditLogScope(AuditLogInfo log)
    {
        Log = log;
    }

    /// <inheritdoc />
    public AuditLogInfo Log { get; }
}
