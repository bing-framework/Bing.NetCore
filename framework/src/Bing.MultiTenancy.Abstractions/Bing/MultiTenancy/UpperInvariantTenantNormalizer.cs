using Bing.DependencyInjection;

namespace Bing.MultiTenancy;

/// <summary>
/// 租户规范化器，使用不区分区域性的大写转换方式 <c>ToUpperInvariant()</c> 进行规范化。
/// </summary>
public class UpperInvariantTenantNormalizer : ITenantNormalizer, ITransientDependency
{
    /// <inheritdoc />
    /// <remarks>输入为 <c>null</c> 时返回 <c>null</c>；非空值先进行 Unicode 规范化，再执行不受区域性影响的大写转换。</remarks>
    public string? NormalizeName(string? name) => name?.Normalize().ToUpperInvariant();
}
