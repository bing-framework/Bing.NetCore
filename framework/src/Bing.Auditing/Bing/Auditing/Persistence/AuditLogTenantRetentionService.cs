using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.Auditing.Persistence;

/// <summary>
/// 在调用租户范围低层存储前执行清理授权。
/// </summary>
public sealed class AuditLogTenantRetentionService : IAuditLogTenantRetentionService
{
    /// <summary>
    /// 单次保留清理允许的最大批量。
    /// </summary>
    private const int MaxBatchSize = 10000;

    /// <summary>
    /// 低层租户审计保留存储。
    /// </summary>
    private readonly IAuditLogTenantRetentionStore _store;

    /// <summary>
    /// 当前调用方的租户清理授权器。
    /// </summary>
    private readonly IAuditLogTenantRetentionAuthorizer _authorizer;

    /// <summary>
    /// 初始化一个 <see cref="AuditLogTenantRetentionService" /> 类型的实例。
    /// </summary>
    /// <param name="store">低层租户审计保留存储。</param>
    /// <param name="authorizer">租户清理授权器。</param>
    public AuditLogTenantRetentionService(IAuditLogTenantRetentionStore store,
        IAuditLogTenantRetentionAuthorizer authorizer)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
    }

    /// <inheritdoc />
    public async Task<int> DeleteBeforeAsync(string tenantId, DateTime cutoffUtc, int batchSize,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(tenantId, cutoffUtc, batchSize);
        cancellationToken.ThrowIfCancellationRequested();
        await _authorizer.AuthorizeAsync(tenantId, cutoffUtc, batchSize, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await _store.DeleteBeforeAsync(tenantId, cutoffUtc, batchSize, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 校验不会产生外部副作用的请求参数。
    /// </summary>
    /// <param name="tenantId">待清理的租户标识。</param>
    /// <param name="cutoffUtc">保留截止时间。</param>
    /// <param name="batchSize">本次最多删除的记录数。</param>
    private static void ValidateRequest(string tenantId, DateTime cutoffUtc, int batchSize)
    {
        AuditLogTenantIdentity.Validate(tenantId, nameof(tenantId), true);
        if (cutoffUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("审计保留截止时间必须使用 UTC。", nameof(cutoffUtc));
        if (batchSize <= 0 || batchSize > MaxBatchSize)
            throw new ArgumentOutOfRangeException(nameof(batchSize),
                $"审计清理批量必须位于 1 到 {MaxBatchSize} 之间。");
    }
}
