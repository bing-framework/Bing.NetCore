namespace Bing.Identity.JwtBearer;

/// <summary>
/// 配置 JWT 令牌签发与校验所需的参数。
/// </summary>
public class JwtOptions
{
    /// <summary>
    /// 获取或设置用于 HMAC-SHA256 签名和校验的密钥。
    /// </summary>
    /// <remarks>该值属于敏感凭据，应通过安全配置源提供，不应写入源码或日志。</remarks>
    public string Secret { get; set; }

    /// <summary>
    /// 获取或设置 JWT 发行方，默认值为 <c>bing_identity</c>。
    /// </summary>
    public string Issuer { get; set; } = "bing_identity";

    /// <summary>
    /// 获取或设置 JWT 受众，默认值为 <c>bing_client</c>。
    /// </summary>
    public string Audience { get; set; } = "bing_client";

    /// <summary>
    /// 获取或设置访问令牌的有效期，单位为分钟。
    /// </summary>
    public double AccessExpireMinutes { get; set; }

    /// <summary>
    /// 获取或设置刷新令牌的有效期，单位为分钟。
    /// </summary>
    public double RefreshExpireMinutes { get; set; }

    /// <summary>
    /// 获取或设置令牌处理失败时是否通过抛出异常报告错误。
    /// </summary>
    public bool ThrowEnabled { get; set; }

    /// <summary>
    /// 获取或设置是否限制同一用户同时仅保留一个有效设备会话。
    /// </summary>
    public bool SingleDeviceEnabled { get; set; }
}
