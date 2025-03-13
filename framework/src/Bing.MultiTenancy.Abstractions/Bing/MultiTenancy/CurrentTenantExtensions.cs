using Bing.Helpers;

namespace Bing.MultiTenancy;

/// <summary>
/// 当前租户(<see cref="ICurrentTenant"/>) 扩展
/// </summary>
public static class CurrentTenantExtensions
{
    /// <summary>
    /// 获取当前租户的 ID，并转换为 <see cref="Guid"/> 类型。
    /// </summary>
    /// <param name="currentTenant">当前租户信息。</param>
    /// <returns>返回租户的 <see cref="Guid"/> ID。</returns>
    /// <exception cref="ArgumentNullException">如果 <paramref name="currentTenant"/> 为空，则抛出此异常。</exception>
    /// <exception cref="ArgumentException">如果租户 ID 为空或无效，则抛出此异常。</exception>
    public static Guid GetId(this ICurrentTenant currentTenant)
    {
        Check.NotNull(currentTenant, nameof(currentTenant));
        if (string.IsNullOrWhiteSpace(currentTenant.Id))
            throw new ArgumentException("Current Tenant Id is not available!");
        return Conv.ToGuid(currentTenant.Id);
    }

    /// <summary>
    /// 获取当前租户的 ID，并转换为指定的类型 <typeparamref name="T"/>。
    /// </summary>
    /// <typeparam name="T">目标 ID 类型，例如 <see cref="Guid"/> 或 <see cref="int"/>。</typeparam>
    /// <param name="currentTenant">当前租户信息。</param>
    /// <returns>返回租户的 ID，转换为类型 <typeparamref name="T"/>。</returns>
    /// <exception cref="ArgumentNullException">如果 <paramref name="currentTenant"/> 为空，则抛出此异常。</exception>
    /// <exception cref="ArgumentException">如果租户 ID 为空或无效，则抛出此异常。</exception>
    public static T GetId<T>(this ICurrentTenant currentTenant)
    {
        Check.NotNull(currentTenant, nameof(currentTenant));
        if (string.IsNullOrWhiteSpace(currentTenant.Id))
            throw new ArgumentException("Current Tenant Id is not available!");
        return Conv.To<T>(currentTenant.Id);
    }
}
