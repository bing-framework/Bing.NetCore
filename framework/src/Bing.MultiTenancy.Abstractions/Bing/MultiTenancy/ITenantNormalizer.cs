namespace Bing.MultiTenancy;

/// <summary>
/// 租户规范化器
/// </summary>
public interface ITenantNormalizer
{
    /// <summary>
    /// 规范化租户名称。
    /// </summary>
    /// <param name="name">要标准化的租户名称，可以为 <c>null</c>。</param>
    /// <returns>
    /// 返回标准化后的租户名称，如果输入为空或无效，则可能返回 <c>null</c> 或默认格式化值。
    /// </returns>
    string? NormalizeName(string? name);
}
