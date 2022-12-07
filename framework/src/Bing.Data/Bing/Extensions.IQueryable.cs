using System;
using System.Linq;
using System.Linq.Expressions;
using Bing.Data;
using Bing.Data.Queries;
using Bing.Data.Queries.Conditions;
using Bing.Data.Queries.Internal;

// ReSharper disable once CheckNamespace
namespace Bing;

/// <summary>
/// <see cref="IQueryable{T}"/> 扩展
/// </summary>
public static partial class QueryableExtensions
{
    #region Where(添加查询条件)

    /// <summary>
    /// 添加查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="query">数据源</param>
    /// <param name="condition">查询条件对象</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static IQueryable<TEntity> Where<TEntity>(this IQueryable<TEntity> query, ICondition<TEntity> condition)
        where TEntity : class
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        if (condition == null)
            throw new ArgumentNullException(nameof(condition));
        var predicate = condition.GetCondition();
        if (predicate == null)
            return query;
        return query.Where(predicate);
    }

    #endregion

    #region WhereIf(添加查询条件)

    /// <summary>
    /// 添加查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="query">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="condition">判断条件，该值为true时添加查询条件，否则忽略</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static IQueryable<TEntity> WhereIf<TEntity>(this IQueryable<TEntity> query, Expression<Func<TEntity, bool>> predicate, bool condition)
        where TEntity : class
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        return condition == false ? query : query.Where(predicate);
    }

    #endregion

    #region WhereIfNotEmpty(添加查询条件)

    /// <summary>
    /// 添加查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="query">数据源</param>
    /// <param name="predicate">查询条件，如果参数为空，则忽略该查询条件，范例：t => t.Name == ""，该查询条件被忽略。注意：一次仅能添加一个条件，范例：t => t.Name == "a" &amp;&amp; t.Mobile == "123"，不支持，将抛出异常</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static IQueryable<TEntity> WhereIfNotEmpty<TEntity>(this IQueryable<TEntity> query, Expression<Func<TEntity, bool>> predicate)
        where TEntity : class
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        predicate = Helper.GetWhereIfNotEmptyExpression(predicate);
        if (predicate == null)
            return query;
        return query.Where(predicate);
    }

    #endregion

    #region Between(添加范围查询条件)

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <param name="query">数据源</param>
    /// <param name="propertyExpression">属性表达式，范例：t => t.Age</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static IQueryable<TEntity> Between<TEntity, TProperty>(this IQueryable<TEntity> query, Expression<Func<TEntity, TProperty>> propertyExpression, int? min, int? max, Boundary boundary = Boundary.Both)
        where TEntity : class
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        return query.Where(new IntSegmentCondition<TEntity, TProperty>(propertyExpression, min, max, boundary));
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <param name="query">数据源</param>
    /// <param name="propertyExpression">属性表达式，范例：t => t.Age</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static IQueryable<TEntity> Between<TEntity, TProperty>(this IQueryable<TEntity> query, Expression<Func<TEntity, TProperty>> propertyExpression, double? min, double? max, Boundary boundary = Boundary.Both)
        where TEntity : class
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        return query.Where(new DoubleSegmentCondition<TEntity, TProperty>(propertyExpression, min, max, boundary));
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <param name="query">数据源</param>
    /// <param name="propertyExpression">属性表达式，范例：t => t.Age</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static IQueryable<TEntity> Between<TEntity, TProperty>(this IQueryable<TEntity> query, Expression<Func<TEntity, TProperty>> propertyExpression, decimal? min, decimal? max, Boundary boundary = Boundary.Both)
        where TEntity : class
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        return query.Where(new DecimalSegmentCondition<TEntity, TProperty>(propertyExpression, min, max, boundary));
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <param name="query">数据源</param>
    /// <param name="propertyExpression">属性表达式，范例：t => t.Time</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static IQueryable<TEntity> Between<TEntity, TProperty>(this IQueryable<TEntity> query, Expression<Func<TEntity, TProperty>> propertyExpression, DateTime? min, DateTime? max, Boundary boundary = Boundary.Both)
        where TEntity : class
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        return query.Where(new DateTimeSegmentCondition<TEntity, TProperty>(propertyExpression, min, max, boundary));
    }

    #endregion

    #region Page(分页，包含排序)

    /// <summary>
    /// 分页，包含排序
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="query">数据源</param>
    /// <param name="pager">分页对象</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public static IQueryable<TEntity> Page<TEntity>(this IQueryable<TEntity> query, IPager pager)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        if (pager == null)
            throw new ArgumentNullException(nameof(pager));
        Helper.InitOrder(query, pager);
        if (pager.TotalCount <= 0)
            pager.TotalCount = query.Count();
        var orderedQueryable = Helper.GetOrderedQueryable(query, pager);
        if (orderedQueryable == null)
            throw new ArgumentException("必须设置排序字段", nameof(orderedQueryable));
        return orderedQueryable.Skip(pager.GetSkipCount()).Take(pager.PageSize);
    }

    #endregion

    #region PageBy(分页)

    /// <summary>
    /// 分页
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="query">数据源</param>
    /// <param name="skipCount">跳行的行数</param>
    /// <param name="maxResultCount">每页显示行数</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static IQueryable<T> PageBy<T>(this IQueryable<T> query, int skipCount, int maxResultCount)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        return query.Skip(skipCount).Take(maxResultCount);
    }

    /// <summary>
    /// 分页
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <typeparam name="TQueryable">数据源类型</typeparam>
    /// <param name="query">数据源</param>
    /// <param name="skipCount">跳过的行数</param>
    /// <param name="maxResultCount">每页显示行数</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static TQueryable PageBy<T,TQueryable>(this TQueryable query,int skipCount,int maxResultCount) where TQueryable : IQueryable<T>
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        return (TQueryable)query.Skip(skipCount).Take(maxResultCount);
    }

    #endregion

    #region ToPagerList(转换为分页列表)

    /// <summary>
    /// 转换为分页列表，包含排序分页操作
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="query">数据源</param>
    /// <param name="pager">分页对象</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static PagerList<TEntity> ToPagerList<TEntity>(this IQueryable<TEntity> query, IPager pager)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        if (pager == null)
            throw new ArgumentNullException(nameof(pager));
        return new PagerList<TEntity>(pager, query.Page(pager).ToList());
    }

    #endregion

}