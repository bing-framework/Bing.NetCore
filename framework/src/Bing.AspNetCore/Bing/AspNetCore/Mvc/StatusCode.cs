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
    /// 未授权的请求
    /// </summary>
    [Description("未授权的请求")]
    Unauthorized = 401
}
