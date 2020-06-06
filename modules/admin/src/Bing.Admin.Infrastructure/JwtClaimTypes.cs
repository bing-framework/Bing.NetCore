namespace Bing.Admin.Infrastructure
{
    /// <summary>
    /// Jwt声明类型
    /// </summary>
    public struct JwtClaimTypes
    {
        /// <summary>
        /// 是否管理
        /// </summary>
        public const string IsAdmin = "is_admin";

        /// <summary>
        /// 角色编码
        /// </summary>
        public const string RoleCode = "role_code";

        /// <summary>
        /// 客户端标识
        /// </summary>
        public const string ClientId = "clientId";

        /// <summary>
        /// 应用ID
        /// </summary>
        public const string AppId = "appId";
    }
}
