using Bing.Domain.Entities;

// ReSharper disable once CheckNamespace
namespace Bing;

/// <summary>
/// 提供领域实体和实体标识集合的比较扩展。
/// </summary>
public static partial class DomainExtensions
{
    /// <summary>
    /// 比较以 <see cref="Guid"/> 为标识的实体集合。
    /// </summary>
    /// <typeparam name="TEntity">实现 <see cref="IKey{TKey}"/> 的实体类型。</typeparam>
    /// <param name="newList">表示期望状态的新实体集合。</param>
    /// <param name="oldList">表示当前状态的旧实体集合。</param>
    /// <returns>按创建、更新和删除分类的实体比较结果。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="newList"/> 或 <paramref name="oldList"/> 为 <c>null</c> 时抛出。</exception>
    public static ListCompareResult<TEntity, Guid> Compare<TEntity>(this IEnumerable<TEntity> newList, IEnumerable<TEntity> oldList)
        where TEntity : IKey<Guid> =>
        Compare<TEntity, Guid>(newList, oldList);

    /// <summary>
    /// 比较指定标识类型的实体集合。
    /// </summary>
    /// <typeparam name="TEntity">实现 <see cref="IKey{TKey}"/> 的实体类型。</typeparam>
    /// <typeparam name="TKey">实体标识类型。</typeparam>
    /// <param name="newList">表示期望状态的新实体集合。</param>
    /// <param name="oldList">表示当前状态的旧实体集合。</param>
    /// <returns>按创建、更新和删除分类的实体比较结果。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="newList"/> 或 <paramref name="oldList"/> 为 <c>null</c> 时抛出。</exception>
    public static ListCompareResult<TEntity, TKey> Compare<TEntity, TKey>(this IEnumerable<TEntity> newList, IEnumerable<TEntity> oldList)
        where TEntity : IKey<TKey>
    {
        var comparator = new ListComparator<TEntity, TKey>();
        return comparator.Compare(newList, oldList);
    }

    /// <summary>
    /// 比较 <see cref="Guid"/> 标识集合。
    /// </summary>
    /// <param name="newList">表示期望状态的新标识集合。</param>
    /// <param name="oldList">表示当前状态的旧标识集合。</param>
    /// <returns>按创建、更新和删除分类的标识比较结果。</returns>
    public static KeyListCompareResult<Guid> Compare(this IEnumerable<Guid> newList, IEnumerable<Guid> oldList) =>
        CompareKey(newList, oldList);

    /// <summary>
    /// 比较字符串标识集合。
    /// </summary>
    /// <param name="newList">表示期望状态的新标识集合。</param>
    /// <param name="oldList">表示当前状态的旧标识集合。</param>
    /// <returns>按创建、更新和删除分类的标识比较结果。</returns>
    public static KeyListCompareResult<string> Compare(this IEnumerable<string> newList, IEnumerable<string> oldList) =>
        CompareKey(newList, oldList);

    /// <summary>
    /// 比较 <see cref="int"/> 标识集合。
    /// </summary>
    /// <param name="newList">表示期望状态的新标识集合。</param>
    /// <param name="oldList">表示当前状态的旧标识集合。</param>
    /// <returns>按创建、更新和删除分类的标识比较结果。</returns>
    public static KeyListCompareResult<int> Compare(this IEnumerable<int> newList, IEnumerable<int> oldList) =>
        CompareKey(newList, oldList);

    /// <summary>
    /// 比较 <see cref="long"/> 标识集合。
    /// </summary>
    /// <param name="newList">表示期望状态的新标识集合。</param>
    /// <param name="oldList">表示当前状态的旧标识集合。</param>
    /// <returns>按创建、更新和删除分类的标识比较结果。</returns>
    public static KeyListCompareResult<long> Compare(this IEnumerable<long> newList, IEnumerable<long> oldList) =>
        CompareKey(newList, oldList);

    /// <summary>
    /// 使用键比较器比较标识集合。
    /// </summary>
    /// <typeparam name="TKey">标识类型。</typeparam>
    /// <param name="newList">表示期望状态的新标识集合。</param>
    /// <param name="oldList">表示当前状态的旧标识集合。</param>
    /// <returns>按创建、更新和删除分类的标识比较结果。</returns>
    private static KeyListCompareResult<TKey> CompareKey<TKey>(IEnumerable<TKey> newList, IEnumerable<TKey> oldList)
    {
        var comparator = new KeyListComparator<TKey>();
        return comparator.Compare(newList, oldList);
    }
}