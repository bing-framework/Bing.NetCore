using System.Security.Claims;
using Bing.Collections;

namespace Bing.Security.Claims;

/// <summary>
/// 配置身份主体创建时的静态、动态 Claims 贡献者和声明映射策略。
/// </summary>
public class BingClaimsPrincipalFactoryOptions
{
    /// <summary>
    /// 获取静态身份主体贡献者类型列表，用于扩展稳定的身份信息。
    /// </summary>
    /// <remarks>
    /// 适用于：<br />
    /// - 普通用户身份认证（OAuth2、Cookie 认证等）。<br />
    /// - 用户角色、权限固定的情况。
    /// </remarks>
    public ITypeList<IBingClaimsPrincipalContributor> Contributors { get; }

    /// <summary>
    /// 获取动态身份主体贡献者类型列表，用于在认证后计算或扩展 Claims。
    /// </summary>
    /// <remarks>
    /// 适用于：<br />
    /// - 需要动态计算用户角色、权限、租户信息的情况。<br />
    /// - 适配 多租户、RBAC、动态权限管理 需求。
    /// </remarks>
    public ITypeList<IBingDynamicClaimsPrincipalContributor> DynamicContributors { get; }

    /// <summary>
    /// 获取需要动态更新的 Claims 名称列表。
    /// </summary>
    /// <remarks>
    /// 默认包含：<br />
    /// - UserName、Name、SurName<br />
    /// - Role<br />
    /// - Email、EmailVerified<br />
    /// - PhoneNumber、PhoneNumberVerified
    /// </remarks>
    public List<string> DynamicClaims { get; }

    /// <summary>
    /// 获取或设置是否启用远程刷新 Claims 机制，默认值为 <c>true</c>。
    /// </summary>
    /// <remarks>
    /// 适用于：<br />
    /// - 微服务架构下的身份信息同步<br />
    /// - 用户信息变更后需要自动更新 Claims
    /// </remarks>
    public bool IsRemoteRefreshEnabled { get; set; }

    /// <summary>
    /// 获取或设置远程刷新 Claims 使用的相对 URL，默认值为 <c>/api/account/dynamic-claims/refresh</c>。
    /// </summary>
    /// <remarks>
    /// 默认值：`/api/account/dynamic-claims/refresh`<br />
    /// - 该 URL 用于 主动请求服务端刷新用户 Claims。
    /// </remarks>
    public string RemoteRefreshUrl { get; set; }

    /// <summary>
    /// 获取或设置框架 Claims 类型到外部 OpenID Connect 或 OAuth2 Claims 名称的映射表。
    /// </summary>
    /// <remarks>
    /// 默认映射：<br />
    /// - "preferred_username", "unique_name" → UserName<br />
    /// - "given_name" → Name<br />
    /// - "family_name" → SurName<br />
    /// - "roles", "role" → Role<br />
    /// - "email" → Email
    /// </remarks>
    public Dictionary<string, List<string>> ClaimsMap { get; set; }

    /// <summary>
    /// 获取或设置是否启用动态 Claims 计算，默认值为 <c>false</c>。
    /// </summary>
    /// <remarks>
    /// 适用于：<br />
    /// - 动态租户信息<br />
    /// - 权限动态计算<br />
    /// - 特定业务需求需要临时调整身份信息
    /// </remarks>
    public bool IsDynamicClaimsEnabled { get; set; }

    /// <summary>
    /// 初始化 <see cref="BingClaimsPrincipalFactoryOptions"/> 的实例及默认贡献者、Claims 列表和映射。
    /// </summary>
    public BingClaimsPrincipalFactoryOptions()
    {
        Contributors = new TypeList<IBingClaimsPrincipalContributor>();
        DynamicContributors = new TypeList<IBingDynamicClaimsPrincipalContributor>();
        DynamicClaims =
        [
            BingClaimTypes.UserName,
            BingClaimTypes.Name,
            BingClaimTypes.SurName,
            BingClaimTypes.Role,
            BingClaimTypes.Email,
            BingClaimTypes.EmailVerified,
            BingClaimTypes.PhoneNumber,
            BingClaimTypes.PhoneNumberVerified
        ];
        RemoteRefreshUrl = "/api/account/dynamic-claims/refresh";
        IsRemoteRefreshEnabled = true;
        ClaimsMap = new Dictionary<string, List<string>>
        {
            { BingClaimTypes.UserName, ["preferred_username", "unique_name", ClaimTypes.Name] },
            { BingClaimTypes.Name, ["given_name", ClaimTypes.GivenName] },
            { BingClaimTypes.SurName, ["family_name", ClaimTypes.Surname] },
            { BingClaimTypes.Role, ["role", "roles", ClaimTypes.Role] },
            { BingClaimTypes.Email, ["email", ClaimTypes.Email] },
        };
        IsDynamicClaimsEnabled = false;
    }
}
