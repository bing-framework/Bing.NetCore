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
    public static T GroupBy<T>(this T source, string columns) where T : IGroupBy
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        SqlQueryOperationAccessor.Mutate(source, accessor => accessor.GroupByClause.GroupBy(columns));
        return source;
    }

    /// <summary>
    /// 设置受信任的原始 Having 条件。
    /// </summary>
    /// <typeparam name="T">源类型。</typeparam>
    /// <param name="source">源对象。</param>
    /// <param name="sql">Having SQL 条件；外部输入必须通过参数 API 提供。</param>
    /// <returns>传入的同一个源对象。</returns>
    public static T HavingRaw<T>(this T source, string sql) where T : IGroupBy
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        SqlQueryOperationAccessor.Mutate(source, accessor => accessor.GroupByClause.HavingRaw(sql));
        return source;
    }

    /// <summary>
    /// 设置 Having 条件，并按当前方言解析方括号标识符。
    /// </summary>
    /// <typeparam name="T">源类型。</typeparam>
    /// <param name="source">源对象。</param>
    /// <param name="sql">Having SQL 条件；外部输入必须通过参数 API 提供。</param>
    /// <returns>传入的同一个源对象。</returns>
    public static T Having<T>(this T source, string sql) where T : IGroupBy
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        SqlQueryOperationAccessor.Mutate(source, accessor => accessor.GroupByClause.Having(sql));
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
        if (string.IsNullOrWhiteSpace(sql))
            return source;
        SqlQueryOperationAccessor.Mutate(source, accessor => accessor.GroupByClause.AppendSql(sql));
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
