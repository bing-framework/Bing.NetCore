using Bing.Auditing;
using Bing.Auditing.Persistence;
using FreeSql;

namespace Bing.FreeSQL.Auditing;

/// <summary>
/// 提供 FreeSQL 业务事务内的关键审计适配。
/// </summary>
public static class FreeSqlAuditingExtensions
{
    /// <summary>
    /// 在当前业务事务内保存关键审计。
    /// </summary>
    /// <param name="unitOfWork">当前工作单元。</param>
    /// <param name="store">关键审计存储。</param>
    /// <param name="auditId">当前租户内稳定的审计标识。</param>
    /// <param name="auditInfo">审计信息。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <remarks>要求已存在事务；不会创建、提交或释放业务事务，审计失败时调用方必须回滚。</remarks>
    public static Task SaveTransactionalAuditAsync(this IUnitOfWork unitOfWork, ITransactionalAuditingStore store,
        string auditId, AuditLogInfo auditInfo, CancellationToken cancellationToken = default)
    {
        if (unitOfWork == null)
            throw new ArgumentNullException(nameof(unitOfWork));
        if (store == null)
            throw new ArgumentNullException(nameof(store));
        var transaction = unitOfWork.GetOrBeginTransaction(false) ??
            throw new InvalidOperationException("FreeSQL 当前没有可复用的审计事务。");
        return store.SaveAsync(transaction, auditId, auditInfo, cancellationToken);
    }
}
