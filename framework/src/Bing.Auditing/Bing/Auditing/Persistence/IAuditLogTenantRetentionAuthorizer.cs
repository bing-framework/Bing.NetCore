using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.Auditing.Persistence;

/// <summary>
/// 校验租户范围审计清理请求的调用权限。
/// </summary>
public interface IAuditLogTenantRetentionAuthorizer
{
    /// <summary>
    /// 授权一次租户范围审计清理。
    /// </summary>
    /// <param name="tenantId">待清理的租户标识。</param>
    /// <param name="cutoffUtc">保留截止时间。</param>
    /// <param name="batchSize">本次最多删除的记录数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示授权检查完成的异步操作。</returns>
    Task AuthorizeAsync(string tenantId, DateTime cutoffUtc, int batchSize,
        CancellationToken cancellationToken = default);
}
