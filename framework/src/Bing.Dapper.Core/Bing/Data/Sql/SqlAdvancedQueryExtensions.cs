using System.ComponentModel;

namespace Bing.Data.Sql;

/// <summary>
/// Dapper 低层结果固定和多映射查询的 Advanced 入口。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class SqlAdvancedQueryExtensions
{
    /// <summary>创建指定结果类型的低层 Fluent 查询描述。</summary>
    public static SqlFluentQuery<TResult> Query<TResult>(this ISqlQuery query) =>
        GetQuery(query).Query<TResult>();

    /// <summary>创建指定结果类型的原生 SQL 查询描述。</summary>
    public static SqlTextQuery<TResult> Sql<TResult>(this ISqlQuery query, string sql, object parameters = null) =>
        GetQuery(query).Sql<TResult>(sql, parameters);

    /// <summary>创建指定结果类型的插值 SQL 查询描述。</summary>
    public static SqlTextQuery<TResult> SqlInterpolated<TResult>(this ISqlQuery query, FormattableString sql) =>
        GetQuery(query).SqlInterpolated<TResult>(sql);

    /// <summary>创建指定结果类型的存储过程查询描述。</summary>
    public static SqlProcedureQuery<TResult> Procedure<TResult>(this ISqlQuery query, string procedure,
        object parameters = null) => GetQuery(query).Procedure<TResult>(procedure, parameters);

    private static SqlQueryBase GetQuery(ISqlQuery query)
    {
        if (query is not SqlQueryBase queryBase)
            throw new ArgumentException("查询对象必须由 Dapper Core 创建。", nameof(query));
        return queryBase;
    }
}