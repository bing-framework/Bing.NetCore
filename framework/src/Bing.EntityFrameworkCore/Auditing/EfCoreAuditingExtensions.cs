using Bing.Auditing;
using Bing.Auditing.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bing.EntityFrameworkCore.Auditing;

/// <summary>
/// 提供 EFCore 业务事务内的关键审计适配。
/// </summary>
public static class EfCoreAuditingExtensions
{
    /// <summary>
    /// 在当前业务事务内保存关键审计。
    /// </summary>
    /// <param name="context">当前数据库上下文。</param>
    /// <param name="store">关键审计存储。</param>
    /// <param name="auditId">当前租户内稳定的审计标识。</param>
    /// <param name="auditInfo">审计信息。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <remarks>要求已存在事务；不会提交、回滚或释放当前上下文，审计失败时调用方必须回滚。</remarks>
    public static Task SaveTransactionalAuditAsync(this DbContext context, ITransactionalAuditingStore store,
        string auditId, AuditLogInfo auditInfo, CancellationToken cancellationToken = default)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (store == null)
            throw new ArgumentNullException(nameof(store));
        var transaction = context.Database.CurrentTransaction?.GetDbTransaction() ??
            throw new InvalidOperationException("EFCore 当前没有可复用的审计事务。");
        return store.SaveAsync(transaction, auditId, auditInfo, cancellationToken);
    }
}
