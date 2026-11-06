namespace Bing.Auditing;

/// <summary>
/// 审计日志保存处理
/// </summary>
public interface IAuditLogSaveHandle : IDisposable
{
    /// <summary>
    /// 保存当前作用域收集的审计日志。
    /// </summary>
    Task SaveAsync();
}

/// <summary>
/// 定义支持调用方取消的审计保存句柄。
/// </summary>
public interface ICancellableAuditLogSaveHandle : IAuditLogSaveHandle
{
    /// <summary>
    /// 使用调用方取消令牌保存审计日志。
    /// </summary>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    Task SaveAsync(CancellationToken cancellationToken);
}
