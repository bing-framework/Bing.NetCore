using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;

// ReSharper disable once CheckNamespace
namespace Bing.Data.Sql;

/// <summary>
/// Join子句扩展
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// 内连接
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="table">表名</param>
    /// <param name="alias">别名</param>
    public static T Join<T>(this T source, string table, string alias = null) where T : IJoin
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.JoinClause.Join(table, alias);
        return source;
    }

    /// <summary>
    /// 添加结构化左连接表引用。
    /// </summary>
    /// <typeparam name="T">源类型。</typeparam>
    /// <param name="source">源。</param>
    /// <param name="table">结构化表引用。</param>
    public static T LeftJoin<T>(this T source, SqlTableReference table) where T : IJoin
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (table == null)
            throw new ArgumentNullException(nameof(table));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.JoinClause.LeftJoin(table);
        return source;
    }

    /// <summary>
    /// 追加原始内连接表表达式。
    /// 原始文本不会经过标识符解析、Schema 解析、方言格式化或别名注册；可通过 <c>AppendOn</c> 向最后一个连接继续添加条件。
    /// 调用方负责 SQL 安全性及通过 <c>AddParam</c> 显式提供占位符参数。
    /// </summary>
    /// <typeparam name="T">源类型。</typeparam>
    /// <param name="source">源。</param>
    /// <param name="sql">原始连接文本；空白文本将被忽略。</param>
    /// <returns>传入的同一个源对象。</returns>
    public static T AppendJoin<T>(this T source, string sql) where T : IJoin
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.JoinClause.AppendJoin(sql);
        return source;
    }

    /// <summary>
    /// 按条件追加原始内连接 SQL。
    /// </summary>
    /// <typeparam name="T">源类型。</typeparam>
    /// <param name="source">源。</param>
    /// <param name="sql">原始连接文本。</param>
    /// <param name="condition">是否添加。</param>
    /// <returns>传入的同一个源对象。</returns>
    public static T AppendJoin<T>(this T source, string sql, bool condition) where T : IJoin =>
        condition ? AppendJoin(source, sql) : source;

    /// <summary>
    /// 内连接
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builder">Sql生成器</param>
    /// <param name="alias">别名</param>
    public static T Join<T>(this T source, ISqlBuilder builder, string alias) where T : IJoin
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.JoinClause.Join(builder, alias);
        return source;
    }

    /// <summary>
    /// 内连接
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="action">子查询操作</param>
    /// <param name="alias">别名</param>
    public static T Join<T>(this T source, Action<ISqlBuilder> action, string alias) where T : IJoin
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.JoinClause.Join(action, alias);
        return source;
    }

    /// <summary>
    /// 使用指定结果类型的 Fluent 查询描述添加内连接子查询。
    /// </summary>
    /// <typeparam name="T">支持 Join 子句的源类型。</typeparam>
    /// <typeparam name="TResult">子查询描述的结果类型。</typeparam>
    /// <param name="source">当前查询源。</param>
    /// <param name="query">作为子查询使用的独立查询描述。</param>
    /// <param name="alias">子查询别名。</param>
    /// <returns>追加连接后的源对象。</returns>
    public static T Join<T, TResult>(this T source, SqlQuery<TResult> query, string alias) where T : IJoin =>
        Join(source, GetQueryBuilder(query, nameof(query)), alias);

    /// <summary>
    /// 左外连接
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="table">表名</param>
    /// <param name="alias">别名</param>
    public static T LeftJoin<T>(this T source, string table, string alias = null) where T : IJoin
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.JoinClause.LeftJoin(table, alias);
        return source;
    }

    /// <summary>
    /// 追加原始左连接表表达式。
    /// 原始文本不会经过标识符解析、Schema 解析、方言格式化或别名注册；可通过 <c>AppendOn</c> 向最后一个连接继续添加条件。
    /// 调用方负责 SQL 安全性及通过 <c>AddParam</c> 显式提供占位符参数。
    /// </summary>
    /// <typeparam name="T">源类型。</typeparam>
    /// <param name="source">源。</param>
    /// <param name="sql">原始连接文本；空白文本将被忽略。</param>
    /// <returns>传入的同一个源对象。</returns>
    public static T AppendLeftJoin<T>(this T source, string sql) where T : IJoin
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.JoinClause.AppendLeftJoin(sql);
        return source;
    }

    /// <summary>
    /// 按条件追加原始左连接 SQL。
    /// </summary>
    /// <typeparam name="T">源类型。</typeparam>
    /// <param name="source">源。</param>
    /// <param name="sql">原始连接文本。</param>
    /// <param name="condition">是否添加。</param>
    /// <returns>传入的同一个源对象。</returns>
    public static T AppendLeftJoin<T>(this T source, string sql, bool condition) where T : IJoin =>
        condition ? AppendLeftJoin(source, sql) : source;

    /// <summary>
    /// 左外连接
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builder">Sql生成器</param>
    /// <param name="alias">别名</param>
    public static T LeftJoin<T>(this T source, ISqlBuilder builder, string alias) where T : IJoin
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.JoinClause.LeftJoin(builder, alias);
        return source;
    }

    /// <summary>
    /// 左外连接
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="action">子查询操作</param>
    /// <param name="alias">别名</param>
    public static T LeftJoin<T>(this T source, Action<ISqlBuilder> action, string alias) where T : IJoin
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.JoinClause.LeftJoin(action, alias);
        return source;
    }

    /// <summary>
    /// 使用指定结果类型的 Fluent 查询描述添加左连接子查询。
    /// </summary>
    /// <typeparam name="T">支持 Join 子句的源类型。</typeparam>
    /// <typeparam name="TResult">子查询描述的结果类型。</typeparam>
    /// <param name="source">当前查询源。</param>
    /// <param name="query">作为子查询使用的独立查询描述。</param>
    /// <param name="alias">子查询别名。</param>
    /// <returns>追加连接后的源对象。</returns>
    public static T LeftJoin<T, TResult>(this T source, SqlQuery<TResult> query, string alias) where T : IJoin =>
        LeftJoin(source, GetQueryBuilder(query, nameof(query)), alias);

    /// <summary>
    /// 右外连接
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="table">表名</param>
    /// <param name="alias">别名</param>
    public static T RightJoin<T>(this T source, string table, string alias = null) where T : IJoin
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.JoinClause.RightJoin(table, alias);
        return source;
    }

    /// <summary>
    /// 追加原始右连接表表达式。
    /// 原始文本不会经过标识符解析、Schema 解析、方言格式化或别名注册；可通过 <c>AppendOn</c> 向最后一个连接继续添加条件。
    /// 调用方负责 SQL 安全性及通过 <c>AddParam</c> 显式提供占位符参数。
    /// </summary>
    /// <typeparam name="T">源类型。</typeparam>
    /// <param name="source">源。</param>
    /// <param name="sql">原始连接文本；空白文本将被忽略。</param>
    /// <returns>传入的同一个源对象。</returns>
    public static T AppendRightJoin<T>(this T source, string sql) where T : IJoin
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.JoinClause.AppendRightJoin(sql);
        return source;
    }

    /// <summary>
    /// 按条件追加原始右连接 SQL。
    /// </summary>
    /// <typeparam name="T">源类型。</typeparam>
    /// <param name="source">源。</param>
    /// <param name="sql">原始连接文本。</param>
    /// <param name="condition">是否添加。</param>
    /// <returns>传入的同一个源对象。</returns>
    public static T AppendRightJoin<T>(this T source, string sql, bool condition) where T : IJoin =>
        condition ? AppendRightJoin(source, sql) : source;

    /// <summary>
    /// 右外连接
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builder">Sql生成器</param>
    /// <param name="alias">别名</param>
    public static T RightJoin<T>(this T source, ISqlBuilder builder, string alias) where T : IJoin
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.JoinClause.RightJoin(builder, alias);
        return source;
    }

    /// <summary>
    /// 右外连接
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="action">子查询操作</param>
    /// <param name="alias">别名</param>
    public static T RightJoin<T>(this T source, Action<ISqlBuilder> action, string alias) where T : IJoin
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.JoinClause.RightJoin(action, alias);
        return source;
    }

    /// <summary>
    /// 使用指定结果类型的 Fluent 查询描述添加右连接子查询。
    /// </summary>
    /// <typeparam name="T">支持 Join 子句的源类型。</typeparam>
    /// <typeparam name="TResult">子查询描述的结果类型。</typeparam>
    /// <param name="source">当前查询源。</param>
    /// <param name="query">作为子查询使用的独立查询描述。</param>
    /// <param name="alias">子查询别名。</param>
    /// <returns>追加连接后的源对象。</returns>
    public static T RightJoin<T, TResult>(this T source, SqlQuery<TResult> query, string alias) where T : IJoin =>
        RightJoin(source, GetQueryBuilder(query, nameof(query)), alias);

    /// <summary>
    /// 设置连接条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="condition">连接条件</param>
    public static T On<T>(this T source, ICondition condition) where T : IJoin
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.JoinClause.On(condition);
        return source;
    }

    /// <summary>
    /// 设置连接条件
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="left">左表列名</param>
    /// <param name="value">值</param>
    /// <param name="operator">条件运算符</param>
    public static T On<T>(this T source, string left, object value, Operator @operator = Operator.Equal)
        where T : IJoin
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.JoinClause.On(left, value, @operator);
        return source;
    }

    /// <summary>
    /// 向最后一个连接添加 On 原始条件。
    /// 没有连接时此调用会被忽略，条件不会保存并应用到后续连接。
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="sql">On 条件文本；方括号标识符会按当前方言解析。</param>
    /// <returns>传入的同一个源对象。</returns>
    public static T AppendOn<T>(this T source, string sql) where T : IJoin
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (SqlQueryOperationAccessor.GetClauseAccessor(source) is { } accessor)
            accessor.JoinClause.AppendOn(sql);
        return source;
    }
}
