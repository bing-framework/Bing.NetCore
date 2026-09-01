namespace Bing.Data.Queries;

/// <summary>
/// 定义实体查询的基础能力。
/// </summary>
/// <typeparam name="TEntity">查询对象对应的实体类型。</typeparam>
public interface IQueryBase<TEntity> : ICondition<TEntity> where TEntity : class
{
    /// <summary>
    /// 获取当前查询的排序条件。
    /// </summary>
    /// <returns>当前查询的排序条件。</returns>
    string GetOrder();

    /// <summary>
    /// 获取当前查询的分页参数。
    /// </summary>
    /// <returns>当前查询的分页参数。</returns>
    IPager GetPager();
}