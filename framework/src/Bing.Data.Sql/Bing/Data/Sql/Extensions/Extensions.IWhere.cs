using Bing.Data.Sql.Builders;

// ReSharper disable once CheckNamespace
namespace Bing.Data.Sql;

/// <summary>
/// Where子句扩展
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// And连接条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="condition">条件</param>
    public static T And<T>(this T source, ICondition condition)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.And(condition);
        return source;
    }

    /// <summary>
    /// Or连接条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="condition">条件</param>
    public static T Or<T>(this T source, ICondition condition)
        where T : IWhere
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.WhereClause.Or(condition);
        return source;
    }

    /// <summary>
    /// Or连接条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="predicate">条件</param>
    /// <param name="condition">该值为true时添加查询条件，否则忽略</param>
    public static T OrIf<T>(this T source, ICondition predicate, bool condition) where T : IWhere => condition ? Or(source, predicate) : source;
}
