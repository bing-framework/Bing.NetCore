using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Bing.Datas.Stores.Operations;
using Bing.Domains.Entities.Trees;

namespace Bing.Domains.Repositories
{
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
    public interface ITreeCompactRepository<TEntity,in TKey,in TParentId>:ICompactRepository<TEntity,TKey>,IFindByIdNoTrackingAsync<TEntity,TKey> where TEntity:class,ITreeEntity<TEntity,TKey,TParentId>
    {
        /// <summary>
        /// 生成排序号
        /// </summary>
        /// <param name="parentId">父标识</param>
        /// <returns></returns>
        Task<int> GenerateSortIdAsync(TParentId parentId);

        /// <summary>
        /// 获取全部下级实体
        /// </summary>
        /// <param name="parent">父实体</param>
        /// <returns></returns>
        Task<List<TEntity>> GetAllChildrenAsync(TEntity parent);
    }
}
