using System;
using System.Linq.Expressions;
using Bing.Domains.Entities.Trees;
using Bing.Domains.Repositories;
using Bing.Expressions;

namespace Bing.Datas.Queries.Trees
{
    /// <summary>
    /// 树型查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public class TreeCriteria<TEntity> : TreeCriteria<TEntity, Guid?>
        where TEntity : IPath, IEnabled, IParentId<Guid?>
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
    /// <typeparam name="TParentId">父标识类型</typeparam>
    public class TreeCriteria<TEntity, TParentId> : ICriteria<TEntity>
        where TEntity : IPath, IEnabled
    {
        /// <summary>
        /// 初始化一个<see cref="TreeCriteria{TEntity,TParentId}"/>类型的实例
        /// </summary>
        /// <param name="parameter">查询参数</param>
        public TreeCriteria(ITreeQueryParameter<TParentId> parameter)
        {
            if (!string.IsNullOrWhiteSpace(parameter.Path))
                Predicate = Predicate.And(t => t.Path.StartsWith(parameter.Path));
            if (parameter.Level != null)
                Predicate = Predicate.And(t => t.Level == parameter.Level);
            if (parameter.Enabled != null)
                Predicate = Predicate.And(t => t.Enabled == parameter.Enabled);
        }

        /// <summary>
        /// 查询条件
        /// </summary>
        protected Expression<Func<TEntity, bool>> Predicate { get; set; }

        /// <summary>
        /// 获取查询条件
        /// </summary>
        public Expression<Func<TEntity, bool>> GetPredicate() => Predicate;
    }
}
