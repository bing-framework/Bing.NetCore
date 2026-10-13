namespace Bing.Caching;

/// <summary>
/// 提供缓存集合输入的容量探测辅助方法。
/// </summary>
internal static class CacheCollectionCount
{
    /// <summary>
    /// 尝试读取集合已有的元素数量，不枚举输入。
    /// </summary>
    /// <typeparam name="T">集合元素类型。</typeparam>
    /// <param name="source">集合输入。</param>
    /// <param name="count">集合元素数量。</param>
    /// <returns>输入提供可靠元素数量时返回 <see langword="true"/> 并设置 <paramref name="count"/>；否则返回 <see langword="false"/> 并将其设为 0。</returns>
    internal static bool TryGetCount<T>(IEnumerable<T> source, out int count)
    {
        if (source is ICollection<T> collection)
        {
            count = collection.Count;
            return true;
        }

        if (source is IReadOnlyCollection<T> readOnlyCollection)
        {
            count = readOnlyCollection.Count;
            return true;
        }

        if (source is System.Collections.ICollection nonGenericCollection)
        {
            count = nonGenericCollection.Count;
            return true;
        }

        count = 0;
        return false;
    }
}
