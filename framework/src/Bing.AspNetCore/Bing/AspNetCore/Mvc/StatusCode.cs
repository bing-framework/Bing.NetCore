using System.ComponentModel;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// 表示框架 API 响应使用的业务状态码。
/// </summary>
public enum StatusCode
{
    /// <summary>
    /// 操作成功。
    /// </summary>
    [Description("成功")]
    Ok = 1,

    /// <summary>
    /// 操作失败。
    /// </summary>
    [Description("失败")]
    Fail = 2,

    /// <summary>
    /// 请求未通过授权。
    /// </summary>
    [Description("未授权的请求")]
    Unauthorized = 401
}
