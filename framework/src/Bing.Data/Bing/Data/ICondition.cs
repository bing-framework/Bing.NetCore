using System.Linq.Expressions;

namespace Bing.Data;

/// <summary>
/// 查询条件
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public interface ICondition<TEntity>
{
    /// <summary>
    /// 获取查询条件
    /// </summary>
    /// <returns>当前查询条件表达式；没有有效条件时可以返回 <see langword="null"/>。</returns>
    Expression<Func<TEntity, bool>> GetCondition();
}