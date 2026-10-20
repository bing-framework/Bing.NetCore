namespace Bing.Security.Claims;

/// <summary>
/// 声明类型
/// </summary>
public static class BingClaimTypes
{
    /// <summary>
    /// 获取或设置用户名对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <see cref="System.Security.Claims.ClaimTypes.Name"/>。</remarks>
    public static string UserName { get; set; } = System.Security.Claims.ClaimTypes.Name;

    /// <summary>
    /// 获取或设置名字对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <see cref="System.Security.Claims.ClaimTypes.GivenName"/>。</remarks>
    public static string Name { get; set; } = System.Security.Claims.ClaimTypes.GivenName;

    /// <summary>
    /// 获取或设置姓氏对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <see cref="System.Security.Claims.ClaimTypes.Surname"/>。</remarks>
    public static string SurName { get; set; } = System.Security.Claims.ClaimTypes.Surname;

    /// <summary>
    /// 获取或设置全名对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>full_name</c>。</remarks>
    public static string FullName { get; set; } = "full_name";

    /// <summary>
    /// 获取或设置用户标识对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/>。</remarks>
    public static string UserId { get; set; } = System.Security.Claims.ClaimTypes.NameIdentifier;

    /// <summary>
    /// 获取或设置电子邮件对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <see cref="System.Security.Claims.ClaimTypes.Email"/>。</remarks>
    public static string Email { get; set; } = System.Security.Claims.ClaimTypes.Email;

    /// <summary>
    /// 获取或设置电子邮件验证状态对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>email_verified</c>。</remarks>
    public static string EmailVerified { get; set; } = "email_verified";

    /// <summary>
    /// 获取或设置手机号码对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>phone_number</c>。</remarks>
    public static string PhoneNumber { get; set; } = "phone_number";

    /// <summary>
    /// 获取或设置手机号码验证状态对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>phone_number_verified</c>。</remarks>
    public static string PhoneNumberVerified { get; set; } = "phone_number_verified";

    /// <summary>
    /// 获取或设置版本标识对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>edition_id</c>。</remarks>
    public static string EditionId { get; set; } = "edition_id";

    /// <summary>
    /// 获取或设置客户端标识对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>client_id</c>。</remarks>
    public static string ClientId { get; set; } = "client_id";

    /// <summary>
    /// 获取或设置用户头像对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>picture</c>。</remarks>
    public static string Picture { get; set; } = "picture";

    /// <summary>
    /// 获取或设置记住登录状态对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>remember_me</c>。</remarks>
    public static string RememberMe { get; set; } = "remember_me";

    /// <summary>
    /// 获取或设置会话标识对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>session_id</c>。</remarks>
    public static string SessionId { get; set; }= "session_id";

    #region Application(应用程序)

    /// <summary>
    /// 获取或设置应用程序标识对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>application_id</c>。</remarks>
    public static string ApplicationId { get; set; } = "application_id";

    /// <summary>
    /// 获取或设置应用程序编码对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>application_code</c>。</remarks>
    public static string ApplicationCode { get; set; } = "application_code";

    /// <summary>
    /// 获取或设置应用程序名称对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>application_name</c>。</remarks>
    public static string ApplicationName { get; set; } = "application_name";

    #endregion

    #region Tenant(租户)

    /// <summary>
    /// 获取或设置租户标识对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>tenant_id</c>。</remarks>
    public static string TenantId { get; set; } = "tenant_id";

    /// <summary>
    /// 获取或设置租户编码对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>tenant_code</c>。</remarks>
    public static string TenantCode { get; set; } = "tenant_code";

    /// <summary>
    /// 获取或设置租户名称对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>tenant_name</c>。</remarks>
    public static string TenantName { get; set; } = "tenant_name";

    #endregion

    #region Impersonator(模拟租户用户)

    /// <summary>
    /// 获取或设置被模拟租户标识对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>impersonator_tenant_id</c>。</remarks>
    public static string ImpersonatorTenantId { get; set; } = "impersonator_tenant_id";

    /// <summary>
    /// 获取或设置被模拟租户编码对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>impersonator_tenant_code</c>。</remarks>
    public static string ImpersonatorTenantCode { get; set; } = "impersonator_tenant_code";

    /// <summary>
    /// 获取或设置被模拟租户名称对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>impersonator_tenant_name</c>。</remarks>
    public static string ImpersonatorTenantName { get; set; } = "impersonator_tenant_name";

    /// <summary>
    /// 获取或设置被模拟用户标识对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>impersonator_userid</c>。</remarks>
    public static string ImpersonatorUserId { get; set; } = "impersonator_userid";

    /// <summary>
    /// 获取或设置被模拟用户名对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>impersonator_username</c>。</remarks>
    public static string ImpersonatorUserName { get; set; } = "impersonator_username";

    #endregion

    #region Role(角色)

    /// <summary>
    /// 获取或设置角色对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <see cref="System.Security.Claims.ClaimTypes.Role"/>。</remarks>
    public static string Role { get; set; } = System.Security.Claims.ClaimTypes.Role;

    /// <summary>
    /// 获取或设置角色标识列表对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>role_ids</c>。</remarks>
    public static string RoleIds { get; set; } = "role_ids";

    /// <summary>
    /// 获取或设置角色编码列表对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>role_codes</c>。</remarks>
    public static string RoleCodes { get; set; } = "role_codes";

    /// <summary>
    /// 获取或设置角色名称列表对应的 Claim 类型名称。
    /// </summary>
    /// <remarks>初始值为 <c>role_names</c>。</remarks>
    public static string RoleNames { get; set; } = "role_names";

    #endregion

}
