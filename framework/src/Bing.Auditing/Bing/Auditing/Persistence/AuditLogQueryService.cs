using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.Auditing.Persistence;

/// <summary>
/// 在调用低层审计查询存储前执行查询授权。
/// </summary>
public sealed class AuditLogQueryService : IAuditLogQueryService
{
    /// <summary>
    /// 低层审计查询存储。
    /// </summary>
    private readonly IAuditLogQueryStore _store;

    /// <summary>
    /// 当前调用方的查询授权器。
    /// </summary>
    private readonly IAuditLogQueryAuthorizer _authorizer;

    /// <summary>
    /// 初始化一个 <see cref="AuditLogQueryService" /> 类型的实例。
    /// </summary>
    /// <param name="store">低层审计查询存储。</param>
    /// <param name="authorizer">查询授权器。</param>
    public AuditLogQueryService(IAuditLogQueryStore store, IAuditLogQueryAuthorizer authorizer)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditLogEntry>> QueryAsync(AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        AuditLogQueryValidator.Validate(query);
        cancellationToken.ThrowIfCancellationRequested();
        await _authorizer.AuthorizeAsync(query, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await _store.QueryAsync(query, cancellationToken).ConfigureAwait(false);
    }
}
