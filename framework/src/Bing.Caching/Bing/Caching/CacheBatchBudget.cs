using System.Text;

namespace Bing.Caching;

/// <summary>
/// 校验缓存批量读取的原始响应字节数。
/// </summary>
/// <remarks>
/// 校验发生在响应到达后、解码或拼接前，不限制网络接收缓冲。
/// </remarks>
internal static class CacheBatchBudget
{
    /// <summary>
    /// 标识由本类型创建的负载超限异常。
    /// </summary>
    private const string PayloadLimitMarker = "Bing.Caching.CacheBatchPayloadLimit";

    /// <summary>
    /// 累计批量响应大小并检查预算。
    /// </summary>
    /// <param name="values">待检查的响应字节数组。</param>
    /// <param name="limit">允许的最大总字节数。</param>
    internal static void Check(IEnumerable<byte[]> values, long limit)
    {
        long total = 0;
        foreach (var value in values) Add(ref total, value?.LongLength ?? 0, limit);
    }

    /// <summary>
    /// 累计批量响应大小并检查预算。
    /// </summary>
    /// <typeparam name="T">响应集合的元素类型。</typeparam>
    /// <param name="values">待检查的响应元素集合；为 <see langword="null"/> 时不执行检查。</param>
    /// <param name="selector">从响应元素中提取原始字节数组的函数。</param>
    /// <param name="limit">允许的最大总字节数。</param>
    internal static void Check<T>(IList<T> values, Func<T, byte[]> selector, long limit)
    {
        if (values == null)
            return;
        long total = 0;
        for (var index = 0; index < values.Count; index++)
            Add(ref total, selector(values[index])?.LongLength ?? 0, limit);
    }
    /// <summary>
    /// 累计批量响应大小并检查预算。
    /// </summary>
    /// <param name="values">待检查的字符串响应集合。</param>
    /// <param name="limit">允许的最大总字节数。</param>
    internal static void Check(IEnumerable<string> values, long limit)
    {
        long total = 0;
        foreach (var value in values) Add(ref total, value == null ? 0 : Encoding.UTF8.GetByteCount(value), limit);
    }
    /// <summary>
    /// 将一段响应长度加入累计值并检查是否超出预算。
    /// </summary>
    /// <param name="total">当前累计字节数。</param>
    /// <param name="length">本次加入的字节数。</param>
    /// <param name="limit">允许的最大总字节数。</param>
    private static void Add(ref long total, long length, long limit)
    {
        if (length > limit - total)
            throw CreateLimitException(total + length, limit);
        total += length;
    }

    /// <summary>
    /// 创建带有缓存负载超限标记的异常。
    /// </summary>
    /// <param name="actualBytes">实际响应字节数。</param>
    /// <param name="limit">允许的最大字节数。</param>
    /// <returns>描述本次负载超限的异常实例。</returns>
    internal static InvalidOperationException CreateLimitException(long actualBytes, long limit)
    {
        var exception = new InvalidOperationException(
            $"Redis 响应批次超过缓存原始负载字节预算（实际 {actualBytes} 字节，上限 {limit} 字节）。");
        exception.Data[PayloadLimitMarker] = true;
        return exception;
    }

    /// <summary>
    /// 判断异常是否由本类型的负载预算检查创建。
    /// </summary>
    /// <param name="exception">待检查的异常。</param>
    /// <returns>异常包含本类型标记时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    internal static bool IsPayloadLimit(Exception exception) =>
        exception is InvalidOperationException &&
        exception.Data[PayloadLimitMarker] is bool marked && marked;
}
