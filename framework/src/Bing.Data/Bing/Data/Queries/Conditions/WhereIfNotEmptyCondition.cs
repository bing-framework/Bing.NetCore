using System;
using System.Linq.Expressions;
using Bing.Expressions;
using Bing.Extensions;
using Bing.Properties;

namespace Bing.Data.Queries.Conditions;

/// <summary>
/// WhereIfNotEmpty 条件
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public class WhereIfNotEmptyCondition<TEntity> : ICondition<TEntity>
{
    /// <summary>
    /// 查询条件
    /// </summary>
    private readonly Expression<Func<TEntity, bool>> _condition;

    /// <summary>
    /// 初始化一个<see cref="WhereIfNotEmptyCondition{TEntity}"/>类型的实例
    /// </summary>
    /// <param name="condition">查询条件</param>
    public WhereIfNotEmptyCondition(Expression<Func<TEntity, bool>> condition) => _condition = condition;

    /// <summary>
    /// 获取查询条件
    /// </summary>
    public Expression<Func<TEntity, bool>> GetCondition()
    {
        if (_condition == null)
            return null;
        if (Lambdas.GetConditionCount(_condition) > 1)
            throw new InvalidOperationException(string.Format(LibraryResource.CanOnlyOneCondition, _condition));
        return _condition.Value().SafeString().IsEmpty() ? null : _condition;
    }
}