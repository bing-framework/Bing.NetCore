﻿using System;
using System.Linq.Expressions;
using Bing.Extensions;

namespace Bing.Data.Queries.Conditions;

/// <summary>
/// 日期范围过滤条件 - 不包含时间
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TProperty">属性类型</typeparam>
public class DateSegmentCondition<TEntity, TProperty> : DateTimeSegmentCondition<TEntity, TProperty> where TEntity : class
{
    /// <summary>
    /// 初始化一个<see cref="DateSegmentCondition{TEntity,TProperty}"/>类型的实例
    /// </summary>
    /// <param name="propertyExpression">属性表达式</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public DateSegmentCondition(Expression<Func<TEntity, TProperty>> propertyExpression
        , DateTime? min
        , DateTime? max
        , Boundary boundary)
        : base(propertyExpression, min, max, boundary)
    {
    }

    /// <summary>
    /// 获取最小值表达式
    /// </summary>
    protected override Expression GetMinValueExpression()
    {
        var minValue = GetMinValue().SafeValue().Date;
        if (GetBoundary() == Boundary.Right || GetBoundary() == Boundary.Neither)
            minValue = minValue.AddDays(1);
        return CreateDateTimeExpression(minValue);
    }

    /// <summary>
    /// 获取最大值表达式
    /// </summary>
    protected override Expression GetMaxValueExpression()
    {
        var maxValue = GetMaxValue().SafeValue().Date;
        if (GetBoundary() == Boundary.Right || GetBoundary() == Boundary.Both)
            maxValue = maxValue.AddDays(1);
        return CreateDateTimeExpression(maxValue);
    }

    /// <summary>
    /// 创建左操作符
    /// </summary>
    /// <param name="boundary">查询边界</param>
    protected override Operator CreateLeftOperator(Boundary? boundary) => Operator.GreaterEqual;

    /// <summary>
    /// 创建右操作符
    /// </summary>
    /// <param name="boundary">查询边界</param>
    protected override Operator CreateRightOperator(Boundary? boundary) => Operator.Less;
}