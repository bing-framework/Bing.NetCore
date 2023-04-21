using Bing.Data.Sql.Builders;

// ReSharper disable once CheckNamespace
namespace Bing.Data.Sql;

/// <summary>
/// Select子句(<see cref="ISelect"/>) 扩展
/// </summary>
public static class SelectClauseExtensions
{
    #region Select(设置列)

    /// <summary>
    /// 设置列名
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="columns">列名。范例：a.AppId As Id,a.Name</param>
    /// <param name="tableAlias">表别名</param>
    public static T Select<T>(this T source, string columns, string tableAlias = null) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.SelectClause.Select(columns, tableAlias);
        return source;
    }

    /// <summary>
    /// 设置子查询列
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builder">Sql生成器</param>
    /// <param name="columnAlias">列别名</param>
    public static T Select<T>(this T source, ISqlBuilder builder, string columnAlias) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.SelectClause.Select(builder, columnAlias);
        return source;
    }

    /// <summary>
    /// 设置子查询列
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="action">子查询操作</param>
    /// <param name="columnAlias">列别名</param>
    public static T Select<T>(this T source, Action<ISqlBuilder> action, string columnAlias) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.SelectClause.Select(action, columnAlias);
        return source;
    }

    #endregion

    #region AppendSelect(添加到Select子句)

    /// <summary>
    /// 添加到Select子句
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="sql">Sql语句。说明：将会原样添加到Sql中，不会进行任何处理</param>
    public static T AppendSelect<T>(this T source, string sql) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.SelectClause.AppendSql(sql);
        return source;
    }

    /// <summary>
    /// 添加到Select子句
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="sql">Sql语句。说明：将会原样添加到Sql中，不会进行任何处理</param>
    /// <param name="condition">该值为true时添加Sql，否则忽略</param>
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
    public static T RemoveSelect<T>(this T source, string columns, string tableAlias = null) where T : ISelect
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.SelectClause.RemoveSelect(columns, tableAlias);
        return source;
    }

    #endregion
}
