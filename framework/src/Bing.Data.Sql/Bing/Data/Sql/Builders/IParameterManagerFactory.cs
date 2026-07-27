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
    /// <param name="dialect">决定参数前缀、名称生成与值转换规则的 SQL 方言。</param>
    /// <returns>独立保存当前 Builder 参数状态的参数管理器。</returns>
    IParameterManager Create(IDialect dialect);
}