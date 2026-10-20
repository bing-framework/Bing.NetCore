namespace Bing.Auditing;

/// <summary>
/// 保存当前操作审计日志的默认作用域实现。
/// </summary>
public class AuditLogScope : IAuditLogScope
{
    /// <summary>
    /// 初始化一个 <see cref="AuditLogScope"/> 类型的实例。
    /// </summary>
    /// <param name="log">要暴露给当前作用域的审计日志。</param>
    public AuditLogScope(AuditLogInfo log)
    {
        Log = log;
    }

    /// <inheritdoc />
    public AuditLogInfo Log { get; }
}
