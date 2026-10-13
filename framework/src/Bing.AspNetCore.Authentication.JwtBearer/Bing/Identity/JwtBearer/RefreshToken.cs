namespace Bing.Identity.JwtBearer;

/// <summary>
/// 刷新Token信息
/// </summary>
[Serializable]
public class RefreshToken
{
    /// <summary>
    /// 获取或设置客户端ID
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// 获取或设置标识值
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// 获取或设置过期时间
    /// </summary>
    public DateTime EndUtcTime { get; set; }
}
