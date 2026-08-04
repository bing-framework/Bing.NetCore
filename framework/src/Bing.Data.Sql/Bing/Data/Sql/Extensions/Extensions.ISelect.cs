using Bing.Data.Sql.Builders;

// ReSharper disable once CheckNamespace
namespace Bing.Data.Sql;

/// <summary>
/// Select子句扩展
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// 过滤重复记录
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    public static T Distinct<T>(this T source) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.SelectClause.Distinct();
        return source;
    }

    /// <summary>
    /// 添加 Count 聚合。
    /// </summary>
    /// <typeparam name="T">源类型。</typeparam>
    /// <param name="source">源对象。</param>
    /// <param name="column">待统计列。默认值为 *，表示统计全部记录。</param>
    /// <param name="alias">聚合结果别名。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    /// <returns>源对象。</returns>
    public static T Count<T>(this T source, string column = "*", string alias = null, bool distinct = false) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.SelectClause.Count(column, alias, distinct);
        return source;
    }

    /// <summary>
    /// 添加结构化聚合列。
    /// </summary>
    /// <typeparam name="T">源类型。</typeparam>
    /// <param name="source">源。</param>
    /// <param name="function">聚合函数。</param>
    /// <param name="column">单个结构化列名，不支持表达式、函数或多个列。</param>
    /// <param name="columnAlias">聚合结果列别名；未提供时不输出 Alias。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    /// <returns>源对象。</returns>
    public static T Aggregate<T>(this T source, SqlAggregateFunction function, string column,
        string columnAlias = null, bool distinct = false) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.SelectClause.Aggregate(function, column, columnAlias, distinct);
        return source;
    }

    /// <summary>
    /// 添加完全原样的聚合参数。
    /// </summary>
    /// <typeparam name="T">源类型。</typeparam>
    /// <param name="source">源。</param>
    /// <param name="function">聚合函数。</param>
    /// <param name="argumentSql">受信任的原始聚合参数 SQL。不解析、不校验标识符，也不转换 []；参数必须通过 AddParam 显式提供。</param>
    /// <param name="columnAlias">聚合结果列别名；未提供时不输出 Alias。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    /// <returns>源对象。</returns>
    public static T AggregateRaw<T>(this T source, SqlAggregateFunction function, string argumentSql,
        string columnAlias = null, bool distinct = false) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.SelectClause.AggregateRaw(function, argumentSql, columnAlias, distinct);
        return source;
    }

    /// <summary>
    /// 添加包含方括号标识符占位符的聚合表达式。
    /// </summary>
    /// <typeparam name="T">源类型。</typeparam>
    /// <param name="source">源对象。</param>
    /// <param name="function">聚合函数。</param>
    /// <param name="expressionSql">聚合表达式 SQL，仅普通 SQL 上下文中的 [] 会按当前方言转换为标识符引用符；字符串和注释原文保持不变，参数必须通过 AddParam 显式提供。</param>
    /// <param name="columnAlias">聚合结果列别名；未提供时不输出 Alias。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    /// <returns>源对象。</returns>
    public static T AggregateExpression<T>(this T source, SqlAggregateFunction function, string expressionSql,
        string columnAlias = null, bool distinct = false) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.SelectClause.AggregateExpression(function, expressionSql, columnAlias, distinct);
        return source;
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public static T Sum<T>(this T source, string column, string columnAlias = null, bool distinct = false) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.SelectClause.Sum(column, columnAlias, distinct);
        return source;
    }

    /// <summary>
    /// 求平均值
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public static T Avg<T>(this T source, string column, string columnAlias = null, bool distinct = false) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.SelectClause.Avg(column, columnAlias, distinct);
        return source;
    }

    /// <summary>
    /// 求最大值
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public static T Max<T>(this T source, string column, string columnAlias = null, bool distinct = false) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.SelectClause.Max(column, columnAlias, distinct);
        return source;
    }

    /// <summary>
    /// 求最小值
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public static T Min<T>(this T source, string column, string columnAlias = null, bool distinct = false) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.SelectClause.Min(column, columnAlias, distinct);
        return source;
    }
}
