using Bing.DependencyInjection;

namespace Bing.MultiTenancy;

/// <summary>
/// 租户规范化器，使用不区分区域性的大写转换方式 (`ToUpperInvariant()`) 进行规范化。
/// </summary>
public class UpperInvariantTenantNormalizer : ITenantNormalizer, ITransientDependency
{
    /// <inheritdoc />
    public string? NormalizeName(string? name) => name?.Normalize().ToUpperInvariant();
}
