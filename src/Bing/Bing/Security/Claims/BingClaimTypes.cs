using System.Security.Claims;

namespace Bing.Security.Claims
{
    /// <summary>
    /// 声明类型
    /// </summary>
    public static class BingClaimTypes
    {
        /// <summary>
        /// 用户名。默认：<see cref="ClaimTypes.Name"/>
        /// </summary>
        public static string UserName { get; set; } = ClaimTypes.Name;

        /// <summary>
        /// 用户标识。默认：<see cref="ClaimTypes.NameIdentifier"/>
        /// </summary>
        public static string UserId { get; set; } = ClaimTypes.NameIdentifier;

        /// <summary>
        /// 角色。默认：<see cref="ClaimTypes.Role"/>
        /// </summary>
        public static string Role { get; set; } = ClaimTypes.Role;

        /// <summary>
        /// 电子邮件。默认：<see cref="ClaimTypes.Email"/>
        /// </summary>
        public static string Email { get; set; } = ClaimTypes.Email;

        /// <summary>
        /// 手机号码。默认："phone_number"
        /// </summary>
        public static string PhoneNumber { get; set; } = "phone_number";

        /// <summary>
        /// 租户标识。默认："tenant_id"
        /// </summary>
        public static string TenantId { get; set; } = "tenant_id";

        /// <summary>
        /// 版本标识。默认："edition_id"
        /// </summary>
        public static string EditionId { get; set; } = "edition_id";

        /// <summary>
        /// 客户端标识。默认："client_id"
        /// </summary>
        public static string ClientId { get; set; } = "client_id";
    }
}
