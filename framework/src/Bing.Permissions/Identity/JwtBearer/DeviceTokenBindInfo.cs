using System;

namespace Bing.Permissions.Identity.JwtBearer;

/// <summary>
/// 设备令牌绑定信息
/// </summary>
[Serializable]
public class DeviceTokenBindInfo
{
    /// <summary>
    /// 用户标识
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// 设备标识
    /// </summary>
    public string DeviceId { get; set; }

    /// <summary>
    /// 设备类型
    /// </summary>
    public string DeviceType { get; set; }

    /// <summary>
    /// 令牌
    /// </summary>
    public JsonWebToken Token { get; set; }
}