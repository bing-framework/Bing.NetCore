namespace Bing.Auditing;

/// <summary>
/// 定义支持调用方取消的审计日志保存目标。
/// </summary>
/// <remarks>这是 <see cref="IAuditingStore"/> 的可选能力；未实现时仍使用无取消令牌的兼容入口。</remarks>
public interface ICancellableAuditingStore : IAuditingStore
{
    /// <summary>
    /// 异步保存审计日志信息。
    /// </summary>
    /// <param name="auditInfo">要保存的审计日志信息。</param>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    Task SaveAsync(AuditLogInfo auditInfo, CancellationToken cancellationToken);
}
