using System.Linq.Expressions;

namespace Bing.Data;

/// <summary>
/// 定义实体查询条件。
/// </summary>
/// <typeparam name="TEntity">查询条件适用的实体类型。</typeparam>
public interface ICondition<TEntity>
{
    /// <summary>
    /// 获取查询条件表达式。
    /// </summary>
    /// <returns>当前查询条件表达式；没有有效条件时可以返回 <see langword="null"/>。</returns>
    Expression<Func<TEntity, bool>> GetCondition();
}