using Bing.Core.Data;

namespace Bing.Caching;

/// <summary>
/// 列表缓存
/// </summary>
/// <typeparam name="T">类型</typeparam>
public interface IListCache<T> : IReaderAll<T>
{
    /// <summary>
    /// 缓存键数量
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 添加。如果存在则不添加，返回false
    /// </summary>
    /// <param name="item">列表项</param>
    bool Add(T item);

    /// <summary>
    /// 移除。如果存在则删除并返回true，否则返回false
    /// </summary>
    /// <param name="item">列表项</param>
    bool Remove(T item);

    /// <summary>
    /// 判断是否存在
    /// </summary>
    /// <param name="item">列表项</param>
    bool Exists(T item);
}
