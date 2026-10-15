using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.Auditing.Persistence;

/// <summary>
/// 提供租户范围的审计日志保留清理能力。
/// </summary>
public interface IAuditLogTenantRetentionStore
{
    /// <summary>
    /// 删除指定租户中早于指定 UTC 时间的普通审计日志。
    /// </summary>
    /// <param name="tenantId">待清理的租户标识。</param>
    /// <param name="cutoffUtc">保留截止时间，早于该时间的记录才会被删除。</param>
    /// <param name="batchSize">本次最多删除的记录数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次实际删除的记录数。</returns>
    Task<int> DeleteBeforeAsync(string tenantId, DateTime cutoffUtc, int batchSize,
        CancellationToken cancellationToken = default);
}
