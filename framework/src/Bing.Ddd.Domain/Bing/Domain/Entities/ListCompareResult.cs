namespace Bing.Domain.Entities;

/// <summary>
/// 表示以新旧实体集合比较得到的创建、更新和删除分类结果。
/// </summary>
/// <typeparam name="TEntity">参与比较的实体类型。</typeparam>
/// <typeparam name="TKey">实体标识类型。</typeparam>
public class ListCompareResult<TEntity, TKey> where TEntity : IKey<TKey>
{
    /// <summary>
    /// 获取仅存在于新实体集合中的实体，供创建操作使用。
    /// </summary>
    public List<TEntity> CreateList { get; }

    /// <summary>
    /// 获取同时存在于新旧实体集合中的新实体版本，供更新操作使用。
    /// </summary>
    public List<TEntity> UpdateList { get; }

    /// <summary>
    /// 获取仅存在于旧实体集合中的实体，供删除操作使用。
    /// </summary>
    public List<TEntity> DeleteList { get; }

    /// <summary>
    /// 使用实体比较分类结果初始化 <see cref="ListCompareResult{TEntity,TKey}"/> 的实例。
    /// </summary>
    /// <param name="createList">仅存在于新实体集合中的实体列表。</param>
    /// <param name="updateList">同时存在于新旧实体集合中的新实体列表。</param>
    /// <param name="deleteList">仅存在于旧实体集合中的实体列表。</param>
    public ListCompareResult(List<TEntity> createList, List<TEntity> updateList, List<TEntity> deleteList)
    {
        CreateList = createList;
        UpdateList = updateList;
        DeleteList = deleteList;
    }
}