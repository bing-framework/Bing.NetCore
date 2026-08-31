using Bing.Domain.Repositories;

namespace Bing.Trees;

/// <summary>
/// 树型仓储
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public interface ITreeRepository<TEntity> : ITreeRepository<TEntity, Guid, Guid?>
    where TEntity : class, ITreeEntity<TEntity, Guid, Guid?>
{
}

/// <summary>
/// 树型仓储
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">实体标识类型</typeparam>
/// <typeparam name="TParentId">父标识类型</typeparam>
public interface ITreeRepository<TEntity, in TKey, in TParentId> : IRepository<TEntity, TKey>
    where TEntity : class, ITreeEntity<TEntity, TKey, TParentId>
{
    /// <summary>
    /// 生成排序号
    /// </summary>
    /// <param name="parentId">父标识</param>
    /// <returns>表示生成排序号结果的异步操作。</returns>
    Task<int> GenerateSortIdAsync(TParentId parentId);

    /// <summary>
    /// 获取全部下级实体
    /// </summary>
    /// <param name="parent">父实体</param>
    /// <returns>表示获取全部下级实体结果的异步操作。</returns>
    Task<List<TEntity>> GetAllChildrenAsync(TEntity parent);
}
