using Bing.Data.Sql.Builders;

// ReSharper disable once CheckNamespace
namespace Bing.Data.Sql;

/// <summary>
/// From子句扩展
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// 设置表名
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="table">表名</param>
    /// <param name="alias">别名</param>
    public static T From<T>(this T source, string table, string alias = null) where T : IFrom
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.FromClause.From(table, alias);
        return source;
    }

    /// <summary>
    /// 追加原始 From SQL。
    /// 原始文本不会经过标识符解析、方言格式化或别名注册；调用方负责 SQL 安全性及多次追加时的分隔符。
    /// 首次追加会替换当前结构化 From，后续原始文本按调用顺序直接拼接。
    /// </summary>
    /// <typeparam name="T">源类型。</typeparam>
    /// <param name="source">源。</param>
    /// <param name="sql">原始 From 文本；空白文本将被忽略。</param>
    public static T AppendFrom<T>(this T source, string sql) where T : IFrom
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.FromClause.AppendSql(sql);
        return source;
    }

    /// <summary>
    /// 按条件追加原始 From SQL。
    /// </summary>
    /// <typeparam name="T">源类型。</typeparam>
    /// <param name="source">源。</param>
    /// <param name="sql">原始 From 文本。</param>
    /// <param name="condition">是否添加。</param>
    public static T AppendFrom<T>(this T source, string sql, bool condition) where T : IFrom =>
        condition ? AppendFrom(source, sql) : source;

    /// <summary>
    /// 设置子查询表
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builder">Sql生成器</param>
    /// <param name="alias">表别名</param>
    public static T From<T>(this T source, ISqlBuilder builder, string alias) where T : IFrom
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.FromClause.From(builder, alias);
        return source;
    }

    /// <summary>
    /// 设置子查询表
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="action">子查询操作</param>
    /// <param name="alias">表别名</param>
    public static T From<T>(this T source, Action<ISqlBuilder> action, string alias) where T : IFrom
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.FromClause.From(action, alias);
        return source;
    }

}
