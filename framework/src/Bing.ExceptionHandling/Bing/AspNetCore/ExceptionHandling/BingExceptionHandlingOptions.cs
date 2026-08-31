namespace Bing.AspNetCore.ExceptionHandling;

/// <summary>
/// 配置异常响应是否向客户端暴露异常详情和堆栈信息。
/// </summary>
public class BingExceptionHandlingOptions
{
    /// <summary>
    /// 获取或设置是否向客户端返回异常详情，默认不启用；生产环境通常不应暴露内部异常信息。
    /// </summary>
    public bool SendExceptionDetailsToClients { get; set; } = false;

    /// <summary>
    /// 获取或设置是否向客户端返回堆栈跟踪信息，默认值为 <c>true</c>；生产环境应结合安全要求谨慎配置。
    /// </summary>
    public bool SendStackTraceToClients { get; set; } = true;
}
