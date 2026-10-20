using Bing.DependencyInjection;

namespace Bing.MultiTenancy;

/// <summary>
/// 使用不依赖当前区域性的规则规范化租户名称。
/// </summary>
public class UpperInvariantTenantNormalizer : ITenantNormalizer, ITransientDependency
{
    /// <inheritdoc />
    /// <remarks>输入为 <c>null</c> 时返回 <c>null</c>；非空值先进行 Unicode 规范化，再执行不受区域性影响的大写转换。</remarks>
    public string? NormalizeName(string? name) => name?.Normalize().ToUpperInvariant();
}
