namespace Bing.Auditing.Persistence;

/// <summary>
/// 提供审计归档表的分批历史清理能力。
/// </summary>
public interface IAuditLogArchiveRetentionStore
{
    /// <summary>
    /// 删除早于指定 UTC 时间的归档审计日志。
    /// </summary>
    /// <param name="cutoffUtc">保留截止时间，早于该时间的归档记录才会被删除。</param>
    /// <param name="batchSize">本次最多删除的记录数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次实际删除的记录数。</returns>
    /// <remarks>
    /// 该操作使用存储自身的连接和事务，不会触及主审计表，也不会自动循环。
    /// 删除前必须确认允许重放窗口、业务幂等记录和外部备份均已满足要求。
    /// </remarks>
    Task<int> DeleteBeforeAsync(DateTime cutoffUtc, int batchSize,
        CancellationToken cancellationToken = default);
}
