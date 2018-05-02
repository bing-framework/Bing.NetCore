using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Bing.Domains.Entities;
using Bing.Validations.Aspects;

namespace Bing.Datas.Stores.Operations
{
    /// <summary>
    /// 添加实体
    /// </summary>
    /// <typeparam name="TEntity">对象类型</typeparam>
    /// <typeparam name="TKey">对象标识类型</typeparam>
    public interface IAddAsync<in TEntity, in TKey> where TEntity : class, IKey<TKey>, IVersion
    {
        /// <summary>
        /// 添加实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <returns></returns>
        Task AddAsync([Valid] TEntity entity);

        /// <summary>
        /// 添加实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        /// <returns></returns>
        Task AddAsync(IEnumerable<TEntity> entities);
    }
}
