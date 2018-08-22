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
}
