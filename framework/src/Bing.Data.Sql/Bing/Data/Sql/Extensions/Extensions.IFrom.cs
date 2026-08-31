using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;

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
    /// <returns>设置 From 子句后的源对象。</returns>
    public static T From<T>(this T source, string table, string alias = null) where T : IFrom
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        SqlQueryOperationAccessor.Mutate(source, accessor => accessor.FromClause.From(table, alias));
        return source;
    }

    /// <summary>
    /// 设置结构化表引用。
    /// </summary>
    /// <typeparam name="T">源类型。</typeparam>
    /// <param name="source">源。</param>
    /// <param name="table">结构化表引用。</param>
    /// <returns>设置 From 子句后的源对象。</returns>
    public static T From<T>(this T source, SqlTableReference table) where T : IFrom
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (table == null)
            throw new ArgumentNullException(nameof(table));
        SqlQueryOperationAccessor.Mutate(source, accessor => accessor.FromClause.From(table));
        return source;
    }

    /// <summary>
    /// 设置或追加完整原始 From 表达式。
    /// 首次追加时，如果存在结构化 From，将替换原 From；后续追加仅按调用顺序直接拼接，不会自动添加空格、逗号或其他分隔符。
    /// 原始文本不会经过标识符解析、Schema 解析、方言格式化或别名注册；调用方负责 SQL 安全性及通过 <c>AddParam</c> 显式提供占位符参数。
    /// </summary>
    /// <typeparam name="T">源类型。</typeparam>
    /// <param name="source">源。</param>
    /// <param name="sql">原始 From 文本；空白文本将被忽略。</param>
    /// <returns>追加 From 子句后的源对象。</returns>
    public static T AppendFrom<T>(this T source, string sql) where T : IFrom
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(sql))
            return source;
        SqlQueryOperationAccessor.Mutate(source, accessor => accessor.FromClause.AppendSql(sql));
        return source;
    }

    /// <summary>
    /// 按条件追加原始 From SQL。
    /// </summary>
    /// <typeparam name="T">源类型。</typeparam>
    /// <param name="source">源。</param>
    /// <param name="sql">原始 From 文本。</param>
    /// <param name="condition">是否添加。</param>
    /// <returns>条件成立时追加 From 子句后的源对象。</returns>
    public static T AppendFrom<T>(this T source, string sql, bool condition) where T : IFrom =>
        condition ? AppendFrom(source, sql) : source;

    /// <summary>
    /// 设置子查询表
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builder">Sql生成器</param>
    /// <param name="alias">表别名</param>
    /// <returns>设置子查询 From 子句后的源对象。</returns>
    public static T From<T>(this T source, ISqlBuilder builder, string alias) where T : IFrom
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        SqlQueryOperationAccessor.Mutate(source, accessor => accessor.FromClause.From(builder, alias));
        return source;
    }

    /// <summary>
    /// 设置子查询表
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="action">子查询操作</param>
    /// <param name="alias">表别名</param>
    /// <returns>设置子查询 From 子句后的源对象。</returns>
    public static T From<T>(this T source, Action<ISqlBuilder> action, string alias) where T : IFrom
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        SqlQueryOperationAccessor.Mutate(source, accessor => accessor.FromClause.From(action, alias));
        return source;
    }

    /// <summary>
    /// 使用 Fluent 查询描述设置子查询表。
    /// </summary>
    /// <typeparam name="T">支持 From 子句的源类型。</typeparam>
    /// <param name="source">当前查询源。</param>
    /// <param name="query">作为子查询使用的独立查询描述。</param>
    /// <param name="alias">子查询表别名。</param>
    /// <returns>追加子查询后的源对象。</returns>
    public static T From<T>(this T source, SqlFluentQuery query, string alias) where T : IFrom =>
        From(source, GetQueryBuilder(query, nameof(query)), alias);

    /// <summary>
    /// 获取 Fluent 操作实际使用的 Builder。
    /// </summary>
    /// <param name="source">Fluent 操作源。</param>
    /// <returns>独立查询描述的内部 Builder，或原始 Builder。</returns>
    private static ISqlBuilder GetOperationBuilder(object source) => SqlQueryOperationAccessor.GetBuilder(source);

    /// <summary>
    /// 获取查询描述专属的内部 Builder。
    /// </summary>
    /// <param name="query">查询描述。</param>
    /// <param name="parameterName">调用方参数名称。</param>
    /// <returns>仅供框架内部组合使用的子查询 Builder。</returns>
    private static ISqlBuilder GetQueryBuilder(SqlFluentQuery query, string parameterName)
    {
        if (query == null)
            throw new ArgumentNullException(parameterName);
        return ((ISqlQueryBuilderAccessor)query).GetSqlBuilder();
    }

}
