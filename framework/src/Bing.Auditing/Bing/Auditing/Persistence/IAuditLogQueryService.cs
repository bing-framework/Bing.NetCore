using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.Auditing.Persistence;

/// <summary>
/// 提供经过授权的审计日志元数据查询能力。
/// </summary>
/// <remarks>业务入口应使用此契约，不应直接向 HTTP 或管理接口暴露低层查询存储契约。</remarks>
public interface IAuditLogQueryService
{
    /// <summary>
    /// 授权并读取审计日志元数据。
    /// </summary>
    /// <param name="query">租户范围和过滤条件。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按存储契约稳定排序的审计日志元数据。</returns>
    Task<IReadOnlyList<AuditLogEntry>> QueryAsync(AuditLogQuery query,
        CancellationToken cancellationToken = default);
}
