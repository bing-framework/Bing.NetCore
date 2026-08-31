using Bing.Security.Claims;

namespace Bing.AspNetCore.Security.Claims;

/// <summary>
/// 配置外部身份令牌声明名称到框架标准声明类型的映射。
/// </summary>
public class BingClaimsMapOptions
{
    /// <summary>
    /// 获取外部声明名称到标准声明类型名称的映射表。
    /// </summary>
    /// <remarks>构造函数会预置常见 OpenID Connect 声明映射；修改集合会影响后续声明转换。</remarks>
    public Dictionary<string, Func<string>> Maps { get; private set; }

    /// <summary>
    /// 初始化 <see cref="BingClaimsMapOptions"/> 的实例及常见外部声明映射。
    /// </summary>
    public BingClaimsMapOptions()
    {
        Maps = new Dictionary<string, Func<string>>
        {
            {"sub", () => BingClaimTypes.UserId},
            {"role", () => BingClaimTypes.Role},
            {"email", () => BingClaimTypes.Email},
            {"name", () => BingClaimTypes.UserName},
            {"family_name", () => BingClaimTypes.SurName},
            {"given_name", () => BingClaimTypes.Name},
        };
    }
}
