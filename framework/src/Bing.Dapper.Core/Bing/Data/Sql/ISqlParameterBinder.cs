using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// Sql 参数绑定器。
/// </summary>
internal interface ISqlParameterBinder
{
    /// <summary>绑定 Sql 生成器参数。</summary>
    /// <param name="builder">用于提供参数上下文的 SQL 生成器。</param>
    /// <returns>绑定后的参数对象。</returns>
    object Bind(ISqlBuilder builder);

    /// <summary>绑定参数对象。</summary>
    /// <param name="parameter">待绑定的参数对象。</param>
    /// <returns>绑定后的参数对象。</returns>
    object Bind(object parameter);
}