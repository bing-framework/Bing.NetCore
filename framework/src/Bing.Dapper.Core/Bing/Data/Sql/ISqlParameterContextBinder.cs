using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql;

/// <summary>
/// 支持数据库上下文的 Sql 参数绑定器。
/// </summary>
internal interface ISqlParameterContextBinder : ISqlParameterBinder
{
    /// <summary>绑定 Sql 生成器参数。</summary>
    object Bind(ISqlBuilder builder, SqlOptions options);

    /// <summary>使用执行上下文绑定 Sql 生成器参数。</summary>
    object Bind(ISqlBuilder builder, SqlOptions options, SqlParameterBindingContext context);

    /// <summary>绑定参数对象。</summary>
    object Bind(object parameter, SqlOptions options);

    /// <summary>使用执行上下文绑定参数对象。</summary>
    object Bind(object parameter, SqlOptions options, SqlParameterBindingContext context);

    /// <summary>获取 Sql 增强参数集合。</summary>
    IReadOnlyCollection<SqlParam> GetSqlParams(ISqlBuilder builder, SqlOptions options);

    /// <summary>使用执行上下文获取 Sql 增强参数集合。</summary>
    IReadOnlyCollection<SqlParam> GetSqlParams(ISqlBuilder builder, SqlOptions options,
        SqlParameterBindingContext context);

    /// <summary>获取参数对象中的 Sql 增强参数集合。</summary>
    IReadOnlyCollection<SqlParam> GetSqlParams(object parameter, SqlOptions options);

    /// <summary>使用执行上下文获取参数对象中的 Sql 增强参数集合。</summary>
    IReadOnlyCollection<SqlParam> GetSqlParams(object parameter, SqlOptions options,
        SqlParameterBindingContext context);
}