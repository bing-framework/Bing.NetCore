using System;
using Bing.Domains.Entities.Trees;

namespace Bing.Domains.Repositories
{
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
    public interface ITreeRepository<TEntity,in TKey,in TParentId>:IRepository<TEntity,TKey>,ITreeCompactRepository<TEntity,TKey,TParentId> where TEntity : class, ITreeEntity<TEntity, TKey, TParentId>
    {
    }
}
