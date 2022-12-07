using System.ComponentModel;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// 状态码
/// </summary>
public enum StatusCode
{
    /// <summary>
    /// 成功
    /// </summary>
    [Description("成功")]
    Ok = 1,

    /// <summary>
    /// 失败
    /// </summary>
    [Description("失败")]
    Fail = 2,

    /// <summary>
    /// 尚未登录
    /// </summary>
    [Description("尚未登录")]
    NotLogin = 401
}