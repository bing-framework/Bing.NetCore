using System.ComponentModel;

namespace Bing.Security;

/// <summary>
/// 表示权限检查和资源访问决策的结果状态。
/// </summary>
public enum AuthorizationStatus
{
    /// <summary>
    /// 权限检查通过，允许继续访问资源。
    /// </summary>
    [Description("权限检查通过")]
    Ok = 200,

    /// <summary>
    /// 未登录，访问被拒绝。
    /// </summary>
    [Description("该操作需要登录后才能继续进行")]
    Unauthorized = 401,

    /// <summary>
    /// 已登录但认证已超时，需要刷新令牌或重新登录。
    /// </summary>
    [Description("登录已超时，请刷新令牌")]
    LoginTimeout = 402,

    /// <summary>
    /// 已登录但权限不足，访问被拒绝。
    /// </summary>
    [Description("当前用户权限不足，不能继续执行")]
    Forbidden = 403,

    /// <summary>
    /// 指定功能或资源不存在。
    /// </summary>
    [Description("指定的功能不存在")]
    NoFound = 404,

    /// <summary>
    /// 指定功能或资源已被锁定。
    /// </summary>
    [Description("指定的功能被锁定")]
    Locked = 423,

    /// <summary>
    /// 账号已在其他设备登录，当前访问被拒绝。
    /// </summary>
    [Description("该账号已在其它设备登录")]
    OtherDeviceLogin = 424,

    /// <summary>
    /// 权限检查过程中发生未分类错误。
    /// </summary>
    [Description("权限检查出现错误")]
    Error = 500
}