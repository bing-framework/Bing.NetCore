namespace Bing.Auditing.Persistence;

/// <summary>
/// 提供普通审计日志的分批保留清理能力。
/// </summary>
public interface IAuditLogRetentionStore
{
    /// <summary>
    /// 删除早于指定 UTC 时间的普通审计日志。
    /// </summary>
    /// <param name="cutoffUtc">保留截止时间，早于该时间的记录才会被删除。</param>
    /// <param name="batchSize">本次最多删除的记录数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次实际删除的记录数。</returns>
    /// <remarks>
    /// 该操作使用存储自身的连接和事务，只删除标记为普通审计的记录，不会处理关键审计记录，
    /// 也不会自动循环删除后续批次。调用方应在每批完成后重新评估容量和业务保留策略。
    /// </remarks>
    Task<int> DeleteBeforeAsync(DateTime cutoffUtc, int batchSize,
        CancellationToken cancellationToken = default);
}
