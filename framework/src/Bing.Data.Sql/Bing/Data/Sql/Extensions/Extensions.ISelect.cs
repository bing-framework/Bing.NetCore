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
        if (source is ISqlPartAccessor accessor)
            accessor.SelectClause.Distinct();
        return source;
    }

    /// <summary>
    /// 求总行数
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="columnAlias">列别名</param>
    public static T Count<T>(this T source, string columnAlias = null) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.SelectClause.Count(columnAlias);
        return source;
    }

    /// <summary>
    /// 求总行数
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="columnAlias">列别名</param>
    public static T Count<T>(this T source, string column, string columnAlias) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.SelectClause.Count(column, columnAlias);
        return source;
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="columnAlias">列别名</param>
    public static T Sum<T>(this T source, string column, string columnAlias = null) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.SelectClause.Sum(column, columnAlias);
        return source;
    }

    /// <summary>
    /// 求平均值
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="columnAlias">列别名</param>
    public static T Avg<T>(this T source, string column, string columnAlias = null) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.SelectClause.Avg(column, columnAlias);
        return source;
    }

    /// <summary>
    /// 求最大值
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="columnAlias">列别名</param>
    public static T Max<T>(this T source, string column, string columnAlias = null) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.SelectClause.Max(column, columnAlias);
        return source;
    }

    /// <summary>
    /// 求最小值
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="column">列名</param>
    /// <param name="columnAlias">列别名</param>
    public static T Min<T>(this T source, string column, string columnAlias = null) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.SelectClause.Min(column, columnAlias);
        return source;
    }
}
