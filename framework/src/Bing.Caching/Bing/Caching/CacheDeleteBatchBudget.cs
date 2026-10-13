using System.Text;

namespace Bing.Caching;

/// <summary>
/// 为 Redis 删除命令按参数 UTF-8 字节数和键数量进行有界切分。
/// </summary>
internal static class CacheDeleteBatchBudget
{
    /// <summary>
    /// 标识由本类型创建的删除命令超限异常。
    /// </summary>
    private const string LimitMarker = "Bing.Caching.CacheDeleteCommandLimit";

    /// <summary>
    /// 物理键对应的租约键后缀。
    /// </summary>
    private const string LeaseSuffix = ":lease";

    /// <summary>
    /// 租约后缀的 UTF-8 字节数。
    /// </summary>
    private static readonly int LeaseSuffixBytes = Encoding.UTF8.GetByteCount(LeaseSuffix);

    /// <summary>
    /// 按物理数据键切分删除批次。每个数据键会同时删除对应租约键。
    /// </summary>
    /// <param name="physicalKeys">待删除的物理数据键。</param>
    /// <param name="maxBatchSize">每批允许的最大数据键数量。</param>
    /// <param name="maxCommandBytes">每批命令参数允许的最大 UTF-8 字节数。</param>
    /// <returns>按原顺序产生的有界物理键批次。</returns>
    internal static IEnumerable<IReadOnlyList<string>> Partition(
        IReadOnlyList<string> physicalKeys, int maxBatchSize, long maxCommandBytes)
    {
        if (physicalKeys == null)
            throw new ArgumentNullException(nameof(physicalKeys));
        if (maxBatchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxBatchSize));
        if (maxCommandBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCommandBytes));

        var batch = new List<string>(Math.Min(maxBatchSize, physicalKeys.Count));
        long bytes = 0;
        for (var index = 0; index < physicalKeys.Count; index++)
        {
            var physicalKey = physicalKeys[index];
            var itemBytes = GetPairBytes(physicalKey);
            if (itemBytes > maxCommandBytes)
                throw CreateLimitException(itemBytes, maxCommandBytes, physicalKey);

            if (batch.Count > 0 &&
                (batch.Count >= maxBatchSize || itemBytes > maxCommandBytes - bytes))
            {
                yield return batch;
                batch = new List<string>(Math.Min(maxBatchSize, physicalKeys.Count - index));
                bytes = 0;
            }

            batch.Add(physicalKey);
            bytes += itemBytes;
        }

        if (batch.Count > 0)
            yield return batch;
    }

    /// <summary>
    /// 计算一个数据键及其租约键参数的 UTF-8 字节数。
    /// </summary>
    /// <param name="physicalKey">物理数据键。</param>
    /// <returns>数据键和对应租约键合计的 UTF-8 字节数。</returns>
    internal static long GetPairBytes(string physicalKey)
    {
        if (physicalKey == null)
            throw new ArgumentNullException(nameof(physicalKey));
        var keyBytes = Encoding.UTF8.GetByteCount(physicalKey);
        checked
        {
            return (long)keyBytes * 2 + LeaseSuffixBytes;
        }
    }

    /// <summary>
    /// 判断异常是否由本类型的删除命令预算检查创建。
    /// </summary>
    /// <param name="exception">待检查的异常。</param>
    /// <returns>异常包含本类型标记时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    internal static bool IsLimit(Exception exception) =>
        exception is InvalidOperationException &&
        exception.Data[LimitMarker] is bool marked && marked;

    /// <summary>
    /// 创建带有删除命令预算超限标记的异常。
    /// </summary>
    /// <param name="actualBytes">当前键对需要的字节数。</param>
    /// <param name="limit">允许的最大命令字节数。</param>
    /// <param name="physicalKey">导致单键超限的物理键。</param>
    /// <returns>描述删除命令超限的异常实例。</returns>
    private static InvalidOperationException CreateLimitException(long actualBytes, long limit,
        string physicalKey)
    {
        var exception = new InvalidOperationException(
            $"Redis 删除命令参数超过 UTF-8 字节预算（键对 {actualBytes} 字节，上限 {limit} 字节）。");
        exception.Data[LimitMarker] = true;
        exception.Data["PhysicalKey"] = physicalKey;
        return exception;
    }
}
