namespace Bing.Auditing.Persistence;

/// <summary>
/// 提供经过授权的审计日志归档能力。
/// </summary>
/// <remarks>归档会从主表删除记录，业务入口应使用此契约而不是直接调用低层存储。</remarks>
public interface IAuditLogArchiveService
{
    /// <summary>
    /// 授权并归档早于指定 UTC 时间的普通审计日志。
    /// </summary>
    /// <param name="cutoffUtc">归档截止时间，早于该时间的记录才会被处理。</param>
    /// <param name="batchSize">本次最多归档的记录数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次实际从主表归档的记录数。</returns>
    Task<int> ArchiveBeforeAsync(DateTime cutoffUtc, int batchSize,
        CancellationToken cancellationToken = default);
}
