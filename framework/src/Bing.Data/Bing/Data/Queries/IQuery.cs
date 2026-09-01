using System.Linq.Expressions;

namespace Bing.Data.Queries;

/// <summary>
/// 定义使用默认标识类型的实体查询。
/// </summary>
/// <typeparam name="TEntity">查询对象对应的实体类型。</typeparam>
public interface IQuery<TEntity> : IQuery<TEntity, Guid> where TEntity : class
{
}

/// <summary>
/// 定义支持条件、范围和排序操作的实体查询。
/// </summary>
/// <typeparam name="TEntity">查询对象对应的实体类型。</typeparam>
/// <typeparam name="TKey">实体标识类型。</typeparam>
public interface IQuery<TEntity, TKey> : IQueryBase<TEntity> where TEntity : class
{
    /// <summary>
    /// 添加查询条件。
    /// </summary>
    /// <param name="predicate">实体查询条件表达式。</param>
    /// <returns>添加条件后的当前查询对象。</returns>
    IQuery<TEntity, TKey> Where(Expression<Func<TEntity, bool>> predicate);

    /// <summary>
    /// 添加查询条件。
    /// </summary>
    /// <param name="condition">实体查询条件。</param>
    /// <returns>添加条件后的当前查询对象。</returns>
    IQuery<TEntity, TKey> Where(ICondition<TEntity> condition);

    /// <summary>
    /// 按条件决定是否添加查询条件。
    /// </summary>
    /// <param name="predicate">实体查询条件表达式。</param>
    /// <param name="condition">为 <see langword="true"/> 时添加条件，否则忽略。</param>
    /// <returns>添加条件后的当前查询对象。</returns>
    IQuery<TEntity, TKey> WhereIf(Expression<Func<TEntity, bool>> predicate, bool condition);

    /// <summary>
    /// 添加查询条件
    /// </summary>
    /// <param name="predicate">查询条件，如果参数值为空，则忽略该查询条件，范例：t => t.Name == "" ，该查询条件被忽略。
    /// 注意：一次仅能添加一个条件，范例：t => t.Name =="a" &amp;&amp; t.Mobile == "123"，不支持，将抛出异常</param>
    /// <returns>添加条件后的当前查询对象。</returns>
    IQuery<TEntity, TKey> WhereIfNotEmpty(Expression<Func<TEntity, bool>> predicate);

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TProperty">范围属性类型。</typeparam>
    /// <param name="propertyExpression">用于选择范围属性的表达式。</param>
    /// <param name="min">范围最小值。</param>
    /// <param name="max">范围最大值。</param>
    /// <param name="boundary">范围边界包含方式，默认为包含两端。</param>
    /// <returns>添加范围条件后的当前查询对象。</returns>
    IQuery<TEntity, TKey> Between<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression, int? min,
        int? max, Boundary boundary = Boundary.Both);

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TProperty">范围属性类型。</typeparam>
    /// <param name="propertyExpression">用于选择范围属性的表达式。</param>
    /// <param name="min">范围最小值。</param>
    /// <param name="max">范围最大值。</param>
    /// <param name="boundary">范围边界包含方式，默认为包含两端。</param>
    /// <returns>添加范围条件后的当前查询对象。</returns>
    IQuery<TEntity, TKey> Between<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression, double? min,
        double? max, Boundary boundary = Boundary.Both);

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TProperty">范围属性类型。</typeparam>
    /// <param name="propertyExpression">用于选择范围属性的表达式。</param>
    /// <param name="min">范围最小值。</param>
    /// <param name="max">范围最大值。</param>
    /// <param name="boundary">范围边界包含方式，默认为包含两端。</param>
    /// <returns>添加范围条件后的当前查询对象。</returns>
    IQuery<TEntity, TKey> Between<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression, decimal? min,
        decimal? max, Boundary boundary = Boundary.Both);

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TProperty">范围属性类型。</typeparam>
    /// <param name="propertyExpression">用于选择范围属性的表达式。</param>
    /// <param name="min">范围最小值。</param>
    /// <param name="max">范围最大值。</param>
    /// <param name="includeTime">是否包含时间部分，默认为包含。</param>
    /// <param name="boundary">范围边界包含方式。</param>
    /// <returns>添加范围条件后的当前查询对象。</returns>
    IQuery<TEntity, TKey> Between<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression, DateTime? min,
        DateTime? max, bool includeTime = true, Boundary? boundary = null);

    /// <summary>
    /// 添加排序
    /// </summary>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <param name="propertyExpression">属性表达式</param>
    /// <param name="desc">是否降序</param>
    /// <returns>添加排序后的当前查询对象。</returns>
    IQuery<TEntity, TKey> OrderBy<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression,
        bool desc = false);

    /// <summary>
    /// 添加排序
    /// </summary>
    /// <param name="propertyName">排序属性</param>
    /// <param name="desc">是否降序</param>
    /// <returns>添加排序后的当前查询对象。</returns>
    IQuery<TEntity, TKey> OrderBy(string propertyName, bool desc = false);

    /// <summary>
    /// 与连接
    /// </summary>
    /// <param name="predicate">查询条件</param>
    /// <returns>与指定条件连接后的当前查询对象。</returns>
    IQuery<TEntity, TKey> And(Expression<Func<TEntity, bool>> predicate);

    /// <summary>
    /// 与连接
    /// </summary>
    /// <param name="query">查询对象</param>
    /// <returns>与指定查询连接后的当前查询对象。</returns>
    IQuery<TEntity, TKey> And(IQuery<TEntity, TKey> query);

    /// <summary>
    /// 或连接
    /// </summary>
    /// <param name="predicates">查询条件</param>
    /// <returns>与指定条件进行或连接后的当前查询对象。</returns>
    IQuery<TEntity, TKey> Or(params Expression<Func<TEntity, bool>>[] predicates);

    /// <summary>
    /// 或连接
    /// </summary>
    /// <param name="query">查询对象</param>
    /// <returns>与指定查询进行或连接后的当前查询对象。</returns>
    IQuery<TEntity, TKey> Or(IQuery<TEntity, TKey> query);
}