using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 默认参数管理器工厂。
/// </summary>
public sealed class DefaultParameterManagerFactory : IParameterManagerFactory
{
    /// <summary>
    /// 默认实例。
    /// </summary>
    public static DefaultParameterManagerFactory Instance { get; } = new();

    /// <inheritdoc />
    public IParameterManager Create(IDialect dialect) => new ParameterManager(dialect);
}