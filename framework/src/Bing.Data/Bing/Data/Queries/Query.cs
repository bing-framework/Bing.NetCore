﻿using System;
using System.Linq.Expressions;
using Bing.Data.Queries.Conditions;
using Bing.Data.Queries.Internal;
using Bing.Expressions;

namespace Bing.Data.Queries;

/// <summary>
/// 查询对象
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public class Query<TEntity> : Query<TEntity, Guid>, IQuery<TEntity> where TEntity : class
{
    /// <summary>
    /// 初始化一个<see cref="Query{TEntity}"/>类型的实例
    /// </summary>
    public Query() { }

    /// <summary>
    /// 初始化一个<see cref="Query{TEntity}"/>类型的实例
    /// </summary>
    /// <param name="queryParameter">查询参数</param>
    public Query(IQueryParameter queryParameter) : base(queryParameter)
    {
    }
}

/// <summary>
/// 查询对象
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">实体标识类型</typeparam>
public class Query<TEntity, TKey> : IQuery<TEntity, TKey> where TEntity : class
{
    /// <summary>
    /// 查询参数
    /// </summary>
    private readonly IQueryParameter _parameter;

    /// <summary>
    /// 查询条件
    /// </summary>
    private Expression<Func<TEntity, bool>> _predicate;

    /// <summary>
    /// 排序生成器
    /// </summary>
    private readonly OrderByBuilder _orderByBuilder;

    /// <summary>
    /// 初始化一个<see cref="Query{TEntity,TKey}"/>类型的实例
    /// </summary>
    public Query() : this(new QueryParameter())
    {
    }

    /// <summary>
    /// 初始化一个<see cref="Query{TEntity,TKey}"/>类型的实例
    /// </summary>
    /// <param name="parameter">查询参数</param>
    public Query(IQueryParameter parameter)
    {
        _parameter = parameter;
        _orderByBuilder = new OrderByBuilder();
        OrderBy(parameter.Order);
    }

    /// <summary>
    /// 获取查询条件
    /// </summary>
    public Expression<Func<TEntity, bool>> GetCondition() => _predicate;

    /// <summary>
    /// 获取排序条件
    /// </summary>
    public string GetOrder() => _orderByBuilder.Generate();

    /// <summary>
    /// 获取分页
    /// </summary>
    public IPager GetPager() => new Pager(_parameter.Page, _parameter.PageSize, _parameter.TotalCount, GetOrder());

    /// <summary>
    /// 添加查询条件
    /// </summary>
    /// <param name="predicate">查询条件</param>
    public IQuery<TEntity, TKey> Where(Expression<Func<TEntity, bool>> predicate) => And(predicate);

    /// <summary>
    /// 添加查询条件
    /// </summary>
    /// <param name="condition">查询条件</param>
    public IQuery<TEntity, TKey> Where(ICondition<TEntity> condition) => And(condition.GetCondition());

    /// <summary>
    /// 添加查询条件
    /// </summary>
    /// <param name="predicate">查询条件</param>
    /// <param name="condition">该值为true时添加查询条件，否则忽略</param>
    public IQuery<TEntity, TKey> WhereIf(Expression<Func<TEntity, bool>> predicate, bool condition) => condition == false ? this : Where(predicate);

    /// <summary>
    /// 添加查询条件
    /// </summary>
    /// <param name="predicate">查询条件，如果参数值为空，则忽略该查询条件，范例：t => t.Name == "" ，该查询条件被忽略。
    /// 注意：一次仅能添加一个条件，范例：t => t.Name =="a" &amp;&amp; t.Mobile == "123"，不支持，将抛出异常</param>
    public IQuery<TEntity, TKey> WhereIfNotEmpty(Expression<Func<TEntity, bool>> predicate)
    {
        predicate = Helper.GetWhereIfNotEmptyExpression(predicate);
        if (predicate == null)
            return this;
        return And(predicate);
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <param name="propertyExpression">属性表达式，范例：t => t.Age</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界，默认：包含两边</param>
    public IQuery<TEntity, TKey> Between<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression, int? min, int? max, Boundary boundary = Boundary.Both) => Where(new IntSegmentCondition<TEntity, TProperty>(propertyExpression, min, max, boundary));

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <param name="propertyExpression">属性表达式，范例：t => t.Age</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界，默认：包含两边</param>
    public IQuery<TEntity, TKey> Between<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression, double? min, double? max, Boundary boundary = Boundary.Both) => Where(new DoubleSegmentCondition<TEntity, TProperty>(propertyExpression, min, max, boundary));

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <param name="propertyExpression">属性表达式，范例：t => t.Age</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界，默认：包含两边</param>
    public IQuery<TEntity, TKey> Between<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression, decimal? min, decimal? max, Boundary boundary = Boundary.Both) => Where(new DecimalSegmentCondition<TEntity, TProperty>(propertyExpression, min, max, boundary));

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <param name="propertyExpression">属性表达式，范例：t => t.Time</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="includeTime">是否包含时间，默认：包含</param>
    /// <param name="boundary">包含边界</param>
    public IQuery<TEntity, TKey> Between<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression, DateTime? min, DateTime? max, bool includeTime = true,
        Boundary? boundary = null) =>
        includeTime
            ? Where(new DateTimeSegmentCondition<TEntity, TProperty>(propertyExpression, min, max, boundary ?? Boundary.Both))
            : Where(new DateSegmentCondition<TEntity, TProperty>(propertyExpression, min, max, boundary ?? Boundary.Left));

    /// <summary>
    /// 添加排序
    /// </summary>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <param name="propertyExpression">属性表达式</param>
    /// <param name="desc">是否降序</param>
    public IQuery<TEntity, TKey> OrderBy<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression, bool desc = false) => OrderBy(Lambdas.GetName(propertyExpression), desc);

    /// <summary>
    /// 添加排序
    /// </summary>
    /// <param name="propertyName">排序属性</param>
    /// <param name="desc">是否降序</param>
    public IQuery<TEntity, TKey> OrderBy(string propertyName, bool desc = false)
    {
        _orderByBuilder.Add(propertyName, desc);
        return this;
    }

    /// <summary>
    /// 与连接
    /// </summary>
    /// <param name="predicate">查询条件</param>
    public IQuery<TEntity, TKey> And(Expression<Func<TEntity, bool>> predicate)
    {
        _predicate = _predicate.And(predicate);
        return this;
    }

    /// <summary>
    /// 与连接
    /// </summary>
    /// <param name="query">查询对象</param>
    public IQuery<TEntity, TKey> And(IQuery<TEntity, TKey> query)
    {
        And(query.GetCondition());
        OrderBy(query.GetOrder());
        return this;
    }

    /// <summary>
    /// 或连接
    /// </summary>
    /// <param name="predicates">查询条件</param>
    public IQuery<TEntity, TKey> Or(params Expression<Func<TEntity, bool>>[] predicates)
    {
        if (predicates == null)
            return this;
        foreach (var item in predicates)
        {
            var predicate = Helper.GetWhereIfNotEmptyExpression(item);
            if (predicate == null)
                continue;
            _predicate = _predicate.Or(predicate);
        }
        return this;
    }

    /// <summary>
    /// 或连接
    /// </summary>
    /// <param name="query">查询对象</param>
    public IQuery<TEntity, TKey> Or(IQuery<TEntity, TKey> query)
    {
        _predicate = _predicate.Or(query.GetCondition());
        OrderBy(query.GetOrder());
        return this;
    }
}