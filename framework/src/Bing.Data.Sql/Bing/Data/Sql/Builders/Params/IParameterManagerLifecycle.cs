namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// 参数管理器生命周期扩展。
/// </summary>
/// <remarks>
/// 该接口为可选扩展，不改变既有 <see cref="IParameterManager"/> 的实现契约。
/// 自定义参数管理器实现此接口后，可在 Builder 创建空查询时保留其具体类型和配置。
/// </remarks>
public interface IParameterManagerLifecycle
{
    /// <summary>
    /// 创建保留当前配置但不包含参数和值的独立参数管理器。
    /// </summary>
    /// <returns>同配置的空参数管理器。</returns>
    IParameterManager CreateEmpty();
}