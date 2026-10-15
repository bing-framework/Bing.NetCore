namespace Bing.Auditing.Persistence;

/// <summary>
/// 提供普通审计日志的分批归档能力。
/// </summary>
public interface IAuditLogArchiveStore
{
    /// <summary>
    /// 将早于指定 UTC 时间的普通审计日志归档并从主表删除。
    /// </summary>
    /// <param name="cutoffUtc">归档截止时间，早于该时间的记录才会被处理。</param>
    /// <param name="batchSize">本次最多归档的记录数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次实际从主表归档的记录数。</returns>
    /// <remarks>
    /// 归档和主表删除在存储自身的同一事务内完成，只处理普通审计记录。
    /// 该操作不会自动循环，也不会删除归档表中的历史记录；调用方应自行定义重放窗口和二次清理策略。
    /// </remarks>
    Task<int> ArchiveBeforeAsync(DateTime cutoffUtc, int batchSize,
        CancellationToken cancellationToken = default);
}
