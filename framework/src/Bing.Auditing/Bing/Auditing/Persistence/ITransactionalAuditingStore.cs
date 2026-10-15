using System.Data.Common;

namespace Bing.Auditing.Persistence;

/// <summary>
/// 在调用方业务事务内保存关键审计日志。
/// </summary>
public interface ITransactionalAuditingStore
{
    /// <summary>
    /// 保存关键审计日志。
    /// </summary>
    /// <param name="transaction">调用方当前事务。</param>
    /// <param name="auditId">当前租户内稳定且唯一的审计标识。</param>
    /// <param name="auditInfo">审计信息。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <remarks>不创建、提交、回滚或释放业务事务；写入失败后调用方必须回滚，不能忽略错误后提交业务。</remarks>
    Task SaveAsync(DbTransaction transaction, string auditId, AuditLogInfo auditInfo,
        CancellationToken cancellationToken = default);
}
