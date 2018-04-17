using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using Bing.Domains.Entities.Trees;
using Bing.Domains.Repositories;
using Bing.Utils.Extensions;

namespace Bing.Datas.Queries.Trees
{
    /// <summary>
    /// 树型查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public class TreeCriteria<TEntity> : TreeCriteria<TEntity,Guid,Guid?>
        where TEntity : ITreeEntity<TEntity, Guid, Guid?>
    {
        /// <summary>
        /// 初始化一个<see cref="TreeCriteria{TEntity}"/>类型的实例
        /// </summary>
        /// <param name="parameter">查询参数</param>
        public TreeCriteria(ITreeQueryParameter<Guid?> parameter) : base(parameter)
        {
        }
    }

    /// <summary>
    /// 树型查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    /// <typeparam name="TParentId">父标识类型</typeparam>
    public class TreeCriteria<TEntity,TKey,TParentId>:ICriteria<TEntity>where TEntity:ITreeEntity<TEntity,TKey,TParentId>
    {
        /// <summary>
        /// 初始化一个<see cref="TreeCriteria{TEntity,TKey,TParentId}"/>类型的实例
        /// </summary>
        /// <param name="parameter">查询参数</param>
        public TreeCriteria(ITreeQueryParameter<TParentId> parameter)
        {
            if (parameter.Level != null)
            {
                Predicate = Predicate.And(t => t.Level == parameter.Level);
            }

            if (!string.IsNullOrWhiteSpace(parameter.Path))
            {
                Predicate = Predicate.And(t => t.Path.StartsWith(parameter.Path));
            }

            if (parameter.Enabled != null)
            {
                Predicate = Predicate.And(t => t.Enabled == parameter.Enabled);
            }
        }

        /// <summary>
        /// 查询条件
        /// </summary>
        protected Expression<Func<TEntity, bool>> Predicate { get; set; }

        /// <summary>
        /// 获取查询条件
        /// </summary>
        /// <returns></returns>
        public Expression<Func<TEntity, bool>> GetPredicate()
        {
            return Predicate;
        }
    }
}
