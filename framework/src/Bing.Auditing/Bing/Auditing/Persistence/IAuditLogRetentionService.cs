using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.Auditing.Persistence;

/// <summary>
/// 提供经过授权的审计日志保留清理能力。
/// </summary>
/// <remarks>业务入口应使用此契约，不应直接向 HTTP 或管理接口暴露低层存储契约。</remarks>
public interface IAuditLogRetentionService
{
    /// <summary>
    /// 授权并删除早于指定 UTC 时间的普通审计日志。
    /// </summary>
    /// <param name="cutoffUtc">保留截止时间，早于该时间的记录才会被删除。</param>
    /// <param name="batchSize">本次最多删除的记录数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次实际删除的记录数。</returns>
    Task<int> DeleteBeforeAsync(DateTime cutoffUtc, int batchSize,
        CancellationToken cancellationToken = default);
}
