namespace Bing.Domains.Entities.Tenants
{
    /// <summary>
    /// 租户
    /// </summary>
    public interface ITenant
    {
        /// <summary>
        /// 租户编号
        /// </summary>
        string TenantId { get; set; }
    }

    /// <summary>
    /// 租户
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    public interface ITenant<TKey>
    {
        /// <summary>
        /// 租户标识
        /// </summary>
        TKey TenantId { get; set; }
    }
}
