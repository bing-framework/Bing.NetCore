using System;
using System.Linq.Expressions;
using Bing.Extensions;

namespace Bing.Data.Queries.Conditions;

/// <summary>
/// 日期范围过滤条件 - 包含时间
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TProperty">属性类型</typeparam>
public class DateTimeSegmentCondition<TEntity, TProperty> : SegmentConditionBase<TEntity, TProperty, DateTime> where TEntity : class
{
    /// <summary>
    /// 初始化一个<see cref="DateTimeSegmentCondition{TEntity,TProperty}"/>类型的实例
    /// </summary>
    /// <param name="propertyExpression">属性表达式</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public DateTimeSegmentCondition(Expression<Func<TEntity, TProperty>> propertyExpression
        , DateTime? min
        , DateTime? max
        , Boundary boundary = Boundary.Both)
        : base(propertyExpression, min, max, boundary)
    {
    }

    /// <summary>
    /// 最小值是否大于最大值
    /// </summary>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    protected override bool IsMinGreaterMax(DateTime? min, DateTime? max) => min > max;

    /// <summary>
    /// 创建 DateTime.Parse(string) 表达式
    /// </summary>
    /// <param name="value">日期值</param>
    protected Expression CreateDateTimeExpression(object value)
    {
        var parse = typeof(DateTime).GetMethod("Parse", new[] { typeof(string) });
        if (parse == null)
            return null;
        var parseExpression = Expression.Call(parse, Expression.Constant(value.SafeString()));
        return Expression.Convert(parseExpression, GetPropertyType());
    }

    /// <summary>
    /// 获取最小值表达式
    /// </summary>
    protected override Expression GetMinValueExpression() => CreateDateTimeExpression(GetMinValue());

    /// <summary>
    /// 获取最大值表达式
    /// </summary>
    protected override Expression GetMaxValueExpression() => CreateDateTimeExpression(GetMaxValue());
}