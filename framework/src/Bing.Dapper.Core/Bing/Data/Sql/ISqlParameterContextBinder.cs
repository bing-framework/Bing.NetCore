using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql;

/// <summary>
/// 支持数据库上下文的 Sql 参数绑定器。
/// </summary>
internal interface ISqlParameterContextBinder : ISqlParameterBinder
{
    /// <summary>
    /// 绑定 SQL 参数。
    /// </summary>
    /// <param name="builder">Sql 生成器。</param>
    /// <param name="options">Sql 配置。</param>
    /// <returns>绑定后的参数对象。</returns>
    object Bind(ISqlBuilder builder, SqlOptions options);

    /// <summary>
    /// 绑定 SQL 参数。
    /// </summary>
    /// <param name="builder">Sql 生成器。</param>
    /// <param name="options">Sql 配置。</param>
    /// <param name="context">参数绑定上下文。</param>
    /// <returns>绑定后的参数对象。</returns>
    object Bind(ISqlBuilder builder, SqlOptions options, SqlParameterBindingContext context);

    /// <summary>
    /// 绑定 SQL 参数。
    /// </summary>
    /// <param name="parameter">参数对象。</param>
    /// <param name="options">Sql 配置。</param>
    /// <returns>绑定后的参数对象。</returns>
    object Bind(object parameter, SqlOptions options);

    /// <summary>
    /// 绑定 SQL 参数。
    /// </summary>
    /// <param name="parameter">参数对象。</param>
    /// <param name="options">Sql 配置。</param>
    /// <param name="context">参数绑定上下文。</param>
    /// <returns>绑定后的参数对象。</returns>
    object Bind(object parameter, SqlOptions options, SqlParameterBindingContext context);

    /// <summary>
    /// 获取 SQL 增强参数集合。
    /// </summary>
    /// <param name="builder">Sql 生成器。</param>
    /// <param name="options">Sql 配置。</param>
    /// <returns>Sql 增强参数集合。</returns>
    IReadOnlyCollection<SqlParam> GetSqlParams(ISqlBuilder builder, SqlOptions options);

    /// <summary>
    /// 获取 SQL 增强参数集合。
    /// </summary>
    /// <param name="builder">Sql 生成器。</param>
    /// <param name="options">Sql 配置。</param>
    /// <param name="context">参数绑定上下文。</param>
    /// <returns>Sql 增强参数集合。</returns>
    IReadOnlyCollection<SqlParam> GetSqlParams(ISqlBuilder builder, SqlOptions options,
        SqlParameterBindingContext context);

    /// <summary>
    /// 获取 SQL 增强参数集合。
    /// </summary>
    /// <param name="parameter">参数对象。</param>
    /// <param name="options">Sql 配置。</param>
    /// <returns>Sql 增强参数集合。</returns>
    IReadOnlyCollection<SqlParam> GetSqlParams(object parameter, SqlOptions options);

    /// <summary>
    /// 获取 SQL 增强参数集合。
    /// </summary>
    /// <param name="parameter">参数对象。</param>
    /// <param name="options">Sql 配置。</param>
    /// <param name="context">参数绑定上下文。</param>
    /// <returns>Sql 增强参数集合。</returns>
    IReadOnlyCollection<SqlParam> GetSqlParams(object parameter, SqlOptions options,
        SqlParameterBindingContext context);
}
