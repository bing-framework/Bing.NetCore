﻿using Bing.Data.Queries;
using Bing.Data.Sql.Builders;

// ReSharper disable once CheckNamespace
namespace Bing.Data.Sql;

/// <summary>
/// Where子句(<see cref="IWhere"/>) 扩展
/// </summary>
public static class WhereClauseExtensions
{
    #region Where

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="condition">条件</param>
    public static T Where<T>(this T source, ICondition condition)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.Where(condition);
        return source;
    }

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    /// <param name="operator">运算符</param>
    public static T Where<T>(this T source, string column, object value, Operator @operator = Operator.Equal)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.Where(column, value, @operator);
        return source;
    }

    /// <summary>
    /// 设置子查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="builder">子查询Sql生成器</param>
    /// <param name="operator">运算符</param>
    public static T Where<T>(this T source, string column, ISqlBuilder builder, Operator @operator = Operator.Equal)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.Where(column, builder, @operator);
        return source;
    }

    /// <summary>
    /// 设置子查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="action">子查询操作</param>
    /// <param name="operator">运算符</param>
    public static T Where<T>(this T source, string column, Action<ISqlBuilder> action, Operator @operator = Operator.Equal)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.Where(column, action, @operator);
        return source;
    }

    #endregion

    #region WhereIf

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    /// <param name="condition">该值为true时添加查询条件，否则忽略</param>
    /// <param name="operator">运算符</param>
    public static T WhereIf<T>(this T source, string column, object value, bool condition, Operator @operator = Operator.Equal) where T : IWhere => condition ? Where(source, column, value, @operator) : source;

    /// <summary>
    /// 设置子查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="builder">子查询Sql生成器</param>
    /// <param name="condition">该值为true时添加查询条件，否则忽略</param>
    /// <param name="operator">运算符</param>
    public static T WhereIf<T>(this T source, string column, ISqlBuilder builder, bool condition, Operator @operator = Operator.Equal) where T : IWhere => condition ? Where(source, column, builder, @operator) : source;

    /// <summary>
    /// 设置子查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="action">子查询操作</param>
    /// <param name="condition">该值为true时添加查询条件，否则忽略</param>
    /// <param name="operator">运算符</param>
    public static T WhereIf<T>(this T source, string column, Action<ISqlBuilder> action, bool condition, Operator @operator = Operator.Equal) where T : IWhere => condition ? Where(source, column, action, @operator) : source;

    #endregion

    #region WhereIfNotEmpty

    /// <summary>
    /// 设置查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="value">值。如果值为空，则忽略该查询条件</param>
    /// <param name="operator">运算符</param>
    public static T WhereIfNotEmpty<T>(this T source, string column, object value, Operator @operator = Operator.Equal)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.WhereIfNotEmpty(column, value, @operator);
        return source;
    }

    #endregion

    #region Equal

    /// <summary>
    /// 设置相等查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    public static T Equal<T>(this T source, string column, object value)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return source.Where(column, value);
    }

    #endregion

    #region NotEqual

    /// <summary>
    /// 设置不相等查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    public static T NotEqual<T>(this T source, string column, object value)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return source.Where(column, value, Operator.NotEqual);
    }

    #endregion

    #region Greater

    /// <summary>
    /// 设置大于查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    public static T Greater<T>(this T source, string column, object value)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return source.Where(column, value, Operator.Greater);
    }

    #endregion

    #region GreaterEqual

    /// <summary>
    /// 设置大于等于查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    public static T GreaterEqual<T>(this T source, string column, object value)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return source.Where(column, value, Operator.GreaterEqual);
    }

    #endregion

    #region Less

    /// <summary>
    /// 设置小于查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    public static T Less<T>(this T source, string column, object value)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return source.Where(column, value, Operator.Less);
    }

    #endregion

    #region LessEqual

    /// <summary>
    /// 设置小于等于查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    public static T LessEqual<T>(this T source, string column, object value)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return source.Where(column, value, Operator.LessEqual);
    }

    #endregion

    #region Contains

    /// <summary>
    /// 设置模糊匹配查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    public static T Contains<T>(this T source, string column, object value)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return source.Where(column, value, Operator.Contains);
    }

    #endregion

    #region Starts

    /// <summary>
    /// 设置头匹配查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    public static T Starts<T>(this T source, string column, object value)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return source.Where(column, value, Operator.Starts);
    }

    #endregion

    #region Ends

    /// <summary>
    /// 设置尾匹配查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    public static T Ends<T>(this T source, string column, object value)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return source.Where(column, value, Operator.Ends);
    }

    #endregion

    #region IsNull

    /// <summary>
    /// 设置Is Null查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    public static T IsNull<T>(this T source, string column)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.IsNull(column);
        return source;
    }

    #endregion

    #region IsNotNull

    /// <summary>
    /// 设置Is Not Null查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    public static T IsNotNull<T>(this T source, string column)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.IsNotNull(column);
        return source;
    }

    #endregion

    #region IsEmpty

    /// <summary>
    /// 设置空条件，范例：[Name] Is Null Or [Name]=''
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    public static T IsEmpty<T>(this T source, string column)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.IsEmpty(column);
        return source;
    }

    #endregion

    #region IsNotEmpty

    /// <summary>
    /// 设置非空条件，范例：[Name] Is Not Null And [Name]&lt;&gt;''
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    public static T IsNotEmpty<T>(this T source, string column)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.IsNotEmpty(column);
        return source;
    }

    #endregion

    #region In

    /// <summary>
    /// 设置In条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="values">值集合</param>
    public static T In<T>(this T source, string column, IEnumerable<object> values)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.In(column, values);
        return source;
    }

    /// <summary>
    /// 设置In条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="builder">Sql生成器</param>
    public static T In<T>(this T source, string column, ISqlBuilder builder)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.In(column, builder);
        return source;
    }

    /// <summary>
    /// 设置In条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="action">子查询操作</param>
    public static T In<T>(this T source, string column, Action<ISqlBuilder> action)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.In(column, action);
        return source;
    }

    #endregion

    #region NotIn

    /// <summary>
    /// 设置In条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="values">值集合</param>
    public static T NotIn<T>(this T source, string column, IEnumerable<object> values)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.NotIn(column, values);
        return source;
    }

    /// <summary>
    /// 设置In条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="builder">Sql生成器</param>
    public static T NotIn<T>(this T source, string column, ISqlBuilder builder)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.NotIn(column, builder);
        return source;
    }

    /// <summary>
    /// 设置In条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="action">子查询操作</param>
    public static T NotIn<T>(this T source, string column, Action<ISqlBuilder> action)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.NotIn(column, action);
        return source;
    }

    #endregion

    #region Exists

    /// <summary>
    /// 设置Exists条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builder">Sql生成器</param>
    public static T Exists<T>(this T source, ISqlBuilder builder)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.Exists(builder);
        return source;
    }

    /// <summary>
    /// 设置Exists条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="action">子查询操作</param>
    public static T Exists<T>(this T source, Action<ISqlBuilder> action)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.Exists(action);
        return source;
    }

    #endregion

    #region NotExists

    /// <summary>
    /// 设置Not Exists条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builder">Sql生成器</param>
    public static T NotExists<T>(this T source, ISqlBuilder builder)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.NotExists(builder);
        return source;
    }

    /// <summary>
    /// 设置Not Exists条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="action">子查询操作</param>
    public static T NotExists<T>(this T source, Action<ISqlBuilder> action)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.NotExists(action);
        return source;
    }

    #endregion

    #region Between

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public static T Between<T>(this T source, string column, int? min, int? max, Boundary boundary = Boundary.Both)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.Between(column, min, max, boundary);
        return source;
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public static T Between<T>(this T source, string column, long? min, long? max, Boundary boundary = Boundary.Both)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.Between(column, min, max, boundary);
        return source;
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public static T Between<T>(this T source, string column, float? min, float? max, Boundary boundary = Boundary.Both)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.Between(column, min, max, boundary);
        return source;
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public static T Between<T>(this T source, string column, double? min, double? max, Boundary boundary = Boundary.Both)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.Between(column, min, max, boundary);
        return source;
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public static T Between<T>(this T source, string column, decimal? min, decimal? max, Boundary boundary = Boundary.Both)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.Between(column, min, max, boundary);
        return source;
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="includeTime">是否包含时间</param>
    /// <param name="boundary">包含边界</param>
    public static T Between<T>(this T source, string column, DateTime? min, DateTime? max, bool includeTime = true, Boundary? boundary = null)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.Between(column, min, max, includeTime, boundary);
        return source;
    }

    #endregion

    #region AppendWhere

    /// <summary>
    /// 添加到Where子句
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="sql">Sql语句。说明：原样添加到Sql中，不会进行任何处理</param>
    public static T AppendWhere<T>(this T source, string sql)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.AppendSql(sql);
        return source;
    }

    /// <summary>
    /// 添加到Where子句
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="sql">Sql语句。说明：原样添加到Sql中，不会进行任何处理</param>
    /// <param name="condition">该值为true时添加Sql，否则忽略</param>
    public static T AppendWhere<T>(this T source, string sql, bool condition) where T : IWhere => condition ? AppendWhere(source, sql) : source;

    #endregion
}
