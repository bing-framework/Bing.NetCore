namespace Bing.AspNetCore.ExceptionHandling;

/// <summary>
/// 异常处理选项配置
/// </summary>
public class BingExceptionHandlingOptions
{
    /// <summary>
    /// 发送异常详情到客户端
    /// </summary>
    public bool SendExceptionDetailsToClients { get; set; } = false;

    /// <summary>
    /// 发送堆栈跟踪信息到客户端
    /// </summary>
    public bool SendStackTraceToClients { get; set; } = true;
}
