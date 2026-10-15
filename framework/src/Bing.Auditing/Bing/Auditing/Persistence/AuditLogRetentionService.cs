using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.Auditing.Persistence;

/// <summary>
/// 在调用低层审计存储前执行保留清理授权。
/// </summary>
public sealed class AuditLogRetentionService : IAuditLogRetentionService
{
    /// <summary>
    /// 单次保留清理允许的最大批量。
    /// </summary>
    private const int MaxBatchSize = 10000;

    /// <summary>
    /// 低层审计保留存储。
    /// </summary>
    private readonly IAuditLogRetentionStore _store;

    /// <summary>
    /// 当前调用方的清理授权器。
    /// </summary>
    private readonly IAuditLogRetentionAuthorizer _authorizer;

    /// <summary>
    /// 初始化一个 <see cref="AuditLogRetentionService" /> 类型的实例。
    /// </summary>
    /// <param name="store">低层审计保留存储。</param>
    /// <param name="authorizer">清理授权器。</param>
    public AuditLogRetentionService(IAuditLogRetentionStore store,
        IAuditLogRetentionAuthorizer authorizer)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
    }

    /// <inheritdoc />
    public async Task<int> DeleteBeforeAsync(DateTime cutoffUtc, int batchSize,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(cutoffUtc, batchSize);
        cancellationToken.ThrowIfCancellationRequested();
        await _authorizer.AuthorizeAsync(cutoffUtc, batchSize, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await _store.DeleteBeforeAsync(cutoffUtc, batchSize, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 校验不会产生外部副作用的请求参数。
    /// </summary>
    /// <param name="cutoffUtc">保留截止时间。</param>
    /// <param name="batchSize">本次最多删除的记录数。</param>
    private static void ValidateRequest(DateTime cutoffUtc, int batchSize)
    {
        if (cutoffUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("审计保留截止时间必须使用 UTC。", nameof(cutoffUtc));
        if (batchSize <= 0 || batchSize > MaxBatchSize)
            throw new ArgumentOutOfRangeException(nameof(batchSize),
                $"审计清理批量必须位于 1 到 {MaxBatchSize} 之间。");
    }
}
