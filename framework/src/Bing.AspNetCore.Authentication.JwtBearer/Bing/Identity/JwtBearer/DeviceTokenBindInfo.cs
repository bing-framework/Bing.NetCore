namespace Bing.Identity.JwtBearer;

/// <summary>
/// 表示用户、设备与访问令牌之间的绑定信息。
/// </summary>
[Serializable]
public class DeviceTokenBindInfo
{
    /// <summary>
    /// 获取或设置绑定令牌的用户标识。
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// 获取或设置绑定令牌的设备标识。
    /// </summary>
    public string DeviceId { get; set; }

    /// <summary>
    /// 获取或设置绑定设备的类型名称。
    /// </summary>
    public string DeviceType { get; set; }

    /// <summary>
    /// 获取或设置与用户和设备绑定的 JSON Web Token。
    /// </summary>
    public JsonWebToken Token { get; set; }
}
