namespace Bing.Data.Sql.Builders;

/// <summary>
/// 暴露 SQL Provider 可选运行能力的契约。
/// </summary>
public interface ISqlProviderCapabilityProvider
{
    /// <summary>
    /// 当前 Provider 的运行能力。
    /// </summary>
    SqlProviderCapabilities Capabilities { get; }
}