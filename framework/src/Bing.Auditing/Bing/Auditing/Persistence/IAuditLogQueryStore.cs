namespace Bing.Auditing.Persistence;

/// <summary>
/// 提供租户范围内的审计日志元数据查询能力。
/// </summary>
/// <remarks>实现只负责数据过滤，不负责 HTTP 授权。调用方必须在暴露查询结果前完成租户和角色授权。</remarks>
public interface IAuditLogQueryStore
{
    /// <summary>
    /// 按查询条件读取审计日志元数据。
    /// </summary>
    /// <param name="query">租户范围和过滤条件。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按执行时间和审计标识稳定排序的审计日志元数据。</returns>
    Task<IReadOnlyList<AuditLogEntry>> QueryAsync(AuditLogQuery query,
        CancellationToken cancellationToken = default);
}
