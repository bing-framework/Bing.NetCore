using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.Auditing
{
    /// <summary>
    /// 空审计数据存储器
    /// </summary>
    public class NullAuditStore : IAuditStore
    {
        /// <summary>
        /// 空审计数据存储实例
        /// </summary>
        public static readonly IAuditStore Instance = new NullAuditStore();

        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="operationEntry">审计操作信息</param>
        public void Save(AuditOperationEntry operationEntry) => Debug.WriteLine(operationEntry);

        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="operationEntry">审计操作信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task SaveAsync(AuditOperationEntry operationEntry, CancellationToken cancellationToken = default)
        {
            Debug.WriteLine(operationEntry);
            return Task.CompletedTask;
        }
    }
}
