namespace Bing.Auditing.Persistence;

/// <summary>
/// 校验审计归档历史清理请求的调用权限。
/// </summary>
public interface IAuditLogArchiveRetentionAuthorizer
{
    /// <summary>
    /// 授权一次审计归档历史清理。
    /// </summary>
    /// <param name="cutoffUtc">保留截止时间。</param>
    /// <param name="batchSize">本次最多删除的记录数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示授权检查完成的异步操作。</returns>
    Task AuthorizeAsync(DateTime cutoffUtc, int batchSize,
        CancellationToken cancellationToken = default);
}
