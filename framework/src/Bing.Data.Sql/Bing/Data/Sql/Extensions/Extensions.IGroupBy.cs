using Bing.Data.Sql.Builders;

// ReSharper disable once CheckNamespace
namespace Bing.Data.Sql;

/// <summary>
/// GroupBy子句扩展
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// 分组
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="columns">分组字段，范例：a.Id,b.Name</param>
    /// <param name="having">分组条件，范例：Count(*) > 1</param>
    public static T GroupBy<T>(this T source, string columns, string having = null) where T : IGroupBy
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.GroupByClause.GroupBy(columns, having);
        return source;
    }

    /// <summary>
    /// 添加到 GroupBy 子句。
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="sql">SQL 文本；方括号标识符会按当前方言解析。</param>
    /// <returns>传入的同一个源对象。</returns>
    public static T AppendGroupBy<T>(this T source, string sql) where T : IGroupBy
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlQueryClauseAccessor accessor)
            accessor.GroupByClause.AppendSql(sql);
        return source;
    }

    /// <summary>
    /// 按条件添加到 GroupBy 子句。
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="sql">SQL 文本；方括号标识符会按当前方言解析。</param>
    /// <param name="condition">该值为true时添加Sql，否则忽略</param>
    /// <returns>传入的同一个源对象。</returns>
    public static T AppendGroupBy<T>(this T source, string sql, bool condition) where T : IGroupBy => condition ? AppendGroupBy(source, sql) : source;
}
