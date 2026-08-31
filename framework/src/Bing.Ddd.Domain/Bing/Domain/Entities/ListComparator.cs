namespace Bing.Domain.Entities;

/// <summary>
/// 根据实体标识比较新旧实体集合。
/// </summary>
/// <typeparam name="TEntity">参与比较的实体类型。</typeparam>
/// <typeparam name="TKey">实体标识类型。</typeparam>
public class ListComparator<TEntity, TKey> where TEntity : IKey<TKey>
{
    /// <summary>
    /// 比较新旧实体集合，并按创建、更新和删除操作分类。
    /// </summary>
    /// <param name="newList">表示期望状态的新实体集合。</param>
    /// <param name="oldList">表示当前状态的旧实体集合。</param>
    /// <returns>包含创建、更新和删除实体分类的比较结果。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="newList"/> 或 <paramref name="oldList"/> 为 <c>null</c> 时引发。</exception>
    public Domain.Entities.ListCompareResult<TEntity, TKey> Compare(IEnumerable<TEntity> newList, IEnumerable<TEntity> oldList)
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
        return new ListCompareResult<TEntity, TKey>(createList, updateList, deleteList);
    }

    /// <summary>
    /// 获取仅存在于新实体集合中的实体。
    /// </summary>
    /// <param name="newList">新实体列表。</param>
    /// <param name="oldList">旧实体列表。</param>
    /// <returns>应创建的实体列表。</returns>
    private List<TEntity> GetCreateList(List<TEntity> newList, List<TEntity> oldList) => newList.Except(oldList).ToList();

    /// <summary>
    /// 获取同时存在于新旧实体集合中的新实体版本。
    /// </summary>
    /// <param name="newList">新实体列表。</param>
    /// <param name="oldList">旧实体列表。</param>
    /// <returns>应更新的实体列表。</returns>
    private List<TEntity> GetUpdateList(List<TEntity> newList, List<TEntity> oldList) => newList.FindAll(entity => oldList.Exists(t => t.Id.Equals(entity.Id)));

    /// <summary>
    /// 获取仅存在于旧实体集合中的实体。
    /// </summary>
    /// <param name="newList">新实体列表。</param>
    /// <param name="oldList">旧实体列表。</param>
    /// <returns>应删除的实体列表。</returns>
    private List<TEntity> GetDeleteList(List<TEntity> newList, List<TEntity> oldList) => oldList.Except(newList).ToList();
}