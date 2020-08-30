namespace Bing.Admin.Domain.Shared
{
    /// <summary>
    /// 角色编码
    /// </summary>
    public static class RoleCode
    {
        /// <summary>
        /// 超级管理员
        /// </summary>
        public static string SuperAdmin => "SuperAdmin";

        /// <summary>
        /// 系统管理员
        /// </summary>
        public static string Admin => "SystemAdmin";

        /// <summary>
        /// 租户管理员
        /// </summary>
        public static string TenantAdmin => "TenantAdmin";
    }

    /// <summary>
    /// 角色类型编码
    /// </summary>
    public static class RoleTypeCode
    {
        /// <summary>
        /// 系统角色
        /// </summary>
        public static string SystemRole => "SystemRole";

        /// <summary>
        /// 租户角色
        /// </summary>
        public static string TenantRole => "TenantRole";
    }
}
