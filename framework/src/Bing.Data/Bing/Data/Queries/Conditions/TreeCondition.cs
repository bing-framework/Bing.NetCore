using System;
using System.Linq.Expressions;
using Bing.Expressions;
using Bing.Trees;

namespace Bing.Data.Queries.Conditions
{
    /// <summary>
    /// 树型查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public class TreeCondition<TEntity> : TreeCondition<TEntity, Guid?>
        where TEntity : IPath, IEnabled, IParentId<Guid?>
    {
        /// <summary>
        /// 初始化一个<see cref="TreeCondition{TEntity}"/>类型的实例
        /// </summary>
        /// <param name="parameter">查询参数</param>
        public TreeCondition(ITreeQueryParameter<Guid?> parameter) : base(parameter)
        {
        }
    }

    /// <summary>
    /// 树型查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TParentId">父标识类型</typeparam>
    public class TreeCondition<TEntity, TParentId> : ICondition<TEntity>
        where TEntity : IPath, IEnabled
    {
        /// <summary>
        /// 初始化一个<see cref="TreeCondition{TEntity,TParentId}"/>类型的实例
        /// </summary>
        /// <param name="parameter">查询参数</param>
        public TreeCondition(ITreeQueryParameter<TParentId> parameter)
        {
            if (parameter == null)
                return;
            if (!string.IsNullOrWhiteSpace(parameter.Path))
                Condition = Condition.And(t => t.Path.StartsWith(parameter.Path));
            if (parameter.Level != null)
                Condition = Condition.And(t => t.Level == parameter.Level);
            if (parameter.Enabled != null)
                Condition = Condition.And(t => t.Enabled == parameter.Enabled);
        }

        /// <summary>
        /// 查询条件
        /// </summary>
        protected Expression<Func<TEntity, bool>> Condition { get; set; }

        /// <summary>
        /// 获取查询条件
        /// </summary>
        public Expression<Func<TEntity, bool>> GetCondition() => Condition;
    }
}
