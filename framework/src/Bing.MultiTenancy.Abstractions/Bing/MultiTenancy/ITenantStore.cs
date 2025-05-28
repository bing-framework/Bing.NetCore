namespace Bing.MultiTenancy;

/// <summary>
/// 租户存储器，用于检索租户配置信息。
/// </summary>
public interface ITenantStore
{
    /// <summary>
    /// 通过标准化名称查找租户配置。
    /// </summary>
    /// <param name="normalizedName">租户的标准化名称。</param>
    /// <returns>
    /// 返回 <see cref="TenantConfiguration"/>，如果未找到匹配的租户，则返回 <c>null</c>。
    /// </returns>
    Task<TenantConfiguration?> FindByNameAsync(string normalizedName);

    /// <summary>
    /// 通过租户 ID 查找租户配置。
    /// </summary>
    /// <param name="id">租户的唯一标识符。</param>
    /// <returns>
    /// 返回 <see cref="TenantConfiguration"/>，如果未找到匹配的租户，则返回 <c>null</c>。
    /// </returns>
    Task<TenantConfiguration?> FindByIdAsync(string id);

    /// <summary>
    /// 获取所有租户的配置列表。
    /// </summary>
    /// <param name="includeDetails">
    /// 是否包含详细信息：<br />
    /// - <c>true</c>：包含所有详细租户信息（如数据库连接、缓存配置等）。<br />
    /// - <c>false</c>（默认）：仅返回基本租户信息。
    /// </param>
    /// <returns>
    /// 返回只读列表 <see cref="IReadOnlyList{T}"/>，包含所有租户的 <see cref="TenantConfiguration"/>。
    /// </returns>
    Task<IReadOnlyList<TenantConfiguration>> GetListAsync(bool includeDetails = false);
}
