using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql;

/// <summary>
/// Dapper 增强参数集访问器。
/// </summary>
public interface IDapperParameterSet
{
    /// <summary>
    /// 获取增强参数集合。
    /// </summary>
    IReadOnlyCollection<SqlParam> Parameters { get; }
}