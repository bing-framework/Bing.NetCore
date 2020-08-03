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
        /// 姓名。默认："full_name"
        /// </summary>
        public static string FullName { get; set; } = "full_name";

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
        /// 已验证电子邮件。默认："email_verified"
        /// </summary>
        public static string EmailVerified { get; set; } = "email_verified";

        /// <summary>
        /// 手机号码。默认："phone_number"
        /// </summary>
        public static string PhoneNumber { get; set; } = "phone_number";

        /// <summary>
        /// 已验证手机号码。默认："phone_number_verified"
        /// </summary>
        public static string PhoneNumberVerified { get; set; } = "phone_number_verified";

        /// <summary>
        /// 租户标识。默认："tenant_id"
        /// </summary>
        public static string TenantId { get; set; } = "tenant_id";

        /// <summary>
        /// 租户编码。默认："tenant_code"
        /// </summary>
        public static string TenantCode { get; set; } = "tenant_code";

        /// <summary>
        /// 租户名称。默认："tenant_name"
        /// </summary>
        public static string TenantName { get; set; } = "tenant_name";

        /// <summary>
        /// 版本标识。默认："edition_id"
        /// </summary>
        public static string EditionId { get; set; } = "edition_id";

        /// <summary>
        /// 客户端标识。默认："client_id"
        /// </summary>
        public static string ClientId { get; set; } = "client_id";

        /// <summary>
        /// 应用程序标识。默认："application_id"
        /// </summary>
        public static string ApplicationId { get; set; } = "application_id";

        /// <summary>
        /// 应用程序编码
        /// </summary>
        public static string ApplicationCode { get; set; } = "application_code";

        /// <summary>
        /// 应用程序名称
        /// </summary>
        public static string ApplicationName { get; set; } = "application_name";
    }
}
