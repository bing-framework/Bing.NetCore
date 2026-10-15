using System.Data.Common;

namespace Bing.Events.Consumption;

/// <summary>
/// 在调用低层消费收据存储前执行清理授权。
/// </summary>
public sealed class EventConsumptionRetentionService : IEventConsumptionRetentionService
{
    /// <summary>
    /// 单次收据清理允许的最大批量。
    /// </summary>
    private const int MaxBatchSize = 10000;

    /// <summary>
    /// 低层收据保留存储。
    /// </summary>
    private readonly IEventConsumptionRetentionStore _store;

    /// <summary>
    /// 当前调用方的清理授权器。
    /// </summary>
    private readonly IEventConsumptionRetentionAuthorizer _authorizer;

    /// <summary>
    /// 初始化一个 <see cref="EventConsumptionRetentionService" /> 类型的实例。
    /// </summary>
    /// <param name="store">低层收据保留存储。</param>
    /// <param name="authorizer">清理授权器。</param>
    public EventConsumptionRetentionService(IEventConsumptionRetentionStore store,
        IEventConsumptionRetentionAuthorizer authorizer)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
    }

    /// <inheritdoc />
    public async Task<int> DeleteBeforeAsync(DbTransaction transaction, string consumer, DateTime cutoffUtc,
        int batchSize, CancellationToken cancellationToken = default)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        ValidateRequest(consumer, cutoffUtc, batchSize);
        cancellationToken.ThrowIfCancellationRequested();
        await _authorizer.AuthorizeAsync(consumer, cutoffUtc, batchSize, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await _store.DeleteBeforeAsync(transaction, consumer, cutoffUtc, batchSize, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 校验不会产生外部副作用的请求参数。
    /// </summary>
    private static void ValidateRequest(string consumer, DateTime cutoffUtc, int batchSize)
    {
        if (string.IsNullOrWhiteSpace(consumer))
            throw new ArgumentException("消费标识不能为空。", nameof(consumer));
        if (consumer.Length > 200)
            throw new ArgumentOutOfRangeException(nameof(consumer), "消费标识不能超过 200 个字符。");
        if (cutoffUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("留存截止时间必须使用 UTC。", nameof(cutoffUtc));
        if (batchSize <= 0 || batchSize > MaxBatchSize)
            throw new ArgumentOutOfRangeException(nameof(batchSize),
                $"清理批量必须位于 1 到 {MaxBatchSize} 之间。");
    }
}
