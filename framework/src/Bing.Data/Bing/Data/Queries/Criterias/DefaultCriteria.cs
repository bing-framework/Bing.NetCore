using System;
using System.Linq.Expressions;

namespace Bing.Data.Queries.Criterias
{
    /// <summary>
    /// 默认查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public class DefaultCriteria<TEntity> : ICriteria<TEntity> where TEntity : class
    {
        /// <summary>
        /// 初始化一个<see cref="DefaultCriteria{TEntity}"/>类型的实例
        /// </summary>
        /// <param name="predicate">查询条件</param>
        public DefaultCriteria(Expression<Func<TEntity, bool>> predicate) => Predicate = predicate;

        /// <summary>
        /// 查询条件
        /// </summary>
        protected Expression<Func<TEntity, bool>> Predicate { get; set; }

        /// <summary>
        /// 获取查询条件
        /// </summary>
        public virtual Expression<Func<TEntity, bool>> GetPredicate() => Predicate;
    }
}
