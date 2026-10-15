namespace Bing.Events.Consumption;

/// <summary>
/// 检查事件消费收据清理请求的调用权限。
/// </summary>
/// <remarks>
/// 授权器应根据当前宿主的身份、租户和重放策略进行判断，不应仅从截止时间推断授权范围。
/// </remarks>
public interface IEventConsumptionRetentionAuthorizer
{
    /// <summary>
    /// 授权一次事件消费收据清理。
    /// </summary>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="cutoffUtc">留存截止时间。</param>
    /// <param name="batchSize">本次最多删除的记录数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示授权检查完成的异步操作。</returns>
    Task AuthorizeAsync(string consumer, DateTime cutoffUtc, int batchSize,
        CancellationToken cancellationToken = default);
}
