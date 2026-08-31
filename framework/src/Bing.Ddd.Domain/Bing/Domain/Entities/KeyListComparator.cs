namespace Bing.Domain.Entities;

/// <summary>
/// 比较新旧标识集合。
/// </summary>
/// <typeparam name="TKey">参与比较的标识类型。</typeparam>
public class KeyListComparator<TKey>
{
    /// <summary>
    /// 比较新旧标识集合，并按创建、更新和删除操作分类。
    /// </summary>
    /// <param name="newList">表示期望状态的新标识集合。</param>
    /// <param name="oldList">表示当前状态的旧标识集合。</param>
    /// <returns>包含创建、更新和删除标识分类的比较结果。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="newList"/> 或 <paramref name="oldList"/> 为 <c>null</c> 时引发。</exception>
    public KeyListCompareResult<TKey> Compare(IEnumerable<TKey> newList, IEnumerable<TKey> oldList)
    {
        if (newList == null)
            throw new ArgumentNullException(nameof(newList));
        if (oldList == null)
            throw new ArgumentNullException(nameof(oldList));
        var newEntities = newList.ToList();
        var oldEntities = oldList.ToList();
        var createList = GetCreateList(newEntities, oldEntities);
        var updateList = GetUpdateList(newEntities, oldEntities);
        var deleteList = GetDeleteList(newEntities, oldEntities);
        return new KeyListCompareResult<TKey>(createList, updateList, deleteList);
    }

    /// <summary>
    /// 获取仅存在于新标识集合中的标识。
    /// </summary>
    /// <param name="newList">新标识列表。</param>
    /// <param name="oldList">旧标识列表。</param>
    /// <returns>应创建的标识列表。</returns>
    private List<TKey> GetCreateList(List<TKey> newList, List<TKey> oldList) => newList.Except(oldList).ToList();

    /// <summary>
    /// 获取同时存在于新旧标识集合中的标识。
    /// </summary>
    /// <param name="newList">新标识列表。</param>
    /// <param name="oldList">旧标识列表。</param>
    /// <returns>应更新的标识列表。</returns>
    private List<TKey> GetUpdateList(List<TKey> newList, List<TKey> oldList) => newList.FindAll(id => oldList.Exists(t => t.Equals(id)));

    /// <summary>
    /// 获取仅存在于旧标识集合中的标识。
    /// </summary>
    /// <param name="newList">新标识列表。</param>
    /// <param name="oldList">旧标识列表。</param>
    /// <returns>应删除的标识列表。</returns>
    private List<TKey> GetDeleteList(List<TKey> newList, List<TKey> oldList) => oldList.Except(newList).ToList();
}