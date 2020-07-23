using System.Threading;
using System.Threading.Tasks;

namespace Bing.Auditing
{
    /// <summary>
    /// 审计数据存储。
    /// 注意：审计操作的记录不能和业务数据操作在同一事务中
    /// </summary>
    public interface IAuditStore
    {
        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="operationEntry">审计操作信息</param>
        void Save(AuditOperationEntry operationEntry);

        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="operationEntry">审计操作信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task SaveAsync(AuditOperationEntry operationEntry, CancellationToken cancellationToken = default);
    }
}
