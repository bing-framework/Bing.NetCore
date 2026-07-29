using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL 通用组件访问器。
/// </summary>
public interface ISqlCommonPartAccessor
{
    /// <summary>
    /// SQL 方言。
    /// </summary>
    IDialect Dialect { get; }

    /// <summary>
    /// 参数管理器。
    /// </summary>
    IParameterManager ParameterManager { get; }
}