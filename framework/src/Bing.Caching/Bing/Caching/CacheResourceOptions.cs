namespace Bing.Caching;

/// <summary>
/// 定义缓存回源、批量操作和扫描的资源上限。
/// </summary>
public sealed class CacheResourceOptions
{
    /// <summary>
    /// 初始化一个 <see cref="CacheResourceOptions"/> 类型的实例。
    /// </summary>
    /// <param name="maxConcurrentFactories">并发回源数上限。</param>
    /// <param name="maxQueuedFactories">等待回源的队列长度上限。</param>
    /// <param name="maxBatchPayloadBytes">单批读取响应载荷字节上限。</param>
    /// <param name="maxScanKeys">单次扫描候选键数上限。</param>
    /// <param name="maxBatchCommandBytes">单条批量命令参数字节上限。</param>
    /// <param name="maxScanPages">单次扫描结果页数上限。</param>
    /// <param name="maxInvalidationMessageBytes">Hybrid 失效消息载荷字节上限。</param>
    /// <remarks>超过配置上限时明确抛出异常，不会静默丢弃缓存值。</remarks>
    public CacheResourceOptions(int maxConcurrentFactories = 8, int maxQueuedFactories = 128,
        long maxBatchPayloadBytes = 8 * 1024 * 1024, int maxScanKeys = 0,
        long maxBatchCommandBytes = 8 * 1024 * 1024, int maxScanPages = 0,
        long maxInvalidationMessageBytes = 0)
    {
        if (maxConcurrentFactories <= 0 || maxQueuedFactories < 0 || maxBatchPayloadBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentFactories), "缓存资源预算无效。");
        if (maxBatchCommandBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxBatchCommandBytes), "删除命令字节预算必须大于零。");
        if (maxScanKeys < 0)
            throw new ArgumentOutOfRangeException(nameof(maxScanKeys), "缓存扫描键数上限不能为负数。");
        if (maxScanPages < 0)
            throw new ArgumentOutOfRangeException(nameof(maxScanPages), "缓存扫描页数上限不能为负数。");
        if (maxInvalidationMessageBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(maxInvalidationMessageBytes),
                "Hybrid 失效消息字节上限不能为负数。");
        MaxConcurrentFactories = maxConcurrentFactories;
        MaxQueuedFactories = maxQueuedFactories;
        MaxBatchPayloadBytes = maxBatchPayloadBytes;
        MaxBatchCommandBytes = maxBatchCommandBytes;
        MaxScanKeys = maxScanKeys;
        MaxScanPages = maxScanPages;
        MaxInvalidationMessageBytes = maxInvalidationMessageBytes;
    }
    /// <summary>
    /// 获取并发回源数上限。
    /// </summary>
    public int MaxConcurrentFactories { get; }
    /// <summary>
    /// 获取回源等待队列长度上限。
    /// </summary>
    public int MaxQueuedFactories { get; }
    /// <summary>
    /// 获取单批读取响应载荷字节上限。
    /// </summary>
    /// <remarks>不包括协议和解码后对象；单个值超过上限时抛出异常。</remarks>
    public long MaxBatchPayloadBytes { get; }

    /// <summary>
    /// 获取单条批量命令参数字节上限。
    /// </summary>
    /// <remarks>计入数据键及对应的 <c>:lease</c> 键，不包括 Redis 协议帧；单个键对超限时抛出异常。</remarks>
    public long MaxBatchCommandBytes { get; }

    /// <summary>
    /// 获取单次扫描候选键数上限。
    /// </summary>
    /// <remarks>0 表示不限制；该上限不代表 Redis SCAN 的全库遍历成本上限。</remarks>
    public int MaxScanKeys { get; }

    /// <summary>
    /// 获取单次扫描结果页数上限。
    /// </summary>
    /// <remarks>0 表示不限制；只有能报告扫描页边界的 Provider 才能执行此限制。</remarks>
    public int MaxScanPages { get; }

    /// <summary>
    /// 获取 Hybrid 失效消息载荷字节上限。
    /// </summary>
    /// <remarks>0 表示不限制；单个键无法容纳时明确失败。</remarks>
    public long MaxInvalidationMessageBytes { get; }
}
