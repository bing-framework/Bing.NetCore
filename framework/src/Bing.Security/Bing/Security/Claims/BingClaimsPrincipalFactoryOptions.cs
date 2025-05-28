using System.Security.Claims;
using Bing.Collections;

namespace Bing.Security.Claims;

/// <summary>
/// 身份主体工厂选项
/// </summary>
public class BingClaimsPrincipalFactoryOptions
{
    /// <summary>
    /// 静态 身份主体 贡献者列表，用于 固定身份信息的扩展。
    /// </summary>
    /// /// <remarks>
    /// 适用于：<br />
    /// - 普通用户身份认证（OAuth2、Cookie 认证等）。<br />
    /// - 用户角色、权限固定的情况。
    /// </remarks>
    public ITypeList<IBingClaimsPrincipalContributor> Contributors { get; }

    /// <summary>
    /// 动态 身份主体 贡献者列表，用于 身份认证后可动态扩展 Claims。
    /// </summary>
    /// <remarks>
    /// 适用于：<br />
    /// - 需要动态计算用户角色、权限、租户信息的情况。<br />
    /// - 适配 多租户、RBAC、动态权限管理 需求。
    /// </remarks>
    public ITypeList<IBingDynamicClaimsPrincipalContributor> DynamicContributors { get; }

    /// <summary>
    /// 需要 动态更新 的 Claims 名称列表。
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
    /// 是否启用 远程刷新 Claims 机制，默认值：true。
    /// </summary>
    /// <remarks>
    /// 适用于：<br />
    /// - 微服务架构下的身份信息同步<br />
    /// - 用户信息变更后需要自动更新 Claims
    /// </remarks>
    public bool IsRemoteRefreshEnabled { get; set; }

    /// <summary>
    /// 远程刷新 Claims 的 URL 地址。
    /// </summary>
    /// <remarks>
    /// 默认值：`/api/account/dynamic-claims/refresh`<br />
    /// - 该 URL 用于 主动请求服务端刷新用户 Claims。
    /// </remarks>
    public string RemoteRefreshUrl { get; set; }

    /// <summary>
    /// Claims 映射关系表，用于转换 OpenID Connect / OAuth2 的 Claims 名称。
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
    /// 是否 启用动态 Claims 计算，默认值：false。
    /// </summary>
    /// <remarks>
    /// 适用于：<br />
    /// - 动态租户信息<br />
    /// - 权限动态计算<br />
    /// - 特定业务需求需要临时调整身份信息
    /// </remarks>
    public bool IsDynamicClaimsEnabled { get; set; }

    /// <summary>
    /// 初始化一个<see cref="BingClaimsPrincipalFactoryOptions"/> 类型的实例。
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
