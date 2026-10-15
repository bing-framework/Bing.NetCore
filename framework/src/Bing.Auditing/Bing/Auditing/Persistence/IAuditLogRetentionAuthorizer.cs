using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.Auditing.Persistence;

/// <summary>
/// 校验审计日志保留清理请求的调用权限。
/// </summary>
/// <remarks>授权器应根据当前宿主的身份、租户和保留策略进行判断，不应从截止时间推断授权范围。</remarks>
public interface IAuditLogRetentionAuthorizer
{
    /// <summary>
    /// 授权一次审计日志保留清理。
    /// </summary>
    /// <param name="cutoffUtc">保留截止时间。</param>
    /// <param name="batchSize">本次最多删除的记录数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示授权检查完成的异步操作。</returns>
    Task AuthorizeAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken = default);
}
