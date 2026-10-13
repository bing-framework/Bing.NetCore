using System.Collections.ObjectModel;

namespace Bing.Caching;

/// <summary>
/// 指定缓存扫描资源上限的计量类型。
/// </summary>
public enum CacheScanLimitKind
{
    /// <summary>
    /// 按已接受的候选逻辑键数量计量。
    /// </summary>
    CandidateKeys,

    /// <summary>
    /// 按底层扫描或页流提供器返回的结果页数量计量。
    /// </summary>
    ScanPages
}

/// <summary>
/// 表示一次缓存扫描已达到调用方配置的候选键或扫描页资源上限。
/// </summary>
/// <remarks>
/// 该异常继承自 <see cref="InvalidOperationException"/>，保留旧的异常捕获兼容性。
/// 扫描可能已经产生部分结果；<see cref="Consumed"/> 表示触发限制前已经消耗的对应资源数量。
/// 当 <see cref="Kind"/> 为 <see cref="CacheScanLimitKind.CandidateKeys"/> 时，它是已经接受的候选逻辑键数量；
/// 当 <see cref="Kind"/> 为 <see cref="CacheScanLimitKind.ScanPages"/> 时，它是已经返回的扫描或页流结果页数量。
/// 客户端分片路径还会提供已经完成的分片和产生部分结果的分片索引。
/// </remarks>
public sealed class CacheScanLimitExceededException : InvalidOperationException
{
    /// <summary>
    /// 初始化一个 <see cref="CacheScanLimitExceededException"/> 类型的实例。
    /// </summary>
    /// <param name="limit">本次扫描允许消耗的资源上限。</param>
    /// <param name="consumed">触发异常前已经消耗的资源数量。</param>
    public CacheScanLimitExceededException(int limit, int consumed)
        : this(limit, consumed, 0, null)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="CacheScanLimitExceededException"/> 类型的实例。
    /// </summary>
    /// <param name="limit">本次扫描允许消耗的资源上限。</param>
    /// <param name="consumed">触发异常前已经消耗的资源数量。</param>
    /// <param name="completedShardCount">已完成分片的数量。</param>
    /// <param name="failedShard">失败的分片索引。</param>
    /// <param name="kind">资源计量类型。</param>
    internal CacheScanLimitExceededException(int limit, int consumed, int completedShardCount,
        int? failedShard, CacheScanLimitKind kind = CacheScanLimitKind.CandidateKeys)
        : this(limit, consumed, CreateCompletedShards(completedShardCount), failedShard, kind)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="CacheScanLimitExceededException"/> 类型的实例。
    /// </summary>
    /// <param name="limit">本次扫描允许消耗的资源上限。</param>
    /// <param name="consumed">触发异常前已经消耗的资源数量。</param>
    /// <param name="completedShards">已完成的分片索引。</param>
    /// <param name="failedShard">失败的分片索引。</param>
    /// <param name="kind">资源计量类型。</param>
    internal CacheScanLimitExceededException(int limit, int consumed, IReadOnlyList<int> completedShards,
        int? failedShard, CacheScanLimitKind kind = CacheScanLimitKind.CandidateKeys)
        : base(CreateMessage(limit, kind))
    {
        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit));
        if (consumed < 0 || consumed > limit)
            throw new ArgumentOutOfRangeException(nameof(consumed));
        if (failedShard.HasValue && failedShard.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(failedShard));
        if (!Enum.IsDefined(typeof(CacheScanLimitKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (completedShards == null)
            throw new ArgumentNullException(nameof(completedShards));

        Limit = limit;
        Consumed = consumed;
        var completed = new List<int>(completedShards.Count);
        for (var index = 0; index < completedShards.Count; index++)
        {
            var shard = completedShards[index];
            if (shard < 0 || completed.Contains(shard))
                throw new ArgumentOutOfRangeException(nameof(completedShards));
            completed.Add(shard);
        }
        completed.Sort();
        CompletedShards = new ReadOnlyCollection<int>(completed);
        FailedShard = failedShard;
        Kind = kind;
    }

    /// <summary>
    /// 根据已完成分片数量创建连续分片索引。
    /// </summary>
    /// <param name="count">已完成分片的数量。</param>
    /// <returns>从零开始的连续分片索引数组。</returns>
    private static int[] CreateCompletedShards(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        var result = new int[count];
        for (var index = 0; index < result.Length; index++)
            result[index] = index;
        return result;
    }

    /// <summary>
    /// 获取本次扫描允许消耗的资源上限。
    /// </summary>
    /// <remarks>资源含义由 <see cref="Kind"/> 决定。</remarks>
    public int Limit { get; }

    /// <summary>
    /// 获取触发异常前已消耗的资源数量。
    /// </summary>
    /// <remarks>资源含义由 <see cref="Kind"/> 决定。</remarks>
    public int Consumed { get; }

    /// <summary>
    /// 获取触发异常的资源计量类型。
    /// </summary>
    public CacheScanLimitKind Kind { get; }

    /// <summary>
    /// 获取已完整完成的分片索引。
    /// </summary>
    /// <remarks>索引按升序排列；并发扫描时不表示完成时序，非分片扫描时为空集合。</remarks>
    public IReadOnlyList<int> CompletedShards { get; }

    /// <summary>
    /// 获取触发资源上限的分片索引。
    /// </summary>
    /// <remarks>非分片扫描时为 <see langword="null"/>；该分片可能已完成部分操作。</remarks>
    public int? FailedShard { get; }

    /// <summary>
    /// 创建扫描资源上限异常的消息。
    /// </summary>
    /// <param name="limit">资源数量上限。</param>
    /// <param name="kind">资源计量类型。</param>
    /// <returns>异常消息。</returns>
    private static string CreateMessage(int limit, CacheScanLimitKind kind)
    {
        return kind == CacheScanLimitKind.ScanPages
            ? $"缓存扫描页数超过资源上限 {limit}。请缩小前缀或提高 MaxScanPages。"
            : $"缓存扫描候选键数超过资源上限 {limit}。请缩小前缀或提高 MaxScanKeys。";
    }
}
