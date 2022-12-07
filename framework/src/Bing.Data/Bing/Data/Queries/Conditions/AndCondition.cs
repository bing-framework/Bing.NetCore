using System;
using System.Linq.Expressions;
using Bing.Expressions;

namespace Bing.Data.Queries.Conditions;

/// <summary>
/// 与查询条件
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public class AndCondition<TEntity> : ICondition<TEntity> where TEntity : class
{
    /// <summary>
    /// 初始化一个<see cref="AndCondition{TEntity}"/>类型的实例
    /// </summary>
    /// <param name="left">查询条件1</param>
    /// <param name="right">查询条件2</param>
    public AndCondition(Expression<Func<TEntity, bool>> left, Expression<Func<TEntity, bool>> right) => Predicate = left.And(right);

    /// <summary>
    /// 查询条件
    /// </summary>
    protected Expression<Func<TEntity, bool>> Predicate { get; set; }

    /// <summary>
    /// 获取查询条件
    /// </summary>
    public virtual Expression<Func<TEntity, bool>> GetCondition() => Predicate;
}