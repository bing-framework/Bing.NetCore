using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL 通用组件访问器。
/// </summary>
public interface ISqlCommonPartAccessor
{
    /// <summary>
    /// 获取SQL 方言。
    /// </summary>
    IDialect Dialect { get; }

    /// <summary>
    /// 获取参数管理器。
    /// </summary>
    IParameterManager ParameterManager { get; }
}