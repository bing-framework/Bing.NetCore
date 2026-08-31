using Bing.Domain.Repositories;

namespace Bing.Trees;

/// <summary>
/// 树型仓储
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public interface ITreeCompactRepository<TEntity> : ITreeCompactRepository<TEntity, Guid, Guid?>
    where TEntity : class, ITreeEntity<TEntity, Guid, Guid?>
{
}

/// <summary>
/// 树型仓储
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">实体类型标识</typeparam>
/// <typeparam name="TParentId">父标识类型</typeparam>
public interface ITreeCompactRepository<TEntity, in TKey, in TParentId> : ICompactRepository<TEntity, TKey>
    where TEntity : class, ITreeEntity<TEntity, TKey, TParentId>
{
    /// <summary>
    /// 查找未跟踪单个实体
    /// </summary>
    /// <param name="id">标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步查找结果的任务；未找到时结果为 null。</returns>
    Task<TEntity> FindByIdNoTrackingAsync(TKey id, CancellationToken cancellationToken = default);

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
