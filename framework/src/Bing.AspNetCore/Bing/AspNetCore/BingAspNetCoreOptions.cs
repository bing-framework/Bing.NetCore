namespace Bing.AspNetCore;

/// <summary>
/// 配置 Bing ASP.NET Core 管道的调试输出、异常状态码和模型验证响应行为。
/// </summary>
public class BingAspNetCoreOptions
{
    /// <summary>
    /// 获取或设置是否输出调试信息，默认值为 <c>true</c>。
    /// </summary>
    public bool IsDebug { get; set; } = true;

    /// <summary>
    /// 获取或设置是否将响应中的业务异常转换为对应的 HTTP 状态码，默认值为 <c>false</c>。
    /// </summary>
    public bool UseResponseExceptionToHttpCode { get; set; } = false;

    /// <summary>
    /// 获取或设置是否在响应中返回全部模型验证错误，默认值为 <c>false</c>。
    /// </summary>
    public bool ResponseAllModelError { get; set; } = false;
}