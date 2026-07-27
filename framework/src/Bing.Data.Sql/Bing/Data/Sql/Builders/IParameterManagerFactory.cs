using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// 参数管理器工厂。
/// </summary>
public interface IParameterManagerFactory
{
    /// <summary>
    /// 创建参数管理器。
    /// </summary>
    IParameterManager Create(IDialect dialect);
}