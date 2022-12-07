using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

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
    /// 额外异常
    /// </summary>
    public Func<HttpContext, ILogger, Exception, Task> OnException { get; set; } = (context, logger, exception) =>
    {
        logger.LogError(exception, $"Request exception, requestId: {context.TraceIdentifier}");
        return Task.CompletedTask;
    };

    /// <summary>
    /// 请求终止
    /// </summary>
    public Func<HttpContext, ILogger, Task> OnRequestAborted { get; set; } = (context, logger) =>
    {
        logger.LogInformation($"Request aborted, requestId: {context.TraceIdentifier}");
        return Task.CompletedTask;
    };
}
