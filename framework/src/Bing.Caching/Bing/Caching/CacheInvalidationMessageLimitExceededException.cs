namespace Bing.Caching;

/// <summary>
/// 表示 Hybrid 失效消息载荷超过资源上限。
/// </summary>
public sealed class CacheInvalidationMessageLimitExceededException : InvalidOperationException
{
    /// <summary>
    /// 初始化一个 <see cref="CacheInvalidationMessageLimitExceededException"/> 类型的实例。
    /// </summary>
    /// <param name="limit">允许的单条消息序列化载荷字节数。</param>
    /// <param name="consumed">单条消息实际序列化载荷字节数。</param>
    public CacheInvalidationMessageLimitExceededException(long limit, long consumed)
        : base($"Hybrid 失效消息序列化载荷超过资源上限 {limit} 字节；单个物理键无法拆分。")
    {
        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit));
        if (consumed <= limit)
            throw new ArgumentOutOfRangeException(nameof(consumed));
        Limit = limit;
        Consumed = consumed;
    }

    /// <summary>
    /// 获取单条消息载荷字节上限。
    /// </summary>
    public long Limit { get; }

    /// <summary>
    /// 获取超限消息的实际载荷字节数。
    /// </summary>
    public long Consumed { get; }
}
