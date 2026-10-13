using Bing.DependencyInjection;

namespace Bing.MultiTenancy;

/// <summary>
/// 空的租户解析结果访问器
/// </summary>
public class NullTenantResolveResultAccessor : ITenantResolveResultAccessor, ISingletonDependency
{
    /// <inheritdoc />
    public TenantResolveResult? Result { get => null; set { } }
}
