using Bing.Core.Data;

namespace Bing.Caching;

/// <summary>
/// 定义基于项目相等性操作的列表缓存。
/// </summary>
/// <typeparam name="T">缓存项目的类型。</typeparam>
public interface IListCache<T> : IReaderAll<T>
{
    /// <summary>
    /// 获取当前缓存项目数量。
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 添加项目。
    /// </summary>
    /// <param name="item">要添加的项目。</param>
    /// <returns>项目不存在且已添加时返回 <c>true</c>；已存在时返回 <c>false</c>。</returns>
    bool Add(T item);

    /// <summary>
    /// 移除项目。
    /// </summary>
    /// <param name="item">要移除的项目。</param>
    /// <returns>项目存在且已移除时返回 <c>true</c>；不存在时返回 <c>false</c>。</returns>
    bool Remove(T item);

    /// <summary>
    /// 判断项目是否存在。
    /// </summary>
    /// <param name="item">要检查的项目。</param>
    /// <returns>项目存在时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    bool Exists(T item);
}
