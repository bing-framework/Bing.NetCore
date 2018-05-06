using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bing.Datas.Stores.Operations;
using Bing.Dependency;
using Bing.Domains.Entities;
using Bing.Validations.Aspects;

namespace Bing.Domains.Repositories
{
    /// <summary>
    /// 仓储
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    public interface ICompactRepository<TEntity> : ICompactRepository<TEntity, Guid>
        where TEntity : class, IAggregateRoot, IKey<Guid>, IVersion
    {
    }

    /// <summary>
    /// 仓储
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    public interface ICompactRepository<TEntity, in TKey>: IScopeDependency,
        IFindById<TEntity,TKey>,IFindByIdAsync<TEntity,TKey>,
        IFindByIds<TEntity,TKey>,IFindByIdsAsync<TEntity,TKey>,
        IExists<TEntity,TKey>,IExistsAsync<TEntity,TKey>,
        IAdd<TEntity,TKey>,IAddAsync<TEntity,TKey>,
        IUpdate<TEntity,TKey>,IUpdateAsync<TEntity,TKey>,
        IRemove<TEntity,TKey>,IRemoveAsync<TEntity,TKey> 
        where TEntity : class, IAggregateRoot,IKey<TKey>,IVersion
    {        
    }
}
