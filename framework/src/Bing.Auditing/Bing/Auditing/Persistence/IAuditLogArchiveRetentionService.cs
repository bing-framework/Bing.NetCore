namespace Bing.Auditing.Persistence;

/// <summary>
/// 提供经过授权的审计归档历史清理能力。
/// </summary>
/// <remarks>该服务不会自动启动调度任务，调用方必须明确归档历史的重放和备份边界。</remarks>
public interface IAuditLogArchiveRetentionService
{
    /// <summary>
    /// 授权并删除早于指定 UTC 时间的归档审计日志。
    /// </summary>
    /// <param name="cutoffUtc">保留截止时间。</param>
    /// <param name="batchSize">本次最多删除的记录数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次实际删除的记录数。</returns>
    Task<int> DeleteBeforeAsync(DateTime cutoffUtc, int batchSize,
        CancellationToken cancellationToken = default);
}
