using Bing.Data.Sql.Builders;

// ReSharper disable once CheckNamespace
namespace Bing.Data.Sql;

/// <summary>
/// Select子句(<see cref="ISelect"/>) 扩展
/// </summary>
public static class SelectClauseExtensions
{
    /// <summary>
    /// 为统一 Builder 添加多个查询输出列。
    /// </summary>
    /// <param name="source">SQL 生成器。</param>
    /// <param name="columns">待添加的查询输出列。</param>
    /// <returns>添加输出列后的 SQL 生成器。</returns>
    public static ISqlBuilder Select(this ISqlBuilder source, params string[] columns)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (columns == null)
            throw new ArgumentNullException(nameof(columns));
        foreach (var column in columns)
            source.SelectClause.Select(column);
        return source;
    }

    #region Select(设置列)

    /// <summary>
    /// 设置列名
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="columns">列名。范例：a.AppId As Id,a.Name</param>
    /// <param name="tableAlias">表别名</param>
    /// <returns>设置投影后的源对象。</returns>
    public static T Select<T>(this T source, string columns, string tableAlias = null) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        SqlQueryOperationAccessor.Mutate(source, accessor => accessor.SelectClause.Select(columns, tableAlias));
        return source;
    }

    /// <summary>
    /// 设置子查询列
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builder">Sql生成器</param>
    /// <param name="columnAlias">列别名</param>
    /// <returns>添加子查询列后的源对象。</returns>
    public static T Select<T>(this T source, ISqlBuilder builder, string columnAlias) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        SqlQueryOperationAccessor.Mutate(source, accessor => accessor.SelectClause.Select(builder, columnAlias));
        return source;
    }

    /// <summary>
    /// 设置子查询列
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="action">子查询操作</param>
    /// <param name="columnAlias">列别名</param>
    /// <returns>添加子查询列后的源对象。</returns>
    public static T Select<T>(this T source, Action<ISqlBuilder> action, string columnAlias) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        SqlQueryOperationAccessor.Mutate(source, accessor => accessor.SelectClause.Select(action, columnAlias));
        return source;
    }

    #endregion

    #region AppendSelect(添加到Select子句)

    /// <summary>
    /// 添加到 Select 子句。
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="sql">SQL 文本；方括号标识符会按当前方言解析。</param>
    /// <returns>追加投影后的源对象。</returns>
    public static T AppendSelect<T>(this T source, string sql) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(sql))
            return source;
        SqlQueryOperationAccessor.Mutate(source, accessor => accessor.SelectClause.AppendSql(sql));
        return source;
    }

    /// <summary>
    /// 按条件添加到 Select 子句。
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="sql">SQL 文本；方括号标识符会按当前方言解析。</param>
    /// <param name="condition">该值为true时添加Sql，否则忽略</param>
    /// <returns>条件成立时追加投影后的源对象。</returns>
    public static T AppendSelect<T>(this T source, string sql, bool condition) where T : ISelect => condition ? AppendSelect(source, sql) : source;

    #endregion

    #region RemoveSelect(移除列名)

    /// <summary>
    /// 移除列名
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="columns">列名。范例：a.AppId,a.Name</param>
    /// <param name="tableAlias">表别名</param>
    /// <returns>移除投影列后的源对象。</returns>
    public static T RemoveSelect<T>(this T source, string columns, string tableAlias = null) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        SqlQueryOperationAccessor.Mutate(source, accessor => accessor.SelectClause.RemoveSelect(columns, tableAlias));
        return source;
    }

    #endregion
}
