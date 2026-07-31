using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// Sql 参数绑定器。
/// </summary>
public interface ISqlParameterBinder
{
    /// <summary>绑定 Sql 生成器参数。</summary>
    object Bind(ISqlBuilder builder);

    /// <summary>绑定参数对象。</summary>
    object Bind(object parameter);
}