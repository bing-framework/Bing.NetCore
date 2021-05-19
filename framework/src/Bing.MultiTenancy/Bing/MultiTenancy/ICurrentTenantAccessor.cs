namespace Bing.MultiTenancy
{
    /// <summary>
    /// 当前租户访问器
    /// </summary>
    public interface ICurrentTenantAccessor
    {
        /// <summary>
        /// 当前基本租户信息
        /// </summary>
        BasicTenantInfo Current { get; set; }
    }
}
