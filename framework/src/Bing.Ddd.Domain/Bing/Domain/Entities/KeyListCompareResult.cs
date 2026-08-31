namespace Bing.Domain.Entities;

/// <summary>
/// 表示以新旧标识集合比较得到的创建、更新和删除分类结果。
/// </summary>
/// <typeparam name="TKey">参与比较的标识类型。</typeparam>
public class KeyListCompareResult<TKey>
{
    /// <summary>
    /// 获取仅存在于新标识集合中的标识，供创建操作使用。
    /// </summary>
    public List<TKey> CreateList { get; }

    /// <summary>
    /// 获取同时存在于新旧标识集合中的标识，供更新操作使用。
    /// </summary>
    public List<TKey> UpdateList { get; }

    /// <summary>
    /// 获取仅存在于旧标识集合中的标识，供删除操作使用。
    /// </summary>
    public List<TKey> DeleteList { get; }

    /// <summary>
    /// 使用标识比较分类结果初始化 <see cref="KeyListCompareResult{TKey}"/> 的实例。
    /// </summary>
    /// <param name="createList">仅存在于新标识集合中的标识列表。</param>
    /// <param name="updateList">同时存在于新旧标识集合中的标识列表。</param>
    /// <param name="deleteList">仅存在于旧标识集合中的标识列表。</param>
    public KeyListCompareResult(List<TKey> createList, List<TKey> updateList, List<TKey> deleteList)
    {
        CreateList = createList;
        UpdateList = updateList;
        DeleteList = deleteList;
    }
}