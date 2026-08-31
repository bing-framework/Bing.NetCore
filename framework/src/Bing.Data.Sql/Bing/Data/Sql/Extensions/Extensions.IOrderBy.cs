using Bing.Data.Sql.Builders;

// ReSharper disable once CheckNamespace
namespace Bing.Data.Sql;

/// <summary>
/// OrderBy子句
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// 排序
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="order">排序列表。范例：a.Id, b.Name desc</param>
    /// <param name="tableAlias">表别名</param>
    /// <returns>设置排序后的源对象。</returns>
    public static T OrderBy<T>(this T source, string order, string tableAlias = null) where T : IOrderBy
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        SqlQueryOperationAccessor.Mutate(source, accessor => accessor.OrderByClause.OrderBy(order, tableAlias));
        return source;
    }

    /// <summary>
    /// 添加到 OrderBy 子句。
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="sql">SQL 文本；方括号标识符会按当前方言解析。</param>
    /// <returns>追加排序后的源对象。</returns>
    public static T AppendOrderBy<T>(this T source, string sql) where T : IOrderBy
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(sql))
            return source;
        SqlQueryOperationAccessor.Mutate(source, accessor => accessor.OrderByClause.AppendSql(sql));
        return source;
    }

    /// <summary>
    /// 按条件添加到 OrderBy 子句。
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="sql">SQL 文本；方括号标识符会按当前方言解析。</param>
    /// <param name="condition">该值为true时添加Sql，否则忽略</param>
    /// <returns>条件成立时追加排序后的源对象。</returns>
    public static T AppendOrderBy<T>(this T source, string sql, bool condition) where T : IOrderBy => condition ? AppendOrderBy(source, sql) : source;
}
